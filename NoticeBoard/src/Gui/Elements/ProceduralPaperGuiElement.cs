using System;
using Cairo;
using NoticeBoard.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

public class ProceduralPaperGuiElement : GuiElementTextBase
{
    private ImageSurface cachedSurface;
    private readonly int messageId;
    private readonly int paperSeed;
    private readonly ParchmentPalette parchmentPalette;
    private readonly bool jaggedEdges;
    private readonly double wear01;
    private readonly ElementBounds visibleBounds;
    private readonly Action<int, bool> onHoverChanged;
    private readonly Action<int> onClicked;
    private bool isHovered;
    private bool downOnPaper;

    public ProceduralPaperGuiElement(
        ICoreClientAPI capi,
        int id,
        ElementBounds bounds,
        ParchmentPalette parchmentPalette,
        bool jaggedEdges = true,
        double wear01 = 0,
        ElementBounds visibleBounds = null,
        Action<int, bool> onHoverChanged = null,
        Action<int> onClicked = null,
        int parchmentSeed = 0
    )
        : base(capi, "", null, bounds)
    {
        this.messageId = id;
        this.paperSeed = parchmentSeed != 0 ? parchmentSeed : id;
        this.parchmentPalette = parchmentPalette;
        this.jaggedEdges = jaggedEdges;
        this.wear01 = wear01;
        this.visibleBounds = visibleBounds;
        this.onHoverChanged = onHoverChanged;
        this.onClicked = onClicked;
    }

    private void GenerateSurface()
    {
        int w = (int)Bounds.OuterWidth;
        int h = (int)Bounds.OuterHeight;
        cachedSurface = new ImageSurface(Format.Argb32, w, h);
        using Context ctx = new Context(cachedSurface);
        ProceduralPaper.Paint(ctx, 0, 0, w, h, paperSeed, parchmentPalette, jaggedEdges, wear01: wear01);
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

    // A row scrolled out of the clip area keeps absolute bounds that can still sit under the
    // cursor, so the visible inset has to agree before this row claims the hover. args.Handled
    // is deliberately left alone: the container stops dispatching move events at the first
    // element that sets it, which would kill the ink buttons' own hover.
    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseMove(api, args);

        bool inside = Bounds.PointInside(args.X, args.Y)
            && (visibleBounds == null || visibleBounds.PointInside(args.X, args.Y));

        if (inside == isHovered)
            return;

        isHovered = inside;
        onHoverChanged?.Invoke(messageId, inside);
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        // Do not call base: GuiElement.OnMouseDownOnElement sets Handled = true
        // and would make downOnPaper unreachable. Buttons already ran (added
        // before this paper) and set Handled when the click was theirs.
        if (args.Handled)
            return;
        downOnPaper = args.Button == EnumMouseButton.Left && RowContains(args.X, args.Y);
    }

    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        bool open = downOnPaper
            && !args.Handled
            && args.Button == EnumMouseButton.Left
            && RowContains(args.X, args.Y);
        downOnPaper = false;
        if (!open)
            return;
        onClicked?.Invoke(messageId);
    }

    private bool RowContains(double x, double y) =>
        Bounds.PointInside(x, y)
        && (visibleBounds == null || visibleBounds.PointInside(x, y));

    public override void Dispose()
    {
        base.Dispose();
        cachedSurface?.Dispose();
    }
}
