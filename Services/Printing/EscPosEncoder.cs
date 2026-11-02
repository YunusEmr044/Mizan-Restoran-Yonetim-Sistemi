using System.Text;

namespace RestoranYonetim.Services.Printing;

// Fişi ESC/POS komutlarına çevirir; Türkçe karakterler CP857 (ESC t 13) ile kodlanır.
public static class EscPosEncoder
{
    private static readonly Encoding Cp857;

    static EscPosEncoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp857 = Encoding.GetEncoding(857, new EncoderReplacementFallback("?"), DecoderFallback.ReplacementFallback);
    }

    public static int LineWidth(int paperWidthMm) => paperWidthMm == 58 ? 32 : 48;

    public static byte[] Encode(KitchenTicket ticket, int paperWidthMm)
    {
        var width = LineWidth(paperWidthMm);
        var output = new List<byte>();
        void Raw(params byte[] bytes) => output.AddRange(bytes);
        void Text(string line) { output.AddRange(Cp857.GetBytes(line)); output.Add(0x0A); }
        void Wrapped(string text, string indent = "") { foreach (var line in Wrap(text, width - indent.Length)) Text(indent + line); }

        Raw(0x1B, 0x40, 0x1B, 0x74, 0x0D);
        Raw(0x1B, 0x61, 0x01, 0x1D, 0x21, 0x11);
        if (ticket.Kind == KitchenTicketKind.Iptal)
        {
            Text("*** İPTAL ***");
        }
        foreach (var line in Wrap(ticket.Title, width / 2))
        {
            Text(line);
        }
        Raw(0x1D, 0x21, 0x00, 0x1B, 0x61, 0x00);

        Text(ticket.OrderId > 0 ? $"Sipariş #{ticket.OrderId} · {ticket.At:HH:mm}" : ticket.At.ToString("dd.MM.yyyy HH:mm"));
        if (ticket.Byline != null) Wrapped(ticket.Byline);
        if (ticket.PromisedAt is DateTime promised) Text($"Teslim saati: {promised:HH:mm}");
        Text(new string('-', width));

        foreach (var line in ticket.Lines)
        {
            Raw(0x1B, 0x45, 0x01);
            Wrapped($"{line.Quantity} x {line.Name}");
            Raw(0x1B, 0x45, 0x00);
            foreach (var option in line.Options) Wrapped("+ " + option, "  ");
            if (line.Note != null) Wrapped("! " + line.Note, "  ");
            if (line.Takeaway) Text("  [PAKET]");
        }

        Text(new string('-', width));
        if (ticket.OrderNote != null) Wrapped("Not: " + ticket.OrderNote);
        if (ticket.Reason != null) Wrapped("Gerekçe: " + ticket.Reason);
        if (ticket.Kind == KitchenTicketKind.Iptal && ticket.By != null) Wrapped("Onaylayan: " + ticket.By);

        Raw(0x1B, 0x64, 0x04, 0x1D, 0x56, 0x42, 0x00);
        return output.ToArray();
    }

    public static IReadOnlyList<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var rest = word;
            while (rest.Length > 0)
            {
                var gap = current.Length > 0 ? 1 : 0;
                if (current.Length + gap + rest.Length <= width)
                {
                    current += (gap == 1 ? " " : "") + rest;
                    rest = "";
                }
                else if (rest.Length > width)
                {
                    var take = width - current.Length - gap;
                    if (take > 0)
                    {
                        current += (gap == 1 ? " " : "") + rest[..take];
                        rest = rest[take..];
                    }
                    lines.Add(current);
                    current = "";
                }
                else
                {
                    lines.Add(current);
                    current = "";
                }
            }
        }
        if (current.Length > 0)
        {
            lines.Add(current);
        }
        return lines;
    }
}
