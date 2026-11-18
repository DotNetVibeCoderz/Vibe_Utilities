namespace Lega.UI;

/// <summary>
/// Dua dunia: panel instrumen gelap (pinus) di atas, daftar kerja terang di bawah.
/// Kunyit hanya dipakai untuk satu makna: ruang yang bisa dikosongkan.
/// </summary>
public static class Theme
{
    // Panel instrumen (atas)
    public static readonly Color Pine = Hex("#16302A");
    public static readonly Color PineDeep = Hex("#0D201B");
    public static readonly Color PineLine = Hex("#2E4C43");
    public static readonly Color PineHover = Hex("#21403A");
    public static readonly Color OnPine = Hex("#ECF3EF");
    public static readonly Color OnPineMuted = Hex("#9AB3A9");
    public static readonly Color Sage = Hex("#86AE9E");

    // Satu makna: bisa dikosongkan
    public static readonly Color Turmeric = Hex("#E8B23F");
    public static readonly Color TurmericDeep = Hex("#B9831F");

    // Daftar kerja (bawah)
    public static readonly Color Surface = Color.White;
    public static readonly Color Paper = Hex("#F5F7F5");
    public static readonly Color Hover = Hex("#F1F5F2");
    public static readonly Color Ink = Hex("#13241F");
    public static readonly Color Muted = Hex("#5C6E67");
    public static readonly Color Line = Hex("#E1E7E3");
    public static readonly Color Edge = Hex("#B2C0B9");
    public static readonly Color Track = Hex("#D5DED9");
    public static readonly Color Accent = Hex("#1D5A49");
    public static readonly Color AccentHover = Hex("#174B3D");
    public static readonly Color AccentDown = Hex("#103B30");
    public static readonly Color Danger = Hex("#B23A28");

    // Pil risiko: (teks, latar)
    public static readonly (Color Fore, Color Back) SafePill = (Hex("#1D5A49"), Hex("#E2EFE9"));
    public static readonly (Color Fore, Color Back) ReviewPill = (Hex("#7A520C"), Hex("#FBEED2"));
    public static readonly (Color Fore, Color Back) CarefulPill = (Hex("#9C2F1F"), Hex("#F9E0DB"));

    private static Color Hex(string s) => ColorTranslator.FromHtml(s);

    private static readonly HashSet<string> Installed =
        new(FontFamily.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

    private static string Pick(params string[] names) =>
        names.FirstOrDefault(Installed.Contains) ?? "Segoe UI";

    // Teks: Segoe UI Variable (Windows 11). Angka dan label instrumen: Bahnschrift, DIN ala papan meter.
    private static readonly string TextFamily = Pick("Segoe UI Variable Text", "Segoe UI");
    private static readonly string SemiFamily = Pick("Segoe UI Variable Text Semibold", "Segoe UI Semibold");
    private static readonly string GaugeFamily = Pick("Bahnschrift SemiBold SemiConden", "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold");
    private static readonly string GaugeLightFamily = Pick("Bahnschrift Light SemiCondensed", "Bahnschrift Light", "Bahnschrift", "Segoe UI");
    private static readonly string LabelFamily = Pick("Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold");
    private static readonly string IconFamily = Pick("Segoe Fluent Icons", "Segoe MDL2 Assets");

    private static readonly Dictionary<(string, float), Font> Cache = new();

    private static Font Get(string family, float pt)
    {
        if (!Cache.TryGetValue((family, pt), out var f))
            Cache[(family, pt)] = f = new Font(family, pt, FontStyle.Regular, GraphicsUnit.Point);
        return f;
    }

    public static Font Text(float pt) => Get(TextFamily, pt);
    public static Font Semi(float pt) => Get(SemiFamily, pt);

    /// <summary>Angka besar dan ukuran: Bahnschrift SemiBold SemiCondensed.</summary>
    public static Font Gauge(float pt) => Get(GaugeFamily, pt);

    /// <summary>Satuan di samping angka besar.</summary>
    public static Font GaugeLight(float pt) => Get(GaugeLightFamily, pt);

    /// <summary>Label kecil berhuruf kapital (eyebrow, skala).</summary>
    public static Font Label(float pt) => Get(LabelFamily, pt);

    public static Font Icon(float pt) => Get(IconFamily, pt);

    public const string GlyphLock = "\uE72E";
    public const string GlyphCheck = "\uE73E";
    public const string GlyphShield = "\uEA18";
    public const string GlyphRefresh = "\uE72C";
}
