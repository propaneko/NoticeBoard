using System;
using System.Collections.Generic;
using Cairo;
using NoticeBoard.Utils;

namespace NoticeBoard.Rendering;

public static class ProceduralPaper
{
    public const int DefaultGrain = 6;

    private const int MaxCachedSurfaces = 64;

    // sheetLower carries a RotateZ(-180), so band 0 is the top of the hanging sheet and image v
    // grows downward from it. Darkening down the page is what makes the two creases read.
    private static readonly double[] FoldBrightness = { 1.0, 0.95, 0.89 };

    private static readonly Dictionary<(int W, int H, int Bucket, string Palette, int Grain, bool Jagged, bool Shaded, int Buckets, int WearBucket), ImageSurface> surfaceCache = new();
    private static readonly Queue<(int W, int H, int Bucket, string Palette, int Grain, bool Jagged, bool Shaded, int Buckets, int WearBucket)> cacheOrder = new();

    private static int MaxBitePixels(int grain = DefaultGrain) => 3 * Math.Max(1, grain);

    public static void Paint(
        Context ctx,
        double x,
        double y,
        int w,
        int h,
        int seed,
        ParchmentPalette palette,
        bool jaggedEdges,
        int grain = DefaultGrain,
        PaperBand[] foldShading = null,
        double wear01 = 0
    )
    {
        if (ctx == null || palette == null || w <= 0 || h <= 0)
            return;

        if (grain < 1)
            grain = DefaultGrain;

        ctx.Save();
        ctx.Rectangle(x, y, w, h);
        ctx.Clip();
        ctx.Translate(x, y);

        PaintLocal(ctx, w, h, seed, palette, jaggedEdges, grain, foldShading, wear01);

        ctx.Restore();
    }

    // Same output as Paint, but reuses finished surfaces across sheets that share a size, theme
    // and variant bucket. A full-height tile is several thousand Cairo fills and the world
    // rebuild runs on the render thread, so a board full of notices would otherwise hitch the
    // first time it comes into view.
    public static void PaintCached(
        Context ctx,
        double x,
        double y,
        int w,
        int h,
        int bucket,
        ParchmentPalette palette,
        bool jaggedEdges,
        int wearBuckets,
        int grain = DefaultGrain,
        PaperBand[] foldShading = null,
        double wear01 = 0
    )
    {
        if (ctx == null || palette == null || w <= 0 || h <= 0)
            return;

        if (grain < 1)
            grain = DefaultGrain;

        ImageSurface cached = GetOrCreatePaper(w, h, bucket, palette, jaggedEdges, grain, foldShading, wear01, wearBuckets);

        // The cached surface is generated at logical size, so a supersampled context would
        // magnify it. Nearest reproduces it exactly: the speckle grid and torn edges are
        // deliberately blocky and axis-aligned, and the extra resolution belongs to the text.
        using SurfacePattern pattern = new SurfacePattern(cached);
        pattern.Filter = Filter.Nearest;

        ctx.Save();
        ctx.Translate(x, y);
        ctx.SetSource(pattern);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();
        ctx.Restore();
    }

    private static ImageSurface GetOrCreatePaper(
        int w,
        int h,
        int bucket,
        ParchmentPalette palette,
        bool jaggedEdges,
        int grain,
        PaperBand[] foldShading,
        double wear01,
        int wearBuckets
    )
    {
        if (grain < 1)
            grain = DefaultGrain;

        int buckets = Math.Max(1, wearBuckets);
        int wearBucket = (int)Math.Round(wear01 * buckets);

        // foldShading is keyed as present/absent, not band contents. Same size/theme/bucket share one surface.
        var key = (w, h, bucket, palette.Name ?? string.Empty, grain, jaggedEdges, foldShading != null, buckets, wearBucket);
        if (!surfaceCache.TryGetValue(key, out ImageSurface cached))
        {
            cached = new ImageSurface(Format.Argb32, w, h);
            using (Context surfaceCtx = new Context(cached))
            {
                PaintLocal(surfaceCtx, w, h, bucket, palette, jaggedEdges, grain, foldShading, wear01);
            }

            surfaceCache[key] = cached;
            cacheOrder.Enqueue(key);

            while (cacheOrder.Count > MaxCachedSurfaces)
            {
                var oldest = cacheOrder.Dequeue();
                if (surfaceCache.Remove(oldest, out ImageSurface evicted))
                    evicted.Dispose();
            }
        }

        return cached;
    }


