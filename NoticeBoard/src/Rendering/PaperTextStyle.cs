using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace NoticeBoard.Rendering;

// Paper text is baked into a texture and then magnified across a 3D quad, so two of
// CairoFont.SetupContext's screen-oriented defaults are wrong here. Everything on the world
// path goes through this type, because SetupContext resets the font options on every call and
// a single missed site would put a differently-rasterised word on the page.
public static class PaperTextStyle
{
    private static readonly FontOptions options = CreateOptions();

    // SetupContext multiplies the size by RuntimeEnv.GUIScale, which would make the player's
    // GUI Scale setting decide how big the text on a block is, and through
    // PaperSize.MeasureHeightUnits how tall the sheet is. Dividing it back out cancels that.
    public static CairoFont Font(double logicalSize, string fontName, double[] color)
    {
        return new CairoFont(logicalSize / RuntimeEnv.GUIScale, fontName, color);
    }

    public static void Setup(Context ctx, CairoFont font)
    {
        font.SetupContext(ctx);
        ctx.FontOptions = options;
    }

    private static FontOptions CreateOptions()
    {
        var opts = new FontOptions();

        // Subpixel antialiasing writes different coverage into R, G and B for an LCD panel at
        // 1:1. Magnified off a texture that is just coloured fringing on every glyph edge, and
        // the per-channel coverage disagrees with the alpha the blend relies on.
        opts.Antialias = Antialias.Gray;

        // Load-bearing: hinted metrics round glyph advances in device space, so a board
        // rasterised at 2x would wrap differently from the unscaled measuring pass in
        // PaperSize.MeasureHeightUnits and text would truncate early.
        opts.HintMetrics = HintMetrics.Off;

        return opts;
    }
}
