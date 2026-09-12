using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace NoticeBoard.Rendering;

public class NoticeBoardLanternRenderer : IRenderer, IDisposable
{
    private const float SwayCullDistance = 32f;

    private readonly ICoreClientAPI api;
    private readonly BlockPos pos;

    private readonly List<LanternDraw> lanternDraws = new();
    private readonly Dictionary<string, MeshData> lanternMeshByKey = new();
    private readonly List<LampLight> lampLights = new();
    private bool lightsInEngine;

    private string lastLeftLanternKey;
    private string lastRightLanternKey;
    private bool lastIsWall;
    private float lastRotateYDeg;
    private int swayStrength = PaperSize.DefaultSwayStrength;

    private float cachedLanternWind;
    private float cachedWindSpeed;
    private float cachedWindX;
    private float cachedWindZ;
    private float cachedWindNx;
    private float cachedWindNz;
    private float cachedLanternWindSmooth;
    private float cachedWindNxSmooth;
    private float cachedWindNzSmooth;
    private long lastWindPollMs = -1;
    private float swayTime;
    private bool swayApplied;
    private bool swayUpdatedThisFrame;

    private static readonly float[] ShadowIdentity = Mat4f.Create();
    private readonly float[] shadowModelMat = Mat4f.Create();
    private readonly float[] shadowModelView = Mat4f.Create();

    public double RenderOrder => 0.5;
    public int RenderRange => 48;

