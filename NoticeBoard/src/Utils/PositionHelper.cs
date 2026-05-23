using System;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Utils;

public static class PositionHelper
{
    /// <summary>
    /// Converts a standard Vintage Story BlockPos.ToString() string back into a BlockPos object.
    /// </summary>
    public static BlockPos FromString(string posString)
    {
        if (string.IsNullOrEmpty(posString))
            return null;

        try
        {
            string[] coordinates = posString.Split(',');

            if (coordinates.Length >= 3)
            {
                int.TryParse(coordinates[0], out int x);
                int.TryParse(coordinates[1], out int y);
                int.TryParse(coordinates[2], out int z);

                int dim = 0;
                if (coordinates.Length >= 4)
                {
                    int.TryParse(coordinates[3], out dim);
                }

                return new BlockPos(x, y, z, dim);
            }
        }
        catch (Exception) { }

        return null;
    }
}