    // The appearance of every existing board is a pure function of the seed and the order of
    // the draws below, so reordering the random draws silently reskins them all.
    private static void PaintLocal(
        Context ctx,
        int w,
        int h,
        int seed,
        ParchmentPalette palette,
        bool jaggedEdges,
        int grain,
        PaperBand[] foldShading,
        double wear01
    )
    {
        FillBase(ctx, w, h, palette, foldShading);

        Random rand = new Random(seed);
        int speckSize = grain;

        int noiseAmount = (w * h) / 150;
        for (int i = 0; i < noiseAmount; i++)
        {
            int nx = rand.Next(Math.Max(1, w / speckSize)) * speckSize;
            int ny = rand.Next(Math.Max(1, h / speckSize)) * speckSize;

            double shade = Brightness(foldShading, h, ny);

            if (rand.NextDouble() > 0.5)
            {
                ctx.SetSourceRGBA(palette.DarkRGB[0] * shade, palette.DarkRGB[1] * shade, palette.DarkRGB[2] * shade, 0.2);
            }
            else
            {
                ctx.SetSourceRGBA(palette.LightRGB[0] * shade, palette.LightRGB[1] * shade, palette.LightRGB[2] * shade, 0.2);
            }

            ctx.Rectangle(nx, ny, speckSize, speckSize);
            ctx.Fill();
        }

        if (jaggedEdges)
            PaintTornEdges(ctx, w, h, rand, speckSize, palette.BaseRGB, palette.DarkRGB, foldShading, wear01);
    }

