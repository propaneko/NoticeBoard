using System;
using System.Collections.Generic;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Rendering;

// VStart/VEnd are fractions of the full 13-unit tile. Band heights scale; UV slices do not.
public readonly struct PaperBand
{
    public PaperBand(float width, float height, float vStart, float vEnd)
    {
        Width = width;
        Height = height;
        VStart = vStart;
        VEnd = vEnd;
    }

    public float Width { get; }
    public float Height { get; }
    public float VStart { get; }
    public float VEnd { get; }
}

// The axis-aligned extent a hanging sheet occupies, in blocks, relative to its tack and with
// the lean already applied. A sheet hangs below and to the side of its nail rather than being
// centred on it, so this is what the board layout has to reason about, not the tack point.
public readonly struct SheetBounds
{
    public SheetBounds(float minX, float maxX, float minY, float maxY)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public float MinX { get; }
    public float MaxX { get; }
    public float MinY { get; }
    public float MaxY { get; }
}

// Single source of truth for how tall one notice sheet is. Two independent things have to agree
// on that number: the Cairo ink tile drawn by NoticeBoardPaperTextRenderer, and the board layout
// that reserves cork for the sheet. If they drift apart a short note claims the space of a
// full-length one.
public static class PaperSize
{
    public const int TextWidth = 256;
    public const int SidePadding = 28;
    public const int TopPadding = 40;
    public const int HeaderBodyGap = 8;
    public const int BottomMargin = 28;
    public const int FooterGap = 4;
    public const double BodyFontSize = 5;
    public const double HeaderFontSize = 12;
    public const double DateFontSize = 8;

    public const float DefaultBoardFontSize = 16f;
    public const float MinBoardFontSize = 10f;
    public const float MaxBoardFontSize = 60f;

    public static float ResolveBoardFontSize(float size) =>
        size <= 0 ? DefaultBoardFontSize : GameMath.Clamp(size, MinBoardFontSize, MaxBoardFontSize);

    public static double ResolveBodyFontSize(float boardFontSize) =>
        BodyFontSize * (ResolveBoardFontSize(boardFontSize) / DefaultBoardFontSize);

    public static double ResolveHeaderFontSize(float boardFontSize) =>
        HeaderFontSize * (ResolveBoardFontSize(boardFontSize) / DefaultBoardFontSize);

    public static double ResolveDateFontSize(float boardFontSize) =>
        DateFontSize * (ResolveBoardFontSize(boardFontSize) / DefaultBoardFontSize);

    public static double BodyOriginY(bool hasAuthor, double headerLineHeight) =>
        hasAuthor ? TopPadding + headerLineHeight + HeaderBodyGap : TopPadding;

    // Performance backstop only, far above any page's real capacity at BodyFontSize: actual
    // truncation happens via the maxLines page-overflow check in the renderer.
    public const int MaxCharsPerMessage = 4000;

    // The sheet's local width (8/16 of a block) spans TextWidth pixels, so one 1/16 block step
    // is worth TextWidth / 8 pixels. FullHeightUnits is the unshrunk sheet from the shape JSON
    // (5 + 5 + 3 bands), giving a full-height tile of 13 * 32 == 416 pixels.
    public const int PixelsPerBlockSixteenth = TextWidth / 8;
    public const int FullHeightUnits = 13;

    // The preview sheet may grow past a real sheet so long notes stay readable, but
    // not without bound - past 3 sheets the text would be too small on screen to read anyway,
    // so it falls back to the same "…" overflow. Raise this if someone wants taller previews.
    public const int PreviewMaxUnits = FullHeightUnits * 3;

    public const double BodyMaxWidth = TextWidth - SidePadding * 2;

    public static readonly double[] InkColor = { 0.04, 0.04, 0.04, 0.95 };

    public static int TilePixels(int units) => units * PixelsPerBlockSixteenth;

    public static int ClampUnits(int units) => GameMath.Clamp(units, 1, FullHeightUnits);

    // How many device pixels per logical pixel the in-world ink is rasterised at. Defaults to
    // 2 so existing boards keep the sharpness they were already rendering at.
    public const int MinTextSharpness = 1;
    public const int MaxTextSharpness = 4;
    public const int DefaultTextSharpness = 2;

