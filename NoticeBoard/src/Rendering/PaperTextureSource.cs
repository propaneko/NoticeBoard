using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Rendering;

public class PaperTextureSource : ITexPositionSource
{
    private readonly ITexPositionSource baseSource;
    private readonly string fromCode;
    private readonly string toCode;

    public PaperTextureSource(ITexPositionSource baseSource, string fromCode, string toCode)
    {
        this.baseSource = baseSource;
        this.fromCode = fromCode;
        this.toCode = toCode;
    }

    // Remap one shape texture code onto another so a shape that expects #paperlabel can use a variant.
    public TextureAtlasPosition this[string textureCode] =>
        baseSource[textureCode == fromCode ? toCode : textureCode];

    public Size2i AtlasSize => baseSource.AtlasSize;
}

public static class PaperTextureVariants
{
    private const int VariantCount = 6;

    // Consecutive message ids need to spread across all variants, so this runs a full avalanche
    // mix instead of a single multiply, which would leave low ids stuck on half the textures.
    public static int IndexFor(int messageId)
    {
        unchecked
        {
            uint hash = (uint)messageId * 0x9E3779B1u;
            hash ^= hash >> 16;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            hash ^= hash >> 16;
            return (int)(hash % VariantCount);
        }
    }
}
