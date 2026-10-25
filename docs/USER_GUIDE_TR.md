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
8. [Pano](#8-pano)
9. [Raporlar](#9-raporlar)
10. [Kullanıcılar](#10-kullanıcılar)
11. [Hesabım ve Güvenlik](#11-hesabım-ve-güvenlik)

## 1. Giriş ve Roller

Uygulamayı kullanmak için **Giriş Yap** sayfasında e-posta ve parolayla oturum açılır. Hesabı olmayan kullanıcılar **Kayıt olun** bağlantısıyla ad, soyad ve departman bilgisini girerek kayıt olabilir; yeni hesaplar **Çalışan** rolüyle açılır. Demo sitesinde giriş formunun altındaki **Çalışan**, **BT Uzmanı** ve **Yönetici** düğmeleri formu ilgili demo hesabıyla doldurur.

E-posta gönderimi ayarlanmışsa giriş sayfasında iki bağlantı daha görünür:

- **Şifremi unuttum**: e-posta adresine bir şifre sıfırlama bağlantısı gönderir; bağlantı yeni şifre belirleme sayfasını açar.
- **Doğrulama e-postası**: kayıt sırasında gelen doğrulama e-postası kaybolduysa yeniden gönderir. Gerçek e-posta gönderimi açıkken yeni hesaplar adreslerini doğrulamadan giriş yapamaz.

![Giriş Ekranı](images/01-login.png)

Her kullanıcının tek bir rolü vardır. Rol, kullanıcının hangi menüleri ve işlemleri göreceğini belirler:

| Rol | Yapabildikleri |
|---|---|
| Çalışan (Employee) | Kendin Çöz sayfasını kullanır, kendi taleplerini açar ve takip eder, kendine zimmetli cihazları ve zimmet formlarını görür. |
| Yönetici (Manager) | Tüm talepleri geçmişleri ve yorumlarıyla okur, Pano ve Raporlar sayfalarını görür. Hiçbir kaydı değiştiremez. |
| BT Personeli (ITAdmin) | Cihazları ve talepleri yönetir, talepleri atar ve kapatır, kullanıcı rollerini değiştirir, pano ve raporlara erişir. |

Geliştirme ortamında ve demo sitesinde hazır demo hesapları bulunur; hesaplar ve parola için README.tr.md dosyasındaki **Demo Hesaplar** bölümüne bakınız. Herkese açık demo sitesi salt okunurdur: tüm ekranlar gezilebilir ama hiçbir değişiklik kaydedilmez ve kayıt olunamaz.

## 2. Ana Sayfa ve Menü

Ana Sayfa, kullanıcıyı adıyla karşılar ve rolüne göre canlı sayılar içeren kısayol kutucukları gösterir:

- **Herkes için**: Yeni Talep, Taleplerim (açık ve işlemdeki talepler), Cihazlarım, imza bekleyen zimmet formu varsa **İmza Bekleyen Formlar** ve Kendin Çöz.
- **BT İşleri** (BT personeli ve yöneticiler): Bana Atanan, Atanmamış Talepler, Hedef Süresi Aşan, Tüm Aktif Talepler, İade Bekleyen Cihazlar ve Garantisi Bitecekler (BT personeli), Pano ve Raporlar. Bekleyen iş olan kutucuklar sarı ya da kırmızı görünür.

Aynı bölümler üst menüde ikonlarıyla yer alır; bulunulan bölüm vurgulanır. Sağ üstteki zil ikonu okunmamış bildirim sayısını gösterir (bkz. [Bildirimler](#7-bildirimler)); kişi ikonundaki menüden oturum kapatılır.

![Ana Sayfa](images/02-home.png)

## 3. Cihaz Yönetimi

Cihaz yönetimi yalnızca **BT Personeli** rolüne açıktır.

### 3.1 Cihazları Listeleme

**Cihazlar** menüsü, kayıtlı tüm cihazları cihaz adı, IP adresi, seri numarası, tür, durum, garanti bitişi ve zimmetli kullanıcıyla listeler.

![Cihaz Listesi](images/03-device-list.png)

- IP adresinin solundaki nokta cihazın ağ durumunu gösterir: yeşil nokta cihazın ping'e yanıt verdiğini, kırmızı nokta yanıt vermediğini belirtir. **Ağ Durumunu Yenile** düğmesi kontrolü tekrarlar.
- Garanti sütunundaki rozetler garantinin durumunu gösterir: **Garantide**, **Yakında Bitiyor** (30 gün içinde) ve **Garanti Bitti**. Üstteki **Garanti** filtresiyle yalnızca garantisi yakında bitecek ya da bitmiş cihazlar listelenebilir.
- Dizüstü ve masaüstü bilgisayarların satırındaki **RDP** bağlantısı, Uzak Masaüstü Bağlantısı ile açılan bir `.rdp` dosyası indirir.
- Çalışanların iade talebi gönderdiği cihazlar listenin üstündeki sarı kutuda sıralanır; cihaz adına tıklamak teslim alma ekranını açar (bkz. [3.5 Geri Gelen Cihazı Teslim Alma](#35-geri-gelen-cihazı-teslim-alma)).

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
- **Zimmet Formları**: cihaz her zimmetlendiğinde açılan formlar, durumları (onay bekliyor, teslim alındı, iade bekliyor, iade edildi), açık formlar için **Teslim Al** düğmesi ve **PDF İndir** bağlantısı. PDF'te teslim bilgileri ve çalışanın imzası, iade edilmiş formlarda ikinci sayfada iade bilgileri ve iki tarafın iade imzaları yer alır.
- **Cihaz Geçmişi**: format, donanım yükseltme, parça değişimi ve onarım kayıtları ile otomatik tutulan zimmet, durum ve port değişiklikleri. BT personeli bu bölümdeki formdan elle kayıt ekleyebilir.

### 3.4 CSV ile İçe ve Dışa Aktarma

Cihazlar sayfasındaki **CSV'den İçe Aktar** düğmesi, birden çok cihazı tek seferde eklemeyi sağlar.

![Cihaz İçe Aktarma](images/06-device-import.png)

1. **Örnek Şablonu İndir** ile şablonu indirin ve Excel'de doldurun.
2. Dosyayı "CSV UTF-8" olarak kaydedip seçin ve **İçe Aktar**'a basın.
3. Herhangi bir satırda sorun varsa hiçbir cihaz eklenmez; sorunlar satır numaralarıyla listelenir. Düzeltip dosyayı yeniden yükleyin.

Tek seferde en fazla 500 cihaz eklenebilir. Zimmetli olarak içe aktarılan cihazlar için zimmet kaydı ve zimmet formu otomatik oluşturulur. **CSV Olarak Dışa Aktar** düğmesi tüm cihazları aynı biçimde indirir.

### 3.5 Geri Gelen Cihazı Teslim Alma

Çalışan "Cihazlarım" sayfasından iade talebi gönderdiğinde tüm BT personeline bildirim gider. Cihaz BT'ye getirildiğinde **Teslim Al** ekranında çalışanın iade sebebi, açıklaması ve imzası görülür.

![Cihazı Teslim Al](images/18-check-in.png)

1. Cihazı fiziksel olarak kontrol edip **Kontrol Sonucu**'nu seçin: Sağlam, Hasarlı veya Eksik parça / aksesuar.
2. Gerekirse **Kontrol Notu** yazın (örn. "şarj aleti eksik").
3. **Cihazın Yeni Durumu**'nu seçin: Depoda, Arızalı veya Hizmet Dışı.
4. İmza alanına imzanızı atıp **İmzala ve Teslim Al**'a basın.

Zimmet formu kapanır, cihaz çalışanın üzerinden düşer, cihaz geçmişine iade ve kontrol sonucu yazılır ve çalışana bildirim gider. Çalışan iade talebi göndermeden cihazı elden getirdiyse (örneğin işten ayrılışta) teslim alma yine aynı ekrandan yapılabilir.

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

- Öncelik (Kritik kırmızıdan Düşük griye) ve durum (Açık, İşlemde, Çözüldü, Kapatıldı) renkli rozetlerle gösterilir.
- Üstteki filtrelerle başlık veya açıklamada arama yapılabilir; kategori, durum ve önceliğe göre süzülebilir.
- **Görünüm** listesi aktif talepleri, çözülen/kapatılan talepleri, hedef süresi geçenleri, BT personeline atananları ve atanmamış talepleri ayırır.
- **Hedef Süre** sütunu önceliğe göre belirlenen çözüm hedefine (Kritik 4, Yüksek 8, Orta 24, Düşük 72 saat) göre talebin durumunu gösterir: **Zamanında**, **Süre doluyor**, **Gecikti**, **Hedefte çözüldü**, **Geç çözüldü**.
- BT personeli atanmamış aktif talepleri **Üzerime Al** düğmesiyle tek tıkla üstlenir; açık talep bu sırada **İşlemde** durumuna geçer.

### 5.2 Yeni Talep Oluşturma

Çalışanlar için **Yeni Talep Oluştur** düğmesi önce Kendin Çöz sayfasına yönlendirir; oradan doğrudan talep formuna geçilebilir. BT personeli formu doğrudan açar ve telefonla ya da yüz yüze gelen talepleri **Talep Eden** alanından çalışan adına açabilir.

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

**Zimmet Formlarım** bölümünde kullanıcı adına açılmış zimmet formları yer alır:

- **İmzala**: cihazı teslim alan kullanıcı form bilgilerini ve taahhüt metnini görür, imza alanına fare ya da parmağıyla imzasını atıp **İmzala ve Teslim Aldım**'a basar.
- **İade Et**: imzalanmış bir cihazı iade etmek için sebep (işten ayrılış, cihaz değişimi, arıza, görev değişikliği, diğer) seçilir, isteğe bağlı açıklama yazılır ve imza atılır. Form **İade bekliyor** durumuna geçer ve BT personeline bildirim gider.
- **PDF İndir**: zimmet formunu imzalarıyla birlikte indirir.

![Cihaz İadesi](images/17-return-request.png)

BT personeli cihazı kontrol edip teslim aldığında (bkz. [3.5 Geri Gelen Cihazı Teslim Alma](#35-geri-gelen-cihazı-teslim-alma)) form **İade edildi** olarak kapanır. Cihaz başka birine verildiğinde, depoya alındığında veya silindiğinde de form kapanır.

## 7. Bildirimler

Sağ üstteki zil ikonunun yanındaki sayı okunmamış bildirimleri gösterir. Bildirimler şu durumlarda oluşur:

- Talebinize BT ekibi yorum yazdığında veya talebinizin durumu değiştiğinde,
- Talebiniz BT ekibi tarafından üstlenildiğinde ya da adınıza talep açıldığında,
- (BT personeli için) bir talep size atandığında, talep eden yorum yazdığında veya talebi değerlendirdiğinde,
- (BT personeli için) bir çalışan cihaz iade talebi gönderdiğinde; çalışan da cihazı teslim alındığında bildirim alır.

![Bildirimler](images/13-notifications.png)

Bir bildirime tıklamak ilgili sayfayı açar ve bildirimi okunmuş sayar. E-posta gönderimi ayarlanmışsa her bildirim, ilgili sayfaya giden bir bağlantıyla birlikte e-postayla da gönderilir. **Tümünü Okundu İşaretle** düğmesi tüm bildirimleri okunmuş yapar.

## 8. Pano

**Pano**, BT personeli ve yöneticilere taleplerin ve cihazların genel durumunu tek sayfada gösterir.

![Pano](images/14-dashboard.png)

- **Özet kartları**: ikonlu kartlarda açık, işlemde, çözülen/kapatılan ve atanmamış aktif talep sayıları ile ortalama çözüm süresi. Kartlara tıklamak ilgili filtreli talep listesini açar.
- **Hedef Çözüm Süreleri (SLA)**: hedef süresi geçen aktif talepler ve hedefinde çözülen taleplerin oranı.
- **Önceliğe Göre Çözüm Süresi** ve **Departman İş Yükü** tabloları.
- **Kronik Sorunlu Cihazlar**: en çok talep alan marka ve modeller.
- **Cihaz Garantileri**: 30 gün içinde garantisi bitecek ve garantisi bitmiş cihaz sayıları. BT personeli için kartlar filtreli cihaz listesini açar.

## 9. Raporlar

**Raporlar** sayfası seçilen döneme (son 7, 30, 90 gün veya 1 yıl) ait rakamları gösterir. BT personeli ve yöneticiler erişebilir.

![Raporlar](images/15-reports.png)

- Dönemde açılan, çözülen ve hâlâ aktif olan talepler, ortalama çözüm süresi ve ortalama memnuniyet puanı.
- Kategori ve alt kategoriye göre talep sayıları ve ortalama çözüm süreleri.
- BT personeli performansı: her personelin üzerindeki aktif talepler, dönemde çözdüğü talepler, ortalama çözüm süresi ve ortalama memnuniyet puanı.
- **Talepleri CSV Olarak İndir** düğmesi dönemin taleplerini Excel'de açılabilen bir dosya olarak indirir.

## 10. Kullanıcılar

**Kullanıcılar** sayfası yalnızca BT personeline açıktır. Kayıtlı kullanıcıları e-posta, departman, rol, zimmetli cihaz sayısı ve üzerlerindeki aktif taleplerle listeler; ad, e-posta veya departmana göre aranabilir ve role göre süzülebilir.

![Kullanıcılar](images/16-users.png)

Bir kullanıcının rolü, satırındaki listeden yeni rol seçilip **Kaydet** düğmesine basılarak değiştirilir:

- Kimse kendi rolünü değiştiremez; satırında "(siz)" yazar.
- BT personeli rolünden çıkarılan kişinin aktif talepleri atanmamış taleplere alınır ve her talebin geçmişine kaydedilir.
- Rol değişikliği, kullanıcının açık oturumunda en geç bir dakika içinde geçerli olur.

Üst üste 5 kez hatalı şifre giren kullanıcının hesabı 15 dakikalığına kilitlenir ve listede **Kilitli** rozetiyle görünür. Telefonla arayan bir çalışan için **Kilidi aç** bağlantısı kilidi hemen kaldırır. İki adımlı doğrulamayı açmış kullanıcıların adının yanında **2FA** rozeti görünür.

## 11. Hesabım ve Güvenlik

Sağ üstteki kişi ikonundan **Hesabım** sayfası açılır (salt okunur demo sitesinde kapalıdır):

- **Profil**: e-posta ve telefon bilgisi.
- **Şifre**: mevcut şifreyle yeni şifre belirlenir. Şifre en az 10 karakter olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.
- **İki Adımlı Doğrulama**: girişte şifrenin yanında telefondaki doğrulama uygulamasının ürettiği 6 haneli kod da istenir.

![İki Adımlı Doğrulama Kurulumu](images/19-two-factor.png)

İki adımlı doğrulamayı açmak için:

1. **Doğrulama uygulaması ekle** düğmesine basın.
2. Microsoft Authenticator ya da Google Authenticator ile ekrandaki QR kodu okutun (ya da anahtarı elle girin).
3. Uygulamanın gösterdiği kodu yazıp **Doğrula ve Aç**'a basın.
4. Gösterilen 10 kurtarma kodunu güvenli bir yere kaydedin. Telefonunuzu kaybederseniz giriş sayfasındaki **Kurtarma koduyla girin** bağlantısıyla bu kodlardan birini kullanabilirsiniz; her kod bir kez geçerlidir.

Bu sayfadan kurtarma kodları yenilenebilir, uygulama anahtarı sıfırlanabilir ya da iki adımlı doğrulama kapatılabilir.
