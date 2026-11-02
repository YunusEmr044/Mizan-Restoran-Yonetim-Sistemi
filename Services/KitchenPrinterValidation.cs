using System.Net;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

public static class KitchenPrinterValidation
{
    public static readonly int[] PaperWidths = { 80, 58 };

    public static string? Validate(KitchenPrinter printer)
    {
        if (string.IsNullOrWhiteSpace(printer.Name) || printer.Name.Trim().Length > KitchenPrinter.NameMax)
        {
            return $"Yazıcı adı zorunlu ve en fazla {KitchenPrinter.NameMax} karakter olabilir.";
        }
        if (!PaperWidths.Contains(printer.PaperWidthMm))
        {
            return "Kâğıt genişliği 80 ya da 58 mm olmalı.";
        }
        if (printer.Connection != PrinterConnection.Ag)
        {
            return null;
        }

        var host = printer.Host?.Trim();
        if (string.IsNullOrEmpty(host) || host.Length > KitchenPrinter.HostMax)
        {
            return "Ağ yazıcısı için IP adresi ya da makine adı girin.";
        }
        // Panelden sunucunun kendisine bağlantı açılmasın diye döngüsel adresler kabul edilmez.
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(host, out var address) && (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)))
        {
            return "Bu adres yazıcı adresi olarak kullanılamaz.";
        }
        if (printer.Port is < 1 or > 65535)
        {
            return "Port 1 ile 65535 arasında olmalı.";
        }
        return null;
    }
}
