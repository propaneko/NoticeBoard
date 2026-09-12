using System;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Utils;

public static class PositionHelper
{
    /// <summary>
    /// Converts a standard Vintage Story BlockPos.ToString() string back into a BlockPos object.
    /// Supports "x, y, z" and dimension-aware "x, y, z : dim".
    /// </summary>
    public static BlockPos FromString(string posString)
    {
        if (string.IsNullOrWhiteSpace(posString))
            return null;

        int dim = 0;
        string xyzPart = posString;
        int colonIndex = posString.LastIndexOf(':');
        if (colonIndex >= 0)
        {
            xyzPart = posString.Substring(0, colonIndex);
            int.TryParse(posString.Substring(colonIndex + 1).Trim(), out dim);
        }

        string[] coordinates = xyzPart.Split(',');

        if (coordinates.Length < 3)
            return null;

        if (
            int.TryParse(coordinates[0].Trim(), out int x)
            && int.TryParse(coordinates[1].Trim(), out int y)
            && int.TryParse(coordinates[2].Trim(), out int z)
        )
        {
            if (coordinates.Length >= 4 && dim == 0)
            {
                int.TryParse(coordinates[3].Trim(), out dim);
            }

            return new BlockPos(x, y, z, dim);
        }

        return null;
    }

    public static float WorldToHud(double world, double spawn) => (float)(world - spawn);

    public static float HudToWorld(double hud, double spawn) => (float)(hud + spawn);

#if DEBUG
    public static void SelfCheckHudCoords()
    {
        const double spawn = 500000;
        if (WorldToHud(500028, spawn) != 28f)
            throw new InvalidOperationException("WorldToHud must subtract spawn.");
        if (HudToWorld(28, spawn) != 500028f)
            throw new InvalidOperationException("HudToWorld must add spawn.");
        if ((int)WorldToHud(499980.7, spawn) != -19)
            throw new InvalidOperationException("HUD integers use (int) truncation, not Floor.");
        float round = HudToWorld(WorldToHud(512028.25, spawn), spawn);
        if (Math.Abs(round - 512028.25f) > 0.01f)
            throw new InvalidOperationException("World HUD round-trip must hold.");
    }
#endif
}
