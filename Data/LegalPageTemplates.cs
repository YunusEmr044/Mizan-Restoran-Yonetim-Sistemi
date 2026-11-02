namespace RestoranYonetim.Data;

// Yasal sayfaların ilk kurulumda oluşturulan Türkçe şablon metinleri. Yönetim panelinden (Site → Yasal Sayfalar) düzenlenir.
public static class LegalPageTemplates
{
    public static class Slugs
    {
        public const string Kvkk = "kvkk";
        public const string Gizlilik = "gizlilik";
        public const string CerezPolitikasi = "cerez-politikasi";
        public const string KullanimKosullari = "kullanim-kosullari";
    }

    public static IReadOnlyList<(string Slug, string Title, string Body)> All { get; } = new[]
    {
        (Slugs.Kvkk, "KVKK Aydınlatma Metni", Kvkk),
        (Slugs.Gizlilik, "Gizlilik Politikası", Gizlilik),
        (Slugs.CerezPolitikasi, "Çerez Politikası", CerezPolitikasi),
        (Slugs.KullanimKosullari, "Kullanım Koşulları", KullanimKosullari)
    };

    // İngilizce taslaklar (sitede /en altında). Hukuki geçerliliği olan metin Türkçe olandır; yayından önce bir hukukçuya kontrol ettirin.
    public static IReadOnlyDictionary<string, (string Title, string Body)> English { get; } = new Dictionary<string, (string, string)>
    {
        [Slugs.Kvkk] = ("Personal Data Protection Notice (KVKK)", KvkkEn),
        [Slugs.Gizlilik] = ("Privacy Policy", GizlilikEn),
        [Slugs.CerezPolitikasi] = ("Cookie Policy", CerezPolitikasiEn),
        [Slugs.KullanimKosullari] = ("Terms of Use", KullanimKosullariEn)
    };

    private const string TranslationNote = "*This is an English translation provided for convenience. In case of any discrepancy, the Turkish version prevails.*";

    private const string KvkkEn = TranslationNote + """


        Under Turkish Law No. 6698 on the Protection of Personal Data ("KVKK"), **{RestoranAdi}**, as the data controller, processes your personal data as described below.

        ## Data controller

        - **Name:** {RestoranAdi}
        - **Address:** {Adres}
        - **Email:** {Eposta}
        - **Phone:** {Telefon}

        ## Personal data we process

        - **Identity and contact details:** name, phone number and email address (reservation and review forms).
        - **Reservation details:** date, time, number of guests and any notes you send us.
        - **Customer transaction details:** if you join our loyalty programme, your phone number, spending and points.
        - **Review details:** your rating, comment and, optionally, the name you share.
        - **Security records:** technical records such as IP address and browser details when you visit the website.

        ## Purposes of processing

        - Receiving and confirming your reservation requests and contacting you,
        - Handling orders and payments and running the loyalty programme,
        - Measuring service quality and publishing your reviews (only after moderation and under the name you choose),
        - Keeping information secure and meeting our legal obligations.

        ## Legal grounds

        Your personal data is processed under Article 5 of the KVKK on the grounds that it is directly necessary for entering into or performing a contract, for complying with our legal obligations, and for our legitimate interests provided that your fundamental rights and freedoms are not harmed. Processing that requires explicit consent, such as analytics cookies, takes place only if you give that consent.

        ## Transfers

        Your personal data may be shared with authorised public institutions where required by law, and with our hosting and technical infrastructure providers, strictly for the purposes above.

        ## Retention

        Your personal data is kept for as long as the purpose of processing and the applicable legislation require; it is then deleted, destroyed or anonymised.

        ## Your rights

        Under Article 11 of the KVKK you have the right to learn whether your personal data is processed, to request information about it, to learn the purpose of processing, to know the parties it is transferred to, to request correction of incomplete or inaccurate data, to request deletion where the legal conditions are met, and to claim compensation for any damage.

        You can send your requests in writing to **{Eposta}** or to **{Adres}**.
        """;

    private const string GizlilikEn = TranslationNote + """


        At **{RestoranAdi}** we care about your privacy. This policy explains how we collect and use information when you use our website at **{SiteAdresi}** and the QR menu on our tables.

        ## Information we collect

        - Information you choose to enter in the reservation and review forms.
        - Orders you place through the QR menu (orders are linked to the table; no personal account is needed).
        - Technical records created while you use the site and, if you consent, analytics cookie data.

        ## How we use information

        We use your information only to manage your reservation, prepare your order, improve our service and meet our legal obligations. We do not sell your information or share it with third parties for advertising.

        ## Security

        Your data is stored on systems that only authorised staff can access and is protected by reasonable technical and organisational measures against unauthorised access.

        ## Your rights

        Your rights regarding your personal data and how to exercise them are explained in the [Personal Data Protection Notice](/en/kvkk). For cookies, see the [Cookie Policy](/en/cerez-politikasi).

        ## Contact

        For any questions, contact us at **{Eposta}** or **{Telefon}**.
        """;

