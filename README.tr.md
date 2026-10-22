# HelpdeskOps

[English version](README.md)

ASP.NET Core MVC (.NET 10) ile geliştirilmiş, kurumsal tarzda bir BT envanter ve yardım masası (helpdesk) destek talebi yönetim sistemi.

Bu proje, gerçek bir kurum içi BT operasyon platformunu simüle eder: donanım envanterini takip etmek, destek taleplerini yönetmek ve çalışanlar, BT uzmanları ve yöneticiler için role dayalı panolar sunmak.

> Bir kurumsal BT departmanındaki staj deneyiminden esinlenilen bir portföy projesidir.

---

## Teknoloji Altyapısı

- **Çatı:** ASP.NET Core MVC (.NET 10)
- **Veritabanı:** Entity Framework Core (Code-First)
- **Kimlik doğrulama:** ASP.NET Core Identity ve role dayalı yetkilendirme (`Employee`, `ITAdmin`)
- **Arayüz:** Razor Views, Bootstrap
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

### Yardım Masası ve Talep Sistemi
- [x] Temel talep modeli (kategori, öncelik, durum, talep eden, atanan kişi)
- [x] Talep yönetim ekranları (listeleme, oluşturma, düzenleme, ayrıntı, silme)
- [x] Talep çözüldüğünde veya kapatıldığında çözülme tarihinin otomatik kaydedilmesi
- [x] Talep edenin oturum açmış kullanıcıdan otomatik belirlenmesi
- [x] Düzenleme ekranından talebin BT personeline atanması
- [x] BT personelinin çalışan adına talep açabilmesi (telefonla veya yüz yüze gelen talepler)
- [x] Atanmamış aktif talepler için tek tıkla "Üzerime Al"
- [x] Talep listesinde arama ve filtreler (durum, öncelik, aktif, çözülen/kapatılan, bana atananlar, atanmamış)
- [x] Talep eden ile BT personeli arasında talep yorumları
- [x] Talep hareket geçmişi: oluşturma ile her durum, öncelik ve atanan kişi değişikliği; kimin ne zaman yaptığıyla birlikte
- [x] Türkçe kullanıcı arayüzü
- [x] Tier 0 self-servis çözüm bankası: çalışanlar talep açmadan önce konu seçip adım adım çözümleri dener; "çözülmedi" denince talep formu doldurulmuş olarak açılır, BT personeli çözümleri yönetir ve her birinin kendi kendine çözüm oranını görür
- [x] Kademeli kategori yapısı: her kategorinin kendi alt kategorileri var, "Diğer" seçilince kısa bir serbest metin açıklama isteniyor; talep listesi kategoriye göre filtrelenebiliyor
- [x] Dijital zimmet formları: cihaz zimmetlenince cihaz ve çalışan bilgilerinin bir kopyasını saklayan form açılır, çalışan "Cihazlarım" sayfasından teslim aldığını onaylar, cihaz başkasına verilince veya silinince form kapanır ve her form PDF olarak indirilebilir

### Role Dayalı Erişim Kontrolü (RBAC)
- [x] Departman alanı olan temel kullanıcı modeli
- [x] Giriş ve kayıt (kayıt sırasında ad ve departman bilgisi alınır)
- [x] Çalışan (Employee) rolü: talep oluşturur ve yalnızca kendi taleplerini görür (kayıt olan kullanıcılara varsayılan olarak verilir)
- [x] BT Yöneticisi (ITAdmin) rolü: cihazlara ve talep yönetimine tam erişim (düzenleme, atama, silme)
- [x] "Cihazlarım" sayfası: her kullanıcı kendine zimmetli cihazları görür; çalışanlar taleplere yalnızca kendi cihazlarını ekleyebilir
- [x] Yönetici (Manager) rolü: tüm talepleri geçmişi ve yorumlarıyla salt okunur görme, pano ve raporlar; cihaz yönetimi ve talep değişikliği yok
- [x] BT yöneticileri için kullanıcı yönetimi: rolleri, zimmetli cihazları ve aktif talepleriyle aranabilir kullanıcı listesi ve kullanıcı bazında rol değiştirme (kimse kendi rolünü değiştiremez; BT personeli rolünden çıkan birinin aktif talepleri atanmamış taleplere döner)

