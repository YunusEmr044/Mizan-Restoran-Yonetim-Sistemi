namespace RestoranYonetim.Services;

public static class RateLimitKeys
{
    // Aynı restoran Wi-Fi'ından (tek genel IP) gelen masalar birbirinin sınırını tüketmesin diye QR işlemleri IP + masa bazında sayılır.
    public static string TableAction(string? ip, string? tableToken) => $"{ip ?? "unknown"}|{tableToken}";
}
