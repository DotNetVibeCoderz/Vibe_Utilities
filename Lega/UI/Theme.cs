namespace Lega.UI;

public static class Theme
{
    public static readonly Color Mist = Hex("#F2F4F7");
    public static readonly Color Surface = Color.White;
    public static readonly Color Hover = Hex("#F6F8FA");
    public static readonly Color Ink = Hex("#17212B");
    public static readonly Color Muted = Hex("#66768A");
    public static readonly Color Line = Hex("#E3E8EE");
    public static readonly Color Edge = Hex("#B9C4D0");
    public static readonly Color Track = Hex("#D3DBE4");
    public static readonly Color Slate = Hex("#51627A");
    public static readonly Color Amber = Hex("#F2A33C");
    public static readonly Color Accent = Hex("#0B7375");
    public static readonly Color AccentHover = Hex("#095F61");
    public static readonly Color AccentDown = Hex("#074C4E");
    public static readonly Color Danger = Hex("#C2412F");

    private static Color Hex(string s) => ColorTranslator.FromHtml(s);

    // Segoe UI Variable ada di Windows 11; jika tidak ada, jatuh ke Segoe UI.
    private static readonly HashSet<string> Installed =
        new(FontFamily.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

    private static string Pick(params string[] names) =>
        names.FirstOrDefault(Installed.Contains) ?? "Segoe UI";

    private static readonly string TextFamily = Pick("Segoe UI Variable Text", "Segoe UI");
    private static readonly string SemiFamily = Pick("Segoe UI Variable Text Semibold", "Segoe UI Semibold");
    private static readonly string DisplayFamily = Pick("Segoe UI Variable Display Semibold", "Segoe UI Semibold");

    private static readonly Dictionary<(string, float), Font> Cache = new();

    private static Font Get(string family, float pt)
    {
        if (!Cache.TryGetValue((family, pt), out var f))
            Cache[(family, pt)] = f = new Font(family, pt, FontStyle.Regular, GraphicsUnit.Point);
        return f;
    }

    public static Font Text(float pt) => Get(TextFamily, pt);
    public static Font Semi(float pt) => Get(SemiFamily, pt);
    public static Font Display(float pt) => Get(DisplayFamily, pt);
}