    public static int ClampSharpness(int sharpness) =>
        GameMath.Clamp(sharpness, MinTextSharpness, MaxTextSharpness);

    // A board that has never had the setting written, and the placeholder object the server
    // hands back for an unknown board, both carry 0 here. Those render at the default rather
    // than dropping to the 1x floor.
    public static int ResolveSharpness(int sharpness) =>
        sharpness <= 0 ? DefaultTextSharpness : ClampSharpness(sharpness);

    public const int MinSwayStrength = 0;
    public const int MaxSwayStrength = 100;
    public const int DefaultSwayStrength = 50;

    public static int ClampSwayStrength(int strength) =>
        GameMath.Clamp(strength, MinSwayStrength, MaxSwayStrength);

    // Lateral travel in blocks at the free bottom corner of a sheet, at full strength in full
    // wind. Two texels, against roughly 0.39 blocks of clear air in front of the cork, so even
    // the maximum stays far inside the room a paper has to move in.
    public const float MaxSwayAmplitude = 0.12f;

    // Extra oscillation-speed multiplier at full wind strength, so the sway clock (and thus the
    // swing itself) races along in a gust rather than just swinging wider at the same tempo -
    // max wind ends up at 1 + this times the base oscillation speed.
    public const float MaxWindFrequencyBoost = 0.5f;

    // Small, fast jitter layered on top of the main swing to read as paper edges straining/
    // flapping in a gust rather than a smooth pendulum. Expressed as a fraction of the current
    // swing amplitude (itself already scaled by sway strength) rather than an absolute value,
    // so a board with sway strength turned down gets proportionally less flutter too.
    public const float FlutterAmplitudeRatio = 0.25f;

    // Peak pendulum at full slider and wind 1. Hang ~0.5 blocks, so 22° is ~0.19 blocks
    // at the lamp. Storms can push wind to MaxLanternWind (1.6) for ~0.29 blocks, still
    // off the cork.
    public const float MaxLanternSwayDeg = 22f;
    public const float MaxLanternWind = 1.6f;
    // Hides the 250 ms wind-sample steps in about 0.2 s. Storms still read; they just do not pop.
    public const float LanternWindSmoothHz = 5f;
    public const float LanternWindRest = 0.02f;
    // Mean lean follows the weather; the rest is the existing flutter so nothing looks frozen.
    public const float WindDirBias = 0.7f;
    public const float WindOscShare = 0.3f;

    // Purity-seal ribbons hang from the wax pin. Local origin is the pin: the strip's top
    // overlaps the blob, the rest extends downward in -Y. A small RotateX tips the ribbon
    // onto the post face. No RotateZ(180); that leftover child-flip was what threw strips
    // off the wax.
    public const float StripWidth = 1.4f / 16f;
    public const float StripHeight = 5.0f / 16f;
    public const float StripFanRotDeg = 5.0f;
    public const float StripOverlapY = 0.55f / 16f;
    public const float PermitWidth = StripWidth;
    public const float PermitHeight = StripHeight;
    public const float PermitFrontZ = 0.2f / 16f;
    public const float PermitRotationXDeg = -3.0f;

    public const float PermitSwayMultiplier = 1.0f;

    // Wax stamp cube on the same pin, matching the baked mark1 element (2×2×1 in 1/16 units).
    public const float MarkWidth = 2f / 16f;
    public const float MarkHeight = 2f / 16f;
    public const float MarkDepth = 1f / 16f;
    public const float MarkFrontZ = 0.002f;

    public static Matrixf PermitMatrix() => StripMatrix(0f);

    public static Matrixf StripMatrix(float offsetX)
    {
        Matrixf m = new Matrixf();
        m.RotateX(PermitRotationXDeg * GameMath.DEG2RAD);
        m.Translate(offsetX - StripWidth * 0.5f, -StripHeight + StripOverlapY, 0f);
        return m;
    }

    public static Matrixf SealMatrix() => new Matrixf();

