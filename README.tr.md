# HelpdeskOps

[English version](README.md)

ASP.NET Core MVC (.NET 10) ile geliştirilmiş, kurumsal tarzda bir BT envanter ve yardım masası (helpdesk) destek talebi yönetim sistemi.

Bu proje, gerçek bir kurum içi BT operasyon platformunu simüle eder: donanım envanterini takip etmek, destek taleplerini yönetmek ve çalışanlar, BT uzmanları ve yöneticiler için role dayalı panolar sunmak.

> Bir kurumsal BT departmanındaki staj deneyiminden esinlenilen bir portföy projesidir.

---

## Teknoloji Altyapısı

- **Çatı:** ASP.NET Core MVC (.NET 10)
- **Veritabanı:** Entity Framework Core (Code-First)
- **Kimlik doğrulama:** ASP.NET Core Identity (kullanıcı modeli hazır; giriş ve role dayalı erişim planlanıyor)
- **Arayüz:** Razor Views, Bootstrap
- **Sürüm kontrolü:** Git ve GitHub

---

## Özellikler

### Donanım Envanteri ve Ağ Yönetimi
- [x] Temel cihaz modeli (cihaz adı, IPv4, seri numarası, marka/model, cihaz türü, durum)
- [x] IPv4 biçim doğrulaması
- [x] Cihaz yönetim ekranları (listeleme, ekleme, düzenleme, ayrıntı, silme)
- [ ] Canlı ağ durumu göstergesi (ping tabanlı, yeşil/kırmızı nokta)
- [ ] Uzak erişim kısayolu (RDP başlatma bağlantısı)
- [ ] Cisco switch port eşleme
- [ ] Cihaz yaşam döngüsü / denetim izi (format, yükseltme ve parça değişimi geçmişi)
- [ ] Garanti takibi ve servis/tedarikçi iletişim entegrasyonu

### Yardım Masası ve Talep Sistemi
- [x] Temel talep modeli (kategori, öncelik, durum, talep eden, atanan kişi)
- [x] Talep yönetim ekranları (listeleme, oluşturma, düzenleme, ayrıntı, silme)
- [x] Talep çözüldüğünde veya kapatıldığında çözülme tarihinin otomatik kaydedilmesi
- [x] Türkçe kullanıcı arayüzü
- [ ] Tier 0 self-servis çözüm bankası (talep açmadan önce yönlendirmeli sorun giderme)
- [ ] Serbest metinli "Diğer" seçeneği olan kademeli kategori yapısı
- [ ] Otomatik PDF kayıtlı dijital zimmet formları

### Role Dayalı Erişim Kontrolü (RBAC)
- [x] Departman alanı olan temel kullanıcı modeli
- [ ] Çalışan rolü: yalnızca kendine atanan cihazları görür, talep oluşturur
- [ ] BT Uzmanı rolü: cihazlara, ağ bilgilerine ve talep yönetimine tam erişim
- [ ] Yönetici rolü: talep geçmişi ve yönetsel raporlar

### Yönetici Panosu ve Raporlama
- [ ] Ortalama talep çözüm süresi
- [ ] Kronik sorunlu cihazlar (marka/modele göre en sık arızalar)
- [ ] Departman iş yükü dağılımı

---

## Proje Durumu

**Geliştirme sürüyor.** Çekirdek veri modelleri (`Device`, `Ticket`, `ApplicationUser`) ilişkileri ve doğrulamalarıyla tamamlandı, veritabanı EF Core Identity ile yapılandırıldı ve ilk migration uygulandı. Cihaz ve talep yönetim ekranları Türkçe arayüzle çalışıyor.

Henüz uygulanmayanlar: kullanıcı girişi ve role dayalı erişim (talepler şu an geçici bir geliştirme kullanıcısı adına oluşturuluyor), arayüzden talep atama, panolar ve raporlar.

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

---

## Yazar

**Kerem Bölek**
Bilgisayar Programcılığı mezunu | BT donanım ve destek geçmişi
[GitHub](https://github.com/kerembolek)
