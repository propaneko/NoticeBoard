using System;
using Cairo;
using Vintagestory.API.Client;

public class GuiElementPaperBackground : GuiElement
{
    public GuiElementPaperBackground(ICoreClientAPI capi, ElementBounds bounds)
        : base(capi, bounds) { }

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();

        double x = Bounds.drawX;
        double y = Bounds.drawY;
        double w = Bounds.InnerWidth;
        double h = Bounds.InnerHeight;

        // Guard against invalid bounds
        if (w <= 0 || h <= 0)
            return;

        double radius = 4.0;
        ctx.Save();

        // Draw parchment-colored rounded rectangle
        ctx.NewPath();
        ctx.Arc(x + radius, y + radius, radius, Math.PI, 1.5 * Math.PI);
        ctx.Arc(x + w - radius, y + radius, radius, 1.5 * Math.PI, 0);
        ctx.Arc(x + w - radius, y + h - radius, radius, 0, 0.5 * Math.PI);
        ctx.Arc(x + radius, y + h - radius, radius, 0.5 * Math.PI, Math.PI);
        ctx.ClosePath();

        ctx.SetSourceRGBA(0.85, 0.78, 0.60, 0.35); // warm parchment tint
        ctx.FillPreserve();

        ctx.SetSourceRGBA(0.75, 0.65, 0.45, 0.5); // slightly darker border
        ctx.LineWidth = 1.0;
        ctx.Stroke();

        ctx.Restore();
    }

    public override void RenderInteractiveElements(float deltaTime) { }
}
