using System;
using Cairo;
using NoticeBoard;
using NoticeBoard.BlockType;
using NoticeBoard.Extensions;
using NoticeBoard.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Rendering;

public class NoticeBoardPreviewOverlay : HudElement
{
    public const string HotkeyCode = "noticeboardpreview";

    public static NoticeBoardPreviewOverlay Instance { get; private set; }

    private LoadedTexture sheetTexture;
    private LoadedTexture dimTexture;
    private BlockPos cachedPos;
    private int cachedMessageId = -1;
    private string cachedStamp;
    private int cachedSuperSample;

    private MessageVisualData pinnedData;
    private string pinnedFont;
    private string pinnedTheme;
    private float pinnedFontSize;
    private int pinnedMessageId = -1;
    private bool pinnedAging;
    private int pinnedLifeDays = GameDateFormatter.VanillaMonthDays;
    private bool mouseHooked;

    public override string ToggleKeyCombinationCode => null;
    public override double DrawOrder => 0.25;
    public override bool Focusable => false;
    public override bool PrefersUngrabbedMouse => false;
    public override bool ShouldReceiveKeyboardEvents() => false;
    public override bool ShouldReceiveMouseEvents() => pinnedData != null;

    public bool IsPinned => pinnedData != null;

    public NoticeBoardPreviewOverlay(ICoreClientAPI capi)
        : base(capi)
    {
        Instance = this;
        TryOpen();
    }

    public override bool TryClose()
    {
        HidePinned();
        return false;
    }

