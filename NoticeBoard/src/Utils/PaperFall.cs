using System;
using NoticeBoard.BlockType;
using NoticeBoard.Rendering;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace NoticeBoard.Utils;

public static class PaperFall
{
    public static int PeelHoldMs => (int)(GameDateFormatter.FallDropStartU * GameDateFormatter.FallDurationMs);

    public static Vec3d LocalToWorld(BlockPos origin, float localX, float localY, float localZ, float rotateYDeg)
    {
        NoticeBoardPaperLayout.LocalOffsetToWorldXZ(localX - 0.5f, localZ - 0.5f, rotateYDeg, out float wx, out float wz);
        return new Vec3d(origin.X + 0.5 + wx, origin.Y + localY, origin.Z + 0.5 + wz);
    }

    // Skip NoticeBoardBlock and BlockMultiblock so a ground board is not its own floor.
    public static double FindFloorY(IWorldAccessor world, Vec3d start, int dimension)
    {
        IBlockAccessor ba = world.BlockAccessor;
        int x = (int)Math.Floor(start.X);
        int z = (int)Math.Floor(start.Z);
        int y0 = (int)Math.Floor(start.Y);
        int yMin = Math.Max(0, y0 - GameDateFormatter.FallSearchBlocks);
        var p = new BlockPos(x, y0, z, dimension);
        for (int y = y0; y >= yMin; y--)
        {
            p.Y = y;
            Block block = ba.GetBlock(p);
            if (block == null || block.Id == 0)
                continue;
            if (block is NoticeBoardBlock || block is BlockMultiblock)
                continue;
            Cuboidf[] boxes = block.GetCollisionBoxes(ba, p);
            if (boxes == null || boxes.Length == 0)
                continue;
            float top = 0f;
            for (int i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].Y2 > top)
                    top = boxes[i].Y2;
            }
            return y + top;
        }
        return yMin;
    }

    public static int DurationMs(double startY, double floorY)
    {
        double height = Math.Max(0, startY - floorY);
        int hold = PeelHoldMs;
        int fall = (int)(1000.0 * Math.Sqrt(2.0 * height / GameDateFormatter.FallGravity));
        int ms = hold + fall;
        if (ms < hold + 1)
            ms = hold + 1;
        if (ms > GameDateFormatter.FallMaxMs)
            ms = GameDateFormatter.FallMaxMs;
        return ms;
    }

    public static void DropAt(long elapsedMs, float landDropY, out float dropY, out float dropOut)
    {
        int hold = PeelHoldMs;
        if (elapsedMs <= hold)
        {
            dropY = 0f;
            dropOut = 0f;
            return;
        }
        float s = (elapsedMs - hold) / 1000f;
        dropY = -0.5f * GameDateFormatter.FallGravity * s * s;
        if (dropY < landDropY)
            dropY = landDropY;
        float ramp = GameDateFormatter.FallDurationMs - hold;
        float u = ramp <= 0 ? 1f : GameMath.Clamp((elapsedMs - hold) / ramp, 0f, 1f);
        dropOut = GameDateFormatter.FallDropOut * u;
    }

#if DEBUG
    public static void SelfCheckFallMs()
    {
        if (PeelHoldMs != 437)
            throw new InvalidOperationException("PeelHoldMs must stay 0.35 * 1250.");
        int twoBlock = DurationMs(2, 0);
        int expect = 437 + (int)(1000.0 * Math.Sqrt(4.0 / 2.6));
        if (twoBlock != expect)
            throw new InvalidOperationException("Fall duration must be hold + sqrt(2h/g).");
    }
#endif
}
