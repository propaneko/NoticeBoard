using System;
using System.Collections.Generic;
using NoticeBoard;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Rendering;

public readonly struct PaperPlacement
{
    public PaperPlacement(
        int messageId,
        float x,
        float y,
        float z,
        float rotZDeg,
        int units = PaperSize.FullHeightUnits,
        int stripCount = 1
    )
    {
        MessageId = messageId;
        X = x;
        Y = y;
        Z = z;
        RotZDeg = rotZDeg;
        Units = units;
        StripCount = stripCount;
    }

    public int MessageId { get; }
    public float X { get; }
    public float Y { get; }
    public float Z { get; }
    public float RotZDeg { get; }

    // The height that decided this position travels with it, so the mesh and the layout cannot
    // disagree about how tall the sheet is.
    public int Units { get; }

    // Purity-seal parchment ribbons hanging under the wax (1 or 2).
    public int StripCount { get; }
}

public readonly struct PaperPin
{
    public PaperPin(float x, float y, float rotZDeg, int layer = 0)
    {
        X = x;
        Y = y;
        RotZDeg = rotZDeg;
        Layer = NoticeBoardPaperLayout.ClampPinLayer(layer);
    }

    public float X { get; }
    public float Y { get; }
    public float RotZDeg { get; }
    public int Layer { get; }
}

internal readonly struct SheetRect
{
    public SheetRect(float minX, float maxX, float minY, float maxY)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
    }

    public float MinX { get; }
    public float MaxX { get; }
    public float MinY { get; }
    public float MaxY { get; }
}

public static class NoticeBoardPaperLayout
{
    public const int MinPapers = 1;
    public const int MaxPapers = 50;
    public const int DefaultMaxPapers = 20;

    private const int CandidateCount = 96;

    // Every sheet gets at least a slight lean so no two look alike, and the tack bounds below
    // already account for how far the maximum lean swings the hanging corner sideways.
    private const float SheetTiltMinDeg = 2f;
    public const float SheetTiltMaxDeg = 12f;

    // Horizontal bounds are the cork panel area plus the very inner edge of the trim: based on
    // the panel extents from the shape, shrunk by the paper-sheet footprint around the nail and
    // the maximum stray lean added by Tilt(). A sheet's width does not vary with its height, so
    // unlike the vertical range these are the same for every notice.
    private const float SheetXMin = 0.49f;
    private const float SheetXMax = 2.28f;

    // Top and bottom edge of the visible cork, straight from the board shapes: corkBoard.from.y
    // plus the 1/16 bottom trim ("frameRailBottom") and the "frameRailTop" member at 31/16. Place() works out each
    // sheet's own tack range from these, since how far a sheet hangs below its nail now varies
    // by a factor of ten and a tack height that suits a full-length notice wastes most of the
    // cork on a short one.
    private const float GroundCorkYMin = 19f / 16f;
    private const float GroundCorkYMax = 49f / 16f;
    private const float WallCorkYMin = 1f / 16f;
    private const float WallCorkYMax = 31f / 16f;
    private const float CorkXMin = 5f / 16f;
    private const float CorkXMax = 43f / 16f;
    private const float GroundSheetZ = 0.5125f;
    private const float WallSheetZ = 0.1375f;

    // Permits hang off the two vertical frame posts, where the original marks are children of
    // "framePostRight": world = corkBoard.from + framePostRight.from + mark.from, divided by 16.
    private const int PermitSlotsPost = 2;
    private const int PermitSlotsBottom = 3;
    private const int MaxPermits = PermitSlotsPost * 2 + PermitSlotsBottom;
    // Frame-post centres from the shape JSON: corkBoard.from [4, *, *] plus
    // framePostLeft [-3..0] / framePostRight [40..43], in blocks.
    private const float PermitLeftX = 2.5f / 16f;
    private const float PermitRightX = 45.5f / 16f;
    private const float GroundPermitYMin = 1.38f;
    private const float GroundPermitYMax = 2.27f;
    private const float WallPermitYMin = 0.26f;
    private const float WallPermitYMax = 1.12f;
    private const float GroundPermitZ = 9.8f / 16f;
    private const float WallPermitZ = 3.8f / 16f;
    private const float GroundPermitBottomY = 18.5f / 16f;
    private const float WallPermitBottomY = 0.5f / 16f;
    private static readonly float[] PermitBottomX = { 12f / 16f, 24f / 16f, 36f / 16f };

