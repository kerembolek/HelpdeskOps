# HelpdeskOps Kullanım Kılavuzu

HelpdeskOps, kurumsal bir BT ortamında cihaz envanterini ve destek taleplerini tek bir arayüzden yönetmek için geliştirilmiş bir ASP.NET Core (.NET 10) web uygulamasıdır. Bu kılavuz, uygulamanın mevcut sürümündeki ekranları ve işlemleri açıklar. Kurulum adımları için [README.tr.md](../README.tr.md) dosyasına bakınız.

## İçindekiler

1. [Giriş ve Roller](#1-giriş-ve-roller)
2. [Ana Sayfa ve Menü](#2-ana-sayfa-ve-menü)
3. [Cihaz Yönetimi](#3-cihaz-yönetimi)
4. [Destek Talebi Yönetimi](#4-destek-talebi-yönetimi)

## 1. Giriş ve Roller

Uygulamayı kullanmak için sağ üstteki **Giriş Yap** bağlantısından e-posta ve parolayla oturum açılır. Hesabı olmayan kullanıcılar **Kayıt Ol** ile ad, soyad ve departman bilgisini girerek kayıt olabilir; yeni hesaplar **Çalışan** rolüyle açılır.

![Giriş Ekranı](images/01-login.png)

Her kullanıcının tek bir rolü vardır. Rol, kullanıcının hangi menüleri ve işlemleri göreceğini belirler:

| Rol | Yapabildikleri |
|---|---|
| Çalışan (Employee) | Kendin Çöz sayfasını kullanır, kendi taleplerini açar ve takip eder, kendine zimmetli cihazları ve zimmet formlarını görür. |
| Yönetici (Manager) | Tüm talepleri geçmişleri ve yorumlarıyla okur, Pano ve Raporlar sayfalarını görür. Hiçbir kaydı değiştiremez. |
| BT Personeli (ITAdmin) | Cihazları ve talepleri yönetir, talepleri atar ve kapatır, kullanıcı rollerini değiştirir, pano ve raporlara erişir. |

Geliştirme ortamında hazır demo hesapları ve parolaları için README.tr.md dosyasındaki **Demo Hesaplar** bölümüne bakınız.

## 2. Ana Sayfa ve Menü

Ana Sayfa, kullanıcının rolüne göre erişebildiği bölümlere kısayol düğmeleri sunar. Aynı bölümler üst menüde de yer alır. Sağ üstteki **Bildirimler** bağlantısı okunmamış bildirim sayısını gösterir (bkz. [Bildirimler](#bildirimler)).

![Ana Sayfa](images/02-home.png)

## 3. Cihaz Yönetimi

Cihaz yönetimi yalnızca **BT Personeli** rolüne açıktır.

### 3.1 Cihazları Listeleme

**Cihazlar** menüsü, kayıtlı tüm cihazları cihaz adı, IP adresi, seri numarası, tür, durum, garanti bitişi ve zimmetli kullanıcıyla listeler.

![Cihaz Listesi](images/03-device-list.png)

- IP adresinin solundaki nokta cihazın ağ durumunu gösterir: yeşil nokta cihazın ping'e yanıt verdiğini, kırmızı nokta yanıt vermediğini belirtir. **Ağ Durumunu Yenile** düğmesi kontrolü tekrarlar.
- Garanti sütunundaki rozetler garantinin durumunu gösterir: **Garantide**, **Yakında Bitiyor** (30 gün içinde) ve **Garanti Bitti**. Üstteki **Garanti** filtresiyle yalnızca garantisi yakında bitecek ya da bitmiş cihazlar listelenebilir.
- Dizüstü ve masaüstü bilgisayarların satırındaki **RDP** bağlantısı, Uzak Masaüstü Bağlantısı ile açılan bir `.rdp` dosyası indirir.

### 3.2 Yeni Cihaz Ekleme

Cihazlar sayfasındaki **Yeni Cihaz Ekle** düğmesi, cihaz kayıt formunu açar.

![Yeni Cihaz Formu](images/04-device-create.png)

| Alan | Açıklama |
|---|---|
| Cihaz Adı | Cihazın ağdaki adı (hostname). Zorunludur. |
| IP Adresi (IPv4) | Geçerli bir IPv4 adresi olmalıdır. Zorunludur. |
| Seri Numarası | Üretici seri numarası. Zorunludur. |
| Marka / Model | Cihazın markası ve modeli. |
| Cihaz Türü | Dizüstü, masaüstü, yazıcı, switch, router, monitör veya diğer. |
| Satın Alma Tarihi / Garanti Bitiş Tarihi | Garanti takibinde kullanılır. Satın alma tarihi ileri bir tarih, garanti bitişi de satın alma tarihinden önce olamaz. |
| Servis / Tedarikçi | Garanti ve onarım için firma adı, telefon ve e-posta. |
| Cihaz Durumu | Aktif, Depoda, Arızalı veya Hizmet Dışı. |
| Zimmetli Kullanıcı | Cihazın verildiği kişi. Seçilirse zimmet kaydı ve zimmet formu otomatik oluşturulur. |
| Switch / Port / VLAN | Cihazın bağlı olduğu Cisco switch, port (örn. `Gi1/0/12`) ve VLAN. Bir porta yalnızca bir cihaz bağlanabilir. |

Oluşturulma ve güncellenme tarihleri sistem tarafından atanır.

### 3.3 Cihaz Ayrıntıları

Listedeki **Detay** bağlantısı cihazın tüm bilgilerini, garanti ve servis bilgilerini, bağlı olduğu switch portunu gösterir.

![Cihaz Detayı](images/05-device-details.png)

Sayfanın alt bölümlerinde şunlar bulunur:

- **Port Tablosu** (yalnızca switch'lerde): switch'in hangi portuna hangi cihazın bağlı olduğu.
- **Zimmet Formları**: cihaz her zimmetlendiğinde açılan formlar, durumları (onay bekliyor, teslim alındı, iade edildi) ve **PDF İndir** bağlantısı. PDF, imza alanlı bir teslim formudur.
- **Cihaz Geçmişi**: format, donanım yükseltme, parça değişimi ve onarım kayıtları ile otomatik tutulan zimmet, durum ve port değişiklikleri. BT personeli bu bölümdeki formdan elle kayıt ekleyebilir.

### 3.4 CSV ile İçe ve Dışa Aktarma

Cihazlar sayfasındaki **CSV'den İçe Aktar** düğmesi, birden çok cihazı tek seferde eklemeyi sağlar.

![Cihaz İçe Aktarma](images/06-device-import.png)

1. **Örnek Şablonu İndir** ile şablonu indirin ve Excel'de doldurun.
2. Dosyayı "CSV UTF-8" olarak kaydedip seçin ve **İçe Aktar**'a basın.
3. Herhangi bir satırda sorun varsa hiçbir cihaz eklenmez; sorunlar satır numaralarıyla listelenir. Düzeltip dosyayı yeniden yükleyin.

Tek seferde en fazla 500 cihaz eklenebilir. Zimmetli olarak içe aktarılan cihazlar için zimmet kaydı ve zimmet formu otomatik oluşturulur. **CSV Olarak Dışa Aktar** düğmesi tüm cihazları aynı biçimde indirir.

## 4. Destek Talebi Yönetimi

### 4.1 Talepleri Listeleme

**Talepler** menüsü, destek taleplerini başlık, kategori, öncelik, durum, oluşturulma tarihi, ilişkili cihaz, talep eden ve atanan kişi bilgileriyle listeler. Tarihler kullanıcının yerel saat dilimine göre gösterilir.

![Talep Listesi](images/04-ticket-list.png)

### 4.2 Yeni Talep Oluşturma

**Yeni Talep Oluştur** bağlantısı talep formunu açar.

![Yeni Talep Formu](images/05-ticket-create.png)

| Alan | Açıklama |
|---|---|
| Başlık | Sorunun kısa özeti. |
| Açıklama | Sorunun ayrıntılı tanımı. |
| Kategori | Talebin kategorisi (ör. Donanım). |
| Öncelik | Talebin aciliyet düzeyi (ör. Düşük, Yüksek). |
| Cihaz | Talebin ilişkili olduğu envanter kaydı. Seçilmesi isteğe bağlıdır. |

Talebin oluşturulma tarihi, durumu ve talep eden bilgisi form üzerinden girilmez, sistem tarafından atanır.

### 4.3 Talep Ayrıntıları ve Durum Takibi

Listedeki **Detaylar** bağlantısı, talebin tüm bilgilerini gösterir.

![Talep Detayı](images/06-ticket-details.png)

Talebin durumu **Düzenle** ekranından güncellenir. Kullanılabilir durumlar:

| Durum | Anlamı |
|---|---|
| Açık | Talep alındı, henüz işleme başlanmadı. |
| İşlemde | Talep üzerinde çalışılıyor. |
| Çözüldü | Sorun giderildi. |
| Kapatıldı | Talep sonlandırıldı. |

Durum **Çözüldü** ya da **Kapatıldı** olarak kaydedildiğinde **Çözülme Tarihi** sistem tarafından otomatik olarak yazılır. Talep daha sonra **Açık** veya **İşlemde** durumuna geri alınırsa Çözülme Tarihi temizlenir. Çözüldü durumundan Kapatıldı durumuna geçişte ilk çözülme tarihi korunur.