    private static void PaintTornEdges(
        Context ctx,
        int w,
        int h,
        Random rand,
        int speckSize,
        double[] baseRGB,
        double[] darkRGB,
        PaperBand[] foldShading,
        double wear01 = 0
    )
    {
        double burntR = Math.Min(1.0, darkRGB[0] + 0.05);
        double burntG = Math.Max(0.0, darkRGB[1] - 0.05);
        double burntB = Math.Max(0.0, darkRGB[2] - 0.15);

        int edgeSize = 4 * speckSize;
        int edgeSpecksCount = (int)(6.0 * (w + h));

        // Weight zones by perimeter length so left/right strips are not overpainted.
        double topBotWeight = Math.Max(1, w);
        double leftRightWeight = Math.Max(1, h);
        double totalWeight = 2.0 * topBotWeight + 2.0 * leftRightWeight;

        for (int i = 0; i < edgeSpecksCount; i++)
        {
            double pick = rand.NextDouble() * totalWeight;
            int zone;
            if (pick < topBotWeight)
                zone = 0;
            else if (pick < 2.0 * topBotWeight)
                zone = 1;
            else if (pick < 2.0 * topBotWeight + leftRightWeight)
                zone = 2;
            else
                zone = 3;

            int nx = 0, ny = 0;
            double distanceFactor = 0;

            switch (zone)
            {
                case 0:
                    nx = rand.Next(Math.Max(1, w / speckSize)) * speckSize;
                    ny = rand.Next(Math.Max(1, edgeSize / speckSize)) * speckSize;
                    distanceFactor = 1.0 - ((double)ny / edgeSize);
                    break;
                case 1:
                    nx = rand.Next(Math.Max(1, w / speckSize)) * speckSize;
                    int distFromBottom = rand.Next(Math.Max(1, edgeSize / speckSize)) * speckSize;
                    ny = h - speckSize - distFromBottom;
                    distanceFactor = 1.0 - ((double)distFromBottom / edgeSize);
                    break;
                case 2:
                    int distFromLeft = rand.Next(Math.Max(1, edgeSize / speckSize)) * speckSize;
                    nx = distFromLeft;
                    ny = rand.Next(Math.Max(1, h / speckSize)) * speckSize;
                    distanceFactor = 1.0 - ((double)distFromLeft / edgeSize);
                    break;
                default:
                    int distFromRight = rand.Next(Math.Max(1, edgeSize / speckSize)) * speckSize;
                    nx = w - speckSize - distFromRight;
                    ny = rand.Next(Math.Max(1, h / speckSize)) * speckSize;
                    distanceFactor = 1.0 - ((double)distFromRight / edgeSize);
                    break;
            }

            double opacity = (0.04 + rand.NextDouble() * 0.18) * distanceFactor;
            if (opacity <= 0.01)
                continue;

            double t = distanceFactor;
            double shade = Brightness(foldShading, h, ny);
            double r = (burntR * t + baseRGB[0] * (1.0 - t)) * shade;
            double g = (burntG * t + baseRGB[1] * (1.0 - t)) * shade;
            double b = (burntB * t + baseRGB[2] * (1.0 - t)) * shade;

            ctx.SetSourceRGBA(r, g, b, opacity);
            ctx.Rectangle(nx, ny, speckSize, speckSize);
            ctx.Fill();
        }

        double offTop = rand.NextDouble() * 1000.0;
        double offBot = rand.NextDouble() * 1000.0;
        double offLft = rand.NextDouble() * 1000.0;
        double offRgt = rand.NextDouble() * 1000.0;

        // Clear punches alpha bites (torn edge). Restore Operator.Over after the four sides and corners.
        ctx.Operator = Operator.Clear;
        int baseBlocks = Math.Max(1, MaxBitePixels(speckSize) / speckSize);
        int wornBlocks = (int)Math.Max(1, baseBlocks * (1.0 + 0.6 * wear01));
        double sideJitter = 0.3 + 0.18 * wear01;
        int holderY = 8 * speckSize;

        for (int x = 0; x < w; x += speckSize)
        {
            double slow = Math.Sin((x + offTop) * 0.008);
            double fast = Math.Sin((x + offTop) * 0.05 + 1.2);
            double burst = Math.Pow(Math.Abs(slow * fast), 2.0);
            double jitter = rand.NextDouble() * 0.3;

            int tearDepth = (int)((burst + jitter) * baseBlocks) + 1;
            tearDepth = Math.Min(tearDepth, baseBlocks);

            ctx.Rectangle(x, 0, speckSize, tearDepth * speckSize);
            ctx.Fill();
        }

        for (int x = 0; x < w; x += speckSize)
        {
            double slow = Math.Sin((x + offBot) * 0.009 + 5.0);
            double fast = Math.Sin((x + offBot) * 0.04 + 3.1);
            double burst = Math.Pow(Math.Abs(slow * fast), 2.0);
            double jitter = rand.NextDouble() * sideJitter;

            int tearDepth = (int)((burst + jitter) * wornBlocks) + 1;
            tearDepth = Math.Min(tearDepth, wornBlocks);

            ctx.Rectangle(x, h - (tearDepth * speckSize), speckSize, tearDepth * speckSize);
            ctx.Fill();
        }

        for (int y = 0; y < h; y += speckSize)
        {
            double slow = Math.Sin((y + offLft) * 0.011 + 2.4);
            double fast = Math.Sin((y + offLft) * 0.06 + 0.9);
            double burst = Math.Pow(Math.Abs(slow * fast), 2.0);
            double jitter = rand.NextDouble() * sideJitter;

            int tearDepth = (int)((burst + jitter) * wornBlocks) + 1;
            tearDepth = Math.Min(tearDepth, wornBlocks);
            if (y < holderY)
                tearDepth = Math.Min(tearDepth, baseBlocks);

            ctx.Rectangle(0, y, tearDepth * speckSize, speckSize);
            ctx.Fill();
        }

        for (int y = 0; y < h; y += speckSize)
        {
            double slow = Math.Sin((y + offRgt) * 0.007 + 7.2);
            double fast = Math.Sin((y + offRgt) * 0.045 + 4.8);
            double burst = Math.Pow(Math.Abs(slow * fast), 2.0);
            double jitter = rand.NextDouble() * sideJitter;

            int tearDepth = (int)((burst + jitter) * wornBlocks) + 1;
            tearDepth = Math.Min(tearDepth, wornBlocks);
            if (y < holderY)
                tearDepth = Math.Min(tearDepth, baseBlocks);

            ctx.Rectangle(w - (tearDepth * speckSize), y, tearDepth * speckSize, speckSize);
            ctx.Fill();
        }

        // Corner cuts use two thirds of the edge bite depth, giving a 2-block cut at the
        // default grain. Top corners stay at that size so the holder still sits in paper.
        int topCorner = Math.Max(1, baseBlocks * 2 / 3) * speckSize;
        int botCorner = Math.Max(1, wornBlocks * 2 / 3) * speckSize;
        ctx.Rectangle(0, 0, topCorner, topCorner);
        ctx.Rectangle(w - topCorner, 0, topCorner, topCorner);
        ctx.Rectangle(0, h - botCorner, botCorner, botCorner);
        ctx.Rectangle(w - botCorner, h - botCorner, botCorner, botCorner);
        ctx.Fill();

        ctx.Operator = Operator.Over;
    }

