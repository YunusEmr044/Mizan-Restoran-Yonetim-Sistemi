namespace RestoranYonetim.Models;

// Ana sayfanın hangi bölümlerinin gösterileceğini ve hangi sırada geleceğini tutar.
// Key sabit bir kod sözcüğü (bkz. HomeSectionCatalog), görünen isim/ikon katalogda tutulur.
// Footer bölümü kasıtlı olarak burada YOK - her zaman gösterilir, kapatılamaz.
public class HomeSection
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public int DisplayOrder { get; set; }
}