    public static string ResolveFont(string boardFont) =>
        string.IsNullOrEmpty(boardFont) ? GuiStyle.StandardFontName : boardFont;

    // Sheet height for the in-world board, capped at the physical sheet: text past that is
    // what the page-overflow "…" in PaintPaperTile drops.
    public static int MeasureHeightUnits(
        ICoreClientAPI api, string text, string boardFont, float boardFontSize = 0, bool hasAuthor = true) =>
        ClampUnits(MeasureHeightUnitsUnclamped(api, text, boardFont, boardFontSize, hasAuthor));

    // How many 1/16-block steps of sheet this message needs. The body column width never
    // depends on the height, so the wrapped line count can be measured once without iterating.
    public static int MeasureHeightUnitsUnclamped(
        ICoreClientAPI api, string text, string boardFont, float boardFontSize = 0, bool hasAuthor = true)
    {
        string font = ResolveFont(boardFont);
        var bodyFont = PaperTextStyle.Font(ResolveBodyFontSize(boardFontSize), font, InkColor);
        var headerFont = PaperTextStyle.Font(ResolveHeaderFontSize(boardFontSize), font, InkColor);
        var dateFont = PaperTextStyle.Font(ResolveDateFontSize(boardFontSize), font, InkColor);

        double lineHeight = api.Gui.Text.GetLineHeight(bodyFont);
        double headerLineHeight = api.Gui.Text.GetLineHeight(headerFont);
        double dateLineHeight = api.Gui.Text.GetLineHeight(dateFont);

        double bodyPx = lineHeight;
        if (!string.IsNullOrWhiteSpace(text))
        {
            // Colors never change glyph advances, so measuring with the ink color for links too
            // yields exactly the wrapping the themed rebuild will produce.
            List<InlineElement> elements = PaperRichTextLayout.Parse(text, InkColor, InkColor);
            (List<InlineElement> capped, _) = PaperRichTextLayout.CapTotalChars(elements, MaxCharsPerMessage);

            using ImageSurface surface = new ImageSurface(Format.Argb32, TextWidth, PixelsPerBlockSixteenth);
            using Context ctx = new Context(surface);
            PaperTextStyle.Setup(ctx, bodyFont);

            var cache = new StyledFontCache(bodyFont);
            List<List<InlineElement>> wrapped = PaperRichTextLayout.Wrap(
                ctx,
                bodyFont,
                capped,
                BodyMaxWidth,
                lineHeight * 1.15,
                cache
            );
            if (wrapped.Count > 0)
            {
                bodyPx = 0;
                for (int i = 0; i < wrapped.Count; i++)
                    bodyPx += PaperRichTextLayout.LineHeight(api, bodyFont, wrapped[i], cache);
            }
        }

        double requiredPx =
            BodyOriginY(hasAuthor, headerLineHeight) + bodyPx + FooterGap + dateLineHeight + BottomMargin;

        return (int)Math.Ceiling(requiredPx / PixelsPerBlockSixteenth);
    }

    // Band heights scale with the sheet; the texture slices they show do not.
    public static PaperBand[] BandLayout(int units)
    {
        float s = units / (float)FullHeightUnits;
        return new[]
        {
            new PaperBand(8.0f / 16.0f, 5.0f * s / 16.0f, 0f, 5.0f / 13.0f),
            new PaperBand(8.0f / 16.0f, 5.0f * s / 16.0f, 5.0f / 13.0f, 10.0f / 13.0f),
            new PaperBand(8.0f / 16.0f, 3.0f * s / 16.0f, 10.0f / 13.0f, 1f),
        };
    }