    // DepthStagger is id-order z-fight only (0.0006). PinLayerStep is player stack (0.008, max 3).
    private const float DepthStagger = 0.0006f;
    public const int PinLayerMax = 3;
    public const float PinLayerStep = 0.008f;

    private const float PermitYJitter = 0.05f;

    public static int ClampPinLayer(int layer) => Math.Clamp(layer, 0, PinLayerMax);

    public static float SheetZWithLayer(bool isWall, int layer, int depthIndex) =>
        SheetZ(isWall) + ClampPinLayer(layer) * PinLayerStep + depthIndex * DepthStagger;

    // Ground cork sits 18/16 above wall cork. Stored pinY is cork-local; remap on attachment flip.
    public static float PinYDelta(bool fromWall, bool toWall)
    {
        if (fromWall == toWall)
            return 0f;
        float shift = GroundCorkYMin - WallCorkYMin;
        return fromWall ? shift : -shift;
    }

    // NULL corkAttachment: pinY above wall max is ground, below ground min is wall, overlap unknown.
    public static bool? InferCorkIsWall(float[] pinYs)
    {
        const float eps = 0.02f;
        if (pinYs == null || pinYs.Length == 0)
            return null;

        bool anyGround = false;
        bool anyWall = false;
        for (int i = 0; i < pinYs.Length; i++)
        {
            float y = pinYs[i];
            if (y > WallCorkYMax + eps)
                anyGround = true;
            if (y < GroundCorkYMin - eps)
                anyWall = true;
        }

        if (anyGround == anyWall)
            return null;
        return anyWall;
    }

    public static int ClampMax(int value)
    {
        if (value < MinPapers)
            return DefaultMaxPapers;
        return Math.Clamp(value, MinPapers, MaxPapers);
    }

    public static bool IsTackOnCork(float tackX, float tackY, bool isWall, int units, float tiltDeg)
    {
        int u = PaperSize.ClampUnits(units);
        SheetBounds bounds = PaperSize.HangingBounds(u, tiltDeg);
        float corkYMin = isWall ? WallCorkYMin : GroundCorkYMin;
        float corkYMax = isWall ? WallCorkYMax : GroundCorkYMax;
        float xFloor = CorkXMin - bounds.MinX;
        float xCeil = CorkXMax - bounds.MaxX;
        float yFloor = corkYMin - bounds.MinY;
        float yCeil = corkYMax - bounds.MaxY;
        return tackX >= xFloor && tackX <= xCeil && tackY >= yFloor && tackY <= yCeil;
    }

    // Which sheet a tack-plane hit lands on, or -1. PaperPlacement.Z already carries
    // SheetZWithLayer, and higher Z is nearer the player, so the largest Z among the sheets
    // covering the point is the one actually on top of the stack.
    public static int PickSheetAt(PaperPlacement[] placements, float tackX, float tackY)
    {
        int best = -1;
        float bestZ = float.NegativeInfinity;
        if (placements == null)
            return best;

        foreach (PaperPlacement p in placements)
        {
            SheetRect rect = Footprint(p.X, p.Y, PaperSize.HangingBounds(p.Units, p.RotZDeg));
            if (tackX < rect.MinX || tackX > rect.MaxX || tackY < rect.MinY || tackY > rect.MaxY)
                continue;
            if (p.Z <= bestZ)
                continue;
            bestZ = p.Z;
            best = p.MessageId;
        }
        return best;
    }

    public static float SheetZ(bool isWall) => isWall ? WallSheetZ : GroundSheetZ;

    public static void WorldOffsetToLocalXZ(float worldX, float worldZ, float rotateYDeg, out float localX, out float localZ)
    {
        float rad = rotateYDeg * GameMath.DEG2RAD;
        float cos = GameMath.Cos(rad);
        float sin = GameMath.Sin(rad);
        localX = worldX * cos - worldZ * sin;
        localZ = worldX * sin + worldZ * cos;
    }