    private const int MaxCachedPermitSurfaces = 40;
    private static readonly Dictionary<(int W, int H, int Bucket, int StripIndex, string Palette), ImageSurface> permitSurfaceCache = new();
    private static readonly Queue<(int W, int H, int Bucket, string Palette)> permitCacheOrder = new();

    // A handful of variants (one per bucket) is cached and reused across every permit on every
    // board, since the badge is small and drawn far more often than the paper's own patterns.
    public static void PaintPermitCached(Context ctx, double x, double y, int w, int h, int bucket, ParchmentPalette palette, int stripIndex = 0)
    {
        if (ctx == null || palette == null || w <= 0 || h <= 0)
            return;

        string paletteName = palette.Name ?? "";
        var key = (w, h, bucket, stripIndex, paletteName);
        if (!permitSurfaceCache.TryGetValue(key, out ImageSurface cached))
        {
            cached = new ImageSurface(Format.Argb32, w, h);
            using (Context surfaceCtx = new Context(cached))
            {
                PaintPermit(surfaceCtx, w, h, bucket, stripIndex, palette);
            }

            permitSurfaceCache[key] = cached;
            permitCacheOrder.Enqueue((w, h, bucket, paletteName));

            while (permitCacheOrder.Count > MaxCachedPermitSurfaces)
            {
                var oldest = permitCacheOrder.Dequeue();
                // Evict all strip variants for this tile size/bucket/theme.
                for (int strip = 0; strip < 2; strip++)
                {
                    var evictKey = (oldest.W, oldest.H, oldest.Bucket, strip, oldest.Palette);
                    if (permitSurfaceCache.Remove(evictKey, out ImageSurface evicted))
                        evicted.Dispose();
                }
            }
        }

        using SurfacePattern pattern = new SurfacePattern(cached);
        pattern.Filter = Filter.Nearest;

        ctx.Save();
        ctx.Translate(x, y);
        ctx.SetSource(pattern);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();
        ctx.Restore();
    }

    private const int MaxCachedWaxSurfaces = 16;
    private static readonly Dictionary<(int W, int H, string Palette), ImageSurface> waxSealSurfaceCache = new();
    private static readonly Queue<(int W, int H, string Palette)> waxSealCacheOrder = new();

    public static void PaintWaxSealCached(Context ctx, double x, double y, int w, int h, ParchmentPalette palette)
    {
        if (ctx == null || palette == null || w <= 0 || h <= 0)
            return;

        string paletteName = palette.Name ?? "";
        var key = (w, h, paletteName);
        if (!waxSealSurfaceCache.TryGetValue(key, out ImageSurface cached))
        {
            cached = new ImageSurface(Format.Argb32, w, h);
            using (Context surfaceCtx = new Context(cached))
            {
                PaintWaxSeal(surfaceCtx, w, h, palette);
            }

            waxSealSurfaceCache[key] = cached;
            waxSealCacheOrder.Enqueue(key);

            while (waxSealCacheOrder.Count > MaxCachedWaxSurfaces)
            {
                var oldest = waxSealCacheOrder.Dequeue();
                if (waxSealSurfaceCache.Remove(oldest, out ImageSurface evicted))
                    evicted.Dispose();
            }
        }

        using SurfacePattern pattern = new SurfacePattern(cached);
        pattern.Filter = Filter.Nearest;

        ctx.Save();
        ctx.Translate(x, y);
        ctx.SetSource(pattern);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();
        ctx.Restore();
    }

