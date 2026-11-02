# Yedekleme ve Şifreleme

## Otomatik yedekleme

Uygulama kendi içinde (`Services/DatabaseBackupService.cs`) periyodik olarak `BACKUP DATABASE` alır; SQL Server Agent gerekmez (Express sürümünde zaten yoktur). Varsayılan olarak günde bir yedek alınır ve son 14 yedek saklanır.

Yedekler SQL Server'ın kendi varsayılan yedek klasörüne yazılır, ör. `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\`. Dosyayı yazan SQL Server servis hesabı olduğu için, bu hesabın zaten tam yetkili olduğu klasör kullanılır.

Ayarlar `appsettings.json` içindeki `DatabaseBackup` bölümündedir:

```json
"DatabaseBackup": {
  "Enabled": true,
  "IntervalHours": 24,
  "RetentionCount": 14,
  "Directory": ""
}
```

`Directory` boşsa varsayılan klasör kullanılır. Başka bir klasör verirseniz SQL Server servis hesabına orada yazma yetkisi vermeniz gerekir. Servis çalışıyorsa başlangıç loglarında "Otomatik veritabanı yedekleme aktif" satırı görünür.

> Yedekleri veritabanıyla aynı diskte bırakmayın; bir disk arızası ikisini birden götürür. Düzenli olarak başka bir diske, ağ konumuna ya da buluta kopyalayın.

## Elle yedek ve geri yükleme

```sql
BACKUP DATABASE RestoranYonetimDb
TO DISK = 'C:\SqlBackups\RestoranYonetimDb.bak'
WITH FORMAT, CHECKSUM;
```

(`COMPRESSION` seçeneği Express sürümünde desteklenmez.)

Geri yüklemek için SSMS'te **Databases → sağ tık → Restore Database** ile `.bak` dosyasını seçin. Başka bir bilgisayara taşırken de aynı yol kullanılır; kullanıcılar ve tüm veriler birlikte gelir.

Anlık geri dönüş (point-in-time restore) gerekiyorsa SQL Server'ın Standard sürümünde recovery model'i `FULL` yapıp günlük tam yedek + saatlik transaction log yedeği planlayın.

## Diskte şifreleme (TDE)

Transparent Data Encryption yalnızca SQL Server **Standard/Enterprise** sürümlerinde vardır, Express'te yoktur.

```sql
USE master;
CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'güçlü-bir-parola';
CREATE CERTIFICATE RestoranYonetimTdeCert WITH SUBJECT = 'RestoranYonetim TDE Certificate';
GO
USE RestoranYonetimDb;
CREATE DATABASE ENCRYPTION KEY WITH ALGORITHM = AES_256 ENCRYPTION BY SERVER CERTIFICATE RestoranYonetimTdeCert;
ALTER DATABASE RestoranYonetimDb SET ENCRYPTION ON;
```

> Sertifikayı özel anahtarıyla birlikte (`BACKUP CERTIFICATE ... WITH PRIVATE KEY`) mutlaka ayrı ve güvenli bir yere yedekleyin. Sertifika kaybolursa şifreli veritabanı ve yedekleri **geri dönülemez** şekilde açılamaz.

Ağ üzerindeki trafiği şifrelemek için bağlantı dizesinde `Encrypt=True` kullanın, bkz. [Production ayarları](YAYINA-ALMA.md#5-production-ayarları).
