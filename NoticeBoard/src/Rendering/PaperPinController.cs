using System;
using NoticeBoard;
using NoticeBoard.BlockType;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.src.Gui.Windows;
using NoticeBoard.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Rendering;

public sealed class PaperPinController : IRenderer
{
    private const float PaperFrontZ = 0.2f / 16f + 0.002f;
    public const float InteractDistance = 6f;
    private const float TiltRepeatDelay = 0.3f;
    private const float TiltRepeatInterval = 0.05f;
    private const float GhostZLift = 0.04f;

    public static PaperPinController Instance { get; private set; }

    public double RenderOrder => 0.51;
    public int RenderRange => 48;

    private readonly ICoreClientAPI capi;
    private bool active;
    private bool restoreOnCancel;
    private bool isDocument;
    private string boardId;
    private BlockPos origin;
    private bool isWall;
    private float rotateYDeg;
    private int units;
    private string text;
    private int anonymous;
    private int holder;
    private string paperTheme;
    private string font;
    private bool hasWaypoint;
    private float waypointX;
    private float waypointZ;
    private string waypointTitle;
    private string waypointIcon;
    private string waypointColor;
    private float tackX;
    private float tackY;
    private float ghostTilt;
    private int ghostLayer;
    private float tiltHoldAcc;
    private bool tiltHoldRepeating;
    private bool canPlace;
    private bool showGhost;
    private bool debugHud;
    private LoadedTexture hintTexture;
    private LoadedTexture statusTexture;
    private string statusLine;
    private LoadedTexture debugTexture;
    private string debugLine;
    private LoadedTexture debugTexture2;
    private string debugLine2;
    private float dbgMinX, dbgMaxX, dbgMinY, dbgMaxY, dbgMinZ, dbgMaxZ;
    private bool dbgUsedSel;
    private int ghostTexId;
    private TextureAtlasPosition ghostTexPos;
    private LoadedTexture ghostSheetTexture;
    private MeshData baseMesh;
    private MeshRef meshRef;
    private MeshData holderBaseMesh;
    private MeshRef holderMeshRef;
    private int holderTexId;
    private bool eventsHooked;
    private int repositionId = -1;
    private int parchmentSeed;

    public PaperPinController(ICoreClientAPI api)
    {
        capi = api;
        Instance = this;
        capi.Event.LeaveWorld += OnLeaveWorld;
        capi.Event.MouseDown += OnWorldRightClick;
    }

    public bool IsActive => active;

    public static bool IsWithinInteractDistance(IPlayer player, BlockPos origin)
    {
        if (player?.Entity == null || origin == null)
            return false;
        Vec3d eye = player.Entity.Pos.XYZ.AddCopy(player.Entity.LocalEyePos);
        double dx = eye.X - origin.X;
        double dy = eye.Y - origin.Y;
        double dz = eye.Z - origin.Z;
        return dx * dx + dy * dy + dz * dz <= InteractDistance * InteractDistance;
    }

    public static bool TryTraceBoard(IWorldAccessor world, IPlayer player, out BlockSelection sel, out NoticeBoardBlockEntity be)
    {
        sel = null;
        be = null;
        if (world == null || player?.Entity == null)
            return false;
        Vec3d eye = player.Entity.Pos.XYZ.AddCopy(player.Entity.LocalEyePos);
        Vec3f look = player.Entity.Pos.GetViewVector();
        Vec3d to = new Vec3d(
            eye.X + look.X * InteractDistance,
            eye.Y + look.Y * InteractDistance,
            eye.Z + look.Z * InteractDistance);
        EntitySelection entitySel = null;
        world.RayTraceForSelection(eye, to, ref sel, ref entitySel, null, null);
        if (sel == null)
            return false;
        be = world.Api.GetNoticeBoardEntity(sel.Position);
        return be != null && IsWithinInteractDistance(player, be.Pos);
    }

    private void OnWorldRightClick(MouseEvent e)
    {
        if (e.Button != EnumMouseButton.Right || e.Handled)
            return;
        if (DialogOwnsMouse())
            return;
        if (capi.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Block is NoticeBoardBlock)
            return;
        BlockSelection current = capi.World.Player.CurrentBlockSelection;
        if (current != null && capi.GetNoticeBoardEntity(current.Position) != null)
            return;
        if (!TryTraceBoard(capi.World, capi.World.Player, out BlockSelection sel, out NoticeBoardBlockEntity be))
            return;
        NoticeBoardBlock board = capi.World.BlockAccessor.GetBlock(sel.Position) as NoticeBoardBlock
            ?? be.Block as NoticeBoardBlock;
        if (board == null)
            return;
        e.Handled = true;
        board.OnBlockInteractStart(capi.World, capi.World.Player, sel);
    }

    private bool DialogOwnsMouse()
    {
        foreach (object gui in capi.OpenedGuis)
        {
            if (gui is not GuiDialog dlg || !dlg.IsOpened())
                continue;
            if (dlg.DialogType != EnumDialogType.Dialog)
                continue;
            if (dlg.Focused || dlg.ShouldReceiveMouseEvents())
                return true;
        }
        return false;
    }

    public bool ToggleDebugHud()
    {
        debugHud = !debugHud;
        return debugHud;
    }

