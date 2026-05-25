using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class StretchTiledGuiElementImage : GuiElementTextBase
{
    private readonly AssetLocation imageAsset;

    public StretchTiledGuiElementImage(ICoreClientAPI capi, ElementBounds bounds, AssetLocation imageAsset)
        : base(capi, "", null, bounds)
    {
        this.imageAsset = imageAsset;
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        context.Save();

        ImageSurface imageSurfaceFromAsset = GuiElement.getImageSurfaceFromAsset(api, imageAsset);

        double scaleX = Bounds.OuterWidth / imageSurfaceFromAsset.Width;

        context.Translate((int)Bounds.drawX, (int)Bounds.drawY);
        context.Scale(scaleX, 1.0);

        context.Rectangle(0, 0, imageSurfaceFromAsset.Width, Bounds.OuterHeight);
        context.Clip();

        int texHeight = imageSurfaceFromAsset.Height;
        int currentY = 0;
        int maxDrawHeight = (int)Math.Ceiling(Bounds.OuterHeight);

        while (currentY < maxDrawHeight)
        {
            context.SetSourceSurface(imageSurfaceFromAsset, 0, currentY);
            context.Paint();
            currentY += texHeight;
        }

        context.Restore();
        imageSurfaceFromAsset.Dispose();
    }
}