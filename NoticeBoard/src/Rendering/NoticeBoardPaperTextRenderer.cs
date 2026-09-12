using System;
using System.Collections.Generic;
using Cairo;
using NoticeBoard;
using NoticeBoard.BlockType;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Rendering;

public class NoticeBoardPaperTextRenderer : IRenderer, IDisposable
{
    private const int TextWidth = PaperSize.TextWidth;
    private const int MaxCharsPerMessage = PaperSize.MaxCharsPerMessage;
    private const int SidePadding = PaperSize.SidePadding;
    private const int TopPadding = PaperSize.TopPadding;
    private const int BottomMargin = PaperSize.BottomMargin;
    private const int FooterGap = PaperSize.FooterGap;

    // Device pixels the packed ink texture may occupy at the default sharpness, and the largest
    // dimension it may reach. 8192 is safely under the limit every desktop GPU enforces.
    private const int SupersampleBudgetPx = 16_000_000;
    private const int MaxTextureDim = 8192;

    private static readonly double[] InkColor = PaperSize.InkColor;

    // How much of the lateral travel the out-of-plane term is worth, and the band the corner
    // bias scales it into. The band never reaches zero, so the term stays non-negative and a
    // sheet can only ever move away from the cork - never into it, at any strength.
    private const float LiftRatio = 0.5f;
    private const float MinCornerLift = 0.6f;
    private const float CornerLiftSpread = 0.4f;

    // Past this the motion is well under a pixel, so the buffer updates stop.
    private const float SwayCullDistance = 32f;

    // Permit badges share none of the message atlas, so they get their own small strip texture:
    // one tile per variant bucket, picked per permit the same way message papers pick a
    // speckle-pattern bucket.
    private const int PermitVariantCount = 4;
    private const int PermitTileWidth = 48;
    private const int PermitTileHeight = 176;

    // The wax seal shares the tag atlas as one extra square tile appended to the right of the
    // variant strip, rather than a texture of its own: one fixed design needs no per-message
    // bucket, and reusing the atlas keeps both quads on the single permit draw call.
    private const int SealTileSize = 64;
    private const int PermitAtlasWidth = PermitVariantCount * PermitTileWidth + SealTileSize;
    private const int PermitAtlasHeight = PermitTileHeight;
    private const float SealUStart = (float)(PermitVariantCount * PermitTileWidth) / PermitAtlasWidth;
    private const float SealUEnd = 1f;
    private const float SealVStart = 0f;
    private const float SealVEnd = (float)SealTileSize / PermitAtlasHeight;

    private readonly ICoreClientAPI api;
    private readonly BlockPos pos;

    private LoadedTexture loadedTexture;
    private SwayBuffer paperBuffer;
    private LoadedTexture permitTexture;
    private SwayBuffer permitBuffer;
    private readonly Dictionary<int, MeshRef> holderBuffersByTexture = new();
    private SwaySheet[] swaySheets = Array.Empty<SwaySheet>();
    private float[] sheetLateral = Array.Empty<float>();
    private float[] sheetLift = Array.Empty<float>();

    private float swayTime;
    private float cachedWindSpeed;
    private float cachedWindX;
    private float cachedWindZ;
    private float cachedWindNx;
    private float cachedWindNz;
    private long lastWindPollMs = -1;
    private bool swayApplied;

    private int[] lastMessageIds = Array.Empty<int>();
    private Dictionary<int, MessageVisualData> lastMessageTexts = new();
    private Dictionary<int, int> lastHeightUnits = new();
    private bool lastIsWall;
    private int lastMaxPapers;
    private float lastRotateYDeg;
    private string lastBoardFont = GuiStyle.StandardFontName;
    private float lastBoardFontSize = PaperSize.DefaultBoardFontSize;
    private string lastBoardTheme;
    private string lastBoardMetal = "copper";
    private int lastTextSharpness = PaperSize.DefaultTextSharpness;
    private bool lastFreezePins;

    // Which slice of the merged paper mesh belongs to each message, so hovering a row in the
    // GUI can dim every other sheet without rebuilding or re-rasterising anything.
    private readonly Dictionary<int, (int Start, int Count)> sheetVertexRanges = new();
    private int highlightedMessageId = -1;

    // Doesn't change a pixel of the Cairo atlas, so it is assigned unconditionally and
    // deliberately left out of the comparison below: dragging the strength must not
    // re-rasterise every sheet on the board.
    private int swayStrength = PaperSize.DefaultSwayStrength;

    private bool isDirty = true;
    private bool swayUpdatedThisFrame;
    private int lastAgingStep = -1;
    private readonly List<FallingSheet> fallingSheets = new();
    private readonly Dictionary<int, PaperPlacement> lastTacks = new();

    private static readonly float[] ShadowIdentity = Mat4f.Create();
    private readonly float[] shadowModelMat = Mat4f.Create();
    private readonly float[] shadowModelView = Mat4f.Create();

    public double RenderOrder => 0.5;
    public int RenderRange => 48;

