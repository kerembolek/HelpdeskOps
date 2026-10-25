# HelpdeskOps

[![CI](https://github.com/kerembolek/HelpdeskOps/actions/workflows/ci.yml/badge.svg)](https://github.com/kerembolek/HelpdeskOps/actions/workflows/ci.yml)

[English version](README.md)

ASP.NET Core MVC (.NET 10) ile geliştirilmiş, kurumsal tarzda bir BT envanter ve yardım masası (helpdesk) destek talebi yönetim sistemi.

Bu proje, gerçek bir kurum içi BT operasyon platformunu simüle eder: donanım envanterini takip etmek, destek taleplerini yönetmek ve çalışanlar, BT uzmanları ve yöneticiler için role dayalı panolar sunmak.

> Bir kurumsal BT departmanındaki staj deneyiminden esinlenilen bir portföy projesidir.

**Canlı demo:** [helpdeskops.runasp.net](https://helpdeskops.runasp.net), [demo hesaplardan](#demo-hesaplar) biriyle giriş yapabilirsiniz.

---

## Teknoloji Altyapısı

- **Çatı:** ASP.NET Core MVC (.NET 10)
- **Veritabanı:** Entity Framework Core (Code-First)
- **Kimlik doğrulama:** ASP.NET Core Identity ve role dayalı yetkilendirme (`Employee`, `ITAdmin`)
- **Arayüz:** Razor Views, Bootstrap, Bootstrap Icons
- **Test:** xUnit birim testleri; her push ve pull request'te GitHub Actions ile çalışır
- **Sürüm kontrolü:** Git ve GitHub

---

## Özellikler

### Donanım Envanteri ve Ağ Yönetimi
- [x] Temel cihaz modeli (cihaz adı, IPv4, seri numarası, marka/model, cihaz türü, durum)
- [x] IPv4 biçim doğrulaması
- [x] Cihaz yönetim ekranları (listeleme, ekleme, düzenleme, ayrıntı, silme)
- [x] Cihaz ekleme/düzenleme ekranlarından kullanıcıya zimmet atama
- [x] Cihaz listesinde ve detayında canlı ağ durumu göstergesi (ping tabanlı, yeşil/kırmızı nokta) ve yenileme butonu
- [x] Uzak erişim kısayolu: dizüstü ve masaüstü bilgisayarlar için Uzak Masaüstü Bağlantısı'nı açan indirilebilir `.rdp` dosyası
- [x] Cisco switch port eşleme: cihazı bir switch portuna (örn. `Gi1/0/12`) ve VLAN'a bağlama, her porta tek cihaz, her switch'in detay sayfasında port tablosu ve cihaz geçmişine kaydedilen port değişiklikleri
- [x] Cihaz geçmişi: format, yükseltme, parça değişimi ve onarım kayıtları ile otomatik zimmet ve durum değişiklikleri
- [x] Garanti takibi: satın alma tarihi, garantide / yakında bitiyor (30 gün) / garanti bitti rozetli garanti bitişi, servis/tedarikçi iletişim bilgileri (firma, telefon, e-posta) ve cihaz listesinde garanti filtreleri
- [x] CSV ile cihaz içe ve dışa aktarma: bir CSV dosyasından tek seferde 500 cihaza kadar ekleme (ya hepsi ya hiçbiri; her sorun satır numarasıyla listelenir), örnek şablon indirme ve cihaz listesini aynı formatta dışa aktarma

### Yardım Masası ve Talep Sistemi
- [x] Temel talep modeli (kategori, öncelik, durum, talep eden, atanan kişi)
- [x] Talep yönetim ekranları (listeleme, oluşturma, düzenleme, ayrıntı, silme)
- [x] Talep çözüldüğünde veya kapatıldığında çözülme tarihinin otomatik kaydedilmesi
- [x] Talep edenin oturum açmış kullanıcıdan otomatik belirlenmesi
- [x] Düzenleme ekranından talebin BT personeline atanması
- [x] BT personelinin çalışan adına talep açabilmesi (telefonla veya yüz yüze gelen talepler)
- [x] Atanmamış aktif talepler için tek tıkla "Üzerime Al"
- [x] Talep listesinde arama ve filtreler (durum, öncelik, aktif, çözülen/kapatılan, bana atananlar, atanmamış)
- [x] Önceliğe göre hedef çözüm süreleri (SLA: Kritik 4, Yüksek 8, Orta 24, Düşük 72 saat); talep listesinde ve detayında zamanında / süre doluyor / gecikti / hedefte çözüldü / geç çözüldü rozetleri ve "hedef süresi geçenler" görünümü
- [x] Talep eden ile BT personeli arasında talep yorumları
- [x] Talep ekleri: talep açarken veya sonradan ekran görüntüsü, PDF, metin/log, Word ve Excel dosyası (en fazla 5 dosya, her biri 5 MB); dosyalar web kökü dışında saklanır ve yalnızca talebi görebilenler indirebilir
- [x] Memnuniyet puanı: talep eden, çözülen talebini bir kez 1-5 arası puanlar ve isterse yorum ekler; puan talepte ve hareket geçmişinde görünür
- [x] Uygulama içi bildirimler: talep eden yorumlardan, durum değişikliklerinden ve adına açılan taleplerden; BT personeli atamalardan, talep edenin yorumlarından ve değerlendirmelerden haberdar olur; menüdeki bağlantı okunmamış bildirim sayısını gösterir
- [x] Talep hareket geçmişi: oluşturma ile her durum, öncelik ve atanan kişi değişikliği; kimin ne zaman yaptığıyla birlikte
- [x] Türkçe kullanıcı arayüzü
- [x] Tier 0 self-servis çözüm bankası: çalışanlar talep açmadan önce konu seçip adım adım çözümleri dener; "çözülmedi" denince talep formu doldurulmuş olarak açılır, BT personeli çözümleri yönetir ve her birinin kendi kendine çözüm oranını görür
- [x] Kademeli kategori yapısı: her kategorinin kendi alt kategorileri var, "Diğer" seçilince kısa bir serbest metin açıklama isteniyor; talep listesi kategoriye göre filtrelenebiliyor
- [x] Dijital zimmet formları: cihaz zimmetlenince cihaz ve çalışan bilgilerinin bir kopyasını saklayan form açılır, çalışan "Cihazlarım" sayfasından teslim aldığını onaylar, cihaz başkasına verilince veya silinince form kapanır ve her form PDF olarak indirilebilir
- [x] İmzalı teslim ve iade: çalışan teslim aldığını ve iade talebini (işten ayrılış, cihaz değişimi gibi bir sebeple) ekrana imza atarak onaylar; BT iade edilen cihazı kontrol edip durumunu kaydeder ve imzalayarak teslim alır, cihaz çalışanın üzerinden düşer ve cihaz geçmişine işlenir; PDF'te tüm imzalar ve iade bilgileri yer alır

### Role Dayalı Erişim Kontrolü (RBAC)
- [x] Departman alanı olan temel kullanıcı modeli
- [x] Giriş ve kayıt (kayıt sırasında ad ve departman bilgisi alınır)
- [x] Çalışan (Employee) rolü: talep oluşturur ve yalnızca kendi taleplerini görür (kayıt olan kullanıcılara varsayılan olarak verilir)
- [x] BT Yöneticisi (ITAdmin) rolü: cihazlara ve talep yönetimine tam erişim (düzenleme, atama, silme)
- [x] "Cihazlarım" sayfası: her kullanıcı kendine zimmetli cihazları görür; çalışanlar taleplere yalnızca kendi cihazlarını ekleyebilir
- [x] Yönetici (Manager) rolü: tüm talepleri geçmişi ve yorumlarıyla salt okunur görme, pano ve raporlar; cihaz yönetimi ve talep değişikliği yok
- [x] BT yöneticileri için kullanıcı yönetimi: rolleri, zimmetli cihazları ve aktif talepleriyle aranabilir kullanıcı listesi ve kullanıcı bazında rol değiştirme (kimse kendi rolünü değiştiremez; BT personeli rolünden çıkan birinin aktif talepleri atanmamış taleplere döner)

### Kalite
- [x] SLA kurallarını, Türkiye saatini, CSV içe aktarmayı, imza doğrulamasını ve salt okunur demo ara katmanını kapsayan xUnit test projesi
- [x] main'e her push'ta ve her pull request'te çözümü derleyip testleri çalıştıran GitHub Actions CI

### Arayüz
- [x] İkonlu lacivert menü çubuğu, bulunulan bölümün vurgulanması ve kullanıcı menüsü
- [x] Role göre ana sayfa: canlı sayılarla kısayol kutucukları (taleplerim, cihazlarım, imza bekleyen formlar; BT için bana atanan, atanmamış, hedef süresi aşan, iade bekleyen ve garantisi bitecek olanlar) ve demo sitesinde ziyaretçiler için demo hesaplar
- [x] Renkli talep durumu ve öncelik rozetleri, ikonlu pano kartları ve telefonda yana kayan tablolar

### Yönetici Panosu ve Raporlama
- [x] Tıklanınca ilgili filtreli talep listesini açan özet kartları (açık, işlemde, çözülen/kapatılan, atanmamış aktif talepler)
- [x] Genel ve önceliğe göre ortalama talep çözüm süresi
- [x] Kronik sorunlu cihazlar (marka/modele göre en sık arızalar)
- [x] Departman iş yükü dağılımı
- [x] Tıklanınca ilgili filtreli cihaz listesini açan garanti kartları (30 gün içinde bitecek, garantisi bitmiş)
- [x] SLA kartları: hedef süresi geçen aktif talepler (filtreli listeyi açar) ve hedefinde çözülen taleplerin oranı
- [x] Seçilen döneme (7, 30, 90 gün, 1 yıl) göre raporlar sayfası: açılan/çözülen sayıları, kategori ve alt kategoriye göre talepler, BT personeli performansı, ortalama memnuniyet (genel ve BT personeli bazında) ve dönemin taleplerini CSV olarak indirme

---

## Yol Haritası: Satılabilir Ürüne Doğru

İlk yol haritası tamamlandı. Sonraki aşamalar, bir şirketin satın almadan önce sorduklarını ve HelpdeskOps'u Türkçe arayüz, zimmet ve KVKK'ya uygun kayıtlar ve kendi sunucusunda çalışma isteyen Türk KOBİ'leri (20-200 çalışan) için öne çıkaracak özellikleri hedefler.

### 1. Aşama: Şirketlerin olmazsa olmazları
- [x] E-posta: uygulama içi her bildirim e-postayla da gider, şifre sıfırlama ve e-posta doğrulama çalışır, hepsi Türkçe (SMTP ya da yerelde `.eml` dosyaları)
- [x] Hesap güvenliği: 5 hatalı şifrede 15 dakikalık kilit (BT kullanıcı listesinden kilidi açabilir), doğrulama uygulamasıyla iki adımlı giriş (QR kod ve kurtarma kodları), en az 10 karakterlik şifreler ve Türkçe "Hesabım" sayfası
- [ ] Denetim kaydı: cihaz, talep, zimmet formu ve rollerde kim neyi ne zaman değiştirdi
- [ ] KVKK: aydınlatma metni, saklama süreleri, kişisel verilerin dışa aktarılması ve silinmesi
- [ ] Binlerce cihaz ve talep için sayfalama ve daha hızlı listeler
- [ ] Kendi sunucusuna kurulum rehberi (IIS ve Docker) ve yedekleme planı

### 2. Aşama: Farklılaştıran özellikler
- [ ] QR etiketler: cihazdaki etiketi okutmak o cihaz için talep (çalışan) ya da cihaz kartını (BT) açar; yazdırılabilir etiket sayfaları
- [ ] Active Directory / Microsoft Entra ID ile giriş ve kullanıcı eşitleme
- [ ] Otomatik envanter: küçük bir PowerShell aracı seri no, RAM, disk ve Windows sürümünü bildirir
- [ ] Hesapları, cihazları ve imzalı iadeleri birbirine bağlayan işe giriş ve çıkış kontrol listeleri
- [ ] Mesai saatine ve Türkiye'deki resmî tatillere göre SLA
- [ ] Yazılım lisansı ve sarf malzemesi (toner, kartuş) takibi

### 3. Aşama: Ek değer
- [ ] E-postayla talep açma
- [ ] Microsoft Teams bildirimleri
- [ ] Kategori, öncelik ve uygun çözüm makalesi için yapay zekâ önerileri

## Proje Durumu

**Geliştirme sürüyor.** Çekirdek veri modelleri (`Device`, `Ticket`, `ApplicationUser`) ilişkileri ve doğrulamalarıyla tamamlandı; cihaz ve talep yönetim ekranları Türkçe arayüzle çalışıyor. Kullanıcılar ASP.NET Core Identity ile oturum açıyor. Çalışanlar önce self-servis çözüm bankasını deneyip ardından kendi taleplerini oluşturup takip ederken BT yöneticileri cihazları yönetip kullanıcılara zimmetliyor, her cihazın bakım geçmişini tutuyor, garanti ve servis bilgilerini takip ediyor, cihazların ping'e yanıt verip vermediğini görüyor, bilgisayarlara RDP ile bağlanıyor, cihazları Cisco switch portlarıyla eşliyor, çalışanlar adına talep açıyor, talepleri tek tıkla üstleniyor, atıyor, düzenliyor ve kapatıyor. Talepler kategori ve alt kategoriye (serbest metinli "Diğer" dahil) göre açılıyor; talep listesinde arama ve filtreleme yapılabiliyor. Talep eden ile BT personeli talep üzerinden yorumlarla yazışıyor; her talep durum, öncelik ve atanan kişi değişikliklerinin geçmişini tutuyor. BT yöneticileri ayrıca çözüm sürelerini, kronik sorunlu cihazları, departman iş yükünü ve garantisi biten cihazları gösteren bir panoya ve döneme göre talep rakamları, BT personeli performansı ve CSV dışa aktarma sunan bir raporlar sayfasına sahip. Yöneticiler tüm talepleri okuyabiliyor, panoyu ve raporları görebiliyor ama hiçbir şeyi değiştiremiyor.

Bir cihaz zimmetlendiğinde çalışanın "Cihazlarım" sayfasından imzaladığı dijital bir zimmet formu açılıyor. Çalışanlar iade talebini imzayla gönderiyor; BT cihazı kontrol edip imzalayarak teslim alıyor. Her form imzalarıyla birlikte PDF olarak indirilebiliyor. BT yöneticileri kullanıcı rollerini kullanıcı listesinden yönetiyor. Mevcut yol haritasındaki tüm maddeler tamamlandı.

---

## Dokümantasyon

- [Kullanım Kılavuzu (Türkçe)](docs/USER_GUIDE_TR.md): uygulamanın mevcut arayüzünü ekran görüntüleriyle anlatan kılavuz.

---

## Başlarken

Ön koşullar: .NET 10 SDK ve EF Core komut satırı araçları (`dotnet tool install --global dotnet-ef`).

```bash
git clone https://github.com/kerembolek/HelpdeskOps.git
cd HelpdeskOps
dotnet restore
dotnet ef database update --project HelpdeskOps
dotnet run --project HelpdeskOps
```

Birim testlerini depo kökünden `dotnet test` ile çalıştırabilirsiniz.

Varsayılan bağlantı dizesi SQL Server LocalDB (`(localdb)\mssqllocaldb`) kullanır. Farklı bir SQL Server örneği kullanıyorsanız `appsettings.json` dosyasını güncelleyin.

Bekleyen migration'lar uygulama açılırken de otomatik uygulanır. Tarihler, sunucunun saat dilimi ne olursa olsun Türkiye saatiyle (`Europe/Istanbul`) gösterilir.

### Yayınlama

Canlı demo MonsterASP.NET üzerinde çalışır ve Visual Studio'dan Web Deploy ile yayınlanır. Ayarları `appsettings.json` dosyasının yanındaki `appsettings.Production.json` dosyasında durur; bu dosya barındırılan veritabanının bağlantı dizesini içerdiği için git tarafından yok sayılır:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "SeedDemoData": true,
  "DemoReadOnly": true
}
```

`SeedDemoData`, aşağıdaki demo verilerini `Development` dışındaki ortamlarda da açar. `DemoReadOnly` herkese açık demoyu salt okunur yapar: giriş ve çıkış dışındaki her form gönderimi bir uyarıyla reddedilir, kayıt ve hesap yönetimi sayfaları kapanır, bildirim açmak onu okundu yapmaz ve ziyaretçilere değişikliklerin kaydedilmediğini söyleyen bir şerit gösterilir. Yayınlama profili `App_Data` klasörünü dışarıda bırakır; yerel ekler ve anahtarlar sunucuya gitmez.

### E-posta

`Email` ayar bölümü e-postaların nasıl gönderileceğini seçer. `Mode` değeri `None` (varsayılan, hiçbir şey gönderilmez; canlı demo böyle çalışır), `Pickup` (her e-posta `App_Data/mail-outbox` klasörüne `.eml` dosyası olarak yazılır; `appsettings.Development.json` bunu açar) ya da `Smtp` olabilir. Gerçek gönderim için SMTP ayarlarını `appsettings.json` yerine git'in yok saydığı `appsettings.Production.json` dosyasına ya da user secrets'a yazın:

```json
{
  "Email": {
    "Mode": "Smtp",
    "Host": "smtp.example.com",
    "Port": 587,
    "UseStartTls": true,
    "UserName": "...",
    "Password": "...",
    "FromAddress": "helpdesk@example.com",
    "FromName": "HelpdeskOps"
  }
}
```

E-postalar kuyruğa alınıp arka planda gönderilir; yavaş ya da çalışmayan bir e-posta sunucusu sayfaları yavaşlatmaz. Uygulama içi her bildirim alıcısına e-postayla da gider. `Smtp` modunda yeni hesapların giriş yapmadan önce e-posta adresini doğrulaması gerekir ve giriş sayfasında şifre sıfırlama bağlantısı görünür.

### Demo Hesaplar

Uygulama `Development` ortamında çalıştığında aşağıdaki hesaplar, bu hesaplara zimmetli, bakım geçmişi ve farklı garanti durumları (garantide, yakında bitiyor, bitmiş) olan birkaç örnek cihaz, bu cihazların portlarına bağlı olduğu bir Cisco switch, zimmetli cihazlar için imzalı zimmet formları (biri imza bekliyor, biri BT'nin teslim alacağı bir iade talebi içeriyor), self-servis çözüm makaleleri ve pano için yorumları ve hareket geçmişiyle birlikte örnek talepler otomatik olarak oluşturulur. Diğer ortamlarda yalnızca `SeedDemoData` değeri `true` olduğunda oluşturulur:

| Rol      | E-posta                | Parola          |
|----------|------------------------|-----------------|
| Employee | `test@helpdeskops.com` | `Helpdesk2026!` |
| ITAdmin  | `it@helpdeskops.com`   | `Helpdesk2026!` |
| Manager  | `mudur@helpdeskops.com` | `Helpdesk2026!` |

Demo cihazlar uydurma `10.10.x.x` adresleri kullanır; ağınızda bu adreslerde cihaz yoksa ağ durumu noktaları kırmızı görünür.

Panoda departmanların karşılaştırılabilmesi için iki çalışan hesabı daha oluşturulur (`ik@helpdeskops.com`, `satis@helpdeskops.com`, parola aynı).

Kayıt sayfasından yeni kayıt olan kullanıcılara `Employee` rolü verilir.

---

## Yazar

**Kerem Bölek**
Bilgisayar Programcılığı mezunu | BT donanım ve destek geçmişi
[GitHub](https://github.com/kerembolek)