    public void BeginCompose(
        string boardId,
        BlockPos origin,
        bool isWall,
        float rotateYDeg,
        int units,
        string text,
        int anonymous,
        int holder,
        string paperTheme,
        string font,
        bool hasWaypoint = false,
        float waypointX = 0,
        float waypointZ = 0,
        string waypointTitle = "",
        string waypointIcon = "",
        string waypointColor = ""
    )
    {
        Begin(
            boardId,
            origin,
            isWall,
            rotateYDeg,
            units,
            isDocument: false,
            text,
            anonymous,
            holder,
            paperTheme,
            font,
            hasWaypoint,
            waypointX,
            waypointZ,
            waypointTitle,
            waypointIcon,
            waypointColor
        );
    }

    public void BeginDocument(
        string boardId,
        BlockPos origin,
        bool isWall,
        float rotateYDeg,
        int units,
        string text
    )
    {
        Begin(
            boardId,
            origin,
            isWall,
            rotateYDeg,
            units,
            isDocument: true,
            text,
            0,
            0,
            "",
            ""
        );
    }

    // TESR skips this id while the ghost is up so the notice is not drawn twice during reposition.
    public static int HiddenMessageId(string boardId)
    {
        var inst = Instance;
        if (inst == null || !inst.active || inst.repositionId < 0 || inst.boardId != boardId)
            return -1;
        return inst.repositionId;
    }

    public void BeginReposition(
        string boardId,
        BlockPos origin,
        bool isWall,
        float rotateYDeg,
        int units,
        string text,
        int anonymous,
        int holder,
        string paperTheme,
        string font,
        int messageId
    )
    {
        Begin(boardId, origin, isWall, rotateYDeg, units, isDocument: false, text, anonymous, holder, paperTheme, font);
        restoreOnCancel = false;
        repositionId = messageId;
        MessageVisualData vis = capi.GetNoticeBoardEntity(origin)?.GetVisualData(messageId);
        parchmentSeed = vis != null ? vis.ResolveParchmentSeed(messageId) : messageId;
        ghostLayer = capi.GetNoticeBoardEntity(origin)?.GetPinLayer(messageId) ?? 0;
        capi.GetNoticeBoardEntity(origin)?.RebuildClientPapers();
    }

    public bool TryConfirm(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!active)
            return false;

        if (TryCameraRayToTack(out float x, out float y)
            && NoticeBoardPaperLayout.IsTackOnCork(x, y, isWall, units, ghostTilt))
            Confirm(x, y);

