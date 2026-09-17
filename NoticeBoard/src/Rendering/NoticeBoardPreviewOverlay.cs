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

    private const float FadeInSeconds = 0.28f;
    private const float FadeOutSeconds = 0.22f;
    private const float DimAlpha = 0.55f;
    private const float GrowFrom = 0.94f;

    private LoadedTexture sheetTexture;
    private LoadedTexture dimTexture;
    private BlockPos cachedPos;
    private int cachedMessageId = -1;
    private string cachedStamp;
    private int cachedSuperSample;
    private float transition;
    private SheetRequest? current;

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

        bool shown = TryResolveRequest(out SheetRequest request);
        if (shown)
            current = request;

        if (current.HasValue && (shown || transition > 0f))
        {
            float step = Math.Max(0f, deltaTime);
            transition = Math.Clamp(
                transition + (shown ? step / FadeInSeconds : -step / FadeOutSeconds),
                0f,
                1f);

            if (transition > 0f)
            {
                DrawSheet(current.Value, EaseOutCubic(transition));
                return;
            }
        }

        transition = 0f;
        current = null;
        ReleaseSheet();
    }

    private static float EaseOutCubic(float t)
    {
        float inverted = 1f - t;
        return 1f - inverted * inverted * inverted;
    }

    private bool TryResolveRequest(out SheetRequest request)
    {
        request = default;

        if (pinnedData != null)
        {
            request = new SheetRequest(
                pinnedData,
                pinnedFont,
                pinnedTheme,
                pinnedFontSize,
                pinnedMessageId,
                null,
                pinnedAging,
                pinnedLifeDays);
            return true;
        }

        if (!IsHotkeyHeld())
            return false;

        BlockSelection sel = capi.World.Player?.CurrentBlockSelection;
        NoticeBoardBlockEntity be = sel != null ? capi.GetNoticeBoardEntity(sel.Position) : null;
        if (be == null && !PaperPinController.TryTraceBoard(capi.World, capi.World.Player, out sel, out be))
            return false;

        if (be == null || sel == null)
            return false;

        if (!PaperPinController.IsWithinInteractDistance(capi.World.Player, sel.Position))
            return false;

        var entity = capi.World.Player.Entity;
        Vec3d eye = entity.Pos.XYZ.AddCopy(entity.LocalEyePos);
        Vec3d hit = sel.Position.ToVec3d().AddCopy(sel.HitPosition);
        Vec3d dir = new Vec3d(hit.X - eye.X, hit.Y - eye.Y, hit.Z - eye.Z);
        int messageId = be.PickMessageAtLookRay(eye, dir);

        MessageVisualData data = messageId >= 0 ? be.GetVisualData(messageId) : null;
        if (data == null || string.IsNullOrWhiteSpace(data.Text))
            return false;

        request = new SheetRequest(
            data,
            be.BoardProperties?.BoardFont,
            be.BoardProperties?.BoardTheme,
            be.BoardProperties?.BoardFontSize ?? 0,
            messageId,
            sel.Position,
            be.BoardProperties?.EnableNoticeAging == 1,
            be.BoardProperties?.NoticeAgingDays ?? GameDateFormatter.DefaultLifeDays(capi.World.Calendar));
        return true;
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

    private void DrawSheet(SheetRequest r, float eased)
    {
        int units = Math.Clamp(
            PaperSize.MeasureHeightUnitsUnclamped(
                capi, r.Data.Text, r.BoardFont, r.BoardFontSize, hasAuthor: !string.IsNullOrEmpty(r.Data.Author)),
            1, PaperSize.PreviewMaxUnits);
        int tilePx = PaperSize.TilePixels(units);
        float scale = Math.Min(
            capi.Render.FrameHeight / (float)tilePx,
            capi.Render.FrameWidth * 0.45f / PaperSize.TextWidth);
        float baseW = PaperSize.TextWidth * scale;
        float grow = GrowFrom + (1f - GrowFrom) * eased;
        float w = baseW * grow;
        float h = tilePx * scale * grow;
        int superSample = GameMath.Clamp((int)Math.Ceiling(baseW / PaperSize.TextWidth), 1, PaperSize.MaxTextSharpness);

        ResolveWear(capi, r.EnableAging, r.NoticeAgingDays, r.Data, out double age01, out int wearBuckets, out int tickStep);
        string stamp = $"{r.Data.Text}\u0000{r.Data.Author}\u0000{r.Data.Date}\u0000{r.Data.PaperTheme}\u0000{r.BoardTheme}\u0000{r.BoardFont}\u0000{r.BoardFontSize}\u0000{r.EnableAging}\u0000{r.NoticeAgingDays}\u0000{tickStep}";
        bool posChanged = r.WorldPos != null && (cachedPos == null || !cachedPos.Equals(r.WorldPos));
        if (sheetTexture == null
            || cachedMessageId != r.MessageId
            || posChanged
            || cachedStamp != stamp
            || cachedSuperSample != superSample)
        {
            ReleaseSheet();
            sheetTexture = NoticeBoardPaperTextRenderer.RasterizeSheet(
                capi, r.Data, units, r.BoardFont, r.BoardTheme,
                superSample, PaperSize.PreviewMaxUnits, r.BoardFontSize, age01, wearBuckets, r.Data.ResolveParchmentSeed(r.MessageId));
            cachedPos = r.WorldPos?.Copy();
            cachedMessageId = r.MessageId;
            cachedStamp = stamp;
            cachedSuperSample = superSample;
        }

        EnsureDimTexture();
        float dim = DimAlpha * eased;
        capi.Render.Render2DTexturePremultipliedAlpha(
            dimTexture.TextureId, 0, 0, capi.Render.FrameWidth, capi.Render.FrameHeight, 50f,
            new Vec4f(dim, dim, dim, dim));
        capi.Render.Render2DTexturePremultipliedAlpha(
            sheetTexture.TextureId,
            (capi.Render.FrameWidth - w) * 0.5f,
            (capi.Render.FrameHeight - h) * 0.5f,
            w, h, 50f,
            new Vec4f(eased, eased, eased, eased));
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
            ctx.SetSourceRGBA(0, 0, 0, 1);
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
        transition = 0f;
        current = null;
        ReleaseSheet();
        dimTexture?.Dispose();
        dimTexture = null;
        base.Dispose();
    }

    private readonly struct SheetRequest
    {
        public SheetRequest(
            MessageVisualData data,
            string boardFont,
            string boardTheme,
            float boardFontSize,
            int messageId,
            BlockPos worldPos,
            bool enableAging,
            int noticeAgingDays)
        {
            Data = data;
            BoardFont = boardFont;
            BoardTheme = boardTheme;
            BoardFontSize = boardFontSize;
            MessageId = messageId;
            WorldPos = worldPos;
            EnableAging = enableAging;
            NoticeAgingDays = noticeAgingDays;
        }

        public MessageVisualData Data { get; }
        public string BoardFont { get; }
        public string BoardTheme { get; }
        public float BoardFontSize { get; }
        public int MessageId { get; }
        public BlockPos WorldPos { get; }
        public bool EnableAging { get; }
        public int NoticeAgingDays { get; }
    }
}