### Yönetici Panosu ve Raporlama
- [x] Tıklanınca ilgili filtreli talep listesini açan özet kartları (açık, işlemde, çözülen/kapatılan, atanmamış aktif talepler)
- [x] Genel ve önceliğe göre ortalama talep çözüm süresi
- [x] Kronik sorunlu cihazlar (marka/modele göre en sık arızalar)
- [x] Departman iş yükü dağılımı
- [x] Tıklanınca ilgili filtreli cihaz listesini açan garanti kartları (30 gün içinde bitecek, garantisi bitmiş)
- [x] Seçilen döneme (7, 30, 90 gün, 1 yıl) göre raporlar sayfası: açılan/çözülen sayıları, kategori ve alt kategoriye göre talepler, BT personeli performansı ve dönemin taleplerini CSV olarak indirme

---

## Proje Durumu

**Geliştirme sürüyor.** Çekirdek veri modelleri (`Device`, `Ticket`, `ApplicationUser`) ilişkileri ve doğrulamalarıyla tamamlandı; cihaz ve talep yönetim ekranları Türkçe arayüzle çalışıyor. Kullanıcılar ASP.NET Core Identity ile oturum açıyor. Çalışanlar önce self-servis çözüm bankasını deneyip ardından kendi taleplerini oluşturup takip ederken BT yöneticileri cihazları yönetip kullanıcılara zimmetliyor, her cihazın bakım geçmişini tutuyor, garanti ve servis bilgilerini takip ediyor, cihazların ping'e yanıt verip vermediğini görüyor, bilgisayarlara RDP ile bağlanıyor, cihazları Cisco switch portlarıyla eşliyor, çalışanlar adına talep açıyor, talepleri tek tıkla üstleniyor, atıyor, düzenliyor ve kapatıyor. Talepler kategori ve alt kategoriye (serbest metinli "Diğer" dahil) göre açılıyor; talep listesinde arama ve filtreleme yapılabiliyor. Talep eden ile BT personeli talep üzerinden yorumlarla yazışıyor; her talep durum, öncelik ve atanan kişi değişikliklerinin geçmişini tutuyor. BT yöneticileri ayrıca çözüm sürelerini, kronik sorunlu cihazları, departman iş yükünü ve garantisi biten cihazları gösteren bir panoya ve döneme göre talep rakamları, BT personeli performansı ve CSV dışa aktarma sunan bir raporlar sayfasına sahip. Yöneticiler tüm talepleri okuyabiliyor, panoyu ve raporları görebiliyor ama hiçbir şeyi değiştiremiyor.

Bir cihaz zimmetlendiğinde çalışanın "Cihazlarım" sayfasından onayladığı dijital bir zimmet formu açılıyor ve her form PDF olarak indirilebiliyor. BT yöneticileri kullanıcı rollerini kullanıcı listesinden yönetiyor. Mevcut yol haritasındaki tüm maddeler tamamlandı.

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
dotnet ef database update
dotnet run
```

Varsayılan bağlantı dizesi SQL Server LocalDB (`(localdb)\mssqllocaldb`) kullanır. Farklı bir SQL Server örneği kullanıyorsanız `appsettings.json` dosyasını güncelleyin.

### Demo Hesaplar

Uygulama `Development` ortamında çalıştığında aşağıdaki hesaplar, bu hesaplara zimmetli, bakım geçmişi ve farklı garanti durumları (garantide, yakında bitiyor, bitmiş) olan birkaç örnek cihaz, bu cihazların portlarına bağlı olduğu bir Cisco switch, zimmetli cihazlar için zimmet formları (biri onay bekliyor), self-servis çözüm makaleleri ve pano için yorumları ve hareket geçmişiyle birlikte örnek talepler otomatik olarak oluşturulur (diğer ortamlarda asla oluşturulmaz):

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
