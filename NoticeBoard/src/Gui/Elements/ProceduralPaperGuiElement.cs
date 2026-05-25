using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class ProceduralPaperGuiElement : GuiElementTextBase
{
    private ImageSurface cachedSurface;
    private static readonly Random globalRand = new Random();
    private readonly int paperSeed;
    private readonly ParchmentPalette parchmentPalette;
    private readonly bool drawJaggedEdges;

    public ProceduralPaperGuiElement(ICoreClientAPI capi, int id, ElementBounds bounds, ParchmentPalette parchmentPalette, bool drawJaggedEdges = true)
        : base(capi, "", null, bounds)
    {
        this.paperSeed = id;
        this.drawJaggedEdges = drawJaggedEdges;
        this.parchmentPalette = parchmentPalette;
    }

    private void GenerateSurface()
    {
        int w = (int)Bounds.OuterWidth;
        int h = (int)Bounds.OuterHeight;

        cachedSurface = new ImageSurface(Format.Argb32, w, h);

        using (Context ctx = new Context(cachedSurface))
        {
            ParchmentPalette palette = parchmentPalette;

            ctx.SetSourceRGBA(palette.BaseRGB[0], palette.BaseRGB[1], palette.BaseRGB[2], 1.0);
            ctx.Rectangle(0, 0, w, h);
            ctx.Fill();

            Random rand = new Random(this.paperSeed);
            int speckSize = 6;

            int noiseAmount = (w * h) / 150;
            for (int i = 0; i < noiseAmount; i++)
            {
                int nx = rand.Next(w / speckSize) * speckSize;
                int ny = rand.Next(h / speckSize) * speckSize;

                if (rand.NextDouble() > 0.5)
                {
                    ctx.SetSourceRGBA(palette.DarkRGB[0], palette.DarkRGB[1], palette.DarkRGB[2], 0.2);
                }
                else
                {
                    ctx.SetSourceRGBA(palette.LightRGB[0], palette.LightRGB[1], palette.LightRGB[2], 0.2);
                }

                ctx.Rectangle(nx, ny, speckSize, speckSize);
                ctx.Fill();
            }

            if (this.drawJaggedEdges)
            {
                double burntR = Math.Min(1.0, palette.DarkRGB[0] + 0.05);
                double burntG = Math.Max(0.0, palette.DarkRGB[1] - 0.05);
                double burntB = Math.Max(0.0, palette.DarkRGB[2] - 0.15);

                int edgeSize = 24; 
                int edgeSpecksCount = (int)(6.0 * (w + h)); 

                for (int i = 0; i < edgeSpecksCount; i++)
                {
                    int zone = rand.Next(4);
                    int nx = 0, ny = 0;
                    double distanceFactor = 0;

                    switch (zone)
                    {
                        case 0: // Top
                            nx = rand.Next(w / speckSize) * speckSize;
                            ny = rand.Next(edgeSize / speckSize) * speckSize;
                            distanceFactor = 1.0 - ((double)ny / edgeSize);
                            break;
                        case 1: // Bottom
                            nx = rand.Next(w / speckSize) * speckSize;
                            int distFromBottom = rand.Next(edgeSize / speckSize) * speckSize;
                            ny = h - speckSize - distFromBottom;
                            distanceFactor = 1.0 - ((double)distFromBottom / edgeSize);
                            break;
                        case 2: // Left
                            int distFromLeft = rand.Next(edgeSize / speckSize) * speckSize;
                            nx = distFromLeft;
                            ny = rand.Next(h / speckSize) * speckSize;
                            distanceFactor = 1.0 - ((double)distFromLeft / edgeSize);
                            break;
                        default: // Right
                            int distFromRight = rand.Next(edgeSize / speckSize) * speckSize;
                            nx = w - speckSize - distFromRight;
                            ny = rand.Next(h / speckSize) * speckSize;
                            distanceFactor = 1.0 - ((double)distFromRight / edgeSize);
                            break;
                    }

                    double opacity = (0.05 + (rand.NextDouble() * 0.25)) * (distanceFactor * distanceFactor);

                    if (opacity > 0.01) 
                    {
                        ctx.SetSourceRGBA(burntR, burntG, burntB, opacity);
                        ctx.Rectangle(nx, ny, speckSize, speckSize);
                        ctx.Fill();
                    }
                }

                double offTop = rand.NextDouble() * 1000.0;
                double offBot = rand.NextDouble() * 1000.0;
                double offLft = rand.NextDouble() * 1000.0;
                double offRgt = rand.NextDouble() * 1000.0;

                ctx.Operator = Operator.Clear;
                int maxTearBlocks = 3;

                // Top Edge
                for (int x = 0; x < w; x += speckSize)
                {
                    double slow = Math.Sin((x + offTop) * 0.008);
                    double fast = Math.Sin((x + offTop) * 0.05 + 1.2);
                    double burst = Math.Pow(Math.Abs(slow * fast), 2.0);
                    double jitter = rand.NextDouble() * 0.3;

                    int tearDepth = (int)((burst + jitter) * maxTearBlocks) + 1;
                    tearDepth = Math.Min(tearDepth, maxTearBlocks);

                    ctx.Rectangle(x, 0, speckSize, tearDepth * speckSize);
                    ctx.Fill();
                }

                // Bottom Edge
                for (int x = 0; x < w; x += speckSize)
                {
                    double slow = Math.Sin((x + offBot) * 0.009 + 5.0);
                    double fast = Math.Sin((x + offBot) * 0.04 + 3.1);
                    double burst = Math.Pow(Math.Abs(slow * fast), 2.0);
                    double jitter = rand.NextDouble() * 0.3;

                    int tearDepth = (int)((burst + jitter) * maxTearBlocks) + 1;
                    tearDepth = Math.Min(tearDepth, maxTearBlocks);

                    ctx.Rectangle(x, h - (tearDepth * speckSize), speckSize, tearDepth * speckSize);
                    ctx.Fill();
                }

                // Left Edge
                for (int y = 0; y < h; y += speckSize)
                {
                    double slow = Math.Sin((y + offLft) * 0.011 + 2.4);
                    double fast = Math.Sin((y + offLft) * 0.06 + 0.9);
                    double burst = Math.Pow(Math.Abs(slow * fast), 2.0);
                    double jitter = rand.NextDouble() * 0.3;

                    int tearDepth = (int)((burst + jitter) * maxTearBlocks) + 1;
                    tearDepth = Math.Min(tearDepth, maxTearBlocks);

                    ctx.Rectangle(0, y, tearDepth * speckSize, speckSize);
                    ctx.Fill();
                }

                // Right Edge
                for (int y = 0; y < h; y += speckSize)
                {
                    double slow = Math.Sin((y + offRgt) * 0.007 + 7.2);
                    double fast = Math.Sin((y + offRgt) * 0.045 + 4.8);
                    double burst = Math.Pow(Math.Abs(slow * fast), 2.0);
                    double jitter = rand.NextDouble() * 0.3;

                    int tearDepth = (int)((burst + jitter) * maxTearBlocks) + 1;
                    tearDepth = Math.Min(tearDepth, maxTearBlocks);

                    ctx.Rectangle(w - (tearDepth * speckSize), y, tearDepth * speckSize, speckSize);
                    ctx.Fill();
                }

                int cornerCut = 2 * speckSize;
                ctx.Rectangle(0, 0, cornerCut, cornerCut); // Top-Left
                ctx.Rectangle(w - cornerCut, 0, cornerCut, cornerCut); // Top-Right
                ctx.Rectangle(0, h - cornerCut, cornerCut, cornerCut); // Bottom-Left
                ctx.Rectangle(w - cornerCut, h - cornerCut, cornerCut, cornerCut); // Bottom-Right
                ctx.Fill();

                ctx.Operator = Operator.Over;
            }
        }
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        if (cachedSurface == null || cachedSurface.Width != (int)Bounds.OuterWidth || cachedSurface.Height != (int)Bounds.OuterHeight)
        {
            cachedSurface?.Dispose();
            GenerateSurface();
        }

        context.Save();
        context.Translate((int)Bounds.drawX, (int)Bounds.drawY);

        context.SetSourceSurface(cachedSurface, 0, 0);
        context.Paint();

        context.Restore();
    }

    public override void Dispose()
    {
        base.Dispose();
        cachedSurface?.Dispose();
    }
}