    public void ShowPinned(
        int messageId,
        MessageVisualData data,
        string boardFont,
        string boardTheme,
        float boardFontSize,
        bool enableAging = false,
        int noticeAgingDays = GameDateFormatter.VanillaMonthDays)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.Text))
            return;
        pinnedData = data;
        pinnedFont = boardFont;
        pinnedTheme = boardTheme;
        pinnedFontSize = boardFontSize;
        pinnedMessageId = messageId;
        pinnedAging = enableAging;
        pinnedLifeDays = noticeAgingDays;
        if (mouseHooked)
            return;
        capi.Event.MouseDown += OnPinnedMouseDown;
        mouseHooked = true;
    }

    public void HidePinned()
    {
        pinnedData = null;
        pinnedFont = null;
        pinnedTheme = null;
        pinnedFontSize = 0;
        pinnedMessageId = -1;
        pinnedAging = false;
        pinnedLifeDays = GameDateFormatter.DefaultLifeDays(capi.World.Calendar);
        if (mouseHooked)
        {
            capi.Event.MouseDown -= OnPinnedMouseDown;
            mouseHooked = false;
        }
        ReleaseSheet();
    }

    private void OnPinnedMouseDown(MouseEvent args)
    {
        if (pinnedData == null)
            return;
        HidePinned();
        args.Handled = true;
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (pinnedData == null)
            return;
        HidePinned();
        args.Handled = true;
    }

    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);

        if (pinnedData != null)
        {
            DrawSheet(
                pinnedData, pinnedFont, pinnedTheme, pinnedFontSize, pinnedMessageId,
                worldPos: null, z: 50f, pinnedAging, pinnedLifeDays);
            return;
        }

        if (!IsHotkeyHeld())
        {
            ReleaseSheet();
            return;
        }

        BlockSelection sel = capi.World.Player?.CurrentBlockSelection;
        NoticeBoardBlockEntity be = sel != null ? capi.GetNoticeBoardEntity(sel.Position) : null;
        if (be == null && !PaperPinController.TryTraceBoard(capi.World, capi.World.Player, out sel, out be))
        {
            ReleaseSheet();
            return;
        }

        if (be == null || sel == null)
        {
            ReleaseSheet();
            return;
        }

        if (!PaperPinController.IsWithinInteractDistance(capi.World.Player, be.Pos))
        {
            ReleaseSheet();
            return;
        }

        var entity = capi.World.Player.Entity;
        Vec3d eye = entity.Pos.XYZ.AddCopy(entity.LocalEyePos);
        Vec3d hit = sel.Position.ToVec3d().AddCopy(sel.HitPosition);
        Vec3d dir = new Vec3d(hit.X - eye.X, hit.Y - eye.Y, hit.Z - eye.Z);
        int messageId = be.PickMessageAtLookRay(eye, dir);

        MessageVisualData data = messageId >= 0 ? be.GetVisualData(messageId) : null;
        if (data == null || string.IsNullOrWhiteSpace(data.Text))
        {
            ReleaseSheet();
            return;
        }

        DrawSheet(
            data,
            be.BoardProperties?.BoardFont,
            be.BoardProperties?.BoardTheme,
            be.BoardProperties?.BoardFontSize ?? 0,
            messageId,
            sel.Position,
            z: 50f,
            be.BoardProperties?.EnableNoticeAging == 1,
            be.BoardProperties?.NoticeAgingDays ?? GameDateFormatter.DefaultLifeDays(capi.World.Calendar)
        );
    }

    private static void ResolveWear(
        ICoreClientAPI capi,
        bool aging,
        int lifeDays,
        MessageVisualData data,
        out double age01,
        out int wearBuckets,
        out int tickStep)
    {
        wearBuckets = GameDateFormatter.WearBuckets(lifeDays);
        age01 = 0;
        tickStep = 0;
        if (!aging || data == null)
            return;
        var cal = capi.World.Calendar;
        int days = GameDateFormatter.ClampLifeDays(lifeDays);
        age01 = GameDateFormatter.Age01(data.TotalHours, cal.TotalHours, cal.HoursPerDay, days);
        tickStep = (int)(cal.TotalHours / GameDateFormatter.WearTickHours(cal.HoursPerDay, days));
    }

    private void DrawSheet(
        MessageVisualData data,
        string boardFont,
        string boardTheme,
        float boardFontSize,
        int messageId,
        BlockPos worldPos,
        float z = 50f,
        bool enableAging = false,
        int noticeAgingDays = GameDateFormatter.VanillaMonthDays
    )
    {
        int units = Math.Clamp(
            PaperSize.MeasureHeightUnitsUnclamped(
                capi, data.Text, boardFont, boardFontSize, hasAuthor: !string.IsNullOrEmpty(data.Author)),
            1, PaperSize.PreviewMaxUnits);
        int tilePx = PaperSize.TilePixels(units);
        float scale = Math.Min(
            capi.Render.FrameHeight / (float)tilePx,
            capi.Render.FrameWidth * 0.45f / PaperSize.TextWidth);
        float w = PaperSize.TextWidth * scale;
        float h = tilePx * scale;
        int superSample = GameMath.Clamp((int)Math.Ceiling(w / PaperSize.TextWidth), 1, PaperSize.MaxTextSharpness);

        ResolveWear(capi, enableAging, noticeAgingDays, data, out double age01, out int wearBuckets, out int tickStep);
        string stamp = $"{data.Text}\u0000{data.Author}\u0000{data.Date}\u0000{data.PaperTheme}\u0000{boardTheme}\u0000{boardFont}\u0000{boardFontSize}\u0000{enableAging}\u0000{noticeAgingDays}\u0000{tickStep}";
        bool posChanged = worldPos != null && (cachedPos == null || !cachedPos.Equals(worldPos));
        if (sheetTexture == null
            || cachedMessageId != messageId
            || posChanged
            || cachedStamp != stamp
            || cachedSuperSample != superSample)
        {
            ReleaseSheet();
            sheetTexture = NoticeBoardPaperTextRenderer.RasterizeSheet(
                capi, data, units, boardFont, boardTheme,
                superSample, PaperSize.PreviewMaxUnits, boardFontSize, age01, wearBuckets, data.ResolveParchmentSeed(messageId));
            cachedPos = worldPos?.Copy();
            cachedMessageId = messageId;
            cachedStamp = stamp;
            cachedSuperSample = superSample;
        }

        EnsureDimTexture();
        capi.Render.Render2DTexturePremultipliedAlpha(
            dimTexture.TextureId, 0, 0, capi.Render.FrameWidth, capi.Render.FrameHeight, z);
        capi.Render.Render2DTexturePremultipliedAlpha(
            sheetTexture.TextureId,
            (capi.Render.FrameWidth - w) * 0.5f,
            (capi.Render.FrameHeight - h) * 0.5f,
            w, h, z);
    }

    // IsHotKeyPressed reads the live held state of the player's own binding, modifiers and
    // mouse buttons included, so nothing here has to touch GlKeys. It carries no XML doc in
    // VintagestoryAPI.xml but is on IInputAPI in 1.22.7: bool IsHotKeyPressed(string).
    // MouseGrabbed is false whenever a dialog owns input, which is what stops the preview
    // from firing while the player is typing in the board GUI.
    private bool IsHotkeyHeld() =>
        capi.Input.MouseGrabbed && capi.Input.IsHotKeyPressed(HotkeyCode);

    private void EnsureDimTexture()
    {
        if (dimTexture != null)
            return;

        dimTexture = new LoadedTexture(capi);
        using ImageSurface surface = new ImageSurface(Format.Argb32, 2, 2);
        using (Context ctx = new Context(surface))
        {
            ctx.SetSourceRGBA(0, 0, 0, 0.55);
            ctx.Paint();
        }
        capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref dimTexture);
    }

    private void ReleaseSheet()
    {
        sheetTexture?.Dispose();
        sheetTexture = null;
        cachedMessageId = -1;
        cachedPos = null;
        cachedStamp = null;
    }

    public override void Dispose()
    {
        HidePinned();
        dimTexture?.Dispose();
        dimTexture = null;
        base.Dispose();
    }
}
