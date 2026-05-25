using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class ScaledGuiElementImage : GuiElementTextBase
{
    private readonly AssetLocation imageAsset;

    public ScaledGuiElementImage(ICoreClientAPI capi, ElementBounds bounds, AssetLocation imageAsset)
        : base(capi, "", null, bounds)
    {
        this.imageAsset = imageAsset;
    }

    public override void ComposeElements(Context context, ImageSurface surface)
    {
        context.Save();

        ImageSurface imageSurfaceFromAsset = GuiElement.getImageSurfaceFromAsset(api, imageAsset);

        double scaleX = Bounds.OuterWidth / imageSurfaceFromAsset.Width;
        double scaleY = Bounds.OuterHeight / imageSurfaceFromAsset.Height;

        context.Translate(Bounds.drawX, Bounds.drawY);

        context.Scale(scaleX, scaleY);

        context.SetSourceSurface(imageSurfaceFromAsset, 0, 0);
        context.Paint();

        context.Restore();
        imageSurfaceFromAsset.Dispose();
    }
}