    private static void PaintWaxSeal(Context ctx, int w, int h, ParchmentPalette palette)
    {
        double[] wax =
        {
            palette.LinkColor[0] * 0.7,
            palette.LinkColor[1] * 0.7,
            palette.LinkColor[2] * 0.7
        };
        ctx.SetSourceRGBA(wax[0], wax[1], wax[2], 1.0);
        ctx.Rectangle(0, 0, w, h);
        ctx.Fill();

        ctx.SetSourceRGBA(
            Math.Min(1.0, wax[0] * 1.25),
            Math.Min(1.0, wax[1] * 1.25),
            Math.Min(1.0, wax[2] * 1.25),
            1.0);
        int inset = Math.Max(1, w / 6);
        ctx.Rectangle(inset, inset, w - inset * 2, h - inset * 2);
        ctx.Fill();
    }

    private static void PaintPermit(Context ctx, int w, int h, int seed, int stripIndex, ParchmentPalette palette)
    {
        int contentW = (int)(w * 0.90);
        int contentH = h;
        int offsetX = (w - contentW) / 2 + stripIndex * 2;

        ctx.Save();
        ctx.Translate(offsetX, 0);

        ctx.SetSourceRGBA(palette.BaseRGB[0], palette.BaseRGB[1], palette.BaseRGB[2], 1.0);
        ctx.Rectangle(0, 0, contentW, contentH);
        ctx.Fill();

        Random rand = new Random(seed * 1009 + stripIndex * 4177);
        int speckSize = Math.Max(1, Math.Min(contentW, contentH) / 28);
        int noiseAmount = (contentW * contentH) / 180;
        for (int i = 0; i < noiseAmount; i++)
        {
            int nx = rand.Next(Math.Max(1, contentW / speckSize)) * speckSize;
            int ny = rand.Next(Math.Max(1, contentH / speckSize)) * speckSize;
            bool stain = rand.NextDouble() > 0.65;
            double[] c = stain ? palette.DarkRGB : (rand.NextDouble() > 0.5 ? palette.DarkRGB : palette.BaseRGB);

            ctx.SetSourceRGBA(c[0], c[1], c[2], stain ? 0.22 : 0.18);
            ctx.Rectangle(nx, ny, speckSize, speckSize);
            ctx.Fill();
        }

        PaintGothicScribble(ctx, contentW, contentH, rand, palette.InkColor);
        PaintHeavyBottomChar(ctx, contentW, contentH, rand, speckSize);
        PaintTornEdges(ctx, contentW, contentH, rand, speckSize, palette.BaseRGB, palette.DarkRGB, null);
        PaintBottomSymbol(ctx, contentW, contentH, rand, palette.InkColor);

        ctx.Restore();
    }