    public static void LocalOffsetToWorldXZ(float localX, float localZ, float rotateYDeg, out float worldX, out float worldZ)
    {
        float rad = rotateYDeg * GameMath.DEG2RAD;
        float cos = GameMath.Cos(rad);
        float sin = GameMath.Sin(rad);
        worldX = localX * cos + localZ * sin;
        worldZ = -localX * sin + localZ * cos;
    }

    public static bool TryWorldHitToTack(
        Vec3d worldHit,
        BlockPos origin,
        float rotateYDeg,
        out float tackX,
        out float tackY
    )
    {
        float x = (float)(worldHit.X - origin.X) - 0.5f;
        float z = (float)(worldHit.Z - origin.Z) - 0.5f;
        WorldOffsetToLocalXZ(x, z, rotateYDeg, out float lx, out _);
        tackX = lx + 0.5f;
        tackY = (float)(worldHit.Y - origin.Y);
        return true;
    }

    // Hit the cork plane at SheetZ in local space. Eye/dir are shifted by 0.5 so local origin is
    // the block centre; do not mix CameraPos here (draw translation only).
    public static bool TryLookRayToTack(
        Vec3d eye,
        Vec3d dir,
        BlockPos origin,
        float rotateYDeg,
        bool isWall,
        out float tackX,
        out float tackY
    )
    {
        tackX = 0f;
        tackY = 0f;
        float eyeX = (float)(eye.X - origin.X) - 0.5f;
        float eyeY = (float)(eye.Y - origin.Y);
        float eyeZ = (float)(eye.Z - origin.Z) - 0.5f;
        WorldOffsetToLocalXZ(eyeX, eyeZ, rotateYDeg, out float lx, out float lz);
        WorldOffsetToLocalXZ((float)dir.X, (float)dir.Z, rotateYDeg, out float dx, out float dz);
        if (Math.Abs(dz) < 1e-5f)
            return false;
        float t = (SheetZ(isWall) - 0.5f - lz) / dz;
        if (t < 0f)
            return false;
        tackX = lx + t * dx + 0.5f;
        tackY = eyeY + t * (float)dir.Y;
        return true;
    }

    public static IReadOnlyDictionary<int, PaperPin> PinsFrom(IReadOnlyDictionary<int, MessageVisualData> texts)
    {
        if (texts == null || texts.Count == 0)
            return null;

        Dictionary<int, PaperPin> pins = null;
        foreach (var kvp in texts)
        {
            if (kvp.Value == null || !kvp.Value.HasPin)
                continue;
            pins ??= new Dictionary<int, PaperPin>();
            pins[kvp.Key] = new PaperPin(
                kvp.Value.PinX,
                kvp.Value.PinY,
                kvp.Value.HasPinRotZ ? kvp.Value.PinRotZ : Tilt(kvp.Key),
                kvp.Value.PinLayer
            );
        }
        return pins;
    }

