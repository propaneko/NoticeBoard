using Vintagestory.API.Common;

namespace NoticeBoard;

public readonly struct MessageHolderDef
{
    public MessageHolderDef(
        int id,
        string langKey,
        float originX,
        float originY,
        float originZ,
        float scale,
        float rotXDeg,
        float rotYDeg,
        float rotZDeg
    )
    {
        Id = id;
        LangKey = langKey;
        OriginX = originX;
        OriginY = originY;
        OriginZ = originZ;
        Scale = scale;
        RotXDeg = rotXDeg;
        RotYDeg = rotYDeg;
        RotZDeg = rotZDeg;
    }

    public int Id { get; }
    public string LangKey { get; }
    public float OriginX { get; }
    public float OriginY { get; }
    public float OriginZ { get; }
    public float Scale { get; }
    public float RotXDeg { get; }
    public float RotYDeg { get; }
    public float RotZDeg { get; }
    public bool UsesItemMesh => Id != MessageHolder.Nail;
    public bool UsesCornerNails => Id == MessageHolder.Nails;
}

public static class MessageHolder
{
    public const int Nail = 0;
    public const int Knife = 1;
    public const int Arrow = 2;
    public const int Stick = 3;
    public const int Bone = 4;
    public const int Spear = 5;
    public const int Chisel = 6;
    public const int Nails = 7;
    public const int Cleaver = 8;
    public const int MaxId = 8;
    public const float LeanDeg = 5f;

    public static int Clamp(int holder)
    {
        if (holder == 9)
            return Cleaver;
        return holder < Nail || holder > MaxId ? Nail : holder;
    }

    public static readonly MessageHolderDef[] All =
    {
        new(Nail, "noticeboard:add-notice-window-holder-nail", 0, 0, 0, 1, 0, 0, 0),
        new(Knife, "noticeboard:add-notice-window-holder-knife", -0.66f, -0.06f, -0.50f, 1f, 0, 90, 0),
        new(Arrow, "noticeboard:add-notice-window-holder-arrow", -1.05f, -0.03f, -0.50f, 0.40f, 0, 90, 0),
        new(Stick, "noticeboard:add-notice-window-holder-stick", -0.50f, -0.05f, -0.50f, 0.45f, 0, 150, 0),
        new(Bone, "noticeboard:add-notice-window-holder-bone", -0.50f, -0.25f, -0.50f, 1f, 90, 0, 0),
        new(Spear, "noticeboard:add-notice-window-holder-spear", -1.15f, -0.04f, -0.50f, 0.60f, 0, 90, 0),
        new(Chisel, "noticeboard:add-notice-window-holder-chisel", -0.75f, -0.04f, -0.50f, 0.70f, 0, 90, 0),
        new(Nails, "noticeboard:add-notice-window-holder-nails", 0, 0, 0, 1, 0, 0, 0),
        new(Cleaver, "noticeboard:add-notice-window-holder-cleaver", -0.18f, -0.35f, -0.50f, 0.70f, 40, -90, 0),
    };

    public static MessageHolderDef Get(int holder) => All[Clamp(holder)];

    public static void HolderLean(int messageId, int salt, out float lx, out float ly, out float lz)
    {
        lx = SignedUnit(messageId, salt) * LeanDeg;
        ly = SignedUnit(messageId, salt + 17) * LeanDeg;
        lz = SignedUnit(messageId, salt + 31) * LeanDeg;
    }

    private static float SignedUnit(int messageId, int salt)
    {
        unchecked
        {
            uint hash = (uint)messageId * 0x9E3779B1u + (uint)salt * 0x85EBCA6Bu;
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return hash / (float)uint.MaxValue * 2f - 1f;
        }
    }

    public static string ItemCode(int holder, string boardMetal)
    {
        switch (Clamp(holder))
        {
            case Knife:
                return "game:knife-generic-" + MapMetal(boardMetal);
            case Arrow:
                return "game:arrow-" + MapMetal(boardMetal);
            case Stick:
                return "game:stick";
            case Bone:
                return "game:bone";
            case Spear:
                return "game:spear-generic-" + MapMetal(boardMetal);
            case Chisel:
                return "game:chisel-" + MapMetal(boardMetal);
            case Cleaver:
                return "game:cleaver-" + MapMetal(boardMetal);
            default:
                return null;
        }
    }

    public static string ItemCode(int holder, string boardMetal, IWorldAccessor world)
    {
        string code = ItemCode(holder, boardMetal);
        if (world == null || string.IsNullOrEmpty(code))
            return code;
        if (world.GetItem(new AssetLocation(code)) != null)
            return code;
        return ItemCode(holder, "copper");
    }

    public static string MapMetal(string boardMetal)
    {
        switch (boardMetal)
        {
            case "copper":
            case "tinbronze":
            case "bismuthbronze":
            case "blackbronze":
            case "gold":
            case "silver":
            case "iron":
            case "meteoriciron":
            case "steel":
                return boardMetal;
            case "bismuth":
                return "bismuthbronze";
            case "electrum":
                return "gold";
            default:
                return "copper";
        }
    }
}