    public NoticeBoardPaperTextRenderer(ICoreClientAPI api, BlockPos pos)
    {
        this.api = api;
        this.pos = pos;
        api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "noticeboard-paper-text");
        api.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar, "noticeboard-paper-text-sf");
        api.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear, "noticeboard-paper-text-sn");
    }

    public void Update(
        int[] messageIds,
        Dictionary<int, MessageVisualData> messageTexts,
        Dictionary<int, int> heightUnits,
        bool isWall,
        int maxPapers,
        float rotateYDeg,
        string boardFont,
        float boardFontSize,
        string boardTheme,
        int textSharpness,
        int swayStrength,
        string boardMetal,
        bool freezePins
    )
    {
        messageIds ??= Array.Empty<int>();
        messageTexts ??= new Dictionary<int, MessageVisualData>();
        heightUnits ??= new Dictionary<int, int>();
        string resolvedFont = PaperSize.ResolveFont(boardFont);
        float resolvedFontSize = PaperSize.ResolveBoardFontSize(boardFontSize);
        int resolvedSharpness = PaperSize.ClampSharpness(textSharpness);
        string resolvedMetal = string.IsNullOrEmpty(boardMetal) ? "copper" : boardMetal;

        // Dropping the strength to zero leaves the sheets wherever the last frame posed them;
        // the next render frame sees a zero amplitude and puts them back on their nails, once.
        this.swayStrength = PaperSize.ClampSwayStrength(swayStrength);

        bool papersChanged = !(
            IdsEqual(lastMessageIds, messageIds)
            && DictionariesEqual(lastMessageTexts, messageTexts)
            && HeightsEqual(lastHeightUnits, heightUnits)
            && lastIsWall == isWall
            && lastMaxPapers == maxPapers
            && Math.Abs(lastRotateYDeg - rotateYDeg) < 0.01f
            && lastBoardFont == resolvedFont
            && Math.Abs(lastBoardFontSize - resolvedFontSize) < 0.001f
            && lastBoardTheme == boardTheme
            && lastBoardMetal == resolvedMetal
            && lastTextSharpness == resolvedSharpness
            && lastFreezePins == freezePins
        );

        if (papersChanged)
        {
            lastMessageIds = (int[])messageIds.Clone();
            lastMessageTexts = new Dictionary<int, MessageVisualData>(messageTexts);
            lastHeightUnits = new Dictionary<int, int>(heightUnits);
            lastIsWall = isWall;
            lastMaxPapers = maxPapers;
            lastRotateYDeg = rotateYDeg;
            lastBoardFont = resolvedFont;
            lastBoardFontSize = resolvedFontSize;
            lastBoardTheme = boardTheme;
            lastBoardMetal = resolvedMetal;
            lastTextSharpness = resolvedSharpness;
            lastFreezePins = freezePins;
            isDirty = true;
        }

        PaperPlacement[] tacks = NoticeBoardPaperLayout.Place(
            lastMessageIds,
            lastIsWall,
            lastMaxPapers,
            lastHeightUnits,
            lastFreezePins ? NoticeBoardPaperLayout.PinsFrom(lastMessageTexts) : null
        );
        for (int i = 0; i < tacks.Length; i++)
            lastTacks[tacks[i].MessageId] = tacks[i];
    }

    // 45% of white. The standard shader multiplies the atlas by the vertex colour, so this can
    // only darken; dimming every other sheet is what makes the hovered one read as picked out.
    private const byte DimmedLevel = 115;

    public void SetHighlightedMessage(int messageId)
    {
        if (highlightedMessageId == messageId)
            return;

        highlightedMessageId = messageId;
        ApplyHighlight();
    }

    public void AddExpiredFalls(List<ExpiredNoticeFall> notices)
    {
        if (notices == null || notices.Count == 0)
            return;

        int wearBuckets = GameDateFormatter.WearBuckets(
            GameDateFormatter.ClampLifeDays(
                api.GetNoticeBoardEntity(pos)?.BoardProperties?.NoticeAgingDays ?? GameDateFormatter.DefaultLifeDays(api.World.Calendar)));
        var calendar = api.World.Calendar;

        foreach (ExpiredNoticeFall notice in notices)
        {
            if (notice == null)
                continue;

            bool already = false;
            for (int i = 0; i < fallingSheets.Count; i++)
            {
                if (fallingSheets[i].Id != notice.Id)
                    continue;
                already = true;
                break;
            }
            if (already)
                continue;

            var data = new MessageVisualData
            {
                Text = notice.Text ?? "",
                Author = notice.Author ?? "",
                Date = GameDateFormatter.FormatImmersiveDate(
                    calendar.HoursPerDay,
                    calendar.DaysPerMonth,
                    notice.TotalHours,
                    includeTime: false),
                Holder = notice.Holder,
                PaperTheme = notice.PaperTheme ?? "",
                PaperSeed = notice.PaperSeed,
                TotalHours = notice.TotalHours,
            };

            int units = PaperSize.MeasureHeightUnits(
                api,
                data.Text,
                lastBoardFont,
                lastBoardFontSize,
                hasAuthor: !string.IsNullOrEmpty(data.Author));
            units = GameMath.Clamp(units, 1, PaperSize.FullHeightUnits);

            LoadedTexture texture = RasterizeSheet(
                api,
                data,
                units,
                lastBoardFont,
                lastBoardTheme,
                lastTextSharpness,
                PaperSize.FullHeightUnits,
                lastBoardFontSize,
                age01: 1,
                wearBuckets,
                notice.Id);

            MeshData mesh = BuildPaperMesh(PaperSize.BandLayout(units), PaperSize.BandMatrices(units));
            mesh.HasAnyWindModeSet = false;
            mesh.Normals = null;
            mesh.NormalsCount = 0;
#if DEBUG
            if (mesh.Normals != null)
                throw new InvalidOperationException("fall mesh must not set Normals (UploadMesh UV shift).");
#endif

            float tackX = notice.PinX;
            float tackY = notice.PinY;
            float tackZ = NoticeBoardPaperLayout.SheetZ(lastIsWall)
                + NoticeBoardPaperLayout.ClampPinLayer(notice.PinLayer) * NoticeBoardPaperLayout.PinLayerStep;
            float rotZ = notice.PinRotZ;
            if (!notice.HasPin && lastTacks.TryGetValue(notice.Id, out PaperPlacement prev))
            {
                tackX = prev.X;
                tackY = prev.Y;
                tackZ = prev.Z;
                rotZ = prev.RotZDeg;
            }

            mesh.Translate(tackX, tackY, tackZ);
            if (Math.Abs(rotZ) > 0.01f)
                mesh.Rotate(new Vec3f(tackX, tackY, tackZ), 0, 0, rotZ * GameMath.DEG2RAD);

            Vec3d start = PaperFall.LocalToWorld(pos, tackX, tackY, tackZ, lastRotateYDeg);
            double floorY = PaperFall.FindFloorY(api.World, start, pos.dimension);
            float landDropY = (float)(floorY - (pos.Y + tackY));
            if (landDropY > 0f)
                landDropY = 0f;
            long landAtMs = PaperFall.DurationMs(pos.Y + tackY, floorY);

            fallingSheets.Add(
                new FallingSheet
                {
                    Id = notice.Id,
                    Texture = texture,
                    Mesh = api.Render.UploadMesh(mesh),
                    StartMs = api.World.ElapsedMilliseconds,
                    TackX = tackX,
                    TackY = tackY,
                    TackZ = tackZ,
                    LandAtMs = landAtMs,
                    LandDropY = landDropY,
                });
        }
    }

    private void AdvanceFallingSheets()
    {
        long now = api.World.ElapsedMilliseconds;
        for (int i = fallingSheets.Count - 1; i >= 0; i--)
        {
            if (now - fallingSheets[i].StartMs < fallingSheets[i].LandAtMs)
                continue;
            fallingSheets[i].Texture?.Dispose();
            fallingSheets[i].Mesh?.Dispose();
            fallingSheets.RemoveAt(i);
        }
    }

    private void RenderFallingSheets(IRenderAPI rpi, IStandardShaderProgram prog, Vec3d camPos)
    {
        long now = api.World.ElapsedMilliseconds;
        float yawRad = lastRotateYDeg * GameMath.DEG2RAD;
        prog.NormalShaded = 0;
        prog.DontWarpVertices = 1;
        for (int i = 0; i < fallingSheets.Count; i++)
        {
            FallingSheet fall = fallingSheets[i];
            if (fall.Texture == null || fall.Mesh == null)
                continue;

            long elapsed = now - fall.StartMs;
            PaperFall.DropAt(elapsed, fall.LandDropY, out float dropY, out float dropOut);
            float u = GameMath.Clamp(elapsed / (float)GameDateFormatter.FallDurationMs, 0, 1);
            float peelRad = 55f * u * GameMath.DEG2RAD;

            prog.ModelMatrix = new Matrixf()
                .Identity()
                .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                .Translate(0.5f, 0.5f, 0.5f)
                .RotateY(yawRad)
                .Translate(-0.5f, -0.5f, -0.5f)
                .Translate(0, dropY, dropOut)
                .Translate(fall.TackX, fall.TackY, fall.TackZ)
                .RotateX(-peelRad)
                .Translate(-fall.TackX, -fall.TackY, -fall.TackZ)
                .Values;
            prog.Tex2D = fall.Texture.TextureId;
            rpi.RenderMesh(fall.Mesh);
        }

        prog.ModelMatrix = new Matrixf()
            .Identity()
            .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
            .Values;
    }

    private void DisposeFallingSheets()
    {
        for (int i = 0; i < fallingSheets.Count; i++)
        {
            fallingSheets[i].Texture?.Dispose();
            fallingSheets[i].Mesh?.Dispose();
        }
        fallingSheets.Clear();
    }

    private void ApplyHighlight()
    {
        MeshData mesh = paperBuffer?.Mesh;
        if (mesh?.Rgba == null || paperBuffer.Ref == null)
            return;

        bool dim = sheetVertexRanges.TryGetValue(highlightedMessageId, out (int Start, int Count) range)
            && range.Start >= 0
            && range.Start + range.Count <= mesh.VerticesCount;

        byte[] rgba = mesh.Rgba;
        byte level = dim ? DimmedLevel : (byte)255;
        for (int i = 0; i < mesh.VerticesCount; i++)
        {
            rgba[i * 4] = level;
            rgba[i * 4 + 1] = level;
            rgba[i * 4 + 2] = level;
            // Alpha stays 255: the atlas is premultiplied, so scaling it would make the sheet
            // translucent instead of dark.
            rgba[i * 4 + 3] = 255;
        }

        if (dim)
        {
            for (int i = range.Start; i < range.Start + range.Count; i++)
            {
                rgba[i * 4] = 255;
                rgba[i * 4 + 1] = 255;
                rgba[i * 4 + 2] = 255;
            }
        }

        api.Render.UpdateMesh(paperBuffer.Ref, paperBuffer.RgbaUpdate);
    }

    // swayUpdatedThisFrame: Opaque, ShadowNear, and ShadowFar share one UpdateSway. lightY is
    // the upper board cell. PremultipliedAlpha is required for the Cairo atlas.
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage == EnumRenderStage.Opaque)
            AdvanceFallingSheets();

        if (!NoticeBoardRenderCull.ShouldDraw(api, pos))
        {
            if (stage == EnumRenderStage.Opaque)
                swayUpdatedThisFrame = false;
            return;
        }

        NoticeBoardBlockEntity agingBe = api.GetNoticeBoardEntity(pos);
        if (agingBe?.BoardProperties?.EnableNoticeAging == 1)
        {
            var cal = api.World.Calendar;
            int days = GameDateFormatter.ClampLifeDays(agingBe.BoardProperties.NoticeAgingDays);
            int step = (int)(cal.TotalHours / GameDateFormatter.WearTickHours(cal.HoursPerDay, days));
            if (step != lastAgingStep)
            {
                lastAgingStep = step;
                isDirty = true;
            }
        }

        if (isDirty)
            Rebuild();

        if (paperBuffer == null && permitBuffer == null && holderBuffersByTexture.Count == 0 && fallingSheets.Count == 0)
            return;

        Vec3d camPos = api.World.Player.Entity.CameraPos;
        if (!swayUpdatedThisFrame)
        {
            UpdateSway(deltaTime, camPos);
            swayUpdatedThisFrame = true;
        }

        if (stage == EnumRenderStage.ShadowNear || stage == EnumRenderStage.ShadowFar)
        {
            RenderIntoShadowMap();
            return;
        }

        swayUpdatedThisFrame = false;

        IRenderAPI rpi = api.Render;

        rpi.GlDisableCullFace();
        rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);

        int lightY = pos.Y + (lastIsWall ? 1 : 2);
        IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, lightY, pos.Z);

        prog.ModelMatrix = new Matrixf()
            .Identity()
            .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
            .Values;

        prog.ViewMatrix = rpi.CameraMatrixOriginf;
        prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
        prog.ExtraGodray = 0;
        prog.SsaoAttn = 0;
        prog.AlphaTest = 0.05f;
        prog.OverlayOpacity = 0;

        if (loadedTexture != null && paperBuffer != null)
        {
            prog.Tex2D = loadedTexture.TextureId;
            prog.NormalShaded = 0;
            rpi.RenderMesh(paperBuffer.Ref);
        }

        if (holderBuffersByTexture.Count > 0)
        {
            rpi.GlToggleBlend(true, EnumBlendMode.Standard);
            prog.NormalShaded = 1;
            foreach (var kvp in holderBuffersByTexture)
            {
                prog.Tex2D = kvp.Key;
                rpi.RenderMesh(kvp.Value);
            }
            rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);
        }

        if (permitTexture != null && permitBuffer != null)
        {
            prog.Tex2D = permitTexture.TextureId;
            prog.NormalShaded = 0;
            rpi.RenderMesh(permitBuffer.Ref);
        }

        if (fallingSheets.Count > 0)
            RenderFallingSheets(rpi, prog, camPos);

        prog.Stop();

        rpi.GlToggleBlend(true, EnumBlendMode.Standard);
    }

    // Shadowmapentityanimated with identity animation UBO: custom meshes cast shadows without
    // an entity animator.
    private void RenderIntoShadowMap()
    {
        if (paperBuffer == null && permitBuffer == null && holderBuffersByTexture.Count == 0)
            return;

        IRenderAPI rpi = api.Render;
        IShaderProgram prog = rpi.GetEngineShader(EnumShaderProgram.Shadowmapentityanimated);
        if (prog == null || prog.Disposed || prog.LoadError)
            return;

        IShaderProgram prev = rpi.CurrentActiveShader;
        prev?.Stop();
        prog.Use();
        rpi.GLDepthMask(true);
        rpi.GlToggleBlend(false);
        rpi.GlDisableCullFace();

        Vec3d camPos = api.World.Player.Entity.CameraPos;
        Mat4f.Identity(shadowModelMat);
        Mat4f.Translate(
            shadowModelMat,
            shadowModelMat,
            (float)(pos.X - camPos.X),
            (float)(pos.Y - camPos.Y),
            (float)(pos.Z - camPos.Z)
        );
        Mat4f.Mul(shadowModelView, rpi.CurrentModelviewMatrix, shadowModelMat);
        prog.UniformMatrix("modelViewMatrix", shadowModelView);
        prog.UniformMatrix("projectionMatrix", rpi.CurrentProjectionMatrix);
        prog.Uniform("addRenderFlags", 0);
        prog.UBOs["Animation"].Update(ShadowIdentity, 0, 16 * 4);

        if (loadedTexture != null && paperBuffer != null)
        {
            prog.BindTexture2D("entityTex", loadedTexture.TextureId, 0);
            rpi.RenderMesh(paperBuffer.Ref);
        }
        foreach (var kvp in holderBuffersByTexture)
        {
            prog.BindTexture2D("entityTex", kvp.Key, 0);
            rpi.RenderMesh(kvp.Value);
        }
        if (permitTexture != null && permitBuffer != null)
        {
            prog.BindTexture2D("entityTex", permitTexture.TextureId, 0);
            rpi.RenderMesh(permitBuffer.Ref);
        }

        prog.Stop();
        prev?.Use();
    }

    public void Dispose()
    {
        Cleanup();
        DisposeFallingSheets();
        api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        api.Event.UnregisterRenderer(this, EnumRenderStage.ShadowFar);
        api.Event.UnregisterRenderer(this, EnumRenderStage.ShadowNear);
    }

    private bool SwayEnabled => swayStrength > 0;

    private readonly struct SwaySheet
    {
        public SwaySheet(Vec3f across, Vec3f outward, float phase)
        {
            Across = across;
            Outward = outward;
            Phase = phase;
        }

        public Vec3f Across { get; }
        public Vec3f Outward { get; }
        public float Phase { get; }
    }

    private sealed class FallingSheet
    {
        public int Id;
        public LoadedTexture Texture;
        public MeshRef Mesh;
        public long StartMs;
        public float TackX;
        public float TackY;
        public float TackZ;
        public long LandAtMs;
        public float LandDropY;
    }

    private sealed class SwayBuffer
    {
        public MeshData Mesh;
        public MeshRef Ref;
        public float[] BaseXyz;
        public float[] Weight;
        public float[] Bias;
        public int[] SheetIndex;

        // Handed to UpdateMesh: only xyz is non-null, and it aliases Mesh.xyz, so a frame
        // rewrites the position buffer alone and leaves uv, colour and indices untouched.
        public MeshData XyzUpdate;

        // Same trick for the hover spotlight: only Rgba is non-null, so a hover change
        // rewrites the colour buffer alone.
        public MeshData RgbaUpdate;
    }

    private static SwayBuffer CreateSwayBuffer(IRenderAPI rpi, MeshData mesh, List<float> weight, List<float> bias, List<int> sheetIndex)
    {
        mesh.XyzStatic = false;
        mesh.RgbaStatic = false;

        var update = new MeshData(1, 1, false, false, false, false);
        update.Indices = null;
        update.xyz = mesh.xyz;
        update.VerticesCount = mesh.VerticesCount;

        var rgbaUpdate = new MeshData(1, 1, false, false, false, false);
        rgbaUpdate.Indices = null;
        rgbaUpdate.xyz = null;
        rgbaUpdate.Rgba = mesh.Rgba;
        rgbaUpdate.VerticesCount = mesh.VerticesCount;

        return new SwayBuffer
        {
            Mesh = mesh,
            Ref = rpi.UploadMesh(mesh),
            BaseXyz = (float[])mesh.xyz.Clone(),
            Weight = weight.ToArray(),
            Bias = bias.ToArray(),
            SheetIndex = sheetIndex.ToArray(),
            XyzUpdate = update,
            RgbaUpdate = rgbaUpdate,
        };
    }

    private float SwayAmplitude()
    {
        if (!SwayEnabled || !api.Settings.Bool["wavingStuff"])
            return 0f;

        long now = api.World.ElapsedMilliseconds;
        if (lastWindPollMs < 0 || now - lastWindPollMs > 250)
        {
            lastWindPollMs = now;
            Vec3d wind = api.World.BlockAccessor.GetWindSpeedAt(pos);
            if (wind == null)
            {
                cachedWindSpeed = 0f;
                cachedWindX = 0f;
                cachedWindZ = 0f;
                cachedWindNx = 0f;
                cachedWindNz = 0f;
            }
            else
            {
                float len = (float)wind.Length();
                cachedWindSpeed = GameMath.Clamp(len, 0f, 1f);
                cachedWindX = (float)wind.X;
                cachedWindZ = (float)wind.Z;
                float hLen = GameMath.Sqrt(cachedWindX * cachedWindX + cachedWindZ * cachedWindZ);
                cachedWindNx = hLen > 0.01f ? cachedWindX / hLen : 0f;
                cachedWindNz = hLen > 0.01f ? cachedWindZ / hLen : 0f;
            }
        }

        return PaperSize.MaxSwayAmplitude * (swayStrength / 100f) * cachedWindSpeed;
    }

    private void UpdateSway(float deltaTime, Vec3d camPos)
    {
        if (paperBuffer == null && permitBuffer == null)
            return;

        double dx = pos.X + 0.5 - camPos.X;
        double dy = pos.Y + 0.5 - camPos.Y;
        double dz = pos.Z + 0.5 - camPos.Z;
        if (dx * dx + dy * dy + dz * dz > SwayCullDistance * SwayCullDistance)
            return;

        float amp = SwayAmplitude();
        if (amp <= 0f)
        {
            if (swayApplied)
                RestoreBasePositions();
            return;
        }

        // /time stop zeros SpeedOfTime; ESC pause is IsGamePaused. Keep the last pose.
        if (api.IsGamePaused || api.World.Calendar.SpeedOfTime <= 0f)
            return;

        // Wrapped rather than taken from the world clock, so the sine arguments keep full float
        // precision however long the session runs. The clock itself runs faster in stronger
        // wind (SwayAmplitude above just refreshed cachedWindSpeed), so a gust reads as paper
        // straining against its pin rather than just a wider swing at the same lazy tempo.
        swayTime += deltaTime * (1f + PaperSize.MaxWindFrequencyBoost * cachedWindSpeed);
        if (swayTime > 10000f)
            swayTime -= 10000f;

        // Per sheet once per frame: doing this per vertex would be some fifteen hundred sine
        // calls a frame for a full board.
        for (int s = 0; s < swaySheets.Length; s++)
        {
            float phase = swaySheets[s].Phase;

            // Scaled off amp (already strength * one power of wind speed) rather than a bare
            // constant, so turning sway strength down also turns the flutter down, and an
            // extra power of wind speed on top keeps it quadratic overall - negligible in a
            // light breeze, only kicking in once it's actually gusty. Its own frequency/phase
            // multipliers keep it from ever falling into step with the main swing below.
            float flutter =
                amp
                * cachedWindSpeed
                * PaperSize.FlutterAmplitudeRatio
                * GameMath.Sin(swayTime * 11.3f + 4.1f * phase);

            SwaySheet sheet = swaySheets[s];
            float dirAcross = cachedWindNx * sheet.Across.X + cachedWindNz * sheet.Across.Z;
            float dirOut = cachedWindNx * sheet.Outward.X + cachedWindNz * sheet.Outward.Z;
            float osc =
                0.7f * GameMath.Sin(swayTime * 1.7f + phase)
                + 0.3f * GameMath.Sin(swayTime * 3.9f + 2.3f * phase);
            sheetLateral[s] =
                amp * (PaperSize.WindDirBias * dirAcross + PaperSize.WindOscShare * osc) + flutter;
            sheetLift[s] =
                amp
                * LiftRatio
                * GameMath.Max(0f, 0.5f + 0.5f * dirOut)
                * (0.5f + 0.5f * GameMath.Sin(swayTime * 2.3f + 1.7f * phase));
        }

        if (paperBuffer != null)
            PoseBuffer(paperBuffer);
        if (permitBuffer != null)
            PoseBuffer(permitBuffer);

        swayApplied = true;
    }

    private void PoseBuffer(SwayBuffer buffer)
    {
        float[] xyz = buffer.Mesh.xyz;
        float[] baseXyz = buffer.BaseXyz;
        int count = buffer.SheetIndex.Length;

        for (int i = 0; i < count; i++)
        {
            int s = buffer.SheetIndex[i];
            SwaySheet sheet = swaySheets[s];
            float weight = buffer.Weight[i];

            float across = sheetLateral[s] * weight;
            float outward =
                sheetLift[s] * weight * (MinCornerLift + CornerLiftSpread * buffer.Bias[i]);

            int v = i * 3;
            xyz[v + 0] = baseXyz[v + 0] + across * sheet.Across.X + outward * sheet.Outward.X;
            xyz[v + 1] = baseXyz[v + 1] + across * sheet.Across.Y + outward * sheet.Outward.Y;
            xyz[v + 2] = baseXyz[v + 2] + across * sheet.Across.Z + outward * sheet.Outward.Z;
        }

        api.Render.UpdateMesh(buffer.Ref, buffer.XyzUpdate);
    }

    private void RestoreBasePositions()
    {
        swayApplied = false;

        if (paperBuffer != null)
            RestoreBuffer(paperBuffer);
        if (permitBuffer != null)
            RestoreBuffer(permitBuffer);
    }

    private void RestoreBuffer(SwayBuffer buffer)
    {
        Array.Copy(buffer.BaseXyz, buffer.Mesh.xyz, buffer.BaseXyz.Length);
        api.Render.UpdateMesh(buffer.Ref, buffer.XyzUpdate);
    }

    // One message that is going to be drawn, with its sheet height already resolved. Collected
    // once per rebuild so that the pass which sizes the atlas and the pass which packs it cannot
    // disagree about which sheets exist or what order they come in.
    private readonly struct PaperTile
    {
        public PaperTile(int messageId, MessageVisualData data, int units)
        {
            MessageId = messageId;
            Data = data;
            Units = units;
            TilePx = PaperSize.TilePixels(units);
        }

        public int MessageId { get; }
        public MessageVisualData Data { get; }
        public int Units { get; }
        public int TilePx { get; }
    }

    private readonly struct PaperRow
    {
        public PaperRow(PaperTile tile, int xOffset, int yOffset)
        {
            MessageId = tile.MessageId;
            Data = tile.Data;
            Units = tile.Units;
            TilePx = tile.TilePx;
            XOffset = xOffset;
            YOffset = yOffset;
        }

        public int MessageId { get; }
        public MessageVisualData Data { get; }
        public int Units { get; }
        public int TilePx { get; }
        public int XOffset { get; }
        public int YOffset { get; }
    }

    private List<PaperTile> CollectTiles()
    {
        var tiles = new List<PaperTile>(lastMessageIds.Length);

        foreach (int id in lastMessageIds)
        {
            if (!lastMessageTexts.TryGetValue(id, out MessageVisualData data) || string.IsNullOrWhiteSpace(data.Text))
                continue;

            int units = lastHeightUnits.TryGetValue(id, out int measured)
                ? PaperSize.ClampUnits(measured)
                : PaperSize.FullHeightUnits;

            tiles.Add(new PaperTile(id, data, units));
        }

        return tiles;
    }

    // The bounding rectangle the packed atlas would occupy in logical pixels at this factor.
    // Measured rather than estimated from the tile areas, because every column that ends short
    // leaves an empty tail inside that rectangle which still has to be allocated.
    private static void MeasureAtlas(List<PaperTile> tiles, int superSample, out int width, out int height)
    {
        int maxColumnPx = MaxTextureDim / superSample;
        int column = 0;
        int columnTop = 0;
        height = 0;

        foreach (PaperTile tile in tiles)
        {
            if (columnTop > 0 && columnTop + tile.TilePx > maxColumnPx)
            {
                column++;
                columnTop = 0;
            }

            columnTop += tile.TilePx;
            height = Math.Max(height, columnTop);
        }

        width = (column + 1) * TextWidth;
    }

    // Highest factor up to the requested one whose atlas the GPU will actually take. Factor and
    // packing are mutually dependent; a higher factor shrinks the per-column pixel budget, which
    // adds columns, which widens the atlas, so both are decided together, per candidate.
    // The budget scales with the request: asking for more sharpness is also asking to spend
    // proportionally more texture memory, and at the default the budget is what it always was.
    private static int ResolveSuperSample(int requested, List<PaperTile> tiles)
    {
        requested = PaperSize.ClampSharpness(requested);
        long budget = (long)SupersampleBudgetPx * requested / PaperSize.DefaultTextSharpness;

        for (int factor = requested; factor > PaperSize.MinTextSharpness; factor--)
        {
            MeasureAtlas(tiles, factor, out int width, out int height);

            if ((long)width * factor <= MaxTextureDim
                && (long)height * factor <= MaxTextureDim
                && (long)width * height * factor * factor <= budget)
            {
                return factor;
            }
        }

        return PaperSize.MinTextSharpness;
    }

    // What Rebuild would settle on for a setting that has not been saved yet, so the slider can
    // admit when a crowded board is forcing it down.
    public int PredictSuperSample(int requested) => ResolveSuperSample(requested, CollectTiles());

    // ItemIconCache calls this once the engine has rendered an <itemstack> this atlas painted
    // blank; the next frame repaints with the icon in place.
    private void MarkDirty() => isDirty = true;

    private void Rebuild()
    {
        isDirty = false;
        Cleanup();

        List<PaperTile> tiles = CollectTiles();
        PaperPlacement[] permitPlacements = NoticeBoardPaperLayout.PlacePermits(lastMessageIds, lastIsWall);

        // Permits are placed off the message count/ids alone, not off which messages carry
        // text, so a board using custom paper textures (no text tiles at all) still needs them.
        if (tiles.Count == 0 && permitPlacements.Length == 0)
            return;

        var rowById = new Dictionary<int, PaperRow>(tiles.Count);
        int surfaceWidth = 0;
        int surfaceHeight = 0;

        if (tiles.Count > 0)
            BuildPaperAtlas(tiles, rowById, ref surfaceWidth, ref surfaceHeight);

        if (permitPlacements.Length > 0)
            BuildPermitTexture();

        BuildMesh(rowById, surfaceWidth, surfaceHeight, permitPlacements);
    }

    private void BuildPaperAtlas(
        List<PaperTile> tiles,
        Dictionary<int, PaperRow> rowById,
        ref int surfaceWidth,
        ref int surfaceHeight
    )
    {
        ParchmentPalette boardTheme = ThemeManager.GetCurrentTheme(lastBoardTheme);
        double[] boardInk = boardTheme?.InkColor ?? InkColor;

        // Rasterise above the logical grid so glyphs get real detail when the player walks up to
        // a sheet. The board's sharpness setting picks the density; ResolveSuperSample steps it
        // down when this many sheets would not fit. Purely a density knob, since Cairo reports
        // text extents in user space and everything below is drawn in logical coordinates.
        int superSample = ResolveSuperSample(lastTextSharpness, tiles);

        var rows = new List<PaperRow>(tiles.Count);

        // A single tall strip would pass the texture size limit at the 50-notice maximum, so
        // tiles wrap into columns. This walk has to land on the same layout MeasureAtlas
        // predicted, which it does by consuming the same list in the same order.
        int maxColumnPx = MaxTextureDim / superSample;
        int column = 0;
        int columnTop = 0;
        surfaceHeight = 0;
        foreach (PaperTile tile in tiles)
        {
            if (columnTop > 0 && columnTop + tile.TilePx > maxColumnPx)
            {
                column++;
                columnTop = 0;
            }

            var paperRow = new PaperRow(tile, column * TextWidth, columnTop);
            rows.Add(paperRow);
            rowById[tile.MessageId] = paperRow;

            columnTop += tile.TilePx;
            surfaceHeight = Math.Max(surfaceHeight, columnTop);
        }

        surfaceWidth = (column + 1) * TextWidth;

        using ImageSurface surface = new ImageSurface(
            Format.Argb32,
            surfaceWidth * superSample,
            surfaceHeight * superSample
        );
        using Context ctx = new Context(surface);
        ctx.Scale(superSample, superSample);

        var font = PaperTextStyle.Font(PaperSize.ResolveBodyFontSize(lastBoardFontSize), lastBoardFont, boardInk);
        PaperTextStyle.Setup(ctx, font);

        int wearBuckets = GameDateFormatter.WearBuckets(
            api.GetNoticeBoardEntity(pos)?.BoardProperties?.NoticeAgingDays ?? GameDateFormatter.DefaultLifeDays(api.World.Calendar));

        foreach (PaperRow paperRow in rows)
        {
            // Everything below is drawn in tile-local coordinates; where the tile sits in the
            // packed atlas is the translate's business alone.
            ctx.Save();
            ctx.Translate(paperRow.XOffset, paperRow.YOffset);
            ctx.Rectangle(0, 0, TextWidth, paperRow.TilePx);
            ctx.Clip();

            PaintPaperTile(
                api,
                ctx,
                paperRow.Data,
                paperRow.Units,
                paperRow.TilePx,
                lastBoardFont,
                lastBoardTheme,
                lastBoardFontSize,
                paperRow.MessageId,
                MarkDirty,
                TileAge01(paperRow.Data),
                wearBuckets
            );

            ctx.Restore();
        }

        loadedTexture = new LoadedTexture(api);
        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref loadedTexture);
    }

    // Shared bake for TESR sheets, the pin ghost, and the hold-R / list overlay.
    // superSample raises device pixels for the on-screen preview only; maxUnits lets that
    // preview outgrow a real sheet. age01 / wearBuckets default to a fresh sheet (pin ghost).
    public static LoadedTexture RasterizeSheet(
        ICoreClientAPI api,
        MessageVisualData data,
        int units,
        string boardFont,
        string boardTheme,
        int superSample = 1,
        int maxUnits = PaperSize.FullHeightUnits,
        float boardFontSize = 0,
        double age01 = 0,
        int wearBuckets = GameDateFormatter.WearSteps,
        int messageId = 0
    )
    {
        units = GameMath.Clamp(units, 1, maxUnits);
        int tilePx = PaperSize.TilePixels(units);
        var texture = new LoadedTexture(api);
        using ImageSurface surface = new ImageSurface(
            Format.Argb32, TextWidth * superSample, tilePx * superSample);
        using (Context ctx = new Context(surface))
        {
            ctx.Scale(superSample, superSample);
            string resolvedFont = PaperSize.ResolveFont(boardFont);
            PaperTextStyle.Setup(ctx, PaperTextStyle.Font(PaperSize.ResolveBodyFontSize(boardFontSize), resolvedFont, InkColor));
            PaintPaperTile(api, ctx, data, units, tilePx, resolvedFont, boardTheme, boardFontSize, messageId, null, age01, wearBuckets);
        }
        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref texture);
        return texture;
    }

    private double TileAge01(MessageVisualData data)
    {
        NoticeBoardBlockEntity be = api.GetNoticeBoardEntity(pos);
        if (be?.BoardProperties?.EnableNoticeAging != 1 || data == null)
            return 0;
        var cal = api.World.Calendar;
        return GameDateFormatter.Age01(
            data.TotalHours,
            cal.TotalHours,
            cal.HoursPerDay,
            GameDateFormatter.ClampLifeDays(be.BoardProperties.NoticeAgingDays));
    }

    private static void DrawWornLine(ICoreClientAPI api, Context ctx, CairoFont font, string text, double x, double y, int seed, int salt, double wear01)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (wear01 <= 0)
        {
            api.Gui.Text.DrawTextLine(ctx, font, text, x, y);
            return;
        }
        double cx = x;
        for (int i = 0; i < text.Length; i++)
        {
            string ch = text.Substring(i, 1);
            double adv = ctx.TextExtents(ch).XAdvance;
            uint h = unchecked((uint)(seed * 16777619) ^ (uint)(salt + i) * 2166136261u);
            double u = (h & 0xFFFF) / 65535.0;
            if (u < 0.12 * wear01) { cx += adv; continue; }
            double u2 = ((h >> 16) & 0xFFFF) / 65535.0;
            if (u2 < 0.096 * wear01)
            {
                double[] c = font.Color;
                var faded = font.Clone().WithColor(new[] { c[0], c[1], c[2], (c.Length > 3 ? c[3] : 1.0) * 0.46 });
                PaperTextStyle.Setup(ctx, faded);
                api.Gui.Text.DrawTextLine(ctx, faded, ch, cx, y);
                PaperTextStyle.Setup(ctx, font);
            }
            else
                api.Gui.Text.DrawTextLine(ctx, font, ch, cx, y);
            cx += adv;
        }
    }

    private static void PaintPaperTile(
        ICoreClientAPI api,
        Context ctx,
        MessageVisualData data,
        int units,
        int tilePx,
        string boardFont,
        string boardTheme,
        float boardFontSize,
        int messageId,
        Action onIconReady,
        double age01 = 0,
        int wearBuckets = GameDateFormatter.WearSteps
    )
    {
        data ??= new MessageVisualData();
        int seed = data.ResolveParchmentSeed(messageId);
        double wear01 = GameDateFormatter.Wear01(age01);
        ParchmentPalette theme = ThemeManager.Age(ThemeManager.Resolve(data.PaperTheme, boardTheme), age01);
        double[] ink = theme?.InkColor ?? InkColor;
        double[] linkColor = theme?.LinkColor ?? InkColor;
        var rowFont = PaperTextStyle.Font(PaperSize.ResolveBodyFontSize(boardFontSize), boardFont, ink);
        var rowHeaderFont = PaperTextStyle.Font(PaperSize.ResolveHeaderFontSize(boardFontSize), boardFont, ink);
        var rowDateFont = PaperTextStyle.Font(PaperSize.ResolveDateFontSize(boardFontSize), boardFont, ink);
        var rowFontCache = new StyledFontCache(rowFont);

        double bodyMaxWidth = PaperSize.BodyMaxWidth;
        double headerLineHeight = api.Gui.Text.GetLineHeight(rowHeaderFont);
        double dateLineHeight = api.Gui.Text.GetLineHeight(rowDateFont);
        double lineHeight = api.Gui.Text.GetLineHeight(rowFont);
        PaperTextStyle.Setup(ctx, rowFont);
        double spaceWidth = ctx.TextExtents(" ").XAdvance;
        double iconSizePixels = lineHeight * 1.15;

        // Parchment first, ink on top, both inside this tile's clip so one sheet's torn
        // edges cannot reach its neighbours in the packed texture.
        ProceduralPaper.PaintCached(
            ctx,
            0,
            0,
            TextWidth,
            tilePx,
            seed,
            theme,
            jaggedEdges: true,
            foldShading: PaperSize.BandLayout(units),
            wear01: wear01,
            wearBuckets: wearBuckets
        );

        bool hasAuthor = !string.IsNullOrEmpty(data.Author);
        double headerY = TopPadding;
        double footerY = tilePx - BottomMargin - dateLineHeight;
        double textY = PaperSize.BodyOriginY(hasAuthor, headerLineHeight);
        double bodyBottom = footerY - FooterGap;

        PaperTextStyle.Setup(ctx, rowHeaderFont);
        if (hasAuthor)
        {
            DrawWornLine(api, ctx, rowHeaderFont, data.Author, SidePadding, headerY, seed, 1, wear01);
        }

        if (!string.IsNullOrEmpty(data.Date))
        {
            PaperTextStyle.Setup(ctx, rowDateFont);
            double dateWidth = ctx.TextExtents(data.Date).XAdvance;
            DrawWornLine(api, ctx, rowDateFont, data.Date, TextWidth - SidePadding - dateWidth, footerY, seed, 2, wear01);
        }

        List<InlineElement> elements = PaperRichTextLayout.Parse(data.Text, ink, linkColor);
        (List<InlineElement> capped, bool wasCapped) = PaperRichTextLayout.CapTotalChars(elements, MaxCharsPerMessage);
        List<List<InlineElement>> wrappedLines = PaperRichTextLayout.Wrap(ctx, rowFont, capped, bodyMaxWidth, iconSizePixels, rowFontCache);

        double y = textY;
        int lastFit = -1;
        var heights = new double[wrappedLines.Count];
        for (int i = 0; i < wrappedLines.Count; i++)
        {
            heights[i] = PaperRichTextLayout.LineHeight(api, rowFont, wrappedLines[i], rowFontCache);
            if (y + heights[i] > bodyBottom + 1e-6)
                break;
            lastFit = i;
            y += heights[i];
        }

        bool truncated = wasCapped || lastFit < wrappedLines.Count - 1;

        y = textY;
        for (int i = 0; i <= lastFit; i++)
        {
            List<InlineElement> line = wrappedLines[i];
            double h = heights[i];
            double lineY = y;
            bool isLastLine = truncated && i == lastFit;
            double lineWidth = PaperRichTextLayout.LineWidth(ctx, rowFont, line, iconSizePixels, rowFontCache);
            EnumTextOrientation align = line.Count > 0 ? line[0].Align : EnumTextOrientation.Left;
            double extraGap = 0;
            double x = SidePadding;
            if (align == EnumTextOrientation.Right)
                x = SidePadding + bodyMaxWidth - lineWidth;
            else if (align == EnumTextOrientation.Center)
                x = SidePadding + (bodyMaxWidth - lineWidth) / 2;
            else if (align == EnumTextOrientation.Justify && line.Count > 1 && !isLastLine)
                extraGap = (bodyMaxWidth - lineWidth) / (line.Count - 1);

            for (int j = 0; j < line.Count; j++)
            {
                InlineElement el = line[j];
                bool isLastElement = isLastLine && j == line.Count - 1;
                double elSize = PaperRichTextLayout.InlinePixelSize(el, iconSizePixels);

                switch (el.Kind)
                {
                    case InlineKind.Word:
                    {
                        CairoFont wordFont = rowFontCache.Get(el);
                        PaperTextStyle.Setup(ctx, wordFont);
                        DrawWornLine(api, ctx, wordFont, el.Text, x, lineY, seed, 10 + i * 200 + j, wear01);
                        double wordAdvance = ctx.TextExtents(el.Text).XAdvance;
                        if (isLastElement)
                        {
                            api.Gui.Text.DrawTextLine(ctx, wordFont, "…", x + wordAdvance, lineY);
                            wordAdvance += ctx.TextExtents("…").XAdvance;
                        }

                        if (el.Underline)
                        {
                            double[] lineColorRgba = el.Color ?? ink;
                            double underlineY = lineY + h * 0.82;
                            ctx.SetSourceRGBA(lineColorRgba[0], lineColorRgba[1], lineColorRgba[2], lineColorRgba.Length > 3 ? lineColorRgba[3] : 1.0);
                            ctx.LineWidth = 0.4;
                            ctx.MoveTo(x, underlineY);
                            ctx.LineTo(x + wordAdvance, underlineY);
                            ctx.Stroke();
                        }

                        x += wordAdvance + (j < line.Count - 1 ? spaceWidth + extraGap : 0);
                        break;
                    }
                    case InlineKind.Icon:
                    {
                        api.Gui.Icons.DrawIcon(ctx, el.Text, x, lineY, elSize, elSize, el.Color ?? ink);
                        if (isLastElement)
                        {
                            PaperTextStyle.Setup(ctx, rowFont);
                            api.Gui.Text.DrawTextLine(ctx, rowFont, "…", x + elSize, lineY);
                        }
                        x += elSize + (j < line.Count - 1 ? spaceWidth + extraGap : 0);
                        break;
                    }
                    case InlineKind.Itemstack:
                    {
                        ItemIconCache.Draw(api, ctx, el.Text, el.IsBlock, x, lineY, elSize, onIconReady);
                        if (isLastElement)
                        {
                            PaperTextStyle.Setup(ctx, rowFont);
                            api.Gui.Text.DrawTextLine(ctx, rowFont, "…", x + elSize, lineY);
                        }
                        x += elSize + (j < line.Count - 1 ? spaceWidth + extraGap : 0);
                        break;
                    }
                }
            }

            y += h;
        }
    }

    private void BuildPermitTexture()
    {
        using ImageSurface surface = new ImageSurface(
            Format.Argb32,
            PermitAtlasWidth,
            PermitAtlasHeight
        );
        using (Context ctx = new Context(surface))
        {
            ParchmentPalette theme = ThemeManager.GetCurrentTheme(lastBoardTheme);
            for (int i = 0; i < PermitVariantCount; i++)
            {
                ProceduralPaper.PaintPermitCached(ctx, i * PermitTileWidth, 0, PermitTileWidth, PermitTileHeight, i, theme, 0);
            }

            ProceduralPaper.PaintWaxSealCached(
                ctx,
                PermitVariantCount * PermitTileWidth,
                0,
                SealTileSize,
                SealTileSize,
                theme
            );
        }

        permitTexture = new LoadedTexture(api);
        api.Gui.LoadOrUpdateCairoTexture(surface, true, ref permitTexture);
    }

    private MeshData BuildPaperMesh(PaperBand[] bandLayout, Matrixf[] bandMatrices)
    {
        MeshData mesh = new MeshData(12, 18, true, false, true, true);
        mesh.xyz = new float[12 * 3];
        mesh.Uv = new float[12 * 2];
        mesh.Rgba = new byte[12 * 4];
        mesh.Normals = new int[12];
        mesh.Indices = new int[18];

        for (int i = 0; i < bandLayout.Length; i++)
        {
            PaperBand band = bandLayout[i];
            AddQuad(mesh, bandMatrices[i], band.Width, band.Height, band.VStart, band.VEnd, PaperFrontZ);
        }

        return mesh;
    }

    // South face (local z = 0.2/16) is #paperlabel, the reader-facing side; north (z = 0) is
    // #paperlabelsample, the back. Sit just past south so this is not behind the paper mesh.
    private const float PaperFrontZ = 0.2f / 16f + 0.002f;

    private void AddQuad(MeshData mesh, Matrixf matrix, float w, float h, float vStartFrac, float vEndFrac, float z, bool flipWinding = false)
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
        if (flipWinding)
        {
            mesh.Indices[i + 0] = v + 0;
            mesh.Indices[i + 1] = v + 3;
            mesh.Indices[i + 2] = v + 2;
            mesh.Indices[i + 3] = v + 0;
            mesh.Indices[i + 4] = v + 2;
            mesh.Indices[i + 5] = v + 1;
        }
        else
        {
            mesh.Indices[i + 0] = v + 0;
            mesh.Indices[i + 1] = v + 1;
            mesh.Indices[i + 2] = v + 2;
            mesh.Indices[i + 3] = v + 0;
            mesh.Indices[i + 4] = v + 2;
            mesh.Indices[i + 5] = v + 3;
        }

        mesh.VerticesCount += 4;
        mesh.IndicesCount += 6;
        SetQuadNormals(mesh, v, 0f, 0f, 1f);
    }

    private static void SetQuadNormals(MeshData mesh, int vertexStart, float nx, float ny, float nz)
    {
        int packed = NormalUtil.PackNormal(nx, ny, nz);
        for (int i = 0; i < 4; i++)
            mesh.Normals[vertexStart + i] = packed;
        mesh.NormalsCount = mesh.VerticesCount;
    }

    // A rectangular quad already given in local 3D space (no matrix), with fully independent UV
    // corners instead of AddQuad's fixed 1..0/vStart..vEnd mapping - needed once faces stop being
    // flat, axis-aligned rectangles sharing the same plane.
    private static void AddQuadRaw(MeshData mesh, Vec3f p0, Vec3f p1, Vec3f p2, Vec3f p3, float u0, float v0, float u1, float v1)
    {
        AddQuadRaw(mesh, p0, p1, p2, p3, u0, v0, u1, v1, 255, 255, 255, 0f, 0f, 1f);
    }

    private static void AddQuadRaw(
        MeshData mesh,
        Vec3f p0, Vec3f p1, Vec3f p2, Vec3f p3,
        float u0, float v0, float u1, float v1,
        byte r, byte g, byte b,
        float nx, float ny, float nz
    )
    {
        int v = mesh.VerticesCount;

        mesh.xyz[v * 3 + 0] = p0.X; mesh.xyz[v * 3 + 1] = p0.Y; mesh.xyz[v * 3 + 2] = p0.Z;
        mesh.xyz[v * 3 + 3] = p1.X; mesh.xyz[v * 3 + 4] = p1.Y; mesh.xyz[v * 3 + 5] = p1.Z;
        mesh.xyz[v * 3 + 6] = p2.X; mesh.xyz[v * 3 + 7] = p2.Y; mesh.xyz[v * 3 + 8] = p2.Z;
        mesh.xyz[v * 3 + 9] = p3.X; mesh.xyz[v * 3 + 10] = p3.Y; mesh.xyz[v * 3 + 11] = p3.Z;

        if (mesh.Flags != null)
        {
            mesh.Flags[v] = 0;
            mesh.Flags[v + 1] = 0;
            mesh.Flags[v + 2] = 0;
            mesh.Flags[v + 3] = 0;
        }

        mesh.Uv[v * 2 + 0] = u1; mesh.Uv[v * 2 + 1] = v0;
        mesh.Uv[v * 2 + 2] = u0; mesh.Uv[v * 2 + 3] = v0;
        mesh.Uv[v * 2 + 4] = u0; mesh.Uv[v * 2 + 5] = v1;
        mesh.Uv[v * 2 + 6] = u1; mesh.Uv[v * 2 + 7] = v1;

        for (int i = 0; i < 4; i++)
        {
            mesh.Rgba[(v + i) * 4 + 0] = r;
            mesh.Rgba[(v + i) * 4 + 1] = g;
            mesh.Rgba[(v + i) * 4 + 2] = b;
            mesh.Rgba[(v + i) * 4 + 3] = 255;
        }

        int idx = mesh.IndicesCount;
        mesh.Indices[idx + 0] = v + 0;
        mesh.Indices[idx + 1] = v + 1;
        mesh.Indices[idx + 2] = v + 2;
        mesh.Indices[idx + 3] = v + 0;
        mesh.Indices[idx + 4] = v + 2;
        mesh.Indices[idx + 5] = v + 3;

        mesh.VerticesCount += 4;
        mesh.IndicesCount += 6;
        SetQuadNormals(mesh, v, nx, ny, nz);
    }

    // Wax stamp cube on the pin, matching the baked mark1 element (2×2×1 in 1/16 units).
    // Every face maps onto the crimson atlas tile; BuildPermits remaps UVs to SealU/V.
    private static MeshData BuildSealMesh(int seed)
    {
        const int faceCount = 6;
        MeshData mesh = new MeshData(faceCount * 4, faceCount * 6, true, false, true, true);
        mesh.xyz = new float[faceCount * 4 * 3];
        mesh.Uv = new float[faceCount * 4 * 2];
        mesh.Rgba = new byte[faceCount * 4 * 4];
        mesh.Normals = new int[faceCount * 4];
        mesh.Indices = new int[faceCount * 6];

        float hw = PaperSize.MarkWidth * 0.5f;
        float hh = PaperSize.MarkHeight * 0.5f;
        float z0 = PaperSize.MarkFrontZ;
        float z1 = z0 + PaperSize.MarkDepth;

        AddQuadRaw(mesh,
            new Vec3f(-hw, -hh, z1), new Vec3f(hw, -hh, z1), new Vec3f(hw, hh, z1), new Vec3f(-hw, hh, z1),
            0f, 0f, 1f, 1f, 255, 255, 255, 0f, 0f, 1f);
        AddQuadRaw(mesh,
            new Vec3f(hw, -hh, z0), new Vec3f(-hw, -hh, z0), new Vec3f(-hw, hh, z0), new Vec3f(hw, hh, z0),
            0f, 0f, 1f, 1f, 255, 255, 255, 0f, 0f, -1f);
        AddQuadRaw(mesh,
            new Vec3f(hw, -hh, z0), new Vec3f(hw, -hh, z1), new Vec3f(hw, hh, z1), new Vec3f(hw, hh, z0),
            0f, 0f, 1f, 1f, 255, 255, 255, 1f, 0f, 0f);
        AddQuadRaw(mesh,
            new Vec3f(-hw, -hh, z1), new Vec3f(-hw, -hh, z0), new Vec3f(-hw, hh, z0), new Vec3f(-hw, hh, z1),
            0f, 0f, 1f, 1f, 255, 255, 255, -1f, 0f, 0f);
        AddQuadRaw(mesh,
            new Vec3f(-hw, hh, z1), new Vec3f(hw, hh, z1), new Vec3f(hw, hh, z0), new Vec3f(-hw, hh, z0),
            0f, 0f, 1f, 1f, 255, 255, 255, 0f, 1f, 0f);
        AddQuadRaw(mesh,
            new Vec3f(-hw, -hh, z0), new Vec3f(hw, -hh, z0), new Vec3f(hw, -hh, z1), new Vec3f(-hw, -hh, z1),
            0f, 0f, 1f, 1f, 255, 255, 255, 0f, -1f, 0f);

        return mesh;
    }

    // freezePins ? PinsFrom(texts) : null is the Manual Pin freeze. Atlas packing and Place()
    // must stay on the same id/height list.
    private void BuildMesh(
        Dictionary<int, PaperRow> rowById,
        int surfaceWidth,
        int surfaceHeight,
        PaperPlacement[] permitPlacements
    )
    {
        int maxPapers = NoticeBoardPaperLayout.ClampMax(lastMaxPapers);
        PaperPlacement[] placements = NoticeBoardPaperLayout.Place(
            lastMessageIds,
            lastIsWall,
            maxPapers,
            lastHeightUnits,
            lastFreezePins ? NoticeBoardPaperLayout.PinsFrom(lastMessageTexts) : null
        );

        Vec3f blockOrigin = new Vec3f(0.5f, 0.5f, 0.5f);
        var baseMeshByUnits = new Dictionary<int, MeshData>();
        var bandLayoutByUnits = new Dictionary<int, PaperBand[]>();
        var bandMatricesByUnits = new Dictionary<int, Matrixf[]>();

        var paper = new SwayAccumulator();
        var sheets = new List<SwaySheet>(placements.Length);
        sheetVertexRanges.Clear();

        foreach (PaperPlacement placement in placements)
        {
            if (!rowById.TryGetValue(placement.MessageId, out PaperRow paperRow))
                continue;

            int units = paperRow.Units;
            if (!bandLayoutByUnits.TryGetValue(units, out PaperBand[] bandLayout))
            {
                bandLayout = PaperSize.BandLayout(units);
                bandLayoutByUnits[units] = bandLayout;
            }

            if (!bandMatricesByUnits.TryGetValue(units, out Matrixf[] bandMatrices))
            {
                bandMatrices = PaperSize.BandMatrices(units);
                bandMatricesByUnits[units] = bandMatrices;
            }

            if (!baseMeshByUnits.TryGetValue(units, out MeshData baseMesh))
            {
                baseMesh = BuildPaperMesh(bandLayout, bandMatrices);
                baseMeshByUnits[units] = baseMesh;
            }

            // AddQuad emits tile-local 0..1 on both axes; remap them onto this tile's cell of
            // the packed atlas.
            float uStart = paperRow.XOffset / (float)surfaceWidth;
            float uEnd = (paperRow.XOffset + TextWidth) / (float)surfaceWidth;
            float vStart = paperRow.YOffset / (float)surfaceHeight;
            float vEnd = (paperRow.YOffset + paperRow.TilePx) / (float)surfaceHeight;

            MeshData quad = baseMesh.Clone();

            for (int i = 0; i < quad.VerticesCount; i++)
            {
                quad.Uv[i * 2] = uStart + quad.Uv[i * 2] * (uEnd - uStart);
                quad.Uv[i * 2 + 1] = vStart + quad.Uv[i * 2 + 1] * (vEnd - vStart);
            }

            quad.Translate(placement.X, placement.Y, placement.Z);

            if (Math.Abs(placement.RotZDeg) > 0.01f)
            {
                quad.Rotate(
                    new Vec3f(placement.X, placement.Y, placement.Z),
                    0,
                    0,
                    placement.RotZDeg * GameMath.DEG2RAD
                );
            }

            if (Math.Abs(lastRotateYDeg) > 0.01f)
            {
                quad.Rotate(blockOrigin, 0, lastRotateYDeg * GameMath.DEG2RAD, 0);
            }

            SheetAxes(placement, lastRotateYDeg, blockOrigin, out Vec3f across, out Vec3f outward);
            int sheetIndex = sheets.Count;
            sheets.Add(new SwaySheet(across, outward, SheetPhase(placement.MessageId)));

            // The weight ramp is measured off the sheet's own extent rather than the tack,
            // because a hanging band's top edge sits a little above the nail it is pinned by.
            // Deriving it from each vertex's own height is also what stops the three folded
            // bands tearing apart: a band's bottom edge is coincident with the next band's top
            // edge, so coincident vertices get identical weights.
            float topY = float.NegativeInfinity;
            float bottomY = float.PositiveInfinity;
            for (int i = 0; i < quad.VerticesCount; i++)
            {
                float y = quad.xyz[i * 3 + 1];
                if (y > topY)
                    topY = y;
                if (y < bottomY)
                    bottomY = y;
            }
            float sheetDrop = Math.Max(topY - bottomY, 1e-4f);

            var quadWeights = new float[quad.VerticesCount];
            var quadBiases = new float[quad.VerticesCount];
            for (int i = 0; i < quad.VerticesCount; i++)
            {
                quadWeights[i] = SwayWeight(topY, sheetDrop, quad.xyz[i * 3 + 1]);
                // The unremapped base mesh still carries AddQuad's 0..1 across the sheet width.
                quadBiases[i] = baseMesh.Uv[i * 2];
            }

            int vertexStart = paper.VerticesCount;
            paper.AddWeighted(quad, sheetIndex, quadWeights, quadBiases);
            sheetVertexRanges[placement.MessageId] = (vertexStart, paper.VerticesCount - vertexStart);
        }

        SwayAccumulator permits = permitPlacements.Length > 0
            ? BuildPermits(permitPlacements, blockOrigin, sheets)
            : null;

        swaySheets = sheets.ToArray();
        sheetLateral = new float[swaySheets.Length];
        sheetLift = new float[swaySheets.Length];
        swayApplied = false;

        paperBuffer = paper.Upload(api.Render);
        ApplyHighlight();

        if (permits != null)
            permitBuffer = permits.Upload(api.Render);

        BuildHolderMeshes(placements, blockOrigin);
    }

    private void BuildHolderMeshes(PaperPlacement[] placements, Vec3f blockOrigin)
    {
        var holdersByTexture = new Dictionary<int, MeshData>();

        foreach (PaperPlacement placement in placements)
        {
            if (!lastMessageTexts.TryGetValue(placement.MessageId, out MessageVisualData data))
                continue;

            MessageHolderDef def = MessageHolder.Get(data.Holder);
            if (!def.UsesItemMesh)
                continue;

            string code = MessageHolder.ItemCode(data.Holder, lastBoardMetal, api.World);
            if (string.IsNullOrEmpty(code) || !ItemMeshCache.TryGet(api, code, isBlock: false, out ItemMeshInfo info))
                continue;

            MeshData mesh = info.Mesh.Clone();
            mesh.Translate(def.OriginX, def.OriginY, def.OriginZ);
            for (int vi = 0; vi < mesh.VerticesCount; vi++)
            {
                mesh.xyz[vi * 3] *= def.Scale;
                mesh.xyz[vi * 3 + 1] *= def.Scale;
                mesh.xyz[vi * 3 + 2] *= def.Scale;
            }
            MessageHolder.HolderLean(data.ResolveParchmentSeed(placement.MessageId), 0, out float lx, out float ly, out float lz);
            mesh.Rotate(
                new Vec3f(0, 0, 0),
                (def.RotXDeg + lx) * GameMath.DEG2RAD,
                (def.RotYDeg + ly) * GameMath.DEG2RAD,
                (def.RotZDeg + lz) * GameMath.DEG2RAD
            );
            ApplyPlacementTransform(mesh, placement, 0f, blockOrigin, lastRotateYDeg);

            if (holdersByTexture.TryGetValue(info.AtlasTextureId, out MeshData acc))
                acc.AddMeshData(mesh);
            else
                holdersByTexture[info.AtlasTextureId] = mesh;
        }

        foreach (var kvp in holdersByTexture)
        {
            holderBuffersByTexture[kvp.Key] = api.Render.UploadMesh(kvp.Value);
        }
    }

    // Strips hang down from the wax pin; the 3D seal is centered on the same origin.
    private SwayAccumulator BuildPermits(PaperPlacement[] permitPlacements, Vec3f blockOrigin, List<SwaySheet> sheets)
    {
        var permits = new SwayAccumulator();

        foreach (PaperPlacement placement in permitPlacements)
        {
            int stripCount = Math.Clamp(placement.StripCount, 1, 2);

            SheetAxes(
                placement,
                lastRotateYDeg,
                blockOrigin,
                PaperSize.StripMatrix(0f),
                out Vec3f across,
                out Vec3f outward
            );
            int sheetIndex = sheets.Count;
            sheets.Add(new SwaySheet(across, outward, SheetPhase(placement.MessageId)));

            for (int stripIndex = 0; stripIndex < stripCount; stripIndex++)
            {
                float extraRot = stripCount == 2
                    ? (stripIndex == 0 ? -PaperSize.StripFanRotDeg : PaperSize.StripFanRotDeg)
                    : 0f;

                int bucket = (PaperTextureVariants.IndexFor(placement.MessageId) + stripIndex) % PermitVariantCount;
                float uStart = bucket * PermitTileWidth / (float)PermitAtlasWidth;
                float uEnd = (bucket + 1) * PermitTileWidth / (float)PermitAtlasWidth;

                MeshData stripQuad = BuildStripQuad(0f);
                if (stripIndex > 0)
                    stripQuad.Translate(0f, 0f, stripIndex * 0.001f);
                for (int i = 0; i < stripQuad.VerticesCount; i++)
                    stripQuad.Uv[i * 2] = uStart + stripQuad.Uv[i * 2] * (uEnd - uStart);

                ApplyPlacementTransform(stripQuad, placement, extraRot, blockOrigin, lastRotateYDeg);
                AddSwayingPermitMesh(permits, stripQuad, sheetIndex, stripQuad);
            }

            MeshData sealMesh = BuildSealMesh(placement.MessageId);
            TransformMeshByMatrix(sealMesh, PaperSize.SealMatrix());
            for (int i = 0; i < sealMesh.VerticesCount; i++)
            {
                sealMesh.Uv[i * 2] = SealUStart + sealMesh.Uv[i * 2] * (SealUEnd - SealUStart);
                sealMesh.Uv[i * 2 + 1] = SealVStart + sealMesh.Uv[i * 2 + 1] * (SealVEnd - SealVStart);
            }

            ApplyPlacementTransform(sealMesh, placement, 0f, blockOrigin, lastRotateYDeg);

            var sealWeights = new float[sealMesh.VerticesCount];
            var sealBiases = new float[sealMesh.VerticesCount];
            permits.AddWeighted(sealMesh, sheetIndex, sealWeights, sealBiases);
        }

        return permits;
    }

    private MeshData BuildStripQuad(float offsetX)
    {
        Matrixf stripMatrix = PaperSize.StripMatrix(offsetX);
        const int faceCount = 1;
        MeshData mesh = new MeshData(faceCount * 4, faceCount * 6, true, false, true, true);
        mesh.xyz = new float[faceCount * 4 * 3];
        mesh.Uv = new float[faceCount * 4 * 2];
        mesh.Rgba = new byte[faceCount * 4 * 4];
        mesh.Normals = new int[faceCount * 4];
        mesh.Indices = new int[faceCount * 6];
        AddQuad(mesh, stripMatrix, PaperSize.StripWidth, PaperSize.StripHeight, 1f, 0f, PaperSize.PermitFrontZ);
        return mesh;
    }

    private static void ApplyPlacementTransform(
        MeshData mesh,
        PaperPlacement placement,
        float extraRotDeg,
        Vec3f blockOrigin,
        float rotateYDeg
    )
    {
        mesh.Translate(placement.X, placement.Y, placement.Z);

        float rotZ = placement.RotZDeg + extraRotDeg;
        if (Math.Abs(rotZ) > 0.01f)
        {
            mesh.Rotate(
                new Vec3f(placement.X, placement.Y, placement.Z),
                0,
                0,
                rotZ * GameMath.DEG2RAD
            );
        }

        if (Math.Abs(rotateYDeg) > 0.01f)
        {
            mesh.Rotate(blockOrigin, 0, rotateYDeg * GameMath.DEG2RAD, 0);
        }
    }

    private static void TransformMeshByMatrix(MeshData mesh, Matrixf matrix)
    {
        for (int i = 0; i < mesh.VerticesCount; i++)
        {
            Vec4f v = matrix.TransformVector(new Vec4f(mesh.xyz[i * 3], mesh.xyz[i * 3 + 1], mesh.xyz[i * 3 + 2], 1f));
            mesh.xyz[i * 3] = v.X;
            mesh.xyz[i * 3 + 1] = v.Y;
            mesh.xyz[i * 3 + 2] = v.Z;
        }
    }

    private static void AddSwayingPermitMesh(SwayAccumulator permits, MeshData quad, int sheetIndex, MeshData uvSource)
    {
        float topY = float.NegativeInfinity;
        float bottomY = float.PositiveInfinity;
        for (int i = 0; i < quad.VerticesCount; i++)
        {
            float y = quad.xyz[i * 3 + 1];
            if (y > topY)
                topY = y;
            if (y < bottomY)
                bottomY = y;
        }
        float sheetDrop = Math.Max(topY - bottomY, 1e-4f);

        var quadWeights = new float[quad.VerticesCount];
        var quadBiases = new float[quad.VerticesCount];
        for (int i = 0; i < quad.VerticesCount; i++)
        {
            quadWeights[i] = SwayWeight(topY, sheetDrop, quad.xyz[i * 3 + 1]) * PaperSize.PermitSwayMultiplier;
            quadBiases[i] = uvSource.Uv[i * 2];
        }

        permits.AddWeighted(quad, sheetIndex, quadWeights, quadBiases);
    }

    // Squared so the top of the sheet is near-still and the free bottom edge takes almost all
    // the motion, which is how a pinned sheet of paper actually behaves.
    private static float SwayWeight(float topY, float sheetDrop, float vertexY)
    {
        float t = GameMath.Clamp((topY - vertexY) / sheetDrop, 0f, 1f);
        return t * t;
    }

    // Irrational multiplier, wrapped into one turn: consecutive message ids land far apart, so
    // neighbouring sheets never drift into step with each other.
    private static float SheetPhase(int messageId) =>
        (float)(messageId * 0.6180339887 % (2.0 * Math.PI));

    // The sway offset is expressed in a sheet's own axes - across its face, and out along its
    // outward normal - so it has to be carried through exactly the same rotations the sheet's
    // vertices were. Pushing basis points through the identical API calls is what guarantees
    // that, rather than restating the rotation maths and risking a sign that would drive papers
    // into the cork.
    private static void SheetAxes(
        PaperPlacement placement,
        float rotateYDeg,
        Vec3f blockOrigin,
        out Vec3f across,
        out Vec3f outward
    )
    {
        MeshData basis = new MeshData(3, 3);
        basis.VerticesCount = 3;
        basis.xyz[0] = 0;
        basis.xyz[1] = 0;
        basis.xyz[2] = 0;
        basis.xyz[3] = 1;
        basis.xyz[4] = 0;
        basis.xyz[5] = 0;
        basis.xyz[6] = 0;
        basis.xyz[7] = 0;
        basis.xyz[8] = 1;

        basis.Translate(placement.X, placement.Y, placement.Z);

        if (Math.Abs(placement.RotZDeg) > 0.01f)
        {
            basis.Rotate(
                new Vec3f(placement.X, placement.Y, placement.Z),
                0,
                0,
                placement.RotZDeg * GameMath.DEG2RAD
            );
        }

        if (Math.Abs(rotateYDeg) > 0.01f)
        {
            basis.Rotate(blockOrigin, 0, rotateYDeg * GameMath.DEG2RAD, 0);
        }

        across = new Vec3f(
            basis.xyz[3] - basis.xyz[0],
            basis.xyz[4] - basis.xyz[1],
            basis.xyz[5] - basis.xyz[2]
        );
        outward = new Vec3f(
            basis.xyz[6] - basis.xyz[0],
            basis.xyz[7] - basis.xyz[1],
            basis.xyz[8] - basis.xyz[2]
        );
    }

    // Same idea as the overload above, but for a sheet whose own local geometry is additionally
    // rotated by localMatrix before placement (the permit badge's rotationOrigin/rotationX/Z),
    // so its across/outward axes have to be pushed through that rotation too.
    private static void SheetAxes(
        PaperPlacement placement,
        float rotateYDeg,
        Vec3f blockOrigin,
        Matrixf localMatrix,
        out Vec3f across,
        out Vec3f outward
    )
    {
        Vec4f origin = localMatrix.TransformVector(new Vec4f(0, 0, 0, 1));
        Vec4f acrossPoint = localMatrix.TransformVector(new Vec4f(1, 0, 0, 1));
        Vec4f outwardPoint = localMatrix.TransformVector(new Vec4f(0, 0, 1, 1));

        MeshData basis = new MeshData(3, 3);
        basis.VerticesCount = 3;
        basis.xyz[0] = origin.X;
        basis.xyz[1] = origin.Y;
        basis.xyz[2] = origin.Z;
        basis.xyz[3] = acrossPoint.X;
        basis.xyz[4] = acrossPoint.Y;
        basis.xyz[5] = acrossPoint.Z;
        basis.xyz[6] = outwardPoint.X;
        basis.xyz[7] = outwardPoint.Y;
        basis.xyz[8] = outwardPoint.Z;

        basis.Translate(placement.X, placement.Y, placement.Z);

        if (Math.Abs(placement.RotZDeg) > 0.01f)
        {
            basis.Rotate(
                new Vec3f(placement.X, placement.Y, placement.Z),
                0,
                0,
                placement.RotZDeg * GameMath.DEG2RAD
            );
        }

        if (Math.Abs(rotateYDeg) > 0.01f)
        {
            basis.Rotate(blockOrigin, 0, rotateYDeg * GameMath.DEG2RAD, 0);
        }

        across = new Vec3f(
            basis.xyz[3] - basis.xyz[0],
            basis.xyz[4] - basis.xyz[1],
            basis.xyz[5] - basis.xyz[2]
        );
        outward = new Vec3f(
            basis.xyz[6] - basis.xyz[0],
            basis.xyz[7] - basis.xyz[1],
            basis.xyz[8] - basis.xyz[2]
        );
    }

    // Collects the merged mesh and its per-vertex sway data in lockstep: AddMeshData appends
    // vertices in order, so appending to the lists at the same time keeps index i pointing at
    // the same vertex in both.
    private sealed class SwayAccumulator
    {
        private readonly MeshData mesh = new MeshData(4, 6);
        private readonly List<float> weight = new();
        private readonly List<float> bias = new();
        private readonly List<int> sheetIndex = new();

        public int VerticesCount => mesh.VerticesCount;

        public void AddWeighted(MeshData part, int sheet, float[] weights, float[] biases)
        {
            for (int i = 0; i < part.VerticesCount; i++)
            {
                weight.Add(weights[i]);
                bias.Add(biases[i]);
                sheetIndex.Add(sheet);
            }

            mesh.AddMeshData(part);
        }

        public SwayBuffer Upload(IRenderAPI rpi) =>
            CreateSwayBuffer(rpi, mesh, weight, bias, sheetIndex);
    }

    private void Cleanup()
    {
        loadedTexture?.Dispose();
        loadedTexture = null;
        paperBuffer?.Ref?.Dispose();
        paperBuffer = null;
        permitTexture?.Dispose();
        permitTexture = null;
        permitBuffer?.Ref?.Dispose();
        permitBuffer = null;

        foreach (MeshRef holderRef in holderBuffersByTexture.Values)
        {
            holderRef?.Dispose();
        }
        holderBuffersByTexture.Clear();

        swaySheets = Array.Empty<SwaySheet>();
        sheetLateral = Array.Empty<float>();
        sheetLift = Array.Empty<float>();
        swayApplied = false;
        sheetVertexRanges.Clear();
    }

    private static bool IdsEqual(int[] a, int[] b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    private static bool HeightsEqual(Dictionary<int, int> a, Dictionary<int, int> b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Count != b.Count)
            return false;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out int value) || value != kvp.Value)
                return false;
        }
        return true;
    }

    private static bool DictionariesEqual(Dictionary<int, MessageVisualData> a, Dictionary<int, MessageVisualData> b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Count != b.Count)
            return false;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var value) || value.Text != kvp.Value.Text || value.Author != kvp.Value.Author || value.Date != kvp.Value.Date || value.Holder != kvp.Value.Holder || value.PaperTheme != kvp.Value.PaperTheme || value.HasPin != kvp.Value.HasPin || value.PinX != kvp.Value.PinX || value.PinY != kvp.Value.PinY || value.HasPinRotZ != kvp.Value.HasPinRotZ || value.PinRotZ != kvp.Value.PinRotZ || value.PinLayer != kvp.Value.PinLayer)
                return false;
        }
        return true;
    }
}
