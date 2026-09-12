using System.Text;

namespace NoticeBoard.Utils;

public static class WaypointPin
{
    public const string DefaultIcon = "circle";
    public const string DefaultColor = "steelblue";
    public const int MaxTitleLength = 64;

    public static readonly string[] Icons =
    {
        "circle", "turnip", "grain", "apple", "berries", "mushroom",
        "bee", "cave", "gear", "gravestone", "home", "ladder", "pick",
        "propick", "rocks", "ruins", "skull_and_crossbones", "spiral",
        "star1", "star2", "trader", "tree", "tree2", "vessel", "x"
    };

    public static readonly string[] Colors =
    {
        "steelblue", "crimson", "tomato", "gold", "orange", "forestgreen",
        "seagreen", "teal", "royalblue", "navy", "purple", "hotpink",
        "sienna", "gray", "white", "black"
    };

    public static string SanitizeIcon(string icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return DefaultIcon;
        for (int i = 0; i < Icons.Length; i++)
            if (Icons[i] == icon) return icon;
        return DefaultIcon;
    }

    public static string SanitizeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color)) return DefaultColor;
        for (int i = 0; i < Colors.Length; i++)
            if (Colors[i] == color) return color;
        return DefaultColor;
    }

    public static string SanitizeTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return "";
        var sb = new StringBuilder(title.Length);
        foreach (char c in title)
        {
            if (c == '\n' || c == '\r' || c == '\t') sb.Append(' ');
            else if (char.IsControl(c)) continue;
            else sb.Append(c);
        }
        string trimmed = sb.ToString().Trim();
        if (trimmed.Length > MaxTitleLength)
            trimmed = trimmed.Substring(0, MaxTitleLength).Trim();
        return trimmed;
    }

    public static string ResolveTitle(string title, string boardName)
    {
        string s = SanitizeTitle(title);
        if (s.Length > 0) return s;
        s = SanitizeTitle(boardName);
        return s.Length > 0 ? s : "Notice Board";
    }

    public static string Label(string code)
    {
        string s = (code ?? "").Replace('_', ' ');
        if (s.Length == 0) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