    // heightUnits maps message id to sheet height in 1/16 blocks; a null dictionary or a
    // missing id means full height.
    public static PaperPlacement[] Place(
        int[] messageIds,
        bool isWall,
        int maxPapers,
        IReadOnlyDictionary<int, int> heightUnits,
        IReadOnlyDictionary<int, PaperPin> pins = null
    )
    {
        _ = maxPapers;

        if (messageIds == null || messageIds.Length == 0)
            return Array.Empty<PaperPlacement>();

        int[] ordered = (int[])messageIds.Clone();
        Array.Sort(ordered);

        float corkYMin = isWall ? WallCorkYMin : GroundCorkYMin;
        float corkYMax = isWall ? WallCorkYMax : GroundCorkYMax;

        var tilts = new float[ordered.Length];
        var units = new int[ordered.Length];
        for (int i = 0; i < ordered.Length; i++)
        {
            tilts[i] = Tilt(ordered[i]);
            units[i] = heightUnits != null && heightUnits.TryGetValue(ordered[i], out int measured)
                ? PaperSize.ClampUnits(measured)
                : PaperSize.FullHeightUnits;
        }

        // Long notices claim their space first and short ones fill the gaps, so posting a short
        // notice leaves the long ones where they are. The tie-break is required rather than
        // cosmetic: Array.Sort with a Comparison is unstable, so without it two equal-height
        // notices could be placed in either order and the block entity and the text renderer
        // could end up disagreeing about the layout.
        var order = new int[ordered.Length];
        for (int i = 0; i < order.Length; i++)
            order[i] = i;

        Array.Sort(
            order,
            (a, b) =>
            {
                int byHeight = units[b].CompareTo(units[a]);
                return byHeight != 0 ? byHeight : ordered[a].CompareTo(ordered[b]);
            }
        );

        var placed = new List<SheetRect>(ordered.Length);
        var result = new PaperPlacement[ordered.Length];
        var filled = new bool[ordered.Length];

        for (int idx = 0; idx < ordered.Length; idx++)
        {
            int messageId = ordered[idx];
            if (pins == null || !pins.TryGetValue(messageId, out PaperPin pin))
                continue;

            float tilt = pin.RotZDeg;
            SheetBounds pinBounds = PaperSize.HangingBounds(units[idx], tilt);
            placed.Add(Footprint(pin.X, pin.Y, pinBounds));
            result[idx] = new PaperPlacement(
                messageId,
                pin.X,
                pin.Y,
                SheetZWithLayer(isWall, pin.Layer, idx),
                tilt,
                units[idx]
            );
            filled[idx] = true;
        }

        foreach (int idx in order)
        {
            if (filled[idx])
                continue;

            int messageId = ordered[idx];
            float tilt = tilts[idx];
            SheetBounds bounds = PaperSize.HangingBounds(units[idx], tilt);

            // A sheet hangs below its tack rather than around it, so both ends of the tack
            // range depend on its height. Offsetting the cork edges by the sheet's own extent
            // keeps every notice, nail included, on the visible board at any length.
            float xFloor = CorkXMin - bounds.MinX;
            float xCeil = CorkXMax - bounds.MaxX;
            float yFloor = corkYMin - bounds.MinY;
            float yCeil = corkYMax - bounds.MaxY;

            float bestX = (xFloor + xCeil) * 0.5f;
            float bestY = (yFloor + yCeil) * 0.5f;
            float bestSeparation = float.NegativeInfinity;
            SheetRect bestRect = Footprint(bestX, bestY, bounds);

            // Pick the candidate whose paper is farthest from every paper already up, which on
            // a full board means the one that overlaps least.
            for (int attempt = 0; attempt < CandidateCount; attempt++)
            {
                var rng = new Random(Hash(messageId, attempt + 1));
                float x = Lerp(xFloor, xCeil, (float)rng.NextDouble());
                float y = Lerp(yFloor, yCeil, (float)rng.NextDouble());
                SheetRect rect = Footprint(x, y, bounds);
                float separation = MinimumSeparation(rect, placed);

                if (separation > bestSeparation)
                {
                    bestSeparation = separation;
                    bestX = x;
                    bestY = y;
                    bestRect = rect;
                }
            }

            placed.Add(bestRect);

            // Depth follows message id, not placement order, so the newest notice still hangs
            // in front of the ones it was pinned over.
            result[idx] = new PaperPlacement(
                messageId,
                bestX,
                bestY,
                SheetZWithLayer(isWall, 0, idx),
                tilt,
                units[idx]
            );
        }

        return result;
    }

    public static PaperPlacement[] PlacePermits(int[] messageIds, bool isWall)
    {
        if (messageIds == null || messageIds.Length < 2)
            return Array.Empty<PaperPlacement>();

        int[] ordered = (int[])messageIds.Clone();
        Array.Sort(ordered);

        int count = Math.Min(ordered.Length / 2, MaxPermits);
        var result = new PaperPlacement[count];
        float zBase = isWall ? WallPermitZ : GroundPermitZ;
        float yMin = isWall ? WallPermitYMin : GroundPermitYMin;
        float yMax = isWall ? WallPermitYMax : GroundPermitYMax;
        float ySpacing = yMax - yMin;
        float bottomY = isWall ? WallPermitBottomY : GroundPermitBottomY;

        for (int written = 0; written < count; written++)
        {
            int messageId = ordered[written * 2];
            float x;
            float y;
            if (written < PermitSlotsPost * 2)
            {
                bool useLeft = written % 2 == 0;
                int slot = written / 2;
                x = useLeft ? PermitLeftX : PermitRightX;
                y = yMin + slot * ySpacing
                    + (Hash(messageId, 24989) / (float)int.MaxValue - 0.5f) * PermitYJitter;
                y = Clamp(y, yMin, yMax);
            }
            else
            {
                int slot = written - PermitSlotsPost * 2;
                x = PermitBottomX[slot]
                    + (Hash(messageId, 24989) / (float)int.MaxValue - 0.5f) * PermitYJitter;
                y = bottomY;
            }

            float rot = (float)(new Random(Hash(messageId, 31337)).NextDouble() * 16.0 - 8.0);
            int stripCount = ComputeStripCount(messageId);
            result[written] = new PaperPlacement(
                messageId, x, y, zBase + written * DepthStagger, rot, stripCount: stripCount);
        }

        return result;
    }

