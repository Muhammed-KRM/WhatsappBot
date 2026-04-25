# CafeBot Test ve Kullanım Rehberi

CafeBot sistemini test edecek birine şu adımları izleterek sistemin tüm özelliklerini test etmesini sağlayabilirsiniz. İşte adım adım kullanım rehberi:

### 1. Sisteme Giriş ve Kayıt (Yeni Eklendi)
*   **Adım:** Tarayıcınızdan uygulamaya girin. Eğer hesabınız yoksa **/register** sayfasından kayıt olun.
*   **İşlem:** E-posta, şifre ve isim bilgilerinizi girerek kendi izole hesabınızı oluşturun.
*   **Sonuç:** Giriş yaptıktan sonra sadece size özel olan Dashboard ekranına yönlendirileceksiniz.

### 2. WhatsApp Bağlantısını Kurma
*   **Adım:** Dashboard (Ana Sayfa) üzerindeki **"WhatsApp'ı Bağla"** veya **"QR Kodu Al"** butonuna basın.
*   **İşlem:** Ekrana gelen QR kodu telefonunuzdaki WhatsApp uygulamasından (Bağlı Cihazlar > Cihaz Bağla) taratın.
*   **Sonuç:** Durum çubuğunda **"Bağlı" (Yeşil)** yazısını gördüğünüzde sistem WhatsApp ile konuşmaya hazır demektir.

### 3. Hedef Grubu Seçme
*   **Adım:** Sol menüden **"Grup Seçimi"** sayfasına gidin.
*   **İşlem:** WhatsApp'ınızdaki grupların listelenmesini bekleyin (ilk açılışta 10-15 sn sürebilir). Botun hangi gruptaki mesajları dinlemesini istiyorsanız o grubun yanındaki **"Seç"** butonuna basın.
*   **Sonuç:** Seçilen grup adı Dashboard'da "Hedef Grup" olarak görünecektir.

### 4. Öncelik Listesini Ayarlama
*   **Adım:** Dashboard üzerindeki **"Öncelik Listesi"** kısmına gelin.
*   **İşlem:** Tercih ettiğiniz saatleri (Örn: 19, 20, 18) sırasıyla girin.
*   **Mantık:** Bot, gelen mesajda bu saatlerin hepsini görürse, en baştaki saate (19) otomatik cevap verir. Eğer 19 yoksa 20'ye, o da yoksa 18'e bakar.

### 5. Sistemi Aktif Etme
*   **Adım:** Dashboard'daki **"Sistem Durumu"** butonuna basın.
*   **İşlem:** Butonun **"Çalışıyor" (Yeşil)** olduğundan emin olun. 
*   **Not:** Sistem "Durduruldu" modundayken mesajları dinler ama gruba cevap yazmaz.

### 6. Test Mesajı Atma (Asıl Test)
*   **İşlem:** Seçtiğiniz WhatsApp grubuna başka bir telefondan (veya kendi telefonunuzdan) şu formatta bir mesaj atın:
    > *"Arkadaşlar yarın için eleman lazım. Saat 18:00 için 2 kişi, 19:00 için 3 kişi, 21:00 için 1 kişi."*
*   **Botun Tepkisi:** Bot anında mesajı analiz edecek, sizin öncelik listenizdeki (Örn: 19) saati bulacak ve gruba sadece **"19"** yazarak cevap verecektir.

### 7. Takip ve Kontrol
*   **Aktivite Geçmişi:** Dashboard'un altındaki tablodan botun mesajı ne zaman aldığını, hangi saatleri bulduğunu ve hangisini seçtiğini anlık (canlı) olarak izleyebilirsiniz.
*   **Loglar:** Eğer bir hata oluşursa (Örn: Yapay zeka mesajı anlayamazsa), sol menüdeki **"Loglar"** sayfasından teknik detayları ve hata nedenlerini görebilirsiniz.

---

**Tester için İpucu:** Bot sadece sizin seçtiğiniz gruba cevap verir, diğer grupları veya kişisel mesajları görmezden gelir. Test sırasında "Aktivite Geçmişi" kısmını açık tutarsanız botun "düşünme" sürecini canlı olarak görebilirsiniz.
