# HelpdeskOps Kurulum ve Yedekleme Rehberi

Bu rehber HelpdeskOps'u şirketinizin kendi sunucusuna kurmayı, güncellemeyi ve yedeklemeyi anlatır. Kullanım için [Kullanım Kılavuzu](USER_GUIDE_TR.md)'na bakın.

## İçindekiler

1. [Hangi kurulum?](#1-hangi-kurulum)
2. [Docker ile kurulum](#2-docker-ile-kurulum)
3. [IIS ile kurulum (Windows Server)](#3-iis-ile-kurulum-windows-server)
4. [İlk ayarlar](#4-ilk-ayarlar)
5. [Güncelleme](#5-güncelleme)
6. [Yedekleme planı](#6-yedekleme-planı)
7. [Sorun giderme](#7-sorun-giderme)

## 1. Hangi kurulum?

| | Docker | IIS |
| --- | --- | --- |
| Sunucu | Linux (önerilen) ya da Docker kurulu Windows | Windows Server 2019 veya üstü |
| Veritabanı | SQL Server 2022 Express, aynı makinede kapsayıcı olarak gelir | SQL Server Express ayrıca kurulur |
| Kurulum süresi | 15 dakika | 45-60 dakika |
| Uygun olduğu durum | Linux sunucunuz varsa ya da en az bakım isteniyorsa | Şirket zaten Windows Server ve IIS kullanıyorsa |

20-200 kişilik bir şirket için 2 çekirdek, 4 GB RAM (SQL Server için en az 2 GB) ve 20 GB disk yeterlidir. SQL Server Express veritabanı başına 10 GB'a kadar ücretsizdir; HelpdeskOps bu sınıra yıllarca yaklaşmaz.

Her iki kurulumda da uygulama, açılırken veritabanı tablolarını kendisi oluşturur ve güncellemelerdeki değişiklikleri (migration) kendisi uygular.

## 2. Docker ile kurulum

### 2.1 Gerekenler

- Docker Engine ve Docker Compose eklentisi ([kurulum](https://docs.docker.com/engine/install/)). `docker compose version` komutu çalışmalı.
- Git

### 2.2 Kurulum

```bash
sudo git clone https://github.com/kerembolek/HelpdeskOps.git /opt/helpdeskops
cd /opt/helpdeskops
sudo cp .env.example .env
sudo nano .env
```

`.env` dosyasında en az şunları doldurun:

| Ayar | Açıklama |
| --- | --- |
| `DB_PASSWORD` | SQL Server `sa` şifresi. En az 8 karakter; büyük harf, küçük harf, rakam ve sembol içermeli. |
| `INITIAL_ADMIN_EMAIL` | İlk BT yöneticisinin e-posta adresi (bkz. [4.1](#41-ilk-bt-yöneticisi)). |
| `KVKK_CONTROLLER_NAME` | Aydınlatma metninde görünecek şirket unvanı. Boş bırakılırsa uygulama başlamaz. |
| `KVKK_CONTROLLER_ADDRESS`, `KVKK_CONTACT_EMAIL` | Şirket adresi ve KVKK başvuru adresi. |

`.env` şifre içerdiği için Git'e girmez; dosyayı yalnızca yöneticilerin okuyabileceği hâle getirin: `sudo chmod 600 .env`.

Ardından uygulamayı derleyip başlatın:

```bash
sudo docker compose up -d --build
sudo docker compose ps
```

İlk derleme birkaç dakika sürer. `db` satırında `healthy`, `app` satırında `Up` görününce tarayıcıdan `http://sunucu-adresi:8080` adresini açın.

Kapsayıcılar sunucu yeniden başlayınca kendiliğinden açılır (`restart: unless-stopped`). Veriler iki Docker biriminde (volume) durur: `db-data` veritabanını, `app-data` ise ekleri ve oturum anahtarlarını tutar. `docker compose down` bu birimleri silmez; `docker compose down -v` siler, kullanmayın.

### 2.3 HTTPS

Şifreler ağ üzerinden gittiği için uygulamayı HTTPS olmadan kullanıma açmayın. En kolay yol, önüne [Caddy](https://caddyserver.com/) koymaktır. Caddy sertifikayı kendisi alır ve yeniler.

1. `.env` dosyasında şu iki değeri değiştirin:

   ```
   APP_PORT=127.0.0.1:8080
   BEHIND_PROXY=true
   ```

   İlki uygulamaya dışarıdan yalnızca Caddy üzerinden ulaşılmasını sağlar. İkincisi uygulamaya Caddy'nin ilettiği gerçek kullanıcı IP'sine ve HTTPS bilgisine güvenmesini söyler. Uygulama doğrudan dışarıya açıkken `BEHIND_PROXY=true` yapmayın; aksi hâlde herkes denetim kaydına sahte IP adresi yazdırabilir.
2. `docker-compose.yml` ile aynı klasöre `Caddyfile` adında bir dosya oluşturun:

   ```
   helpdesk.sirketiniz.com.tr {
       reverse_proxy app:8080
   }
   ```

   Alan adı yalnızca şirket içinden çözülüyorsa (internete açık değilse) ikinci satırın altına `tls internal` ekleyin. Caddy bu durumda kendi sertifikasını üretir.

3. `docker-compose.yml` dosyasındaki `services:` bölümüne şunu ekleyin:

   ```yaml
     caddy:
       image: caddy:2
       restart: unless-stopped
       ports:
         - "80:80"
         - "443:443"
       volumes:
         - ./Caddyfile:/etc/caddy/Caddyfile:ro
         - caddy-data:/data
   ```

   En alttaki `volumes:` bölümüne de `caddy-data:` satırını ekleyin.

4. `sudo docker compose up -d` komutunu çalıştırın.

Bu ayarlarla denetim kaydına Caddy'nin değil, kullanıcının gerçek IP adresi yazılır.

## 3. IIS ile kurulum (Windows Server)

Aşağıdaki PowerShell komutlarını **yönetici olarak** açılmış bir PowerShell penceresinde çalıştırın.

### 3.1 Gerekenler

1. **IIS ve Application Initialization**:

   ```powershell
   Install-WindowsFeature Web-Server, Web-AppInit -IncludeManagementTools
   ```

2. **.NET 10 Hosting Bundle**: [.NET 10 indirme sayfasından](https://dotnet.microsoft.com/download/dotnet/10.0) "ASP.NET Core Runtime → Hosting Bundle" kurulumunu indirip çalıştırın. Ardından `iisreset` komutunu çalıştırın.
3. **SQL Server 2022 Express**: "Basic" kurulum yeterlidir. Kurulumu yapan Windows hesabı SQL Server'da yönetici (sysadmin) olur.
4. **sqlcmd**: SQL Server kurulumuyla gelmezse [sqlcmd indirme sayfasından](https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility) kurun.

### 3.2 Uygulama dosyaları

.NET 10 SDK kurulu bir bilgisayarda (örneğin geliştirici makinesinde) uygulamayı yayınlayın:

```powershell
git clone https://github.com/kerembolek/HelpdeskOps.git
cd HelpdeskOps
dotnet publish HelpdeskOps/HelpdeskOps.csproj --configuration Release --output publish
```

Visual Studio'da aynı işlem **HelpdeskOps projesine sağ tık → Publish → Folder** ile yapılır.

`publish` klasörünün içeriğini sunucuda `C:\inetpub\HelpdeskOps` klasörüne kopyalayın ve aynı klasörde `appsettings.Production.json` adında bir dosya oluşturun:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=HelpdeskOps;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "InitialAdminEmail": "ad.soyad@sirketiniz.com.tr",
  "Kvkk": {
    "ControllerName": "Şirketinizin Unvanı A.Ş.",
    "ControllerAddress": "Şirket adresi",
    "ContactEmail": "kvkk@sirketiniz.com.tr"
  },
  "Email": {
    "Mode": "None"
  }
}
```

Bu dosya `appsettings.json` içindeki ayarların üzerine yazar. Şifre içerebileceği için Git'e girmez.

### 3.3 Uygulama havuzu ve site

```powershell
Import-Module WebAdministration

New-WebAppPool -Name HelpdeskOps
# .NET Core kendi işleminde çalışır; IIS'in .NET Framework'ü yüklemesine gerek yok
Set-ItemProperty IIS:\AppPools\HelpdeskOps -Name managedRuntimeVersion -Value ''
# Uygulama hiç kapanmasın; e-posta kuyruğu ve günlük KVKK saklama süresi kontrolü arka planda çalışır
Set-ItemProperty IIS:\AppPools\HelpdeskOps -Name startMode -Value AlwaysRunning
Set-ItemProperty IIS:\AppPools\HelpdeskOps -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)

New-Website -Name HelpdeskOps -PhysicalPath C:\inetpub\HelpdeskOps -ApplicationPool HelpdeskOps -Port 80
Set-ItemProperty IIS:\Sites\HelpdeskOps -Name applicationDefaults.preloadEnabled -Value $true

# Ekler, oturum anahtarları ve e-posta kuyruğu App_Data klasörüne yazılır
New-Item -ItemType Directory -Path C:\inetpub\HelpdeskOps\App_Data -Force
icacls C:\inetpub\HelpdeskOps\App_Data /grant "IIS AppPool\HelpdeskOps:(OI)(CI)M"
```

Sunucuda başka bir site 80 numaralı portu kullanıyorsa `-Port` değerini değiştirin ya da `-HostHeader helpdesk.sirketiniz.com.tr` ekleyin.

### 3.4 Veritabanı izni

Uygulama, veritabanına uygulama havuzunun Windows kimliğiyle (`IIS APPPOOL\HelpdeskOps`) bağlanır. Veritabanını oluşturup bu kimliğe yetki verin:

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -Q "CREATE DATABASE [HelpdeskOps]"
sqlcmd -S .\SQLEXPRESS -E -C -d HelpdeskOps -Q "CREATE LOGIN [IIS APPPOOL\HelpdeskOps] FROM WINDOWS; CREATE USER [IIS APPPOOL\HelpdeskOps] FOR LOGIN [IIS APPPOOL\HelpdeskOps]; ALTER ROLE db_owner ADD MEMBER [IIS APPPOOL\HelpdeskOps];"
```

`db_owner` yetkisi, uygulamanın güncellemelerde tabloları kendisi değiştirebilmesi için gereklidir. Bu yetki yalnızca HelpdeskOps veritabanında geçerlidir.

Tarayıcıdan `http://sunucu-adresi` adresini açın. İlk açılışta tablolar oluşturulduğu için sayfa birkaç saniye geç gelebilir.

### 3.5 HTTPS

IIS Yöneticisi'nde **HelpdeskOps sitesi → Bindings → Add → https** yolunu izleyerek bir sertifika seçin:

- Şirketin kendi sertifika otoritesi varsa ondan alınan sertifikayı kullanın.
- Alan adı internete açıksa [win-acme](https://www.win-acme.com/) ücretsiz Let's Encrypt sertifikası alır ve otomatik yeniler.

HTTPS çalıştıktan sonra 80 numaralı port bağlantısını kaldırabilirsiniz. Uygulama HTTP isteklerini zaten HTTPS'e yönlendirir.

## 4. İlk ayarlar

### 4.1 İlk BT yöneticisi

Yeni kurulumda hiç BT yöneticisi yoktur. Kayıt olan herkes "Çalışan" rolü alır ve rolleri yalnızca bir BT yöneticisi değiştirebilir. İlk yöneticiyi atamak için:

1. `InitialAdminEmail` ayarına (Docker'da `.env` içindeki `INITIAL_ADMIN_EMAIL`) kendi e-posta adresinizi yazın.
2. Uygulamayı açıp **Kayıt Ol** sayfasından bu adresle hesap oluşturun.
3. Uygulamayı yeniden başlatın:
   - Docker: `sudo docker compose restart app`
   - IIS: `Restart-WebAppPool -Name HelpdeskOps`
4. Çıkış yapıp tekrar giriş yapın. Menüde **Cihazlar** ve **Kullanıcılar** görünür.

Sistemde bir BT yöneticisi olduğu sürece bu ayar hiçbir şey yapmaz; bu yüzden ayarı yerinde bırakmak güvenlik riski oluşturmaz. Diğer çalışanların rolleri **Kullanıcılar** sayfasından verilir.

### 4.2 E-posta

E-posta gönderilmezse bildirimler yalnızca uygulama içinde görünür ve şifre sıfırlama çalışmaz. SMTP ayarları için:

- Docker: `.env` içinde `EMAIL_MODE=Smtp` ve `SMTP_*` değerleri.
- IIS: `appsettings.Production.json` içinde `Email` bölümü ([README.tr.md](../README.tr.md#e-posta) içindeki örnek).

SMTP açıkken yeni hesaplar, giriş yapmadan önce e-posta adreslerini doğrulamak zorundadır.

### 4.3 KVKK

Aydınlatma metni şirket bilgilerini ayarlardan alır. Metin bir başlangıç şablonudur, hukuki görüş değildir; kullanıma açmadan önce şirketinizin hukuk danışmanına okutun. Saklama süreleri `Kvkk:Retention` ayarlarıyla değiştirilir (bkz. [Kullanım Kılavuzu, bölüm 13](USER_GUIDE_TR.md#13-kvkk-ve-kişisel-veriler)).

### 4.4 Otomatik envanter

`scripts/Send-Inventory.ps1` her bilgisayarın seri numarasını, marka/modelini, işlemcisini, RAM'ini, sistem diskini, Windows sürümünü ve IP adresini HelpdeskOps'a gönderir. Bilgisayar seri numarasıyla eşleştirilir: kayıtlıysa güncellenir, değilse yeni cihaz olarak eklenir. Elle girilen marka ve model ezilmez.

1. Uzun, rastgele bir anahtar üretin (örn. `openssl rand -hex 32`) ve ayarlara yazın:
   - Docker: `.env` içinde `INVENTORY_API_KEY=...`
   - IIS: `appsettings.Production.json` içinde `"Inventory": { "ApiKey": "..." }`
   Anahtar boşsa `/api/inventory` adresi kapalıdır (404).
2. Betiği tüm bilgisayarların okuyabildiği bir paylaşıma koyun (örn. `\\sunucu\netlogon`).
3. Group Policy ile oturum açılışında ve günde bir kez çalışan bir zamanlanmış görev dağıtın:
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File \\sunucu\netlogon\Send-Inventory.ps1 -ServerUrl https://helpdesk.sirket.com -ApiKey <anahtar>`
4. Önce bir bilgisayarda `-DryRun` ile deneyin; gönderilecek bilgiler ekrana yazılır, hiçbir şey gönderilmez.

Antivirüs uyarısı: Sistem bilgisi toplayıp bir sunucuya gönderdiği için bazı antivirüsler (örn. Bitdefender) betiği sezgisel olarak zararlı sayabilir. Betiğin bulunduğu klasörü antivirüs istisnalarına ekleyin.

Parça toplama bilgisayarlarda BIOS seri numarası yerine "System Serial Number" gibi bir yer tutucu bulunur. Betik bu durumda anakart seri numarasını, o da yoksa sistem UUID'sini kullanır; sunucu yer tutucu seri numaralarını reddeder.

### 4.5 Mesai saatleri ve SLA

Kritik talepler 4 saatlik hedefini 7/24 sayar; yüksek, orta ve düşük öncelikli talepler yalnızca mesai saatini sayar. Hafta sonları, resmî tatiller ve bayramlar sayılmaz; 28 Ekim ile bayram arifeleri saat 13:00'te biter. Mesai dışında açılan talebin süresi ilk mesai saatinde başlar.

- Docker: `.env` içinde `SLA_WORKDAY_START=09:00:00` ve `SLA_WORKDAY_END=18:00:00`.
- IIS: `appsettings.Production.json` içinde `"Sla": { "WorkdayStart": "08:30:00", "WorkdayEnd": "17:30:00" }`.

Ramazan ve Kurban Bayramı tarihleri 2030'a kadar uygulamanın içindedir. Köprü izinleri, şirkete özel tatiller veya Diyanet takviminin değiştirdiği günler için `Sla:Holidays` listesine tarih ekleyin, örn. `"Holidays": [ "2026-12-31" ]` (Docker'da `Sla__Holidays__0=2026-12-31`). Hedef, talep açılırken ya da önceliği değişirken hesaplanır; ayarı değiştirmek eski taleplerin hedefini değiştirmez.

### 4.6 Şirket dizini (Active Directory / Microsoft Entra ID)

İsteğe bağlıdır. Açıldığında çalışanlar şirket hesabıyla girer, ayrı bir HelpdeskOps şifresi tutulmaz. Bir kurulum tek dizine bağlanır. Hesaplar eşitlemeyle açıldığı için **Kayıt Ol** sayfası kapanır; roller yine **Kullanıcılar** sayfasından verilir.

**Microsoft Entra ID**

1. [Entra yönetim merkezi](https://entra.microsoft.com) → **App registrations → New registration**. Hesap türü: **Single tenant**. Redirect URI (Web): `https://helpdesk.sirket.com/signin-entra`.
2. **Certificates & secrets** altında bir client secret oluşturun ve bitiş tarihini takvime yazın; süresi dolunca Microsoft girişi ve eşitleme durur.
3. **API permissions → Add a permission → Microsoft Graph → Application permissions → User.Read.All** ekleyin ve **Grant admin consent** deyin. Bu, tüm kullanıcıları okuma iznidir ve yalnızca eşitleme kullanır.
4. Ayarları yazın:
   - Docker: `.env` içinde `DIRECTORY_PROVIDER=Entra`, `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, `ENTRA_CLIENT_SECRET`.
   - IIS: `appsettings.Production.json` içinde `Directory` bölümü ([README.tr.md](../README.tr.md#şirket-dizini) içindeki örnek).

`TenantId` şirketinizin kendi kiracısı olmalıdır; `common` gibi ortak değerler uygulama açılırken reddedilir. Girişi Microsoft yaptığı için çok adımlı doğrulama ve koşullu erişim kuralları Entra tarafında uygulanır. Uygulama HTTPS arkasında olmalıdır; Docker'da ters vekil kullanıyorsanız `BEHIND_PROXY=true` de gerekir, yoksa Microsoft geri dönüş adresini reddeder.

Eşitleme yalnızca kiracının kendi kullanıcılarını alır. Konuklar ve kullanıcı adında `#EXT#` geçen dış hesaplar, örneğin kiracıyı kişisel Microsoft hesabıyla açan kişi, atlanır. BT yöneticisi olacak kişi kiracıda gerçek bir kullanıcı olmalıdır.

**Active Directory**

1. Yalnızca okuma yetkili bir hizmet hesabı açın (örn. `CN=helpdesk-sync,OU=Service,DC=sirket,DC=local`). Parolasının süresi dolmasın.
2. Etki alanı denetleyicisinde LDAPS'in (636) çalıştığını ve sertifikasına HelpdeskOps sunucusunun güvendiğini doğrulayın. Düz LDAP'ta şifreler ağda açık gideceği için uygulama `Development` dışında bu ayarla açılmaz.
3. `BaseDn` olarak gerçek çalışanların bulunduğu OU'yu seçin; kapsam dışındaki hesaplar eşitlenmez.
4. Ayarları yazın:
   - Docker: `.env` içinde `DIRECTORY_PROVIDER=ActiveDirectory` ve `LDAP_*` değerleri.
   - IIS: `appsettings.Production.json` içinde `Directory` bölümü.

Çalışanlar giriş formuna şirket e-postalarını ve Windows şifrelerini yazar, şifreyi Active Directory doğrular. Dizine ulaşılamazsa form bunu söyler ve deneme hatalı giriş sayılmaz.

**İlk BT yöneticisi:** Hesaplar eşitlemeyle açıldığından [4.1](#41-i̇lk-bt-yöneticisi)'deki kayıt adımı yerine uygulamayı başlatıp bir dakika bekleyin, `InitialAdminEmail` ayarına dizindeki adresinizi yazın ve uygulamayı yeniden başlatın.

**Eşitleme:** Açılıştan bir dakika sonra ve her gün çalışır; **Kullanıcılar** sayfasındaki **Dizinle Eşitle** düğmesi hemen başlatır. Ad, e-posta ve departman dizinden gelir. Dizinde kapatılan ya da silinen kişinin hesabı kapanır, açık oturumları en geç bir dakikada biter. Dizinde yeniden açılan hesap kendiliğinden açılmaz; BT **Yeniden aç** der. Dizin kimseyi döndürmezse ya da eşitleme bağlı hesapların yarısından fazlasını kapatacaksa hiçbir şey değiştirmeden durur. Var olan yerel hesaplar aynı e-posta adresiyle eşleşir, dizinde bulunmayan yerel hesaplara dokunulmaz.

**Dizin kaybolursa:** `DIRECTORY_PROVIDER=None` yapıp uygulamayı yeniden başlatın. Bağlı hesaplar yerel şifrelerine döner; eşitlemeyle açılmış hesapların şifresi yoktur, **Şifremi unuttum** ile belirlerler.

### 4.7 E-postayla talep

İsteğe bağlıdır. Çalışanlar `destek@sirket.com` gibi bir adrese yazarak talep açar. Bu iş için ayrı bir posta kutusu açın; okunmamış her e-posta işlenir, kişisel bir kutu kullanmayın.

1. Posta kutusunda IMAP'ı açın. Uygulama TLS ile 993 numaralı porta bağlanır.
2. Ayarları yazın:
   - Docker: `.env` içinde `IMAP_HOST`, `IMAP_PORT`, `IMAP_USER`, `IMAP_PASSWORD` ve `APP_PUBLIC_URL=https://helpdesk.sirket.com`.
   - IIS: `appsettings.Production.json` içinde `Email:Inbound` ve `Email:PublicUrl` ([README.tr.md](../README.tr.md#e-posta) içindeki örnek).
3. Onay ve bildirim e-postaları için [4.2](#42-e-posta) içindeki SMTP ayarları da gerekir.

Kutu dakikada bir okunur, işlenen e-posta okundu işaretlenir ve kutuda kalır. Yalnızca açık hesabı olan kişilerin e-postaları talep olur. Talepler "Diğer" kategorisinde ve orta öncelikle açılır, BT bunları düzenler.

Microsoft 365 uyarısı: Exchange Online temel kimlik doğrulamayı (kullanıcı adı ve şifre) IMAP için kapatmıştır. Bu sürüm yalnızca kullanıcı adı ve şifreyle bağlanır, bu yüzden Microsoft 365 posta kutularında çalışmaz. Şirket içi Exchange, Google Workspace (uygulama şifresiyle) ve kendi posta sunucunuz çalışır.

Güvenlik: Posta sunucunuz gönderen adresini denetlemiyorsa dışarıdan biri çalışan adına e-posta gönderebilir. Etki alanınızda SPF ve DMARC'ı açın; uygulama sunucunun yazdığı denetim sonucunda DMARC ya da SPF başarısızsa e-postayı reddeder. E-postayla yalnızca talep açılır ve yorum eklenir, yetki gerektiren işlem yapılamaz.

### 4.8 Microsoft Teams bildirimleri

İsteğe bağlıdır. Her yeni talep, BT ekibinin Teams kanalına kart olarak düşer. Uygulama kaydı gerekmez; kanala erişen bir Teams hesabı yeterlidir.

1. Teams'te BT kanalını açın, kanalın yanındaki **⋯** menüsünden **Workflows**'u seçin.
2. **Send webhook alerts to a channel** şablonunu seçin, takımı ve kanalı doğrulayıp kaydedin.
3. Verilen adresi kopyalayın. Bu adres bir şifre gibidir: elinde olan herkes kanala mesaj yazabilir, bu yüzden e-postayla ya da sohbette paylaşmayın.
4. Ayarları yazın:
   - Docker: `.env` içinde `TEAMS_WEBHOOK_URL` ve `APP_PUBLIC_URL=https://helpdesk.sirket.com`.
   - IIS: `appsettings.Production.json` içinde `"Teams": { "WebhookUrl": "https://..." }` ve `Email:PublicUrl`.
5. Uygulamayı yeniden başlatıp deneme talebi açın; kart birkaç saniye içinde kanala düşer. Adres `https://` ile başlamıyorsa uygulama açılmaz.

Kartta yalnızca talep numarası, kategori, öncelik, SLA hedefi ve **HelpdeskOps'ta Görüntüle** bağlantısı bulunur. Teams mesajları Microsoft'un sunucularında durduğu için başlık, açıklama ve talep eden kişi karta konmaz; aydınlatma metnindeki "verileriniz yurt dışına aktarılmaz" ifadesi bu sayede doğru kalır.

Kart gönderilemezse uygulama günlüğüne `Could not post ticket` kaydı düşer, talep ise normal şekilde açılır; kart yeniden gönderilmez. Sürekli hata görürseniz iş akışının açık olduğunu ve onu kuran hesabın hâlâ etkin olduğunu kontrol edin: iş akışı o kişinin hesabına bağlıdır. Adres sızarsa iş akışını silip yenisini kurun.

### 4.9 Yapay zekâ önerileri

İsteğe bağlıdır ve varsayılan olarak kapalıdır. Açıldığında talep formunda **Öneri al** düğmesi görünür; kişi düğmeye bastığında Claude kategori, öncelik ve uygun Kendin Çöz makalelerini önerir. Öneri, **Forma uygula** düğmesine basılmadan formu değiştirmez.

**Açmadan önce KVKK:** Düğmeye basıldığında talep başlığı ve açıklaması ABD'deki Anthropic'e gönderilir. E-posta, telefon, T.C. kimlik, IBAN ve IP adresi önce gizlenir, kişinin adı ve hesabı eklenmez; ama metne yazılan isimler gizlenemez. Bu bir yurt dışı aktarımıdır. KVKK 9. maddeye göre aktarım dayanağını hukuk danışmanınızla değerlendirmeden açmayın.

1. [Claude Console](https://platform.claude.com)'da kuruluş hesabı açıp API anahtarı oluşturun ve harcama sınırı koyun.
2. Ayarları yazın:
   - Docker: `.env` içinde `SUGGESTIONS_API_KEY=...`.
   - IIS: `appsettings.Production.json` içinde `"Suggestions": { "ApiKey": "..." }`.
3. `Kvkk:NoticeVersion` değerini yükseltin. Aydınlatma metninde aktarım paragrafı kendiliğinden çıkar, "yurt dışına aktarılmaz" cümlesi kalkar ve herkesten metni yeniden okuması istenir.
4. Uygulamayı yeniden başlatıp bir taslak taleple deneyin.

Varsayılan model `claude-opus-5-5`'tir. Daha ucuz bir model için `Suggestions:Model` ayarını değiştirin (örneğin `claude-haiku-5-5`), sonra birkaç gerçek taleple önerilerin kalitesine bakın. Her kişi dakikada en fazla 10 öneri isteyebilir.

Öneri alınamazsa form kullanılmaya devam eder ve uygulama günlüğüne `Could not get a ticket suggestion` düşer; günlüğe talep metni ve anahtar yazılmaz. Sürekli hata görürseniz anahtarı ve Console'daki harcama sınırını kontrol edin. Anahtar sızarsa Console'dan silip yenisini oluşturun.

## 5. Güncelleme

Her güncellemeden önce yedek alın (bkz. [6](#6-yedekleme-planı)). Veritabanı değişiklikleri uygulama açılırken kendiliğinden uygulanır.

**Docker:**

```bash
cd /opt/helpdeskops
sudo scripts/backup-docker.sh
sudo git pull
sudo docker compose up -d --build
```

**IIS:**

1. Geliştirici makinesinde yeni sürümü `dotnet publish` ile yayınlayın.
2. Sunucuda `C:\HelpdeskOps\scripts\Backup-HelpdeskOps.ps1` ile yedek alın.
3. `C:\inetpub\HelpdeskOps` klasörüne `app_offline.htm` adında bir dosya koyun. IIS uygulamayı durdurur ve kullanıcılara bu dosyanın içeriğini gösterir.
4. Yeni dosyaları kopyalayın. `App_Data` klasörüne ve `appsettings.Production.json` dosyasına dokunmayın.
5. `app_offline.htm` dosyasını silin.

## 6. Yedekleme planı

### 6.1 Neler yedeklenir?

| Veri | Nerede | Kaybolursa |
| --- | --- | --- |
| Veritabanı | SQL Server | Tüm cihazlar, talepler, zimmet formları ve kullanıcılar gider. |
| `App_Data/attachments` | Uygulama klasörü / `app-data` birimi | Taleplere eklenen dosyalar açılmaz. |
| `App_Data/keys` | Uygulama klasörü / `app-data` birimi | Herkesin oturumu kapanır ve şifre sıfırlama bağlantıları geçersiz olur. |
| `.env` ya da `appsettings.Production.json` | Uygulama klasörü | Bağlantı ve SMTP ayarları yeniden girilir. Betikler bu dosyayı yedeklemez; bir kopyasını şifre kasasında saklayın. |

Betikler her çalıştığında veritabanını ve `App_Data` klasörünü tarih-saat adlı bir klasöre yedekler. Veritabanı yedeği `CHECKSUM` ile alınır ve `RESTORE VERIFYONLY` ile kontrol edilir. Varsayılan olarak 14 günden eski yedekler silinir.

### 6.2 Otomatik günlük yedek

**Docker:** cron ile her gece 02:00'de yedek alın:

```bash
sudo crontab -e
```

```
0 2 * * * cd /opt/helpdeskops && scripts/backup-docker.sh >> backups/backup.log 2>&1
```

Yedekler `/opt/helpdeskops/backups` klasörüne yazılır. Saklama süresini değiştirmek için gün sayısını betiğe verin: `scripts/backup-docker.sh 30`.

**IIS:**

1. Yedek klasörünü oluşturun ve SQL Server hizmet hesabına yazma izni verin, çünkü `.bak` dosyasını SQL Server'ın kendisi yazar:

   ```powershell
   New-Item -ItemType Directory -Path D:\Yedek\HelpdeskOps -Force
   icacls D:\Yedek\HelpdeskOps /grant "NT Service\MSSQL`$SQLEXPRESS:(OI)(CI)M"
   ```

2. Görev Zamanlayıcı'da (Task Scheduler) yeni bir görev oluşturun:
   - **Kullanıcı:** SQL Server'ı kuran yönetici hesabı. **"Kullanıcı oturum açmamış olsa da çalıştır"** seçeneğini işaretleyin.
   - **Tetikleyici:** her gün 02:00.
   - **Eylem:** `powershell.exe` programı, şu bağımsız değişkenlerle:

     ```
     -NoProfile -ExecutionPolicy Bypass -File C:\HelpdeskOps\scripts\Backup-HelpdeskOps.ps1 -BackupRoot D:\Yedek\HelpdeskOps
     ```

   Depodaki `scripts` klasörünü sunucuda `C:\HelpdeskOps\scripts` klasörüne kopyalayın. SQL örneği, veritabanı adı ya da uygulama klasörü farklıysa `-SqlInstance`, `-Database` ve `-AppPath` değerlerini de verin.

### 6.3 Sunucu dışına kopya (3-2-1 kuralı)

Yedekler sunucuyla aynı diskte durursa disk arızasında yedekler de gider. Verinin **3** kopyası, **2** farklı ortamda ve **1**'i başka bir yerde olmalıdır:

- Yedek klasörünü her gece bir NAS'a ya da başka bir sunucuya kopyalayın. Linux'ta `rsync -a /opt/helpdeskops/backups/ nas:/yedek/helpdeskops/`, Windows'ta `robocopy D:\Yedek\HelpdeskOps \\nas\yedek\helpdeskops /MIR` komutu kullanılabilir. Bu komutu yedek görevinden sonra çalışacak ikinci bir görev olarak ekleyin.
- Haftada bir kopyayı şirket dışında tutun (şifreli harici disk ya da bulut depolama).

Yedekler kişisel veri içerir. KVKK gereği yedek konumuna yalnızca yetkili kişiler erişebilmeli, şirket dışına çıkan kopyalar şifrelenmelidir.

### 6.4 Geri yükleme

Geri yükleme mevcut tüm verinin yerine yedeği koyar. Betik devam etmeden önce `EVET` yazmanızı ister.

**Docker:**

```bash
cd /opt/helpdeskops
sudo scripts/restore-docker.sh backups/20261026-020000
```

**IIS** (yönetici PowerShell):

```powershell
C:\HelpdeskOps\scripts\Restore-HelpdeskOps.ps1 -BackupPath D:\Yedek\HelpdeskOps\20261026-020000
```

Her iki betik de uygulamayı durdurur, veritabanını ve `App_Data` klasörünü geri yükler, sonra uygulamayı yeniden başlatır.

**Ayda bir geri yüklemeyi deneyin.** Hiç denenmemiş bir yedek, yedek sayılmaz. Bunun için ikinci bir makineye (ya da sanal makineye) aynı kurulumu yapıp son yedeği geri yükleyin ve birkaç talebi açıp eklerin indiğini kontrol edin.

### 6.5 Ne kadar veri kaybedilebilir?

Günlük yedekle, en kötü durumda son yedekten sonra girilen bir günlük veri kaybolur. Bu süre çok uzunsa yedek görevini günde birkaç kez çalıştırın; betikler çalışan uygulamayı durdurmaz.

## 7. Sorun giderme

| Belirti | Çözüm |
| --- | --- |
| `docker compose up` "Set ... in .env" hatası veriyor | `.env` içinde zorunlu bir değer boş: `DB_PASSWORD` ya da `KVKK_CONTROLLER_NAME`. |
| Docker'da `app` sürekli yeniden başlıyor | `sudo docker compose logs app` komutuyla hatayı okuyun. |
| `db` kapsayıcısı başlamıyor ya da `unhealthy` | `DB_PASSWORD` SQL Server'ın şifre kuralına uymuyordur. Değiştirin, `sudo docker compose down` ve `sudo docker volume rm helpdeskops_db-data` ile boş veritabanını silip yeniden başlatın (yalnızca ilk kurulumda; veri varsa silmeyin). |
| IIS'te "HTTP Error 500.30" | Uygulama açılamadı. `C:\inetpub\HelpdeskOps\web.config` dosyasında `stdoutLogEnabled="true"` yapıp `logs` klasörünü oluşturun, sayfayı yenileyip oradaki günlüğü okuyun. Çoğu zaman veritabanı izni (bkz. [3.4](#34-veritabanı-izni)) eksiktir. |
| IIS'te "HTTP Error 500.19" ya da "500.31" | .NET 10 Hosting Bundle kurulu değil ya da kurulumdan sonra `iisreset` yapılmadı. |
| Eklenen dosyalar yüklenmiyor | `App_Data` klasörüne yazma izni yok (IIS'te bkz. [3.3](#33-uygulama-havuzu-ve-site)). |
| Uygulama her açıldığında herkesin oturumu kapanıyor | `App_Data/keys` klasörü korunmuyor. Docker'da `app-data` biriminin silinmediğinden, IIS'te klasöre yazma izni olduğundan emin olun. |
| İlk yönetici atanmadı | Günlükte `InitialAdminEmail` uyarısını arayın. Adres, kayıt olunan adresle aynı olmalı ve uygulama kayıttan sonra yeniden başlatılmalıdır. |
| Cihazların ping durumu hep kapalı | Sunucunun cihazlara ICMP (ping) ile ulaşabildiğinden emin olun. Güvenlik duvarı ICMP'yi engelliyor olabilir. |
