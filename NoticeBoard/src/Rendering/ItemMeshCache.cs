using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace NoticeBoard.Rendering;

public readonly struct ItemMeshInfo
{
    public ItemMeshInfo(MeshData mesh, int atlasTextureId)
    {
        Mesh = mesh;
        AtlasTextureId = atlasTextureId;
    }

    public MeshData Mesh { get; }
    public int AtlasTextureId { get; }
}

// Process-lifetime tessellation for holders (TESR and pin ghost). Not used for <itemstack>
// on paper. Null results are cached too. Bind GetPosition.atlasTextureId, not TextureIds[0]
// (tessellated items leave that empty or as a page index) and not always atlas page 0
// (gold/silver ingots often land on a later sheet).
public static class ItemMeshCache
{
    private static readonly Dictionary<string, ItemMeshInfo?> cache = new();

    public static bool TryGet(ICoreClientAPI api, string code, bool isBlock, out ItemMeshInfo info)
    {
        string key = (isBlock ? "block:" : "item:") + code;

        if (cache.TryGetValue(key, out ItemMeshInfo? cached))
        {
            info = cached ?? default;
            return cached.HasValue;
        }

        ItemMeshInfo? built = Build(api, code, isBlock);
        cache[key] = built;
        info = built ?? default;
        return built.HasValue;
    }

    private static ItemMeshInfo? Build(ICoreClientAPI api, string code, bool isBlock)
    {
        try
        {
            var location = new AssetLocation(code);

            if (isBlock)
            {
                Block block = api.World.GetBlock(location);
                if (block == null)
                    return null;

                api.Tesselator.TesselateBlock(block, out MeshData mesh);
                int texId = AtlasId(api.BlockTextureAtlas.GetPosition(block, null, false));
                if (texId == 0)
                    texId = api.BlockTextureAtlas.AtlasTextures[0].TextureId;
                return new ItemMeshInfo(mesh, texId);
            }

            Item item = api.World.GetItem(location);
            if (item == null)
                return null;

            api.Tesselator.TesselateItem(item, out MeshData itemMesh);
            int itemTexId = AtlasId(api.ItemTextureAtlas.GetPosition(item, null, false));
            if (itemTexId == 0)
                itemTexId = api.ItemTextureAtlas.AtlasTextures[0].TextureId;
            return new ItemMeshInfo(itemMesh, itemTexId);
        }
        catch (Exception ex)
        {
            api.Logger.Warning($"[NoticeBoard] Failed to build preview mesh for '{code}': {ex.Message}");
            return null;
        }
    }

    private static int AtlasId(TextureAtlasPosition pos)
    {
        if (pos != null && pos.atlasTextureId != 0)
            return pos.atlasTextureId;
        return 0;
    }
}
