# HelpdeskOps Kullanım Kılavuzu

HelpdeskOps, kurumsal bir BT ortamında cihaz envanterini ve destek taleplerini tek bir arayüzden yönetmek için geliştirilmiş bir ASP.NET Core (.NET 10) web uygulamasıdır. Bu kılavuz, uygulamanın mevcut sürümündeki ekranları ve işlemleri açıklar. Kurulum adımları için [README.tr.md](../README.tr.md) dosyasına bakınız.

## İçindekiler

1. [Giriş ve Roller](#1-giriş-ve-roller)
2. [Ana Sayfa ve Menü](#2-ana-sayfa-ve-menü)
3. [Cihaz Yönetimi](#3-cihaz-yönetimi)
4. [Kendin Çöz](#4-kendin-çöz)
5. [Destek Talebi Yönetimi](#5-destek-talebi-yönetimi)
6. [Cihazlarım ve Zimmet Formları](#6-cihazlarım-ve-zimmet-formları)
7. [Bildirimler](#7-bildirimler)

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

Ana Sayfa, kullanıcının rolüne göre erişebildiği bölümlere kısayol düğmeleri sunar. Aynı bölümler üst menüde de yer alır. Sağ üstteki **Bildirimler** bağlantısı okunmamış bildirim sayısını gösterir (bkz. [Bildirimler](#7-bildirimler)).

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

## 4. Kendin Çöz

Çalışanlar talep açmadan önce **Kendin Çöz** sayfasında sık karşılaşılan sorunların çözümlerini dener. Konu kartlarından biri seçildiğinde o konudaki çözümler adım adım listelenir.

![Kendin Çöz](images/11-self-service.png)

- **Sorunum çözüldü** düğmesi çözümün işe yaradığını kaydeder.
- **Çözülmedi, talep aç** düğmesi talep formunu kategori, başlık ve denenen çözüm bilgisi doldurulmuş olarak açar.

BT personeli **Kendin Çöz → Çözümleri Yönet** ekranından çözüm makalelerini ekler, düzenler, yayından kaldırır ve her makalenin self-servis başarı oranını görür.

## 5. Destek Talebi Yönetimi

### 5.1 Talepleri Listeleme

**Talepler** menüsü talepleri başlık, kategori, öncelik, durum, hedef süre, oluşturulma tarihi, cihaz, talep eden ve atanan kişi bilgileriyle listeler. Çalışanlar yalnızca kendi taleplerini, BT personeli ve yöneticiler tüm talepleri görür.

![Talep Listesi](images/07-ticket-list.png)

- Üstteki filtrelerle başlık veya açıklamada arama yapılabilir; kategori, durum ve önceliğe göre süzülebilir.
- **Görünüm** listesi aktif talepleri, çözülen/kapatılan talepleri, hedef süresi geçenleri, BT personeline atananları ve atanmamış talepleri ayırır.
- **Hedef Süre** sütunu önceliğe göre belirlenen çözüm hedefine (Kritik 4, Yüksek 8, Orta 24, Düşük 72 saat) göre talebin durumunu gösterir: **Zamanında**, **Süre doluyor**, **Gecikti**, **Hedefte çözüldü**, **Geç çözüldü**.
- BT personeli atanmamış aktif talepleri **Üzerime Al** düğmesiyle tek tıkla üstlenir; açık talep bu sırada **İşlemde** durumuna geçer.

### 5.2 Yeni Talep Oluşturma

Çalışanlar için **Yeni Talep Oluştur** bağlantısı önce Kendin Çöz sayfasına yönlendirir; oradan doğrudan talep formuna geçilebilir. BT personeli formu doğrudan açar ve telefonla ya da yüz yüze gelen talepleri **Talep Eden** alanından çalışan adına açabilir.

![Yeni Talep Formu](images/08-ticket-create.png)

| Alan | Açıklama |
|---|---|
| Başlık / Açıklama | Sorunun kısa özeti ve ayrıntılı tanımı. Zorunludur. |
| Kategori / Alt Kategori | Örneğin Donanım › Monitör / ekran. Alt kategori listesi seçilen kategoriye göre değişir. **Diğer** seçildiğinde konuyu kısaca yazmak gerekir. |
| Öncelik | Düşük, Orta, Yüksek veya Kritik. Hedef çözüm süresini belirler. |
| Cihaz | Talebin ilgili olduğu cihaz. Çalışanlar yalnızca kendilerine zimmetli cihazları seçebilir. İsteğe bağlıdır. |
| Ekler | Ekran görüntüsü, PDF, metin/log, Word veya Excel dosyası; en fazla 5 dosya, her biri en fazla 5 MB. İsteğe bağlıdır. |

Talebin oluşturulma tarihi, durumu ve talep eden bilgisi sistem tarafından atanır.

### 5.3 Talep Ayrıntıları

Listedeki **Detaylar** bağlantısı talebin tüm bilgilerini, hedef çözüm zamanını ve atanan kişiyi gösterir.

![Talep Detayı](images/09-ticket-details.png)

Sayfanın alt bölümlerinde şunlar bulunur:

![Talep Detayı, alt bölümler](images/10-ticket-details-bottom.png)

- **Memnuniyet**: talep çözüldükten veya kapatıldıktan sonra talep eden kişi talebi bir kez 1-5 arası puanlar ve isterse yorum ekler. Verilen puan herkes tarafından görülür.
- **Ekler**: talebe eklenen dosyalar. Talep eden ve BT personeli dosya yükleyebilir; herkes kendi yüklediği dosyayı, BT personeli ise tüm dosyaları silebilir. Dosyaları yalnızca talebi görebilenler indirebilir.
- **Hareket Geçmişi**: talebin oluşturulması, durum, öncelik ve atanan kişi değişiklikleri ve değerlendirme; kimin yaptığı ve ne zaman yapıldığıyla.
- **Yorumlar**: talep eden ile BT personeli arasındaki yazışma.

### 5.4 Durum Takibi

BT personeli talebi **Düzenle** ekranından günceller ve bir BT personeline atar. Kullanılabilir durumlar:

| Durum | Anlamı |
|---|---|
| Açık | Talep alındı, henüz işleme başlanmadı. |
| İşlemde | Talep üzerinde çalışılıyor. |
| Çözüldü | Sorun giderildi. |
| Kapatıldı | Talep sonlandırıldı. |

Durum **Çözüldü** ya da **Kapatıldı** olarak kaydedildiğinde **Çözülme Tarihi** otomatik yazılır. Talep **Açık** veya **İşlemde** durumuna geri alınırsa Çözülme Tarihi temizlenir. Çözüldü durumundan Kapatıldı durumuna geçişte ilk çözülme tarihi korunur.

## 6. Cihazlarım ve Zimmet Formları

**Cihazlarım** sayfası, oturum açan kullanıcıya zimmetli cihazları garanti durumlarıyla listeler.

![Cihazlarım](images/12-my-devices.png)

**Zimmet Formlarım** bölümünde kullanıcı adına açılmış zimmet formları yer alır. Cihazı teslim alan kullanıcı **Teslim Aldım** düğmesiyle formu onaylar. **PDF İndir** düğmesi imza alanlı teslim formunu indirir. Cihaz başka birine verildiğinde, depoya alındığında veya silindiğinde form **İade edildi** olarak kapanır.

## 7. Bildirimler

Sağ üstteki **Bildirimler** bağlantısının yanındaki sayı okunmamış bildirimleri gösterir. Bildirimler şu durumlarda oluşur:

- Talebinize BT ekibi yorum yazdığında veya talebinizin durumu değiştiğinde,
- Talebiniz BT ekibi tarafından üstlenildiğinde ya da adınıza talep açıldığında,
- (BT personeli için) bir talep size atandığında, talep eden yorum yazdığında veya talebi değerlendirdiğinde.

![Bildirimler](images/13-notifications.png)

Bir bildirime tıklamak ilgili talebi açar ve bildirimi okunmuş sayar. **Tümünü Okundu İşaretle** düğmesi tüm bildirimleri okunmuş yapar.
