using Cairo;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class BottomAnchoredGuiElementImage : GuiElementTextBase
{
    private readonly AssetLocation imageAsset;

    public BottomAnchoredGuiElementImage(ICoreClientAPI capi, ElementBounds bounds, AssetLocation imageAsset)
        : base(capi, "", null, bounds)
    {
        this.imageAsset = imageAsset;
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        context.Save();

        ImageSurface imageSurfaceFromAsset = GuiElement.getImageSurfaceFromAsset(api, imageAsset);

        double scaleX = Bounds.OuterWidth / imageSurfaceFromAsset.Width;

        double scaledImageHeight = imageSurfaceFromAsset.Height;

        double yOffset = Math.Max(0, scaledImageHeight - Bounds.OuterHeight);

        context.Translate((int)Bounds.drawX, (int)Bounds.drawY);
        context.Scale(scaleX, 1.0);

        context.Rectangle(0, 0, imageSurfaceFromAsset.Width, Bounds.OuterHeight);
        context.Clip();

        context.SetSourceSurface(imageSurfaceFromAsset, 0, -(int)yOffset);
        context.Paint();

        context.Restore();
        imageSurfaceFromAsset.Dispose();
    }
}