    internal static int ComputeStripCount(int messageId) =>
        Hash(messageId, 90210) % 256 < 112 ? 2 : 1;

#if DEBUG
    public static void SelfCheckPermitStability()
    {
        var before = PlacePermits(new[] { 10, 20, 30, 40 }, isWall: false);
        var after = PlacePermits(new[] { 10, 20, 30, 40, 50 }, isWall: false);

        var byId = new Dictionary<int, PaperPlacement>(before.Length);
        foreach (PaperPlacement p in before)
            byId[p.MessageId] = p;

        foreach (PaperPlacement p in after)
        {
            if (!byId.TryGetValue(p.MessageId, out PaperPlacement prev))
                continue;

            if (prev.X != p.X || prev.Y != p.Y || prev.RotZDeg != p.RotZDeg || prev.StripCount != p.StripCount)
                throw new InvalidOperationException(
                    $"Permit placement for message {p.MessageId} shifted when a new message was added."
                );
        }
    }

    public static void SelfCheckPinnedStability()
    {
        var pins = new Dictionary<int, PaperPin> { [10] = new PaperPin(1.2f, 1.6f, 0f) };
        var heights = new Dictionary<int, int>();
        PaperPlacement[] before = Place(new[] { 10, 20 }, isWall: false, 20, heights, pins);
        PaperPlacement[] after = Place(new[] { 10, 20, 30 }, isWall: false, 20, heights, pins);

        PaperPlacement prev = default;
        PaperPlacement next = default;
        bool foundBefore = false;
        bool foundAfter = false;
        foreach (PaperPlacement p in before)
        {
            if (p.MessageId != 10)
                continue;
            prev = p;
            foundBefore = true;
        }
        foreach (PaperPlacement p in after)
        {
            if (p.MessageId != 10)
                continue;
            next = p;
            foundAfter = true;
        }

        if (!foundBefore || !foundAfter || prev.X != next.X || prev.Y != next.Y || prev.RotZDeg != next.RotZDeg)
            throw new InvalidOperationException("Pinned placement for message 10 shifted when a new message was added.");
    }

    public static void SelfCheckAutoIgnoresPins()
    {
        var pins = new Dictionary<int, PaperPin> { [10] = new PaperPin(1.2f, 1.6f, 0f) };
        var heights = new Dictionary<int, int>();
        int[] ids = { 10, 20, 30 };
        PaperPlacement[] frozen = Place(ids, isWall: false, 20, heights, pins);
        PaperPlacement[] packed = Place(ids, isWall: false, 20, heights, null);
        float fx = 0, px = 0;
        foreach (PaperPlacement p in frozen)
            if (p.MessageId == 10) fx = p.X;
        foreach (PaperPlacement p in packed)
            if (p.MessageId == 10) px = p.X;
        if (fx == 1.2f && px == 1.2f)
            throw new InvalidOperationException("Auto Place() must ignore stored pins.");
    }