    private const string CerezPolitikasiEn = TranslationNote + """


        This page explains the cookies used on the **{RestoranAdi}** website and how you can manage your preferences.

        ## What is a cookie?

        Cookies are small text files saved in your browser when you visit a website.

        ## Cookies we use

        - **Essential cookies:** needed for the site to work securely and correctly (for example, form security and remembering your cookie choice). These cannot be switched off.
        - **Analytics cookies:** help us understand how many people visit the site and which pages they use. They are used only if you consent.

        We do not use advertising or targeting cookies.

        ## Managing your preferences

        You can change your consent to analytics cookies at any time with the **Cookie preferences** link at the bottom of the page. You can also delete or block cookies in your browser settings; some parts of the site may then not work as expected.

        ## Contact

        For questions about cookies, write to **{Eposta}**.
        """;

    private const string KullanimKosullariEn = TranslationNote + """


        By using the website and QR menu at **{SiteAdresi}** you accept the following terms.

        ## Scope of the service

        The site lets you browse our menu, send reservation requests, order from your table and leave reviews. Menu items and prices may change without notice; the price that applies is the one in effect at the restaurant when you order.

        ## Reservations

        The reservation form is a request; your reservation is final once **{RestoranAdi}** confirms it.

        ## Orders

        Orders placed from the QR menu are added to your table's bill. Payment is taken at the restaurant's till.

        ## User content

        Reviews are checked before they are published. Reviews containing insults, personal data or unlawful content are not published.

        ## Liability

        We take reasonable care to keep the site running without interruption or errors, but we cannot be held liable for indirect losses caused by technical problems.

        ## Contact

        **{RestoranAdi}** · {Adres} · {Telefon} · {Eposta}
        """;

    private const string Kvkk = """
        6698 sayılı Kişisel Verilerin Korunması Kanunu ("KVKK") uyarınca, veri sorumlusu sıfatıyla **{RestoranAdi}** olarak kişisel verilerinizi aşağıda açıklanan kapsamda işlemekteyiz.

        ## Veri sorumlusu

        - **Unvan:** {RestoranAdi}
        - **Adres:** {Adres}
        - **E-posta:** {Eposta}
        - **Telefon:** {Telefon}

        ## İşlenen kişisel veriler

        - **Kimlik ve iletişim bilgileri:** ad soyad, telefon numarası, e-posta adresi (rezervasyon ve değerlendirme formları).
        - **Rezervasyon bilgileri:** tarih, saat, kişi sayısı ve iletmek istediğiniz notlar.
        - **Müşteri işlem bilgileri:** sadakat programına katılırsanız telefon numaranız, harcama tutarı ve puanlarınız.
        - **Değerlendirme bilgileri:** puanınız, yorumunuz ve isteğe bağlı olarak paylaştığınız adınız.
        - **İşlem güvenliği bilgileri:** web sitesini ziyaretinizde IP adresi ve tarayıcı bilgileri gibi teknik kayıtlar.

        ## İşleme amaçları

        - Rezervasyon taleplerinizi almak, teyit etmek ve sizinle iletişime geçmek,
        - Sipariş ve ödeme süreçlerini yürütmek, sadakat programını işletmek,
        - Hizmet kalitemizi ölçmek ve değerlendirmelerinizi yayımlamak (yalnızca onayımızla ve tercih ettiğiniz adla),
        - Bilgi güvenliğini sağlamak ve yasal yükümlülüklerimizi yerine getirmek.

        ## Hukuki sebepler

        Kişisel verileriniz KVKK'nın 5. maddesi kapsamında; bir sözleşmenin kurulması veya ifasıyla doğrudan ilgili olması, veri sorumlusunun hukuki yükümlülüğünü yerine getirmesi ve temel hak ve özgürlüklerinize zarar vermemek kaydıyla meşru menfaatimiz hukuki sebeplerine dayanarak işlenir. Analitik çerezler gibi açık rızaya bağlı işlemler yalnızca onay vermeniz hâlinde yapılır.

        ## Aktarım

        Kişisel verileriniz; yasal zorunluluk hâlinde yetkili kamu kurum ve kuruluşlarıyla, hizmet aldığımız barındırma ve teknik altyapı sağlayıcılarıyla, yalnızca yukarıdaki amaçlarla sınırlı olarak paylaşılabilir.

        ## Saklama süresi

        Kişisel verileriniz işleme amacının gerektirdiği süre ve ilgili mevzuatta öngörülen süreler boyunca saklanır; süre sona erdiğinde silinir, yok edilir veya anonim hâle getirilir.

        ## Haklarınız

        KVKK'nın 11. maddesi uyarınca; kişisel verilerinizin işlenip işlenmediğini öğrenme, işlenmişse bilgi talep etme, işlenme amacını öğrenme, aktarıldığı kişileri bilme, eksik veya yanlış işlenmişse düzeltilmesini, şartları oluştuğunda silinmesini isteme ve zarara uğramanız hâlinde zararın giderilmesini talep etme haklarına sahipsiniz.

        Başvurularınızı **{Eposta}** adresine veya **{Adres}** adresine yazılı olarak iletebilirsiniz.
        """;