        return true;
    }

    public static void TryRestoreDraft(NoticeBoardMainWindowGui gui)
    {
        Instance?.RestoreDraft(gui);
    }

    // Opaque is the 3D ghost; Ortho is the how-to + pin-status HUD. Cancel if the BE is gone
    // or the camera is farther than InteractDistance (6).
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (!active)
            return;
        if (stage != EnumRenderStage.Opaque && stage != EnumRenderStage.Ortho)
            return;

        NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(origin);
        if (be == null || string.IsNullOrEmpty(be.uniqueID) || be.uniqueID != boardId)
        {
            Cancel(restore: true);
            return;
        }

        if (!IsWithinInteractDistance(capi.World.Player, origin))
        {
            Cancel(restore: true);
            return;
        }

        if (stage == EnumRenderStage.Opaque)
        {
            TickTiltHold(deltaTime);
            UpdateGhostFromLook();
            if (showGhost)
                DrawGhost();
        }
        if (stage == EnumRenderStage.Ortho)
        {
            DrawHint();
            if (debugHud)
                DrawDebug();
        }
    }

    public void Dispose()
    {
        End(restore: false, reopen: false);
        ghostSheetTexture?.Dispose();
        ghostSheetTexture = null;
        ghostTexId = 0;
        ghostTexPos = null;
    }

    private void Begin(
        string boardId,
        BlockPos origin,
        bool isWall,
        float rotateYDeg,
        int units,
        bool isDocument,
        string text,
        int anonymous,
        int holder,
        string paperTheme,
        string font,
        bool hasWaypoint = false,
        float waypointX = 0,
        float waypointZ = 0,
        string waypointTitle = "",
        string waypointIcon = "",
        string waypointColor = ""
    )
    {
        End(restore: false, reopen: false);

        this.boardId = boardId;
        this.origin = origin.Copy();
        this.isWall = isWall;
        this.rotateYDeg = rotateYDeg;
        this.units = PaperSize.ClampUnits(units);
        this.isDocument = isDocument;
        this.text = text ?? "";
        this.anonymous = anonymous;
        this.holder = holder;
        this.paperTheme = paperTheme ?? "";
        this.font = font ?? "";
        this.hasWaypoint = hasWaypoint;
        this.waypointX = waypointX;
        this.waypointZ = waypointZ;
        this.waypointTitle = waypointTitle ?? "";
        this.waypointIcon = waypointIcon ?? "";
        this.waypointColor = waypointColor ?? "";
        parchmentSeed = MessageVisualData.NewPaperSeed();
        restoreOnCancel = true;
        canPlace = false;
        showGhost = false;
        ghostTilt = 0f;
        ghostLayer = 0;
        tiltHoldAcc = 0;
        tiltHoldRepeating = false;

        HookEvents();
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "noticeboard-pin-ghost");
        capi.Event.RegisterRenderer(this, EnumRenderStage.Ortho, "noticeboard-pin-hint");
        active = true;
    }

    private void Confirm(float pinX, float pinY)
    {
        var channel = capi.Network.GetChannel("noticeboard");
        if (repositionId >= 0)
        {
            channel.SendPacket(
                new PlayerRepositionMessage
                {
                    Id = repositionId,
                    BoardId = boardId,
                    PinX = pinX,
                    PinY = pinY,
                    PinRotZ = ghostTilt,
                    PinLayer = ghostLayer,
                }
            );
            End(restore: false, reopen: false);
            return;
        }

        if (isDocument)
        {
            channel.SendPacket(
                new PlayerSendDocument
                {
                    Document = text,
                    BoardId = boardId,
                    PlayerId = capi.World.Player.PlayerUID,
                    TotalHours = capi.World.Calendar.TotalHours,
                    HasPin = true,
                    PinX = pinX,
                    PinY = pinY,
                    PinRotZ = ghostTilt,
                    PinLayer = ghostLayer,
                    PaperSeed = parchmentSeed,
                }
            );
        }
        else
        {
            channel.SendPacket(
                new PlayerSendMessage
                {
                    Message = text,
                    BoardId = boardId,
                    PlayerId = capi.World.Player.PlayerUID,
                    TotalHours = capi.World.Calendar.TotalHours,
                    IsAnonymous = anonymous,
                    Holder = holder,
                    PaperTheme = paperTheme,
                    HasPin = true,
                    PinX = pinX,
                    PinY = pinY,
                    PinRotZ = ghostTilt,
                    PinLayer = ghostLayer,
                    HasWaypoint = hasWaypoint,
                    WaypointX = waypointX,
                    WaypointZ = waypointZ,
                    WaypointTitle = waypointTitle,
                    WaypointIcon = waypointIcon,
                    WaypointColor = waypointColor,
                    PaperSeed = parchmentSeed,
                }
            );
        }

        End(restore: false, reopen: false);
    }

    private void Cancel(bool restore)
    {
        if (!active)
            return;

        if (repositionId >= 0)
        {
            BlockPos boardOrigin = origin;
            End(restore: false, reopen: false);
            capi.GetNoticeBoardEntity(boardOrigin)?.RebuildClientPapers();
            capi.TriggerIngameError(this, "pin_cancelled", Lang.Get("noticeboard:pin-reposition-cancelled"));
            return;
        }

        End(restore, reopen: restore);
        if (restore)
            capi.TriggerIngameError(this, "pin_cancelled", Lang.Get("noticeboard:pin-placement-cancelled"));
    }

    private void End(bool restore, bool reopen)
    {
        bool wasActive = active;
        active = false;
        showGhost = false;
        canPlace = false;
        tiltHoldAcc = 0;
        tiltHoldRepeating = false;
        repositionId = -1;
        parchmentSeed = 0;
        UnhookEvents();

        if (wasActive)
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Ortho);
        }

        if (meshRef != null)
        {
            meshRef.Dispose();
            meshRef = null;
        }
        baseMesh = null;
        if (holderMeshRef != null)
        {
            capi.Render.DeleteMesh(holderMeshRef);
            holderMeshRef = null;
        }
        holderBaseMesh = null;
        holderTexId = 0;

        ghostSheetTexture?.Dispose();
        ghostSheetTexture = null;
        ghostTexId = 0;
        ghostTexPos = null;

        hintTexture?.Dispose();
        hintTexture = null;
        statusTexture?.Dispose();
        statusTexture = null;
        statusLine = null;
        debugTexture?.Dispose();
        debugTexture = null;
        debugLine = null;
        debugTexture2?.Dispose();
        debugTexture2 = null;
        debugLine2 = null;

        if (reopen && restore && !string.IsNullOrEmpty(boardId))
        {
            restoreOnCancel = true;
            capi.Network.GetChannel("noticeboard")
                .SendPacket(
                    new RequestAllMessages
                    {
                        BoardId = boardId,
                        PlayerId = capi.World.Player.PlayerUID,
                    }
                );
        }
        else
        {
            restoreOnCancel = false;
        }
    }

    private void RestoreDraft(NoticeBoardMainWindowGui gui)
    {
        if (!restoreOnCancel || gui == null || gui.BoardId != boardId)
            return;

        restoreOnCancel = false;
        if (isDocument)
            return;

        gui.OpenTextInput("add", -1, text, anonymous, holder, paperTheme, hasWaypoint, waypointX, waypointZ, waypointTitle, waypointIcon, waypointColor);
    }

    private void OnLeaveWorld() => End(restore: false, reopen: false);

    private void HookEvents()
    {
        if (eventsHooked)
            return;
        capi.Event.MouseDown += OnMouseDown;
        capi.Event.KeyDown += OnKeyDown;
        eventsHooked = true;
    }

    private void UnhookEvents()
    {
        if (!eventsHooked)
            return;
        capi.Event.MouseDown -= OnMouseDown;
        capi.Event.KeyDown -= OnKeyDown;
        eventsHooked = false;
    }

    private void OnMouseDown(MouseEvent args)
    {
        if (!active)
            return;
        if (args.Button != EnumMouseButton.Left)
            return;

        args.Handled = true;
        Cancel(restore: true);
    }

    private void OnKeyDown(KeyEvent args)
    {
        if (!active)
            return;

        if (args.KeyCode == (int)GlKeys.Escape)
        {
            args.Handled = true;
            Cancel(restore: true);
            return;
        }

        if (args.KeyCode == (int)GlKeys.Left || args.KeyCode == (int)GlKeys.Right)
        {
            args.Handled = true;
            float delta = args.KeyCode == (int)GlKeys.Left ? -1f : 1f;
            ghostTilt = GameMath.Clamp(
                ghostTilt + delta,
                -NoticeBoardPaperLayout.SheetTiltMaxDeg,
                NoticeBoardPaperLayout.SheetTiltMaxDeg
            );
            tiltHoldAcc = 0;
            tiltHoldRepeating = false;
            return;
        }

        if (args.KeyCode == (int)GlKeys.Up || args.KeyCode == (int)GlKeys.Down)
        {
            args.Handled = true;
            int delta = args.KeyCode == (int)GlKeys.Up ? 1 : -1;
            ghostLayer = NoticeBoardPaperLayout.ClampPinLayer(ghostLayer + delta);
            return;
        }
    }

    private void TickTiltHold(float dt)
    {
        bool left = capi.Input.KeyboardKeyStateRaw[(int)GlKeys.Left];
        bool right = capi.Input.KeyboardKeyStateRaw[(int)GlKeys.Right];
        if (left == right)
        {
            tiltHoldAcc = 0;
            tiltHoldRepeating = false;
            return;
        }

        float delta = left ? -1f : 1f;
        tiltHoldAcc += dt;
        float wait = tiltHoldRepeating ? TiltRepeatInterval : TiltRepeatDelay;
        while (tiltHoldAcc >= wait)
        {
            tiltHoldAcc -= wait;
            ghostTilt = GameMath.Clamp(
                ghostTilt + delta,
                -NoticeBoardPaperLayout.SheetTiltMaxDeg,
                NoticeBoardPaperLayout.SheetTiltMaxDeg
            );
            tiltHoldRepeating = true;
            wait = TiltRepeatInterval;
        }
    }

    private bool TryCameraRayToTack(out float rayTackX, out float rayTackY)
    {
        rayTackX = 0f;
        rayTackY = 0f;
        var entity = capi.World.Player.Entity;
        Vec3d eye = entity.Pos.XYZ.AddCopy(entity.LocalEyePos);
        Vec3d dir;
        BlockSelection sel = capi.World.Player.CurrentBlockSelection;
        dbgUsedSel = sel != null;
        if (sel != null)
        {
            Vec3d hit = sel.Position.ToVec3d().AddCopy(sel.HitPosition);
            dir = new Vec3d(hit.X - eye.X, hit.Y - eye.Y, hit.Z - eye.Z);
        }
        else
        {
            Vec3f look = entity.Pos.GetViewVector();
            dir = new Vec3d(look.X, look.Y, look.Z);
        }
        return NoticeBoardPaperLayout.TryLookRayToTack(
            eye, dir, origin, rotateYDeg, isWall, out rayTackX, out rayTackY);
    }

    private void UpdateGhostFromLook()
    {
        showGhost = false;
        canPlace = false;
        if (!TryCameraRayToTack(out tackX, out tackY))
            return;
        canPlace = NoticeBoardPaperLayout.IsTackOnCork(tackX, tackY, isWall, units, ghostTilt);
        showGhost = true;
    }

    private void BakeGhostSheet()
    {
        ghostSheetTexture?.Dispose();
        ghostSheetTexture = null;
        ghostTexId = 0;
        ghostTexPos = null;

        var cal = capi.World.Calendar;
        string author = anonymous == 1 ? "" : TheBasicsNick.Resolve(capi, capi.World.Player, capi.World.Player?.PlayerName ?? "");
        string date = GameDateFormatter.FormatImmersiveDate(
            cal.HoursPerDay, cal.DaysPerMonth, cal.TotalHours, includeTime: false);

        NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(origin);
        string boardTheme = be?.BoardProperties?.BoardTheme;
        string boardFont = string.IsNullOrEmpty(font) ? be?.BoardProperties?.BoardFont : font;

        var data = new MessageVisualData(text, author, date, holder, paperTheme);
        int sharpness = PaperSize.ResolveSharpness(be?.BoardProperties?.TextSharpness ?? 0);
        double age01 = 0;
        int wearBuckets = GameDateFormatter.WearSteps;
        if (repositionId >= 0 && be?.BoardProperties?.EnableNoticeAging == 1)
        {
            MessageVisualData vis = be.GetVisualData(repositionId);
            if (vis != null)
            {
                int days = GameDateFormatter.ClampLifeDays(be.BoardProperties.NoticeAgingDays);
                age01 = GameDateFormatter.Age01(vis.TotalHours, cal.TotalHours, cal.HoursPerDay, days);
                wearBuckets = GameDateFormatter.WearBuckets(days);
            }
        }
        ghostSheetTexture = NoticeBoardPaperTextRenderer.RasterizeSheet(
            capi, data, units, boardFont, boardTheme,
            superSample: sharpness,
            boardFontSize: be?.BoardProperties?.BoardFontSize ?? 0,
            age01: age01,
            wearBuckets: wearBuckets,
            messageId: parchmentSeed);

        if (ghostSheetTexture != null && ghostSheetTexture.TextureId != 0)
            ghostTexId = ghostSheetTexture.TextureId;
    }

    // Ghost shows the same rasterized sheet the TESR will pin (text, author, date, holder).
    // Atlas paperlabel is only the fallback when the bake yields no texture.
    private void EnsureGhostTexture()
    {
        if (ghostTexId != 0)
            return;

        if (ghostSheetTexture == null)
            BakeGhostSheet();
        if (ghostTexId != 0)
            return;

        NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(origin);
        TextureAtlasPosition texPos = be?.Block != null
            ? capi.Tesselator.GetTextureSource(be.Block)["paperlabel"]
            : null;

        if (texPos == null)
        {
            capi.BlockTextureAtlas.GetOrInsertTexture(
                new AssetLocation("noticeboard:block/notice/paperlabel"),
                out _,
                out texPos
            );
        }

        if (texPos != null && texPos.atlasTextureId != 0)
        {
            ghostTexPos = texPos;
            ghostTexId = texPos.atlasTextureId;
            return;
        }

        ghostTexPos = null;
        ghostTexId = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
    }

    private void DrawGhost()
    {
        EnsureGhostTexture();
        RebuildGhostMesh();
        RebuildHolderMesh();
        if (meshRef == null)
            return;

        IRenderAPI rpi = capi.Render;
        Vec3d camPos = capi.World.Player.Entity.CameraPos;

        rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
        int lightY = origin.Y + (isWall ? 1 : 2);
        IStandardShaderProgram prog = rpi.PreparedStandardShader(origin.X, lightY, origin.Z);
        prog.ModelMatrix = new Matrixf()
            .Identity()
            .Translate(origin.X - camPos.X, origin.Y - camPos.Y, origin.Z - camPos.Z)
            .Values;
        prog.ViewMatrix = rpi.CameraMatrixOriginf;
        prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
        prog.ExtraGodray = 0;
        prog.SsaoAttn = 0;
        prog.AlphaTest = 0.05f;
        prog.OverlayOpacity = 0;
        prog.NormalShaded = 0;
        prog.DontWarpVertices = 1;
        prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
        prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
        prog.Tex2D = ghostTexId;
        // Mild tint so the sheet text stays readable: green = on cork, red = off cork.
        prog.RgbaTint = canPlace
            ? new Vec4f(0.7f, 1.25f, 0.7f, 1f)
            : new Vec4f(1.3f, 0.5f, 0.45f, 1f);
        rpi.GlDisableCullFace();
        rpi.RenderMesh(meshRef);

        if (holderMeshRef != null)
        {
            rpi.GlToggleBlend(true, EnumBlendMode.Standard);
            prog.Tex2D = holderTexId;
            prog.NormalShaded = 1;
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            rpi.RenderMesh(holderMeshRef);
            rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
        }

        prog.Stop();
        rpi.GlEnableCullFace();
        rpi.GlToggleBlend(true, EnumBlendMode.Standard);
    }

    // Same cork plane as Place(), plus layer * 0.008, plus GhostZLift so the ghost clears the
    // real stack. Do not drop GhostZLift or the ghost z-fights TESR at the same layer.
    private float GhostSheetZ() =>
        NoticeBoardPaperLayout.SheetZ(isWall)
        + NoticeBoardPaperLayout.ClampPinLayer(ghostLayer) * NoticeBoardPaperLayout.PinLayerStep
        + GhostZLift;

    // Same tack translate, tilt, and board yaw as the TESR. Atlas UV remap only when falling
    // back to paperlabel; the Cairo bake already has sheet UVs.
    private void RebuildGhostMesh()
    {
        if (baseMesh == null)
        {
            baseMesh = BuildPaperMesh(PaperSize.BandLayout(units), PaperSize.BandMatrices(units));
            baseMesh.HasAnyWindModeSet = false;
        }

        float z = GhostSheetZ();
        PaperSize.TopCornerNailOffsets(
            out float leftX, out float leftY, out float leftZ,
            out float rightX, out float rightY, out float rightZ);
        float pinX = (leftX + rightX) * 0.5f;
        float pinY = (leftY + rightY) * 0.5f;
        float pinZ = (leftZ + rightZ) * 0.5f;
        MeshData draw = baseMesh.Clone();
        draw.Translate(tackX - pinX, tackY - pinY, z - pinZ);
        if (Math.Abs(ghostTilt) > 0.01f)
            draw.Rotate(new Vec3f(tackX, tackY, z), 0, 0, ghostTilt * GameMath.DEG2RAD);
        if (Math.Abs(rotateYDeg) > 0.01f)
            draw.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rotateYDeg * GameMath.DEG2RAD, 0);

        if (ghostTexPos != null)
        {
            float x1 = ghostTexPos.x1, y1 = ghostTexPos.y1;
            float dx = ghostTexPos.x2 - ghostTexPos.x1;
            float dy = ghostTexPos.y2 - ghostTexPos.y1;
            for (int i = 0; i < draw.Uv.Length; i += 2)
            {
                draw.Uv[i] = x1 + draw.Uv[i] * dx;
                draw.Uv[i + 1] = y1 + draw.Uv[i + 1] * dy;
            }
        }

        byte r = 255, g = 255, b = 255;
        for (int i = 0; i < draw.Rgba.Length; i += 4)
        {
            draw.Rgba[i] = r;
            draw.Rgba[i + 1] = g;
            draw.Rgba[i + 2] = b;
            draw.Rgba[i + 3] = 255;
        }

        dbgMinX = dbgMinY = dbgMinZ = float.MaxValue;
        dbgMaxX = dbgMaxY = dbgMaxZ = float.MinValue;
        for (int i = 0; i < draw.VerticesCount; i++)
        {
            float vx = draw.xyz[i * 3];
            float vy = draw.xyz[i * 3 + 1];
            float vz = draw.xyz[i * 3 + 2];
            if (vx < dbgMinX) dbgMinX = vx;
            if (vx > dbgMaxX) dbgMaxX = vx;
            if (vy < dbgMinY) dbgMinY = vy;
            if (vy > dbgMaxY) dbgMaxY = vy;
            if (vz < dbgMinZ) dbgMinZ = vz;
            if (vz > dbgMaxZ) dbgMaxZ = vz;
        }

        meshRef?.Dispose();
        meshRef = capi.Render.UploadMesh(draw);
    }

    // Holder lean seed is parchmentSeed so the ghost nail matches the baked sheet.
    private void EnsureHolderBase()
    {
        if (holderBaseMesh != null || !MessageHolder.Get(holder).UsesItemMesh)
            return;
        if (MessageHolder.Get(holder).UsesCornerNails)
            return;

        NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(origin);
        string metal = be?.Block?.Variant?["metal"] ?? be?.restoreMetal ?? "copper";
        MessageHolderDef def = MessageHolder.Get(holder);
        string code = MessageHolder.ItemCode(holder, metal, capi.World);
        if (string.IsNullOrEmpty(code) || !ItemMeshCache.TryGet(capi, code, isBlock: false, out ItemMeshInfo info))
            return;

        MeshData mesh = info.Mesh.Clone();
        mesh.Translate(def.OriginX, def.OriginY, def.OriginZ);
        for (int vi = 0; vi < mesh.VerticesCount; vi++)
        {
            mesh.xyz[vi * 3] *= def.Scale;
            mesh.xyz[vi * 3 + 1] *= def.Scale;
            mesh.xyz[vi * 3 + 2] *= def.Scale;
        }
        mesh.Rotate(
            new Vec3f(0, 0, 0),
            def.RotXDeg * GameMath.DEG2RAD,
            def.RotYDeg * GameMath.DEG2RAD,
            def.RotZDeg * GameMath.DEG2RAD
        );
        mesh.HasAnyWindModeSet = false;
        holderBaseMesh = mesh;
        holderTexId = info.AtlasTextureId;
    }

    private void RebuildHolderMesh()
    {
        if (holderMeshRef != null)
        {
            capi.Render.DeleteMesh(holderMeshRef);
            holderMeshRef = null;
        }

        int leanId = parchmentSeed;
        PaperSize.TopCornerNailOffsets(
            out float leftX, out float leftY, out float leftZ,
            out float rightX, out float rightY, out float rightZ);
        float pinX = (leftX + rightX) * 0.5f;
        float pinY = (leftY + rightY) * 0.5f;
        float pinZ = (leftZ + rightZ) * 0.5f;

        if (MessageHolder.Get(holder).UsesCornerNails)
        {
            NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(origin);
            MeshData nail = be?.GetNailUnitMesh(capi.Tesselator);
            if (nail == null)
                return;

            float nailZ = GhostSheetZ();
            MeshData left = nail.Clone();
            MessageHolder.HolderLean(leanId, 1, out float leftLx, out float leftLy, out float leftLz);
            left.Rotate(
                new Vec3f(0, 0, 0),
                leftLx * GameMath.DEG2RAD,
                leftLy * GameMath.DEG2RAD,
                leftLz * GameMath.DEG2RAD
            );
            left.Translate(tackX - pinX + leftX, tackY - pinY + leftY, nailZ - pinZ + leftZ);
            MeshData right = nail.Clone();
            MessageHolder.HolderLean(leanId, 2, out float rightLx, out float rightLy, out float rightLz);
            right.Rotate(
                new Vec3f(0, 0, 0),
                rightLx * GameMath.DEG2RAD,
                rightLy * GameMath.DEG2RAD,
                rightLz * GameMath.DEG2RAD
            );
            right.Translate(tackX - pinX + rightX, tackY - pinY + rightY, nailZ - pinZ + rightZ);
            left.AddMeshData(right);
            left.HasAnyWindModeSet = false;
            if (Math.Abs(ghostTilt) > 0.01f)
                left.Rotate(new Vec3f(tackX, tackY, nailZ), 0, 0, ghostTilt * GameMath.DEG2RAD);
            if (Math.Abs(rotateYDeg) > 0.01f)
                left.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rotateYDeg * GameMath.DEG2RAD, 0);
            holderTexId = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;
            holderMeshRef = capi.Render.UploadMesh(left);
            return;
        }

        EnsureHolderBase();
        if (holderBaseMesh == null)
            return;

        float z = GhostSheetZ();
        MeshData draw = holderBaseMesh.Clone();
        MessageHolder.HolderLean(leanId, 0, out float lx, out float ly, out float lz);
        draw.Rotate(
            new Vec3f(0, 0, 0),
            lx * GameMath.DEG2RAD,
            ly * GameMath.DEG2RAD,
            lz * GameMath.DEG2RAD
        );
        draw.Translate(tackX - pinX, tackY - pinY, z - pinZ);
        if (Math.Abs(ghostTilt) > 0.01f)
            draw.Rotate(new Vec3f(tackX, tackY, z), 0, 0, ghostTilt * GameMath.DEG2RAD);
        if (Math.Abs(rotateYDeg) > 0.01f)
            draw.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, rotateYDeg * GameMath.DEG2RAD, 0);

        holderMeshRef = capi.Render.UploadMesh(draw);
    }

    // No Normals on purpose: UploadMesh binds attributes in order (xyz, Normals, Uv, Rgba, Flags),
    // but standard.vsh expects uv=1, color=2, flags=3. A Normals buffer shifts them all by one,
    // so uv reads the rotated normal: (0,0) on N/S/E, (1,0) on W = transparent atlas corner.
    private MeshData BuildPaperMesh(PaperBand[] bandLayout, Matrixf[] bandMatrices)
    {
        MeshData mesh = new MeshData(12, 18, false, false, true, true);
        mesh.xyz = new float[12 * 3];
        mesh.Uv = new float[12 * 2];
        mesh.Rgba = new byte[12 * 4];
        mesh.Indices = new int[18];

        for (int i = 0; i < bandLayout.Length; i++)
        {
            PaperBand band = bandLayout[i];
            AddQuad(mesh, bandMatrices[i], band.Width, band.Height, band.VStart, band.VEnd, PaperFrontZ);
        }

        if (mesh.VerticesCount != 12 || mesh.IndicesCount != 18)
            throw new InvalidOperationException("ghost paper must be 3 standard quads.");

        mesh.HasAnyWindModeSet = false;
        return mesh;
    }

    private void AddQuad(MeshData mesh, Matrixf matrix, float w, float h, float vStartFrac, float vEndFrac, float z)
    {
        Vec4f v0 = matrix.TransformVector(new Vec4f(0, 0, z, 1));
        Vec4f v1 = matrix.TransformVector(new Vec4f(w, 0, z, 1));
        Vec4f v2 = matrix.TransformVector(new Vec4f(w, h, z, 1));
        Vec4f v3 = matrix.TransformVector(new Vec4f(0, h, z, 1));

        int v = mesh.VerticesCount;

        mesh.xyz[v * 3 + 0] = v0.X; mesh.xyz[v * 3 + 1] = v0.Y; mesh.xyz[v * 3 + 2] = v0.Z;
        mesh.xyz[v * 3 + 3] = v1.X; mesh.xyz[v * 3 + 4] = v1.Y; mesh.xyz[v * 3 + 5] = v1.Z;
        mesh.xyz[v * 3 + 6] = v2.X; mesh.xyz[v * 3 + 7] = v2.Y; mesh.xyz[v * 3 + 8] = v2.Z;
        mesh.xyz[v * 3 + 9] = v3.X; mesh.xyz[v * 3 + 10] = v3.Y; mesh.xyz[v * 3 + 11] = v3.Z;

        if (mesh.Flags != null)
        {
            mesh.Flags[v] = 0;
            mesh.Flags[v + 1] = 0;
            mesh.Flags[v + 2] = 0;
            mesh.Flags[v + 3] = 0;
        }

        mesh.Uv[v * 2 + 0] = 1; mesh.Uv[v * 2 + 1] = vStartFrac;
        mesh.Uv[v * 2 + 2] = 0; mesh.Uv[v * 2 + 3] = vStartFrac;
        mesh.Uv[v * 2 + 4] = 0; mesh.Uv[v * 2 + 5] = vEndFrac;
        mesh.Uv[v * 2 + 6] = 1; mesh.Uv[v * 2 + 7] = vEndFrac;

        mesh.Rgba[v * 4 + 0] = 255; mesh.Rgba[v * 4 + 1] = 255; mesh.Rgba[v * 4 + 2] = 255; mesh.Rgba[v * 4 + 3] = 255;
        mesh.Rgba[v * 4 + 4] = 255; mesh.Rgba[v * 4 + 5] = 255; mesh.Rgba[v * 4 + 6] = 255; mesh.Rgba[v * 4 + 7] = 255;
        mesh.Rgba[v * 4 + 8] = 255; mesh.Rgba[v * 4 + 9] = 255; mesh.Rgba[v * 4 + 10] = 255; mesh.Rgba[v * 4 + 11] = 255;
        mesh.Rgba[v * 4 + 12] = 255; mesh.Rgba[v * 4 + 13] = 255; mesh.Rgba[v * 4 + 14] = 255; mesh.Rgba[v * 4 + 15] = 255;

        int i = mesh.IndicesCount;
        mesh.Indices[i + 0] = v + 0;
        mesh.Indices[i + 1] = v + 1;
        mesh.Indices[i + 2] = v + 2;
        mesh.Indices[i + 3] = v + 0;
        mesh.Indices[i + 4] = v + 2;
        mesh.Indices[i + 5] = v + 3;

        mesh.VerticesCount += 4;
        mesh.IndicesCount += 6;
    }

    // How-to texture is baked once. pin-status rebuilds when tilt or layer changes.
    private void DrawHint()
    {
        if (hintTexture == null)
        {
            hintTexture = capi.Gui.TextTexture.GenTextTexture(
                Lang.Get(repositionId >= 0 ? "noticeboard:pin-reposition-hint" : "noticeboard:pin-placement-hint"),
                CairoFont.WhiteSmallishText()
            );
        }

        float hintY = capi.Render.FrameHeight * 0.78f;
        float x = (capi.Render.FrameWidth - hintTexture.Width) * 0.5f;
        capi.Render.Render2DTexturePremultipliedAlpha(
            hintTexture.TextureId,
            x,
            hintY,
            hintTexture.Width,
            hintTexture.Height
        );

        string line = Lang.Get("noticeboard:pin-status", Math.Round(ghostTilt), ghostLayer);
        if (line != statusLine)
        {
            statusTexture?.Dispose();
            statusTexture = capi.Gui.TextTexture.GenTextTexture(line, CairoFont.WhiteSmallishText());
            statusLine = line;
        }

        if (statusTexture != null)
        {
            float sx = (capi.Render.FrameWidth - statusTexture.Width) * 0.5f;
            float sy = hintY + hintTexture.Height + 4;
            capi.Render.Render2DTexturePremultipliedAlpha(
                statusTexture.TextureId,
                sx,
                sy,
                statusTexture.Width,
                statusTexture.Height
            );
        }
    }

    private void DrawDebug()
    {
        string line = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "ghost tex={0} show={1} place={2} mesh={3} tack={4:0.00},{5:0.00} z={6:0.000} tilt={7:0} layer={8}",
            ghostTexId,
            showGhost ? 1 : 0,
            canPlace ? 1 : 0,
            meshRef != null ? 1 : 0,
            tackX,
            tackY,
            GhostSheetZ(),
            ghostTilt,
            ghostLayer
        );
        if (line != debugLine)
        {
            debugTexture?.Dispose();
            debugTexture = capi.Gui.TextTexture.GenTextTexture(line, CairoFont.WhiteSmallishText());
            debugLine = line;
        }
        if (debugTexture != null)
        {
            float x = (capi.Render.FrameWidth - debugTexture.Width) * 0.5f;
            float y = capi.Render.FrameHeight * 0.78f;
            if (hintTexture != null)
                y += hintTexture.Height + 4;
            if (statusTexture != null)
                y += statusTexture.Height + 4;
            capi.Render.Render2DTexturePremultipliedAlpha(
                debugTexture.TextureId, x, y, debugTexture.Width, debugTexture.Height
            );
        }

        PaperSize.TopCornerNailOffsets(
            out float leftX, out float leftY, out float leftZ,
            out float rightX, out float rightY, out float rightZ);
        float pinX = (leftX + rightX) * 0.5f;
        float pinY = (leftY + rightY) * 0.5f;
        float pinZ = (leftZ + rightZ) * 0.5f;
        float tz = GhostSheetZ() - pinZ;
        string line2 = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "rot={0:0} pin={1:0.00},{2:0.00},{3:0.00} tz={4:0.000} sel={5} xyz={6:0.00}..{7:0.00},{8:0.00}..{9:0.00},{10:0.00}..{11:0.00}",
            rotateYDeg, pinX, pinY, pinZ, tz, dbgUsedSel ? 1 : 0,
            dbgMinX, dbgMaxX, dbgMinY, dbgMaxY, dbgMinZ, dbgMaxZ);
        if (line2 != debugLine2)
        {
            debugTexture2?.Dispose();
            debugTexture2 = capi.Gui.TextTexture.GenTextTexture(line2, CairoFont.WhiteSmallishText());
            debugLine2 = line2;
        }
        if (debugTexture2 != null && debugTexture != null)
        {
            float x2 = (capi.Render.FrameWidth - debugTexture2.Width) * 0.5f;
            float y = capi.Render.FrameHeight * 0.78f;
            if (hintTexture != null)
                y += hintTexture.Height + 4;
            if (statusTexture != null)
                y += statusTexture.Height + 4;
            float y2 = y + debugTexture.Height + 2;
            capi.Render.Render2DTexturePremultipliedAlpha(
                debugTexture2.TextureId, x2, y2, debugTexture2.Width, debugTexture2.Height
            );
        }

        if (ghostSheetTexture != null && ghostSheetTexture.TextureId != 0 && ghostSheetTexture.Height > 0)
        {
            float maxH = 128f;
            float scale = maxH / ghostSheetTexture.Height;
            float dw = ghostSheetTexture.Width * scale;
            float dh = ghostSheetTexture.Height * scale;
            capi.Render.Render2DTexturePremultipliedAlpha(
                ghostSheetTexture.TextureId, 8, 64, dw, dh);
        }
    }
}