    public NoticeBoardLanternRenderer(ICoreClientAPI api, BlockPos pos)
    {
        this.api = api;
        this.pos = pos;
        api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "noticeboard-lantern");
        api.Event.RegisterRenderer(this, EnumRenderStage.ShadowFar, "noticeboard-lantern-sf");
        api.Event.RegisterRenderer(this, EnumRenderStage.ShadowNear, "noticeboard-lantern-sn");
    }

    public void Update(
        bool isWall,
        float rotateYDeg,
        int swayStrength,
        ItemStack leftLantern,
        ItemStack rightLantern
    )
    {
        this.swayStrength = PaperSize.ClampSwayStrength(swayStrength);

        bool facingChanged =
            lastIsWall != isWall || Math.Abs(lastRotateYDeg - rotateYDeg) > 0.01f;
        string leftKey = LanternKey(leftLantern);
        string rightKey = LanternKey(rightLantern);
        if (facingChanged || lastLeftLanternKey != leftKey || lastRightLanternKey != rightKey)
        {
            lastIsWall = isWall;
            lastRotateYDeg = rotateYDeg;
            lastLeftLanternKey = leftKey;
            lastRightLanternKey = rightKey;
            RebuildLanterns(leftLantern, rightLantern);
        }
    }

    // Opaque + shadows share one sway tick. Light is sampled from the upper board cell
    // (Y+1 wall, Y+2 ground) so the lamps match the hanging papers. Cairo tiles need
    // PremultipliedAlpha; lantern meshes use it for the same shader setup.
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (lanternDraws.Count == 0)
            return;

        if (!NoticeBoardRenderCull.ShouldDraw(api, pos))
        {
            if (stage == EnumRenderStage.Opaque)
            {
                swayUpdatedThisFrame = false;
                SetLampLightsInEngine(false);
            }
            return;
        }

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

        SetLampLightsInEngine(true);
        if (!swayApplied)
            UpdateLampLightPositions();

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
        prog.NormalShaded = 1;

        foreach (LanternDraw lantern in lanternDraws)
        {
            prog.Tex2D = lantern.TextureId;
            rpi.RenderMesh(lantern.Ref);
        }

        prog.Stop();
        rpi.GlToggleBlend(true, EnumBlendMode.Standard);
    }

    public void Dispose()
    {
        CleanupLanterns();
        ClearLampLights();
        api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
        api.Event.UnregisterRenderer(this, EnumRenderStage.ShadowFar);
        api.Event.UnregisterRenderer(this, EnumRenderStage.ShadowNear);
    }

    private bool SwayEnabled => swayStrength > 0;

    private sealed class LanternDraw
    {
        public MeshData Mesh;
        public MeshRef Ref;
        public MeshData XyzUpdate;
        public float[] BaseXyz;
        public Vec3f Hang;
        public Vec3f AxisX;
        public Vec3f AxisZ;
        public float Phase;
        public int TextureId;
        public bool Sway;
        public Vec3f LightColor;
    }

    private sealed class LampLight : IPointLight
    {
        public Vec3f Color { get; set; } = new Vec3f(5.5f, 7.7f, 8.9f);
        public Vec3d Pos { get; } = new Vec3d();
    }

    private void RenderIntoShadowMap()
    {
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

        foreach (LanternDraw lantern in lanternDraws)
        {
            prog.BindTexture2D("entityTex", lantern.TextureId, 0);
            rpi.RenderMesh(lantern.Ref);
        }

        prog.Stop();
        prev?.Use();
    }

    private void PollWind()
    {
        long now = api.World.ElapsedMilliseconds;
        if (lastWindPollMs >= 0 && now - lastWindPollMs <= 250)
            return;

        lastWindPollMs = now;
        Vec3d wind = api.World.BlockAccessor.GetWindSpeedAt(pos);
        if (wind == null)
        {
            cachedWindSpeed = 0f;
            cachedLanternWind = 0f;
            cachedWindX = 0f;
            cachedWindZ = 0f;
            cachedWindNx = 0f;
            cachedWindNz = 0f;
            return;
        }

        float len = (float)wind.Length();
        cachedWindSpeed = GameMath.Clamp(len, 0f, 1f);
        cachedLanternWind = GameMath.Clamp(len, 0f, PaperSize.MaxLanternWind);
        cachedWindX = (float)wind.X;
        cachedWindZ = (float)wind.Z;
        float hLen = GameMath.Sqrt(cachedWindX * cachedWindX + cachedWindZ * cachedWindZ);
        cachedWindNx = hLen > 0.01f ? cachedWindX / hLen : 0f;
        cachedWindNz = hLen > 0.01f ? cachedWindZ / hLen : 0f;
    }

    private void UpdateSway(float deltaTime, Vec3d camPos)
    {
        double dx = pos.X + 0.5 - camPos.X;
        double dy = pos.Y + 0.5 - camPos.Y;
        double dz = pos.Z + 0.5 - camPos.Z;
        if (dx * dx + dy * dy + dz * dz > SwayCullDistance * SwayCullDistance)
            return;

        if (!SwayEnabled || !api.Settings.Bool["wavingStuff"])
        {
            if (swayApplied)
                RestoreLanterns();
            return;
        }

        PollWind();
        float k = 1f - (float)Math.Exp(-deltaTime * PaperSize.LanternWindSmoothHz);
        cachedLanternWindSmooth += (cachedLanternWind - cachedLanternWindSmooth) * k;
        cachedWindNxSmooth += (cachedWindNx - cachedWindNxSmooth) * k;
        cachedWindNzSmooth += (cachedWindNz - cachedWindNzSmooth) * k;
        float nLen = GameMath.Sqrt(cachedWindNxSmooth * cachedWindNxSmooth + cachedWindNzSmooth * cachedWindNzSmooth);
        if (nLen > 0.01f)
        {
            cachedWindNxSmooth /= nLen;
            cachedWindNzSmooth /= nLen;
        }

        if (cachedLanternWindSmooth <= PaperSize.LanternWindRest)
        {
            cachedLanternWindSmooth = 0f;
            if (swayApplied)
                RestoreLanterns();
            return;
        }

        // /time stop zeros SpeedOfTime; ESC pause is IsGamePaused. Keep the last pose.
        if (api.IsGamePaused || api.World.Calendar.SpeedOfTime <= 0f)
            return;

        swayTime += deltaTime * (1f + PaperSize.MaxWindFrequencyBoost * GameMath.Clamp(cachedLanternWindSmooth, 0f, 1f));
        if (swayTime > 10000f)
            swayTime -= 10000f;

        PoseLanterns();
        swayApplied = true;
    }

    private static string LanternKey(ItemStack stack)
    {
        if (stack?.Block == null)
            return null;

        string material = stack.Attributes?.GetString("material") ?? "copper";
        string lining = stack.Attributes?.GetString("lining") ?? "plain";
        string glass = stack.Attributes?.GetString("glass") ?? "quartz";
        return stack.Block.Code + "-" + material + "-" + lining + "-" + glass;
    }

    private void RebuildLanterns(ItemStack leftLantern, ItemStack rightLantern)
    {
        CleanupLanterns();

        TryAddLantern(leftLantern, isRight: false);
        TryAddLantern(rightLantern, isRight: true);
        swayApplied = false;
        SyncLampLights();
    }

    // Two meshes per lamp: Ring stays on the boom, everything else under "lantern" sways.
    // "mount" is stripped so the vanilla lantern hang does not draw.
    private void TryAddLantern(ItemStack stack, bool isRight)
    {
        if (stack == null)
            return;

        MeshData hookUnit = GetCeilingLanternMesh(stack, hookOnly: true);
        MeshData bodyUnit = GetCeilingLanternMesh(stack, hookOnly: false);
        if (hookUnit == null || bodyUnit == null)
            return;

        Vec3f translate = LanternTranslate(lastIsWall, isRight);
        Vec3f blockOrigin = new Vec3f(0.5f, 0.5f, 0.5f);
        float phase = isRight ? 2.3f : 0.4f;

        AddStaticLanternDraw(hookUnit, translate, blockOrigin);
        AddSwayingLanternDraw(bodyUnit, translate, blockOrigin, phase, LightColorFromLining(stack));
    }

    private void AddStaticLanternDraw(MeshData unit, Vec3f translate, Vec3f blockOrigin)
    {
        MeshData mesh = unit.Clone();
        mesh.Translate(translate.X, translate.Y, translate.Z);
        if (Math.Abs(lastRotateYDeg) > 0.01f)
            mesh.Rotate(blockOrigin, 0, lastRotateYDeg * GameMath.DEG2RAD, 0);

        var draw = new LanternDraw
        {
            Mesh = mesh,
            Ref = api.Render.UploadMesh(mesh),
            Sway = false,
            TextureId = LanternTextureId(mesh),
        };
        lanternDraws.Add(draw);
    }

    private void AddSwayingLanternDraw(MeshData unit, Vec3f translate, Vec3f blockOrigin, float phase, Vec3f lightColor)
    {
        MeshData mesh = unit.Clone();
        mesh.Translate(translate.X, translate.Y, translate.Z);
        if (Math.Abs(lastRotateYDeg) > 0.01f)
            mesh.Rotate(blockOrigin, 0, lastRotateYDeg * GameMath.DEG2RAD, 0);

        Vec3f hangLocal = new Vec3f(0.5f, 10.75f / 16f, 0.5f);
        Vec3f hangBeforeYaw = new Vec3f(
            translate.X + hangLocal.X,
            translate.Y + hangLocal.Y,
            translate.Z + hangLocal.Z
        );
        LanternAxes(translate, hangBeforeYaw, lastRotateYDeg, blockOrigin, out Vec3f axisX, out Vec3f axisZ, out Vec3f hang);

        mesh.XyzStatic = false;
        var update = new MeshData(1, 1, false, false, false, false);
        update.Indices = null;
        update.xyz = mesh.xyz;
        update.VerticesCount = mesh.VerticesCount;

        var draw = new LanternDraw
        {
            Mesh = mesh,
            Ref = api.Render.UploadMesh(mesh),
            XyzUpdate = update,
            BaseXyz = (float[])mesh.xyz.Clone(),
            Hang = hang,
            AxisX = axisX,
            AxisZ = axisZ,
            Phase = phase,
            Sway = true,
            TextureId = LanternTextureId(mesh),
            LightColor = lightColor,
        };
        lanternDraws.Add(draw);
    }

    private static Vec3f LightColorFromLining(ItemStack stack)
    {
        string lining = stack?.Attributes?.GetString("lining") ?? "plain";
        bool lined = lining.Length > 0 && lining != "plain";
        return lined ? new Vec3f(6.4f, 9.0f, 10.4f) : new Vec3f(5.5f, 7.7f, 8.9f);
    }

    // Post centres from the board shapes. Wall cork is 18/16 lower than ground, so Y/Z differ.
    private static Vec3f LanternTranslate(bool isWall, bool isRight)
    {
        float x = isRight ? 2.34375f : -0.3175f;
        if (isWall)
            return new Vec3f(x, 1.2525f, 0.3025f);
        return new Vec3f(x, 2.3725f, 0.6725f);
    }

    private int LanternTextureId(MeshData mesh)
    {
        if (mesh.TextureIds != null && mesh.TextureIds.Length > 0)
            return mesh.TextureIds[0];
        return api.BlockTextureAtlas.AtlasTextures[0].TextureId;
    }

    private MeshData GetCeilingLanternMesh(ItemStack stack, bool hookOnly)
    {
        string baseKey = LanternKey(stack);
        if (baseKey == null)
            return null;

        string key = baseKey + (hookOnly ? "/hook" : "/body");
        if (lanternMeshByKey.TryGetValue(key, out MeshData cached))
            return cached;

        Block lanternBlock = stack.Block;
        string material = stack.Attributes?.GetString("material") ?? "copper";
        string lining = stack.Attributes?.GetString("lining") ?? "plain";
        string glass = stack.Attributes?.GetString("glass") ?? "quartz";

        Shape full = Shape.TryGet(api, new AssetLocation("game:shapes/block/metal/lantern/small/ceiling.json"));
        if (full == null)
            return null;

        Shape shape = CeilingLanternPart(full, hookOnly);

        MeshData mesh;
        ITesselatorAPI tesselator = api.Tesselator;
        if (lanternBlock is BlockLantern lantern)
        {
            mesh = lantern.GenMesh(api, material, lining, glass, shape, tesselator);
        }
        else
        {
            tesselator.TesselateShape(
                "lantern",
                shape,
                out mesh,
                tesselator.GetTextureSource(lanternBlock),
                null
            );
        }

        if (mesh == null)
            return null;

        if (mesh.Flags != null)
            Array.Clear(mesh.Flags, 0, mesh.Flags.Length);

        lanternMeshByKey[key] = mesh;
        return mesh;
    }

    // hookOnly keeps child "Ring"; body keeps the rest of "lantern". Clone first.
    private static Shape CeilingLanternPart(Shape full, bool hookOnly)
    {
        Shape shape = full.Clone();
        List<ShapeElement> roots = new List<ShapeElement>();
        foreach (ShapeElement el in shape.Elements)
        {
            if (el.Name != "mount")
                roots.Add(el);
        }
        shape.Elements = roots.ToArray();
        foreach (ShapeElement root in shape.Elements)
        {
            if (root.Name != "lantern" || root.Children == null)
                continue;
            List<ShapeElement> keep = new List<ShapeElement>();
            foreach (ShapeElement child in root.Children)
            {
                bool isRing = child.Name == "Ring";
                if (hookOnly ? isRing : !isRing)
                    keep.Add(child);
            }
            root.Children = keep.ToArray();
        }
        return shape;
    }

    private static void LanternAxes(
        Vec3f translate,
        Vec3f hangBeforeYaw,
        float rotateYDeg,
        Vec3f blockOrigin,
        out Vec3f axisX,
        out Vec3f axisZ,
        out Vec3f hang
    )
    {
        MeshData basis = new MeshData(4, 4);
        basis.VerticesCount = 4;
        basis.xyz[0] = hangBeforeYaw.X;
        basis.xyz[1] = hangBeforeYaw.Y;
        basis.xyz[2] = hangBeforeYaw.Z;
        basis.xyz[3] = translate.X;
        basis.xyz[4] = translate.Y;
        basis.xyz[5] = translate.Z;
        basis.xyz[6] = translate.X + 1f;
        basis.xyz[7] = translate.Y;
        basis.xyz[8] = translate.Z;
        basis.xyz[9] = translate.X;
        basis.xyz[10] = translate.Y;
        basis.xyz[11] = translate.Z + 1f;

        if (Math.Abs(rotateYDeg) > 0.01f)
            basis.Rotate(blockOrigin, 0, rotateYDeg * GameMath.DEG2RAD, 0);

        hang = new Vec3f(basis.xyz[0], basis.xyz[1], basis.xyz[2]);
        axisX = new Vec3f(
            basis.xyz[6] - basis.xyz[3],
            basis.xyz[7] - basis.xyz[4],
            basis.xyz[8] - basis.xyz[5]
        );
        axisZ = new Vec3f(
            basis.xyz[9] - basis.xyz[3],
            basis.xyz[10] - basis.xyz[4],
            basis.xyz[11] - basis.xyz[5]
        );
        axisX.Normalize();
        axisZ.Normalize();
    }

    // Mean lean follows wind on AxisX/AxisZ (board space after yaw). Flutter uses WindOscShare.
    // Paper sway clamps wind to 1; lanterns allow PaperSize.MaxLanternWind (1.6).
    private void PoseLanterns()
    {
        float deg = PaperSize.MaxLanternSwayDeg * (swayStrength / 100f) * cachedLanternWindSmooth;
        int lightIndex = 0;
        foreach (LanternDraw lantern in lanternDraws)
        {
            if (!lantern.Sway)
                continue;
            float dirPitch = -(cachedWindNxSmooth * lantern.AxisZ.X + cachedWindNzSmooth * lantern.AxisZ.Z);
            float dirRoll = cachedWindNxSmooth * lantern.AxisX.X + cachedWindNzSmooth * lantern.AxisX.Z;
            float oscPitch =
                0.7f * GameMath.Sin(swayTime * 1.7f + lantern.Phase)
                + 0.3f * GameMath.Sin(swayTime * 3.9f + 2.3f * lantern.Phase);
            float oscRoll =
                0.7f * GameMath.Sin(swayTime * 1.7f + lantern.Phase + GameMath.PIHALF)
                + 0.3f * GameMath.Sin(swayTime * 3.9f + 2.3f * lantern.Phase + 1.1f);
            float pitch =
                deg
                * GameMath.DEG2RAD
                * (PaperSize.WindDirBias * dirPitch + PaperSize.WindOscShare * oscPitch);
            float roll =
                deg
                * GameMath.DEG2RAD
                * (PaperSize.WindDirBias * dirRoll + PaperSize.WindOscShare * oscRoll);
            LampLight light = lightIndex < lampLights.Count ? lampLights[lightIndex] : null;
            lightIndex++;
            PoseLantern(lantern, pitch, roll, light);
        }
    }

    private void PoseLantern(LanternDraw lantern, float pitch, float roll, LampLight light)
    {
        float[] xyz = lantern.Mesh.xyz;
        float[] baseXyz = lantern.BaseXyz;
        int verts = lantern.Mesh.VerticesCount;
        Vec3f hang = lantern.Hang;
        Vec3f ax = lantern.AxisX;
        Vec3f az = lantern.AxisZ;

        for (int i = 0; i < verts; i++)
        {
            int v = i * 3;
            float px = baseXyz[v] - hang.X;
            float py = baseXyz[v + 1] - hang.Y;
            float pz = baseXyz[v + 2] - hang.Z;
            RotateAroundAxis(ref px, ref py, ref pz, ax.X, ax.Y, ax.Z, pitch);
            RotateAroundAxis(ref px, ref py, ref pz, az.X, az.Y, az.Z, roll);
            xyz[v] = hang.X + px;
            xyz[v + 1] = hang.Y + py;
            xyz[v + 2] = hang.Z + pz;
        }

        api.Render.UpdateMesh(lantern.Ref, lantern.XyzUpdate);

        if (light == null)
            return;
        float lx = 0f, ly = -0.22f, lz = 0f;
        RotateAroundAxis(ref lx, ref ly, ref lz, ax.X, ax.Y, ax.Z, pitch);
        RotateAroundAxis(ref lx, ref ly, ref lz, az.X, az.Y, az.Z, roll);
        light.Pos.Set(pos.X + hang.X + lx, pos.Y + hang.Y + ly, pos.Z + hang.Z + lz);
    }

    private static void RotateAroundAxis(
        ref float x,
        ref float y,
        ref float z,
        float ax,
        float ay,
        float az,
        float angle
    )
    {
        float c = GameMath.Cos(angle);
        float s = GameMath.Sin(angle);
        float oneMinus = 1f - c;
        float dot = x * ax + y * ay + z * az;
        float cx = ay * z - az * y;
        float cy = az * x - ax * z;
        float cz = ax * y - ay * x;
        x = x * c + cx * s + ax * dot * oneMinus;
        y = y * c + cy * s + ay * dot * oneMinus;
        z = z * c + cz * s + az * dot * oneMinus;
    }

    private void RestoreLanterns()
    {
        swayApplied = false;
        foreach (LanternDraw lantern in lanternDraws)
            RestoreLantern(lantern);
    }

    private void RestoreLantern(LanternDraw lantern)
    {
        if (!lantern.Sway)
            return;
        Array.Copy(lantern.BaseXyz, lantern.Mesh.xyz, lantern.BaseXyz.Length);
        api.Render.UpdateMesh(lantern.Ref, lantern.XyzUpdate);
    }

    private void CleanupLanterns()
    {
        ClearLampLights();
        foreach (LanternDraw lantern in lanternDraws)
        {
            lantern.Ref?.Dispose();
        }
        lanternDraws.Clear();
        lanternMeshByKey.Clear();
    }

    private void ClearLampLights()
    {
        foreach (LampLight light in lampLights)
            api.Render.RemovePointLight(light);
        lampLights.Clear();
        lightsInEngine = false;
    }

    private void SetLampLightsInEngine(bool on)
    {
        if (on == lightsInEngine)
            return;
        if (on)
        {
            foreach (LampLight light in lampLights)
                api.Render.AddPointLight(light);
        }
        else
        {
            foreach (LampLight light in lampLights)
                api.Render.RemovePointLight(light);
        }
        lightsInEngine = on && lampLights.Count > 0;
    }

    // Point lights on swaying bodies only. Glow sits in the cage (hang Y minus 0.22), not the ring.
    private void SyncLampLights()
    {
        ClearLampLights();
        foreach (LanternDraw lantern in lanternDraws)
        {
            if (!lantern.Sway)
                continue;
            var light = new LampLight { Color = lantern.LightColor };
            api.Render.AddPointLight(light);
            lampLights.Add(light);
        }
        lightsInEngine = lampLights.Count > 0;
        UpdateLampLightPositions();
    }

    private void UpdateLampLightPositions()
    {
        int i = 0;
        foreach (LanternDraw lantern in lanternDraws)
        {
            if (!lantern.Sway)
                continue;
            LampLight light = lampLights[i++];
            light.Pos.Set(pos.X + lantern.Hang.X, pos.Y + lantern.Hang.Y - 0.22f, pos.Z + lantern.Hang.Z);
        }
    }
}