    private const string Gizlilik = """
        **{RestoranAdi}** olarak gizliliğinize önem veriyoruz. Bu politika, **{SiteAdresi}** adresindeki web sitemizi ve masalarımızdaki QR menüyü kullanırken bilgilerinizi nasıl topladığımızı ve kullandığımızı açıklar.

        ## Topladığımız bilgiler

        - Rezervasyon ve değerlendirme formlarına kendi isteğinizle girdiğiniz bilgiler.
        - QR menü üzerinden verdiğiniz siparişler (siparişler masaya bağlıdır, kişisel hesap gerekmez).
        - Siteyi kullanırken oluşan teknik kayıtlar ve, onay vermeniz hâlinde, analitik çerez verileri.

        ## Bilgileri nasıl kullanıyoruz

        Bilgilerinizi yalnızca rezervasyonunuzu yönetmek, siparişinizi hazırlamak, hizmetimizi iyileştirmek ve yasal yükümlülüklerimizi yerine getirmek için kullanırız. Bilgilerinizi satmayız ve reklam amacıyla üçüncü kişilerle paylaşmayız.

        ## Güvenlik

        Verileriniz erişimi yetkili personelle sınırlandırılmış sistemlerde saklanır ve yetkisiz erişime karşı makul teknik ve idari önlemlerle korunur.

        ## Haklarınız

        Kişisel verilerinize ilişkin haklarınız ve başvuru yöntemleri [KVKK Aydınlatma Metni](/kvkk) sayfasında açıklanmıştır. Çerez kullanımı için [Çerez Politikası](/cerez-politikasi) sayfasına bakabilirsiniz.

        ## İletişim

        Sorularınız için **{Eposta}** adresinden veya **{Telefon}** numarasından bize ulaşabilirsiniz.
        """;

    private const string CerezPolitikasi = """
        Bu sayfa, **{RestoranAdi}** web sitesinde kullanılan çerezleri ve tercihlerinizi nasıl yönetebileceğinizi açıklar.

        ## Çerez nedir?

        Çerezler, bir web sitesini ziyaret ettiğinizde tarayıcınıza kaydedilen küçük metin dosyalarıdır.

        ## Kullandığımız çerezler

        - **Zorunlu çerezler:** Sitenin güvenli ve düzgün çalışması için gereklidir (ör. form güvenliği, çerez tercihinizin hatırlanması). Bu çerezler kapatılamaz.
        - **Analitik çerezler:** Siteyi kaç kişinin ziyaret ettiğini ve hangi sayfaların kullanıldığını anlamamıza yardımcı olur. Yalnızca onay verirseniz kullanılır.

        Reklam veya hedefleme amaçlı çerez kullanmıyoruz.

        ## Tercihlerinizi yönetme

        Analitik çerezlere verdiğiniz onayı istediğiniz zaman sayfanın en altındaki **Çerez tercihleri** bağlantısından değiştirebilirsiniz. Tarayıcınızın ayarlarından da çerezleri silebilir veya engelleyebilirsiniz; bu durumda sitenin bazı bölümleri beklendiği gibi çalışmayabilir.

        ## İletişim

        Çerezlerle ilgili sorularınız için **{Eposta}** adresine yazabilirsiniz.
        """;

    private const string KullanimKosullari = """
        **{SiteAdresi}** adresindeki web sitesini ve QR menüyü kullanarak aşağıdaki koşulları kabul etmiş olursunuz.

        ## Hizmetin kapsamı

        Site; menümüzü incelemeniz, rezervasyon talebi oluşturmanız, masanızdan sipariş vermeniz ve değerlendirme bırakmanız için sunulmaktadır. Menüdeki fiyatlar ve ürünler önceden haber verilmeksizin değiştirilebilir; geçerli fiyat, sipariş anında restoranda uygulanan fiyattır.

        ## Rezervasyonlar

        Rezervasyon formu bir talep niteliğindedir; rezervasyonunuz **{RestoranAdi}** tarafından onaylandığında kesinleşir.

        ## Siparişler

        QR menüden verilen siparişler masanızın hesabına eklenir. Ödeme restoranda, kasada alınır.

        ## Kullanıcı içerikleri

        Değerlendirmeler yayımlanmadan önce incelenir. Hakaret, kişisel veri veya yasa dışı içerik barındıran değerlendirmeler yayımlanmaz.

        ## Sorumluluk

        Sitenin kesintisiz ve hatasız çalışması için makul özeni gösteririz; ancak teknik aksaklıklardan doğabilecek dolaylı zararlardan sorumlu tutulamayız.

        ## İletişim

        **{RestoranAdi}** · {Adres} · {Telefon} · {Eposta}
        """;
}