    public static void SelfCheckWorldHitToTack()
    {
        var origin = new BlockPos(10, 20, 30);
        const float localX = 1.2f;
        const float localY = 1.6f;
        const float localZ = 0.5f;
        float[] facings = { 0f, 90f, 180f, 270f };

        foreach (float rotateYDeg in facings)
        {
            float rad = rotateYDeg * GameMath.DEG2RAD;
            float cos = GameMath.Cos(rad);
            float sin = GameMath.Sin(rad);
            float dx = localX - 0.5f;
            float dz = localZ - 0.5f;
            var worldHit = new Vec3d(
                origin.X + 0.5f + dx * cos + dz * sin,
                origin.Y + localY,
                origin.Z + 0.5f + -dx * sin + dz * cos
            );

            TryWorldHitToTack(worldHit, origin, rotateYDeg, out float tackX, out float tackY);
            if (Math.Abs(tackX - localX) > 1e-4f || Math.Abs(tackY - localY) > 1e-4f)
                throw new InvalidOperationException(
                    $"TryWorldHitToTack round-trip failed at rotateY {rotateYDeg}: got {tackX},{tackY}."
                );

            LocalOffsetToWorldXZ(dx, dz, rotateYDeg, out float worldX, out float worldZ);
            WorldOffsetToLocalXZ(worldX, worldZ, rotateYDeg, out float backX, out float backZ);
            if (Math.Abs(backX - dx) > 1e-4f || Math.Abs(backZ - dz) > 1e-4f)
                throw new InvalidOperationException(
                    $"LocalOffsetToWorldXZ round-trip failed at rotateY {rotateYDeg}."
                );
        }
    }

    public static void SelfCheckLookRayToTack()
    {
        var origin = new BlockPos(10, 20, 30);
        const float localX = 1.2f;
        const float localY = 1.6f;
        float localZ = SheetZ(false);
        float[] facings = { 0f, 90f, 180f, 270f };

        foreach (float rotateYDeg in facings)
        {
            float rad = rotateYDeg * GameMath.DEG2RAD;
            float cos = GameMath.Cos(rad);
            float sin = GameMath.Sin(rad);
            float dx = localX - 0.5f;
            float dz = localZ - 0.5f;
            var worldPoint = new Vec3d(
                origin.X + 0.5f + dx * cos + dz * sin,
                origin.Y + localY,
                origin.Z + 0.5f + -dx * sin + dz * cos
            );
            var dir = new Vec3d(sin, 0, cos);
            var eye = new Vec3d(
                worldPoint.X - 2.0 * dir.X,
                worldPoint.Y,
                worldPoint.Z - 2.0 * dir.Z
            );

            if (!TryLookRayToTack(eye, dir, origin, rotateYDeg, isWall: false, out float tackX, out float tackY))
                throw new InvalidOperationException(
                    $"TryLookRayToTack missed at rotateY {rotateYDeg}."
                );
            if (Math.Abs(tackX - localX) > 1e-4f || Math.Abs(tackY - localY) > 1e-4f)
                throw new InvalidOperationException(
                    $"TryLookRayToTack round-trip failed at rotateY {rotateYDeg}: got {tackX},{tackY}."
                );
        }
    }

    public static void SelfCheckAttachmentPinY()
    {
        const float shift = 18f / 16f;
        if (Math.Abs(PinYDelta(false, true) + shift) > 1e-5f)
            throw new InvalidOperationException("ground to wall pinY delta");
        if (Math.Abs(PinYDelta(true, false) - shift) > 1e-5f)
            throw new InvalidOperationException("wall to ground pinY delta");
        if (PinYDelta(false, false) != 0f || PinYDelta(true, true) != 0f)
            throw new InvalidOperationException("same-attachment pinY delta");
        if (InferCorkIsWall(new[] { 2.5f }) != false)
            throw new InvalidOperationException("infer ground from high pinY");
        if (InferCorkIsWall(new[] { 0.5f }) != true)
            throw new InvalidOperationException("infer wall from low pinY");
        if (InferCorkIsWall(new[] { 1.5f }) != null)
            throw new InvalidOperationException("overlap pinY must not infer");
    }

    public static void SelfCheckPinLayer()
    {
        if (ClampPinLayer(-1) != 0 || ClampPinLayer(4) != 3)
            throw new InvalidOperationException("ClampPinLayer range");
        if (Math.Abs(SheetZWithLayer(false, 3, 0) - SheetZWithLayer(false, 0, 0) - 3 * PinLayerStep) > 1e-5f)
            throw new InvalidOperationException("SheetZWithLayer step");
        if (SheetZWithLayer(false, 0, 1) <= SheetZWithLayer(false, 0, 0))
            throw new InvalidOperationException("SheetZWithLayer depth index");
    }