    private static void PaintGothicScribble(Context ctx, int w, int h, Random rand, double[] ink)
    {
        double marginX = w * 0.12;
        double marginY = h * 0.06;
        double colW = w * 0.22;
        int colCount = Math.Max(2, (int)((w - marginX * 2) / colW));

        ctx.LineCap = LineCap.Square;
        ctx.LineJoin = LineJoin.Miter;

        for (int col = 0; col < colCount; col++)
        {
            double x = marginX + col * colW + rand.NextDouble() * colW * 0.2;
            double y = marginY;
            double colBottom = h * (0.72 + rand.NextDouble() * 0.08);

            while (y < colBottom)
            {
                double segH = h * (0.025 + rand.NextDouble() * 0.04);
                double tilt = (rand.NextDouble() - 0.5) * 0.4;
                double thickness = Math.Max(0.8, w * 0.018) * (0.8 + rand.NextDouble() * 0.5);

                ctx.SetSourceRGBA(ink[0], ink[1], ink[2], 0.45 + rand.NextDouble() * 0.35);
                ctx.LineWidth = thickness;
                ctx.MoveTo(x, y);
                ctx.LineTo(x + tilt * segH, y + segH);
                ctx.Stroke();

                if (rand.NextDouble() > 0.55)
                {
                    double branch = segH * (0.3 + rand.NextDouble() * 0.4);
                    ctx.MoveTo(x + tilt * segH * 0.5, y + segH * 0.5);
                    ctx.LineTo(x + branch, y + segH * 0.5 - branch * 0.3);
                    ctx.Stroke();
                }

                y += segH * (0.85 + rand.NextDouble() * 0.25);
            }
        }
    }

    private static void PaintHeavyBottomChar(Context ctx, int w, int h, Random rand, int speckSize)
    {
        int charBandTop = (int)(h * 0.78);
        int charBandHeight = h - charBandTop;
        int specks = charBandHeight * w / (speckSize * speckSize * 2);

        for (int i = 0; i < specks; i++)
        {
            int nx = rand.Next(Math.Max(1, w / speckSize)) * speckSize;
            int ny = charBandTop + rand.Next(Math.Max(1, charBandHeight / speckSize)) * speckSize;
            double t = (double)(ny - charBandTop) / Math.Max(1, charBandHeight);
            double opacity = 0.15 + t * 0.45;

            ctx.SetSourceRGBA(0.05, 0.03, 0.02, opacity);
            ctx.Rectangle(nx, ny, speckSize, speckSize);
            ctx.Fill();
        }
    }

    private static void PaintBottomSymbol(Context ctx, int w, int h, Random rand, double[] ink)
    {
        double cx = w * 0.5;
        double cy = h * 0.90;
        double size = w * 0.18;

        ctx.SetSourceRGBA(ink[0], ink[1], ink[2], 0.55);
        ctx.LineWidth = Math.Max(1.0, w * 0.025);
        ctx.MoveTo(cx - size, cy);
        ctx.LineTo(cx + size, cy);
        ctx.Stroke();
        ctx.MoveTo(cx, cy - size * 0.7);
        ctx.LineTo(cx, cy + size * 0.5);
        ctx.Stroke();
    }

    private static void FillBase(Context ctx, int w, int h, ParchmentPalette palette, PaperBand[] foldShading)
    {
        if (foldShading == null || foldShading.Length == 0)
        {
            ctx.SetSourceRGBA(palette.BaseRGB[0], palette.BaseRGB[1], palette.BaseRGB[2], 1.0);
            ctx.Rectangle(0, 0, w, h);
            ctx.Fill();
            return;
        }

        for (int i = 0; i < foldShading.Length; i++)
        {
            double top = Math.Floor(foldShading[i].VStart * h);
            double bottom = i == foldShading.Length - 1 ? h : Math.Floor(foldShading[i].VEnd * h);
            if (bottom <= top)
                continue;

            double shade = FoldBrightness[Math.Min(i, FoldBrightness.Length - 1)];
            ctx.SetSourceRGBA(palette.BaseRGB[0] * shade, palette.BaseRGB[1] * shade, palette.BaseRGB[2] * shade, 1.0);
            ctx.Rectangle(0, top, w, bottom - top);
            ctx.Fill();
        }
    }

    private static double Brightness(PaperBand[] foldShading, int h, double pixelY)
    {
        if (foldShading == null || foldShading.Length == 0 || h <= 0)
            return 1.0;

        double v = pixelY / h;
        for (int i = 0; i < foldShading.Length; i++)
        {
            if (v <= foldShading[i].VEnd)
                return FoldBrightness[Math.Min(i, FoldBrightness.Length - 1)];
        }

        return FoldBrightness[Math.Min(foldShading.Length - 1, FoldBrightness.Length - 1)];
    }
}
