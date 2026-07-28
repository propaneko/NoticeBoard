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
}