    public static void SelfCheckPickSheetAt()
    {
        var heights = new Dictionary<int, int>();
        PaperPlacement[] placed = Place(new[] { 10, 20 }, isWall: false, 20, heights, null);

        foreach (PaperPlacement p in placed)
        {
            SheetBounds bounds = PaperSize.HangingBounds(p.Units, p.RotZDeg);
            SheetRect rect = Footprint(p.X, p.Y, bounds);
            float probeX = p.X;
            float probeY = p.Y - 0.25f;
            if (probeX < rect.MinX || probeX > rect.MaxX || probeY < rect.MinY || probeY > rect.MaxY)
            {
                probeX = (rect.MinX + rect.MaxX) * 0.5f;
                probeY = (rect.MinY + rect.MaxY) * 0.5f;
            }
            if (PickSheetAt(placed, probeX, probeY) != p.MessageId)
                throw new InvalidOperationException($"PickSheetAt missed sheet {p.MessageId} at its own centre");
        }

        if (PickSheetAt(placed, -5f, -5f) != -1)
            throw new InvalidOperationException("PickSheetAt matched a point far off the cork");

        var back = new PaperPlacement(1, 1.5f, 1.5f, SheetZ(false), 0f);
        var front = new PaperPlacement(2, 1.5f, 1.5f, SheetZ(false) + PinLayerStep, 0f);
        SheetBounds overlapBounds = PaperSize.HangingBounds(PaperSize.FullHeightUnits, 0f);
        SheetRect overlapRect = Footprint(1.5f, 1.5f, overlapBounds);
        float ox = 1.5f;
        float oy = 1.5f - 0.25f;
        if (ox < overlapRect.MinX || ox > overlapRect.MaxX || oy < overlapRect.MinY || oy > overlapRect.MaxY)
        {
            ox = (overlapRect.MinX + overlapRect.MaxX) * 0.5f;
            oy = (overlapRect.MinY + overlapRect.MaxY) * 0.5f;
        }
        if (PickSheetAt(new[] { back, front }, ox, oy) != 2
            || PickSheetAt(new[] { front, back }, ox, oy) != 2)
            throw new InvalidOperationException("PickSheetAt did not prefer the sheet nearest the player");
    }
#endif

    // Drawn from its own seed so the lean never tracks whichever candidate position won.
    private static float Tilt(int messageId)
    {
        var rng = new Random(Hash(messageId, 31337));
        float magnitude = Lerp(SheetTiltMinDeg, SheetTiltMaxDeg, (float)rng.NextDouble());
        return rng.Next(2) == 0 ? -magnitude : magnitude;
    }

    private static SheetRect Footprint(float x, float y, SheetBounds bounds) =>
        new SheetRect(x + bounds.MinX, x + bounds.MaxX, y + bounds.MinY, y + bounds.MaxY);

    private static float MinimumSeparation(SheetRect rect, List<SheetRect> placed)
    {
        if (placed.Count == 0)
            return float.MaxValue;

        float min = float.MaxValue;
        for (int i = 0; i < placed.Count; i++)
        {
            float separation = Separation(rect, placed[i]);
            if (separation < min)
                min = separation;
        }

        return min;
    }

    // Signed distance between two sheets: positive is the gap between them, negative is how
    // deeply they overlap. One expression covers both, which is what lets the search spread
    // notices out while they still fit and then share the crowding evenly once they do not.
    private static float Separation(SheetRect a, SheetRect b)
    {
        float dx = Math.Max(a.MinX - b.MaxX, b.MinX - a.MaxX);
        float dy = Math.Max(a.MinY - b.MaxY, b.MinY - a.MaxY);

        if (dx > 0f && dy > 0f)
            return MathF.Sqrt(dx * dx + dy * dy);

        return Math.Max(dx, dy);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);

    // Same avalanche finalizer as PaperTextureVariants.IndexFor: messageId and salt are combined
    // with distinct multipliers so the two never collide, then run through three multiply/xor-
    // shift rounds so neighbouring message ids (or salts) land far apart instead of clustering.
    private static int Hash(int messageId, int salt)
    {
        unchecked
        {
            uint hash = (uint)messageId * 0x9E3779B1u + (uint)salt * 0x85EBCA6Bu;
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return (int)hash;
        }
    }
}
