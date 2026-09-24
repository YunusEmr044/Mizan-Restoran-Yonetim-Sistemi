#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Basit yük testi scripti - RestoranYonetim sitesinin (herkese açık taraf: ana sayfa,
menü, galeri, hakkımızda, iletişim + isteğe bağlı müşteri QR menü sayfası) eşzamanlı
trafik altında ne kadar dayanıklı olduğunu ölçmek için.

NEDEN SADECE OKUMA (GET) İSTEKLERİ?
Bu script varsayılan olarak sadece GET (salt okuma) istekleri gönderir - sipariş
verme/garson çağırma gibi POST uç noktalarına gerçek yük göndermez. Sebepleri:
  1) POST uç noktaları artık IP başına dakikada 20 istekle sınırlı (bkz. Program.cs
     "public-endpoints" rate limit policy'si) - ham bir yük testi bu sınıra hemen
     çarpıp "429 Too Many Requests" görür. Bu bir HATA DEĞİL, güvenlik önleminin
     doğru çalıştığının kanıtıdır.
  2) Gerçek POST istekleri veritabanına gerçek taslak sipariş/garson çağrısı kayıtları
     yazar - prod ortamda çalıştırırsanız verinizi kirletir.
Sipariş akışını da yük altında test etmek isterseniz, --with-orders bayrağını
kullanın (aşağıda açıklanıyor) - ama bunu SADECE test/staging ortamında, prod'da
DEĞİL çalıştırın; hem gerçek sipariş kayıtları oluşturur hem de rate limit'e bilerek
çarpar (bu da beklenen/istenen bir sonuçtur).

