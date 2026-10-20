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
- [ ] Canlı ağ durumu göstergesi (ping tabanlı, yeşil/kırmızı nokta)
- [ ] Uzak erişim kısayolu (RDP başlatma bağlantısı)
- [ ] Cisco switch port eşleme
- [ ] Cihaz yaşam döngüsü / denetim izi (format, yükseltme ve parça değişimi geçmişi)
- [ ] Garanti takibi ve servis/tedarikçi iletişim entegrasyonu

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
- [ ] Tier 0 self-servis çözüm bankası (talep açmadan önce yönlendirmeli sorun giderme)
- [ ] Serbest metinli "Diğer" seçeneği olan kademeli kategori yapısı
- [ ] Otomatik PDF kayıtlı dijital zimmet formları

### Role Dayalı Erişim Kontrolü (RBAC)
- [x] Departman alanı olan temel kullanıcı modeli
- [x] Giriş ve kayıt (kayıt sırasında ad ve departman bilgisi alınır)
- [x] Çalışan (Employee) rolü: talep oluşturur ve yalnızca kendi taleplerini görür (kayıt olan kullanıcılara varsayılan olarak verilir)
- [x] BT Yöneticisi (ITAdmin) rolü: cihazlara ve talep yönetimine tam erişim (düzenleme, atama, silme)
- [x] "Cihazlarım" sayfası: her kullanıcı kendine zimmetli cihazları görür; çalışanlar taleplere yalnızca kendi cihazlarını ekleyebilir
- [ ] Yönetici rolü: talep geçmişi ve yönetsel raporlar

### Yönetici Panosu ve Raporlama
- [x] Tıklanınca ilgili filtreli talep listesini açan özet kartları (açık, işlemde, çözülen/kapatılan, atanmamış aktif talepler)
- [x] Genel ve önceliğe göre ortalama talep çözüm süresi
- [x] Kronik sorunlu cihazlar (marka/modele göre en sık arızalar)
- [x] Departman iş yükü dağılımı

---

## Proje Durumu

**Geliştirme sürüyor.** Çekirdek veri modelleri (`Device`, `Ticket`, `ApplicationUser`) ilişkileri ve doğrulamalarıyla tamamlandı; cihaz ve talep yönetim ekranları Türkçe arayüzle çalışıyor. Kullanıcılar ASP.NET Core Identity ile oturum açıyor. Çalışanlar kendi taleplerini oluşturup takip ederken BT yöneticileri cihazları yönetip kullanıcılara zimmetliyor, çalışanlar adına talep açıyor, talepleri tek tıkla üstleniyor, atıyor, düzenliyor ve kapatıyor. Talep listesinde arama ve filtreleme yapılabiliyor. Talep eden ile BT personeli talep üzerinden yorumlarla yazışıyor; her talep durum, öncelik ve atanan kişi değişikliklerinin geçmişini tutuyor. BT yöneticileri ayrıca çözüm sürelerini, kronik sorunlu cihazları ve departman iş yükünü gösteren bir panoya sahip.

Henüz uygulanmayanlar: yönetici rolü ve bu role özel raporlar.

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

Uygulama `Development` ortamında çalıştığında aşağıdaki hesaplar, bu hesaplara zimmetli birkaç örnek cihaz ve pano için yorumları ve hareket geçmişiyle birlikte örnek talepler otomatik olarak oluşturulur (diğer ortamlarda asla oluşturulmaz):

| Rol      | E-posta                | Parola          |
|----------|------------------------|-----------------|
| Employee | `test@helpdeskops.com` | `Helpdesk2026!` |
| ITAdmin  | `it@helpdeskops.com`   | `Helpdesk2026!` |

Panoda departmanların karşılaştırılabilmesi için iki çalışan hesabı daha oluşturulur (`ik@helpdeskops.com`, `satis@helpdeskops.com`, parola aynı).

Kayıt sayfasından yeni kayıt olan kullanıcılara `Employee` rolü verilir.

---

## Yazar

**Kerem Bölek**
Bilgisayar Programcılığı mezunu | BT donanım ve destek geçmişi
[GitHub](https://github.com/kerembolek)
