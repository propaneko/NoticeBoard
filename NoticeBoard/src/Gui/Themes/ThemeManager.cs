using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;

public class ParchmentPalette
{
    public string Name { get; set; }

    public double[] InkColor { get; set; }
    public double[] LinkColor { get; set; }

    public double[] BaseRGB { get; set; }
    public double[] DarkRGB { get; set; }
    public double[] LightRGB { get; set; }

    public ParchmentPalette(
        string name,
        double[] inkColor,
        double[] linkColor,
        double[] baseRGB,
        double[] darkRGB,
        double[] lightRGB
    )
    {
        Name = name;

        InkColor = inkColor;
        LinkColor = linkColor;

        BaseRGB = baseRGB;
        DarkRGB = darkRGB;
        LightRGB = lightRGB;
    }
}

public static class ThemeManager
{
    static List<ParchmentPalette> themesList = new List<ParchmentPalette>
    {
        new ParchmentPalette(
            "Classic Aged",
            [0.24, 0.16, 0.10, 1.0],
            [0.15, 0.25, 0.45, 1.0],
            [0.82, 0.75, 0.63],
            [0.67, 0.60, 0.51],
            [0.90, 0.83, 0.68]
        ),
        new ParchmentPalette(
            "Dark Medieval",
            [0.18, 0.12, 0.08, 1.0],
            [0.30, 0.22, 0.12, 1.0],
            [0.67, 0.60, 0.45],
            [0.42, 0.33, 0.24],
            [0.78, 0.70, 0.55]
        ),
        new ParchmentPalette(
            "Royal Ivory",
            [0.22, 0.18, 0.14, 1.0],
            [0.18, 0.28, 0.52, 1.0],
            [0.91, 0.88, 0.78],
            [0.70, 0.65, 0.52],
            [0.97, 0.95, 0.87]
        ),
        new ParchmentPalette(
            "Burned Edges",
            [0.12, 0.08, 0.05, 1.0],
            [0.40, 0.18, 0.08, 1.0],
            [0.74, 0.61, 0.42],
            [0.33, 0.20, 0.11],
            [0.84, 0.72, 0.50]
        ),
        new ParchmentPalette(
            "Desert Map",
            [0.30, 0.20, 0.10, 1.0],
            [0.65, 0.32, 0.10, 1.0],
            [0.88, 0.78, 0.52],
            [0.62, 0.48, 0.24],
            [0.96, 0.88, 0.64]
        ),
        new ParchmentPalette(
            "Moldy Archive",
            [0.12, 0.12, 0.10, 1.0],
            [0.20, 0.36, 0.22, 1.0],
            [0.66, 0.68, 0.54],
            [0.42, 0.44, 0.32],
            [0.78, 0.80, 0.66]
        ),
        new ParchmentPalette(
            "Vampire Manuscript",
            [0.08, 0.04, 0.04, 1.0],
            [0.55, 0.05, 0.05, 1.0],
            [0.60, 0.52, 0.48],
            [0.28, 0.16, 0.16],
            [0.72, 0.64, 0.60]
        ),
        new ParchmentPalette(
            "Frosted Paper",
            [0.18, 0.20, 0.24, 1.0],
            [0.25, 0.45, 0.70, 1.0],
            [0.86, 0.88, 0.90],
            [0.66, 0.70, 0.74],
            [0.96, 0.97, 0.99]
        ),
        new ParchmentPalette(
            "Dwarven Ledger",
            [0.10, 0.08, 0.06, 1.0],
            [0.52, 0.34, 0.12, 1.0],
            [0.58, 0.50, 0.38],
            [0.32, 0.24, 0.16],
            [0.70, 0.62, 0.48]
        ),
        new ParchmentPalette(
            "Elven Songbook",
            [0.18, 0.22, 0.16, 1.0],
            [0.22, 0.52, 0.42, 1.0],
            [0.84, 0.90, 0.78],
            [0.60, 0.68, 0.54],
            [0.94, 0.98, 0.88]
        ),
    };
    public static ParchmentPalette GetCurrentTheme(string themeName)
    {
        return themesList.Find((theme) => theme.Name == themeName);
    }

    /// <summary>
    /// Gets an array of all available theme names for UI selectors.
    /// </summary>
    public static string[] GetThemeNames()
    {
        return themesList.Select(theme => theme.Name).ToArray();
    }
}
