# Mizan'ı Docker ile çalıştırma

Bu klasör Mizan'ı Docker ile kurmak içindir. Projenin geri kalanına dokunmaz; Windows'ta `.exe` / Windows Service ile kurulum ([ana README](../README.md)) olduğu gibi geçerlidir.

| Dosya | Ne işe yarar |
|---|---|
| `Dockerfile` | Mizan **imajını** derler (yalnızca linux-x64, ~400 MB). Hazır imaj: `ghcr.io/yunusemr044/mizan` |
| `docker-compose.yml` | **Konteynerleri** birlikte çalıştırır: uygulama, SQL Server, isteğe bağlı HTTPS (Caddy) |
| `.env.example` | Ayar şablonu; `.env` adıyla kopyalanıp doldurulur (git'e girmez) |
| `Caddyfile` | Domain ile otomatik HTTPS sertifikası |

**İmaj ve konteyner:** İmaj, derlenmiş uygulamanın kalıbıdır; konteyner o kalıptan çalışan kopyadır. Konteynerler silinip yeniden kurulabilir. Veriler konteynerin içinde değil, ayrı **birimlerde** (volume) durur ve güncellemede kaybolmaz:

| Birim | İçerik |
|---|---|
| `mizan_db-data` | Veritabanı |
| `mizan_db-backup` | Otomatik günlük yedekler (son 14 gün) |
| `mizan_app-uploads` | Panelden yüklenen görseller |
| `mizan_app-keys` | Şifreleme anahtarları (yazar kasa ve SMTP şifreleri, oturumlar). **Kaybolursa kayıtlı şifreler çözülemez**; yedeklerinizle birlikte saklayın |

## 1. Windows'ta WSL ve Docker kurulumu

1. PowerShell'i **yönetici olarak** açıp WSL'i kurun, ardından bilgisayarı yeniden başlatın:
   ```powershell
   wsl --install
   ```
2. [Docker Desktop](https://www.docker.com/products/docker-desktop/)'ı kurun. Kurulumda **"Use WSL 2 based engine"** seçili olsun. (Docker Desktop, 250 çalışandan ve 10 milyon dolar cirodan küçük işletmeler için ücretsizdir.)
3. Docker Desktop'ı açın; sol altta motorun çalıştığını gösteren yeşil işareti bekleyin. Kontrol:
   ```powershell
   docker version
   docker compose version
   ```

> Docker Desktop yerine doğrudan WSL'deki Ubuntu'ya Docker Engine da kurulabilir (`sudo apt install docker.io docker-compose-v2`); komutlar aynıdır, yalnızca Ubuntu terminalinden çalıştırılır.

## 2. Mizan'ı çalıştırma

Depoyu indirip bu klasöre geçin:

```bash
git clone https://github.com/YunusEmr044/Mizan-Restoran-Yonetim-Sistemi.git
cd Mizan-Restoran-Yonetim-Sistemi/docker
cp .env.example .env
```

`.env` dosyasını açıp en az şunları doldurun:

- `DB_PASSWORD`: SQL Server şifresi (en az 8 karakter; büyük/küçük harf, rakam, sembol)
- `ADMIN_EMAIL` ve `ADMIN_PASSWORD`: ilk Süper Admin hesabı (şifre en az 8 karakter, bir rakam ve bir küçük harf)

Başlatın:

```bash
docker compose up -d
```

Hazır imaj GitHub'dan indirilir (`ghcr.io/yunusemr044/mizan`); kaynak kodu derlemeye gerek yoktur. Kaynak koddan kendiniz derlemek isterseniz `docker compose up -d --build` kullanın.

İlk seferde imajlar indirildiği için birkaç dakika sürer. Ardından **http://localhost:5080** adresinden `ADMIN_EMAIL` ile giriş yapın; **Sistem → Kurulum** sayfası sizi adım adım yönlendirir.

Durumu görmek için:

```bash
docker compose ps
docker compose logs -f app
```

### Örnek veriyle denemek

Boş başlamak yerine örnek restoranı (menü, masalar, siparişler, raporlar) görmek isterseniz `.env` içinde `MIZAN_ENVIRONMENT=Development` yazıp yeniden başlatın. Bu modda kaynak koddaki örnek hesaplar oluşur; **yalnızca kendi bilgisayarınızda deneme için** kullanın, restoranda ve sunucuda asla.

## 3. Restoran ağından erişim (tablet, telefon, QR)

Docker Desktop, 5080 portunu Windows'un kendisinde açar; aynı ağdaki tabletler `http://<bu bilgisayarın IP'si>:5080` ile bağlanır.

1. IP'yi öğrenin: `ipconfig` → "IPv4 Address" (ör. `192.168.1.50`). Modemden bu bilgisayara **sabit IP** verin.
2. `.env` içinde `PUBLIC_BASE_URL=http://192.168.1.50:5080` yazın ve `docker compose up -d` çalıştırın. QR kodları bu adrese yönlenir.
3. Windows Güvenlik Duvarı sorarsa **Özel ağlar** için izin verin.

**Mutfak yazıcısı** (ağ yazıcısı, IP:9100) konteynerden de çalışır. **USB/seri porta takılı yazar kasa** konteynerden kullanılamaz; bu durumda Windows kurulumunu tercih edin.

## 4. Sunucuda domain ve HTTPS ile

Bir Linux sunucuda (en az 4 GB RAM; SQL Server 2 GB ister) Docker kurulduktan sonra:

1. Domain'in DNS **A kaydını** sunucunun IP'sine yönlendirin; 80 ve 443 portları açık olsun.
2. `.env` içinde:
   ```
   DOMAIN=restoraniniz.com
   PUBLIC_BASE_URL=https://restoraniniz.com
   REQUIRE_HTTPS=true
   ALLOWED_HOSTS=restoraniniz.com
   ```
3. HTTPS profiliyle başlatın; Caddy sertifikayı Let's Encrypt'ten kendisi alır ve yeniler:
   ```bash
   docker compose --profile https up -d
   ```

Sunucudaki uygulama restorandaki ağ yazıcısına doğrudan ulaşamaz; mutfakta **Yazıcılar → İstasyonu aç** (tarayıcı istasyonu) modunu kullanın.

## 5. Güncelleme

```bash
git pull
docker compose pull
docker compose up -d
```

Veritabanı değişiklikleri açılışta otomatik uygulanır; birimlerdeki veriler korunur. Belirli bir sürüme sabitlemek için `.env` içine `MIZAN_IMAGE=ghcr.io/yunusemr044/mizan:1.0` yazın.

## 6. Yedekler

Otomatik yedek her 24 saatte bir `mizan_db-backup` birimine alınır. Bilgisayara kopyalamak için:

```bash
docker compose cp db:/var/opt/mssql/backup ./yedekler
```

Şifreleme anahtarlarını da yedekleyin (yalnızca güvenli bir yere):

```bash
docker compose cp app:/app/App_Data/keys ./yedekler/anahtarlar
```

Bir yedeği geri yüklemek için (uygulama kapalıyken; `DOSYA.bak` yerine yedek adını yazın):

```bash
docker compose stop app
docker compose exec db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "ALTER DATABASE [MizanDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE [MizanDb] FROM DISK = N'"'"'/var/opt/mssql/backup/DOSYA.bak'"'"' WITH REPLACE; ALTER DATABASE [MizanDb] SET MULTI_USER;"'
docker compose start app
```

## 7. Durdurma ve kaldırma

```bash
docker compose stop        # durdurur, veriler kalır
docker compose down        # konteynerleri kaldırır, veriler (birimler) kalır
docker compose down -v     # DİKKAT: veritabanı, yedekler ve görseller dahil her şeyi siler
```

## Yeni sürümü yayımlamak (geliştirici için)

İmaj GitHub Container Registry'de, depoya bağlı paket olarak durur. Yeni sürüm yayımlamak için (bir kez `docker login ghcr.io -u <kullanıcı>` ile, `write:packages` izinli bir erişim belirteciyle giriş yapılmış olmalı):

```bash
cd ..   # depo kökü
docker build -f docker/Dockerfile -t ghcr.io/yunusemr044/mizan:1.1 -t ghcr.io/yunusemr044/mizan:latest .
docker push ghcr.io/yunusemr044/mizan:1.1
docker push ghcr.io/yunusemr044/mizan:latest
```

## Sorun giderme

- **"DB_PASSWORD .env dosyasında tanımlı olmalı"**: `.env` dosyası yok ya da şifre boş.
- **Veritabanı başlamıyor**: Şifre SQL Server kurallarına uymuyor olabilir; `docker compose logs db` bakın.
- **Saatler 3 saat kayık**: Konteynerler `TZ=Europe/Istanbul` ile çalışır; `.env` değil compose dosyası ayarlar, değiştirmeyin.
- **Docker Desktop açılırken "Sistem dosyaya erişemiyor ... .sock" hatası**: Windows'un bazı önizleme (Insider) sürümlerinde Docker'ın eski soket dosyaları silinemez. Docker Desktop'ı kapatıp `%LOCALAPPDATA%\Docker\run` ve `%LOCALAPPDATA%\docker-secrets-engine` klasörlerini yeniden adlandırın (ör. sonuna `.eski` ekleyin), sonra yeniden açın. **"Reset to factory defaults"a basmayın**; tüm imajları ve birimleri siler.