KULLANIM:
    pip install aiohttp
    python load-test.py --base-url http://localhost:5080 --concurrency 30 --duration 30
    python load-test.py --base-url http://localhost:5080 --token ABCDEF123456 --concurrency 30 --duration 30

    --base-url      Sitenin adresi (varsayılan: http://localhost:5080 - bkz. proje
                     notları, uygulama bu portta çalışıyor).
    --token         Bir masanın QR token'ı (RestorantTables.QrToken) - verilirse
                     /menu/{token} müşteri menü sayfası da teste dahil edilir.
                     Admin panelde Masalar ekranından bir masanın QR linkini açıp
                     URL'deki /menu/xxxxx kısmındaki token'ı kopyalayabilirsiniz.
    --concurrency   Aynı anda kaç "sanal kullanıcı" sürekli istek göndersin (varsayılan 20).
    --duration      Test kaç saniye sürsün (varsayılan 30).
    --with-orders   (opsiyonel, SADECE test/staging'de kullanın) --token ile birlikte
                     verilirse, sepete ürün ekleyip sipariş oluşturma akışını da
                     düşük bir hızda (rate limit'e kasıtlı çarpacak şekilde) dener.

ÇIKTI:
Test bitince her uç nokta için: toplam istek, başarı/hata sayısı, durum kodu dağılımı,
ortalama/95. yüzdelik/en yavaş yanıt süresi (ms) ve saniyedeki istek sayısı (RPS)
yazdırılır. "500" hatası veya bağlantı hatası (timeout/connection refused) görürseniz
bu gerçek bir sorunu işaret eder - "429" ise rate limiter'ın çalıştığını (istenen
davranış) gösterir.
"""
from __future__ import annotations

import argparse
import asyncio
import random
import sys
import time
from collections import defaultdict
from dataclasses import dataclass, field

try:
    import aiohttp
except ImportError:
    print("Bu script 'aiohttp' paketine ihtiyaç duyuyor. Kurmak için:\n    pip install aiohttp")
    sys.exit(1)


@dataclass
class EndpointStats:
    latencies_ms: list = field(default_factory=list)
    status_counts: dict = field(default_factory=lambda: defaultdict(int))
    errors: int = 0

    @property
    def total(self) -> int:
        return len(self.latencies_ms) + self.errors


def percentile(values: list, pct: float) -> float:
    if not values:
        return 0.0
    values = sorted(values)
    idx = min(len(values) - 1, int(len(values) * pct))
    return values[idx]


async def hit(session: aiohttp.ClientSession, method: str, url: str, stats: EndpointStats, **kwargs):
    start = time.perf_counter()
    try:
        async with session.request(method, url, allow_redirects=True, timeout=aiohttp.ClientTimeout(total=15), **kwargs) as resp:
            await resp.read()
            elapsed_ms = (time.perf_counter() - start) * 1000
            stats.latencies_ms.append(elapsed_ms)
            stats.status_counts[resp.status] += 1
    except Exception as ex:  # noqa: BLE001 - yük testinde her hatayı (timeout, reset, vb.) yakalayıp saymak istiyoruz
        stats.errors += 1
        stats.status_counts[f"HATA:{type(ex).__name__}"] += 1


async def worker(session: aiohttp.ClientSession, base_url: str, token: str | None, with_orders: bool,
                  stop_at: float, all_stats: dict):
    # NOT: bu yollar bilerek "/Home/..." öneki ile yazıldı. Varsayılan MVC route şablonu
    # {controller=Home}/{action=Index}/{id?} olduğu için "/Menu" gibi bir URL, HomeController.
    # Menu() eylemine DEĞİL, "Menu" adında bir controller'ın Index() eylemine (yani ayrı bir
    # sınıf olan MenuController'a - müşteri QR sipariş ekranı) yönlendirilmeye çalışılır ve
    # orada parametresiz bir Index() eylemi olmadığı için 404 döner. Aynı şekilde "/Hakkimizda",
    # "/Galeri", "/Iletisim" de o isimde bir controller aranıp bulunamadığı için 404 verir.
    # Siteyi tarayıcıdan gezerken linkler asp-controller="Home" asp-action="..." ile üretildiği
    # için gerçek URL'ler hep "/Home/..." önekini içerir - bu script de aynısını kullanmalı,
    # yoksa (daha önce olduğu gibi) "sunucu çöküyor" değil "test scripti yanlış adrese
    # gidiyor" durumunu ölçmüş oluruz.
    public_paths = ["/", "/Home/Menu", "/Home/Hakkimizda", "/Home/Galeri", "/Home/Iletisim"]

    while time.perf_counter() < stop_at:
        path = random.choice(public_paths)
        await hit(session, "GET", base_url + path, all_stats[path])

        if token:
            menu_path = f"/menu/{token}"
            await hit(session, "GET", base_url + menu_path, all_stats[menu_path])

            if with_orders and random.random() < 0.2:
                # Sipariş akışını da düşük bir olasılıkla dener - bilerek rate
                # limit'e (dakikada 20 istek/IP) çarpacak şekilde tasarlandı.
                await hit(session, "POST", base_url + f"{menu_path}/garson", all_stats["POST /menu/{token}/garson"])

        await asyncio.sleep(random.uniform(0.05, 0.3))


async def main_async(args):
    stop_at = time.perf_counter() + args.duration
    all_stats: dict = defaultdict(EndpointStats)

    print(f"Yük testi başlıyor: {args.base_url}  |  eşzamanlılık={args.concurrency}  |  süre={args.duration}s")
    if args.token:
        print(f"Müşteri menü sayfası da test edilecek: /menu/{args.token}")
    if args.with_orders:
        print("UYARI: --with-orders açık - gerçek taslak sipariş/garson çağrısı kayıtları oluşturulacak. "
              "Bunu SADECE test/staging ortamında çalıştırdığınızdan emin olun.")
    if args.concurrency >= 300:
        print(f"NOT: {args.concurrency} eşzamanlılık isteniyor - sunucu VE bu script AYNI makinede "
              "çalışıyorsa (bkz. --base-url localhost), bu artık 'internetten gelen 2000 farklı "
              "müşteri' değil, tek bir PC'nin hem sunucuyu hem de bu kadar isteği aynı anda üretmeye "
              "çalışmasıdır - darboğaz gerçek ağ/sunucu kapasitesi değil, o PC'nin CPU'su/ağ yığını "
              "olabilir. Sonuçları buna göre yorumlayın; ayrı bir makineden çalıştırmak (varsa) veya "
              "--base-url ile gerçek bir hosting adresine yönlendirmek daha gerçekçi bir sonuç verir.")

    # Tüm sanal kullanıcılar TEK bir ClientSession/bağlantı havuzunu paylaşıyor (worker başına ayrı
    # session yerine) - yüksek eşzamanlılıkta (ör. 2000) yüzlerce ayrı TCP bağlantı havuzu açıp
    # kapatmak istemci makinesinde gereksiz CPU/bellek israfına yol açardı. limit/limit_per_host
    # istenen eşzamanlılık kadar (+pay) açık bırakılıyor, aksi halde aiohttp'nin varsayılan 100
    # bağlantı sınırı isteklerin kendi içinde kuyruğa girmesine (ve testin server'ı değil connector'ı
    # ölçmesine) yol açar.
    connector = aiohttp.TCPConnector(limit=args.concurrency + 50, limit_per_host=args.concurrency + 50)
    started = time.perf_counter()
    async with aiohttp.ClientSession(connector=connector, headers={"User-Agent": "RestoranYonetim-LoadTest"}) as session:
        workers = [
            worker(session, args.base_url, args.token, args.with_orders, stop_at, all_stats)
            for _ in range(args.concurrency)
        ]
        await asyncio.gather(*workers)
    total_elapsed = time.perf_counter() - started

    print("\n" + "=" * 78)
    print(f"{'Uç Nokta':35} {'İstek':>7} {'Hata':>6} {'Ort(ms)':>9} {'P95(ms)':>9} {'Maks(ms)':>9} {'RPS':>7}")
    print("-" * 78)
    grand_total = 0
    grand_errors = 0
    for path, stats in sorted(all_stats.items()):
        avg = sum(stats.latencies_ms) / len(stats.latencies_ms) if stats.latencies_ms else 0.0
        p95 = percentile(stats.latencies_ms, 0.95)
        mx = max(stats.latencies_ms) if stats.latencies_ms else 0.0
        rps = stats.total / total_elapsed if total_elapsed > 0 else 0.0
        print(f"{path:35} {stats.total:>7} {stats.errors:>6} {avg:>9.1f} {p95:>9.1f} {mx:>9.1f} {rps:>7.1f}")
        grand_total += stats.total
        grand_errors += stats.errors

        status_summary = ", ".join(f"{code}: {count}" for code, count in sorted(stats.status_counts.items(), key=str))
        print(f"   durum kodları -> {status_summary}")

    print("-" * 78)
    print(f"TOPLAM: {grand_total} istek, {grand_errors} hata, {total_elapsed:.1f} saniyede "
          f"({grand_total / total_elapsed:.1f} istek/saniye)")
    print("=" * 78)

    if grand_errors > 0:
        print("\nNot: 'HATA:*' satırları bağlantı reddi/timeout gibi gerçek sorunları gösterir - "
              "bunlar varsa sunucu loglarını (konsol/Serilog varsa dosya) kontrol edin.")
    print("Not: '429' durum kodları rate limiter'ın (bkz. Program.cs) çalıştığının kanıtıdır, hata değildir.")


def main():
    parser = argparse.ArgumentParser(description="RestoranYonetim için basit yük testi scripti.")
    parser.add_argument("--base-url", default="http://localhost:5080", help="Sitenin adresi (varsayılan: http://localhost:5080)")
    parser.add_argument("--token", default=None, help="Test edilecek bir masanın QR token'ı (opsiyonel)")
    parser.add_argument("--concurrency", type=int, default=20, help="Eşzamanlı sanal kullanıcı sayısı (varsayılan: 20)")
    parser.add_argument("--duration", type=int, default=30, help="Test süresi, saniye (varsayılan: 30)")
    parser.add_argument("--with-orders", action="store_true", help="SADECE test/staging'de: sipariş/garson çağrısı akışını da dener")
    args = parser.parse_args()

    args.base_url = args.base_url.rstrip("/")
    asyncio.run(main_async(args))


if __name__ == "__main__":
    main()
