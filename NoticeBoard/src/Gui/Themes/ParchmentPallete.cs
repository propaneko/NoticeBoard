public class ParchmentPalette
{
    public string Name { get; set; }

    public double[] InkColor { get; set; }

    public double[] BaseRGB { get; set; }
    public double[] DarkRGB { get; set; }
    public double[] LightRGB { get; set; }


    public ParchmentPalette(
        string name,
        double[] inkColor,
         double[] baseRGB,
         double[] darkRGB,
         double[] lightRGB
     )
    {
        Name = name;
        InkColor = inkColor;

        BaseRGB = baseRGB;
        DarkRGB = darkRGB;
        LightRGB = lightRGB;
    }
}