    // Mirrors the nesting and fold angles of noticeboard-paper-unit.json. Only the two
    // inter-band step-ups shrink; the tack offset and every rotation stay put.
    public static Matrixf[] BandMatrices(int units)
    {
        float s = units / (float)FullHeightUnits;

        Matrixf mLower = new Matrixf();
        mLower.RotateX(-14 * GameMath.DEG2RAD);
        mLower.Translate(4.4f / 16f, 1.0f / 16f, 1.3f / 16f);
        mLower.RotateX(17 * GameMath.DEG2RAD);
        mLower.RotateZ(-180 * GameMath.DEG2RAD);

        Matrixf mMid = mLower.Clone();
        mMid.Translate(0, 5.0f * s / 16f, 0);
        mMid.RotateX(8 * GameMath.DEG2RAD);

        Matrixf mTop = mMid.Clone();
        mTop.Translate(0, 5.0f * s / 16f, 0);
        mTop.RotateX(17 * GameMath.DEG2RAD);

        return new[] { mLower, mMid, mTop };
    }

    public const float TopNailInsetX = 1.0f / 16f;
    public const float TopNailInsetY = 1.5f / 16f;
    public const float SheetFrontZ = 0.2f / 16f + 0.002f;
    public const float NailCorkEmbedZ = 1.6f / 16f;
    public const float DefaultNailCorkEmbedZ = 0.4f / 16f;

    // Nails live in full-height band 0 local space, then that band matrix, at SheetFrontZ minus
    // NailCorkEmbedZ so the tack sits in the cork.
    public static void TopCornerNailOffsets(
        out float leftX, out float leftY, out float leftZ,
        out float rightX, out float rightY, out float rightZ)
    {
        const float width = 8.0f / 16.0f;
        Matrixf m = BandMatrices(FullHeightUnits)[0];
        Vec4f left = m.TransformVector(new Vec4f(TopNailInsetX, TopNailInsetY, SheetFrontZ - NailCorkEmbedZ, 1));
        Vec4f right = m.TransformVector(new Vec4f(width - TopNailInsetX, TopNailInsetY, SheetFrontZ - NailCorkEmbedZ, 1));
        leftX = left.X;
        leftY = left.Y;
        leftZ = left.Z;
        rightX = right.X;
        rightY = right.Y;
        rightZ = right.Z;
    }

    // Where this sheet ends up around its tack, in blocks. Derived from the bands the mesh is
    // actually built from rather than hand-measured, because mLower carries a RotateZ(-180)
    // plus two RotateX folds: the vertical drop is well short of the sheet's contour length,
    // and the lean tips one bottom corner lower than the other.
    public static SheetBounds HangingBounds(int units, float tiltDeg)
    {
        PaperBand[] bands = BandLayout(units);
        Matrixf[] matrices = BandMatrices(units);

        float sin = GameMath.Sin(tiltDeg * GameMath.DEG2RAD);
        float cos = GameMath.Cos(tiltDeg * GameMath.DEG2RAD);

        // Seeded at the tack, which is both correct and load-bearing: the nail is a visible
        // object that has to stay on the cork, and MinY therefore stays at 0 for a sheet so
        // short it hangs entirely above its tack.
        float minX = 0f;
        float maxX = 0f;
        float minY = 0f;
        float maxY = 0f;

        for (int i = 0; i < bands.Length; i++)
        {
            PaperBand band = bands[i];
            for (int corner = 0; corner < 4; corner++)
            {
                float localX = corner == 1 || corner == 2 ? band.Width : 0f;
                float localY = corner >= 2 ? band.Height : 0f;

                Vec4f p = matrices[i].TransformVector(new Vec4f(localX, localY, 0, 1));

                // The lean is MeshData.Rotate about Z, applied around the tack at local origin.
                float x = p.X * cos - p.Y * sin;
                float y = p.X * sin + p.Y * cos;

                if (x < minX)
                    minX = x;
                if (x > maxX)
                    maxX = x;
                if (y < minY)
                    minY = y;
                if (y > maxY)
                    maxY = y;
            }
        }

        return new SheetBounds(minX, maxX, minY, maxY);
    }

#if DEBUG
    public static void SelfCheckLanternWindSmooth()
    {
        float k0 = 1f - (float)Math.Exp(-0f * LanternWindSmoothHz);
        if (k0 != 0f) throw new InvalidOperationException("smooth k at dt 0");
        float k1 = 1f - (float)Math.Exp(-1f * LanternWindSmoothHz);
        if (k1 <= 0.98f) throw new InvalidOperationException("smooth k at 1s");
    }
#endif
}
