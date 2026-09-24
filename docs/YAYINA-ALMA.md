# Yayına Alma

Mizan bir web uygulamasıdır: **tek bir bilgisayarda** (restorandaki bir PC ya da bir sunucu) çalışır. Garson tableti, mutfak ekranı, kasa ve müşteri telefonları hiçbir şey kurmaz; hepsi tarayıcıdan bu bilgisayara bağlanır.

- [1. Tek bilgisayarda çalıştırma](#1-tek-bilgisayarda-çalıştırma)
- [2. Yerel ağdan ve internetten erişim](#2-yerel-ağdan-ve-internetten-erişim)
- [3. Windows Service olarak 7/24 çalıştırma](#3-windows-service-olarak-724-çalıştırma)
- [4. VPS üzerinde domain + HTTPS ile yayın](#4-vps-üzerinde-domain--https-ile-yayın)
- [5. Production ayarları](#5-production-ayarları)

---

## 1. Tek bilgisayarda çalıştırma

**Sunucu bilgisayarda gerekenler:** yalnızca **SQL Server Express** (ücretsiz; kurulumda "Basic" seçmek ve örnek adını `SQLEXPRESS` bırakmak yeterli). .NET kurulumu gerekmez, exe'nin içindedir.

### Tek dosya exe üretme

Geliştirme bilgisayarında:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o C:\web\RestoranYonetimYayin
```

> Çıktı klasörünü (`-o`) proje klasörünün **içine** vermeyin; bir sonraki derleme o dosyaları projenin parçası sanar.

Yayın klasörü başka bir bilgisayara olduğu gibi kopyalanabilir. Güncelleme için aynı komutu tekrar çalıştırmak yeterlidir; çalışan uygulamayı önce kapatın. Canlıda yüklenen görseller `wwwroot/uploads` içindedir, yayın klasörünü silmeyin.

### Ayar dosyası

Yayınlanan exe `Production` ortamında çalışır ve `appsettings.Development.json`'u okumaz. Makineye özel ayarlar proje kökündeki `appsettings.Production.json` dosyasından gelir (publish ile yayın klasörüne kopyalanır, `.gitignore`'dadır):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=RestoranYonetimDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "PublicBaseUrl": "http://192.168.1.50:5080",
  "RequireHttps": false,
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:5080" } } }
}
```

| Ayar | Açıklama |
|---|---|
| `PublicBaseUrl` | QR kodlarının göstereceği adres. Müşteri telefonlarının açabilmesi için bilgisayarın yerel IP'si olmalı. Yönetim panelinde **Ayarlar → Site Adresi**'nden de girilebilir; panel bu bilgisayarın ağ adreslerini önerir ve paneldeki değer dosyadakinden önceliklidir. IP değişmesin diye modemden bu bilgisayara sabit IP verin. |
| `RequireHttps` | Sertifikasız, sadece yerel ağda çalışan kurulumda `false`. Domain ve sertifika varsa `true` (varsayılan). |
| `Kestrel` | `0.0.0.0:5080` uygulamayı ağdaki diğer cihazlara açar. |

### Tek tıkla başlatma

Yayın klasöründeki `Baslat.bat` çift tıklandığında uygulama kapalıysa başlatır, hazır olmasını bekler ve tarayıcıda yönetim panelini açar; zaten açıksa sadece tarayıcıyı açar. Sağ tık → Gönder → Masaüstü ile kısayol oluşturabilirsiniz.

Görev çubuğundaki küçültülmüş **Mizan** penceresi sunucunun kendisidir; kapatılırsa site durur. Sürekli çalışması için [Windows Service](#3-windows-service-olarak-724-çalıştırma) kurun.

### Yeni bir bilgisayarda ilk kurulum

Veritabanı ve tablolar ilk açılışta otomatik oluşur ama **boş** başlar. İlk yönetici hesabı için, uygulamayı ilk kez başlatmadan önce yönetici olarak açılmış bir komut isteminde:

```bat
setx INITIAL_SUPERADMIN_EMAIL "admin@restoraniniz.com" /M
setx INITIAL_SUPERADMIN_PASSWORD "GucluBirSifre123" /M
```

Hesap oluştuktan sonra bu değişkenler bir daha kullanılmaz, silinebilir. Mevcut veriyi taşımak için bunun yerine eski bilgisayardaki `.bak` yedeğini SSMS ile geri yükleyin ([Yedekleme](YEDEKLEME.md)).

---

## 2. Yerel ağdan ve internetten erişim

**Yalnızca aynı Wi-Fi / yerel ağ:** Yukarıdaki ayarlarla aynı ağdaki her cihaz `http://<yerel-IP>:5080` adresinden bağlanır. Ağ dışından erişilemez; masadaki müşteri ve personel için yeterli ve en güvenli kurulumdur. İlk çalıştırmada Windows Güvenlik Duvarı izin sorar, **Özel ağlar** için izin verin.

**İnternetten erişim** için iki yol vardır:

| Yol | Artı | Eksi |
|---|---|---|
| Modemde port yönlendirme + DDNS (No-IP, DuckDNS) | Hızlı, ücretsiz | Ev/iş ağını internete açar; ev interneti kesinti ve düşük yükleme hızına açıktır |
| VPS + domain + HTTPS | Gerçek bir site için doğru ve güvenli yol | Aylık sunucu ve yıllık domain ücreti |

---

## 3. Windows Service olarak 7/24 çalıştırma

Service olarak kurulan uygulama bilgisayar açıldığında kendiliğinden başlar, açık bir pencere gerektirmez. Yönetici olarak açılmış komut isteminde:

```bat
sc.exe create RestoranYonetim binPath= "C:\web\RestoranYonetimYayin\RestoranYonetim.exe" start= auto
sc.exe description RestoranYonetim "Mizan - Restoran Yonetim Sistemi"
sc.exe start RestoranYonetim
```

`binPath=` ve `start=` sonrasındaki boşluk `sc.exe` söz diziminin gereğidir. Durdurmak/kaldırmak için `sc.exe stop RestoranYonetim` / `sc.exe delete RestoranYonetim`. Loglar **Olay Görüntüleyicisi → Windows Günlükleri → Uygulama** altında, "RestoranYonetim" kaynağıyla görünür.

> **Veritabanı bağlantısı:** Service varsayılan olarak `Local System` hesabıyla çalışır. Bağlantı `Trusted_Connection=True` kullanıyorsa bu hesabın SQL Server'da yetkisi olmayabilir. Çözüm: `services.msc` → RestoranYonetim → Özellikler → **Oturum Aç** sekmesinden service'i kendi Windows hesabınızla çalıştırın ya da SQL Server'da `NT AUTHORITY\SYSTEM` için bir login oluşturup `RestoranYonetimDb` üzerinde yetki verin.

---

## 4. VPS üzerinde domain + HTTPS ile yayın

Proje Windows + SQL Server üzerine kurulu olduğundan en az iş gerektiren yol bir **Windows Server VPS**'tir. Restoran ölçeği için 2 vCPU / 4 GB RAM yeterlidir.

1. **Kurulum:** RDP ile bağlanıp SQL Server Express, [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download) ve IIS'i (Sunucu Yöneticisi → Rol Ekle → Web Sunucusu) kurun.
2. **Yayınlama:** Hosting Bundle .NET çalışma zamanını sağladığı için daha küçük bir çıktı yeterlidir:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained false -o C:\web\RestoranYonetimYayin
   ```
3. **IIS sitesi:** IIS Yöneticisi → Site Ekle, fiziksel yol yayın klasörü. Uygulama havuzunda **.NET CLR sürümü: Yönetilmeyen Kod**.
4. **DNS:** Domain panelinden bir **A kaydı** ile domain'i VPS'in IP adresine yönlendirin.
5. **HTTPS:** [win-acme](https://www.win-acme.com/) ile ücretsiz Let's Encrypt sertifikası alın; IIS'e kendisi ekler ve otomatik yeniler.
6. **Ayarlar:** [Production ayarları](#5-production-ayarları)'nın tamamını uygulayın; `RequireHttps` burada `true` kalır.
7. **Güvenlik duvarı:** 80 ve 443 portlarını açın; SQL Server portu (1433) dışarıya **kapalı** kalmalı.

IIS ile uygulama aynı makinede olduğu sürece ters proxy için ek ayar gerekmez.

---

## 5. Production ayarları

Uygulama `Development` dışındaki ortamlarda geçersiz ayarla başlamaz; başlangıçta açık bir hata verir.

| Ayar | Nereden | Açıklama |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `appsettings.Production.json` ya da ortam değişkeni `ConnectionStrings__DefaultConnection` | Kaynak koddaki `appsettings.json`'da bilinçli olarak boştur. Uzak bir SQL Server'da `Encrypt=True` kullanın, `TrustServerCertificate=True` kullanmayın. |
| `PublicBaseUrl` | aynı, ya da panelde **Ayarlar → Site Adresi** | QR ve site linklerinin adresi, ör. `https://restoraniniz.com`. Panelde girilen değer önceliklidir; ikisi de boşsa isteğin kendi adresi kullanılır. |
| `INITIAL_SUPERADMIN_EMAIL` / `INITIAL_SUPERADMIN_PASSWORD` | Ortam değişkeni | Sistemde hiç kullanıcı yokken ilk yöneticiyi oluşturur. Dosyaya yazmayın. |
| `AllowedHosts` | `appsettings.Production.json` | Gerçek domain(ler)inizle sınırlayın; `*` kalırsa uygulama uyarı loglar. |
| `RequireHttps` | aynı | Varsayılan `true`: HTTPS yönlendirmesi + HSTS. |
| `ReverseProxy:KnownProxies` | aynı | Yalnızca ters proxy **başka bir makinedeyse** o makinenin IP'si. Aksi halde hız sınırlama tüm kullanıcıları proxy'nin IP'sinde birleştirir. |
| `DatabaseBackup` | aynı | Otomatik yedek ayarları, bkz. [Yedekleme](YEDEKLEME.md). |

Büyük şema değişikliklerini canlıya almadan önce mutlaka yedek alın.
