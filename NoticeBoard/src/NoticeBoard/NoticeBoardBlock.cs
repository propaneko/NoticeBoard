using System;
using System.Collections.Generic;
using System.Text;
using AttributeRenderingLibrary;
using NoticeBoard.Configs;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

using Vintagestory.GameContent;

namespace NoticeBoard.BlockType;

public class NoticeBoardBlock : Block, IClaimTraverseable, IMultiBlockColSelBoxes
{
    WorldInteraction[] interactions;

    protected bool isWallBoard;

    private static readonly Dictionary<string, string> VariantDisplayNames = new()
    {
        ["aged"] = "Aged",
        ["veryaged"] = "Very Aged",
        ["birch"] = "Birch",
        ["oak"] = "Oak",
        ["maple"] = "Maple",
        ["pine"] = "Pine",
        ["acacia"] = "Acacia",
        ["kapok"] = "Kapok",
        ["baldcypress"] = "Bald Cypress",
        ["larch"] = "Larch",
        ["redwood"] = "Redwood",
        ["ebony"] = "Ebony",
        ["walnut"] = "Walnut",
        ["purpleheart"] = "Purpleheart",
        ["brass"] = "Brass",
        ["copper"] = "Copper",
        ["cupronickel"] = "Cupronickel",
        ["tinbronze"] = "Tin Bronze",
        ["bismuthbronze"] = "Bismuth Bronze",
        ["blackbronze"] = "Black Bronze",
        ["iron"] = "Iron",
        ["meteoriciron"] = "Meteoric Iron",
        ["steel"] = "Steel",
        ["gold"] = "Gold",
        ["silver"] = "Silver",
        ["bismuth"] = "Bismuth",
        ["molybdochalkos"] = "Molybdochalkos",
        ["electrum"] = "Electrum",
    };

    private static string GetVariantDisplayName(string code)
    {
        if (string.IsNullOrEmpty(code))
            return null;

        if (VariantDisplayNames.TryGetValue(code, out string displayName))
            return displayName;

        return char.ToUpperInvariant(code[0]) + code.Substring(1);
    }

    public static ITreeAttribute GetTypes(ItemStack stack)
    {
        return stack?.Attributes?.GetTreeAttribute("types");
    }

    public static void CopyTypesOnto(ItemStack stack, ITreeAttribute types)
    {
        if (stack?.Attributes == null || types == null)
            return;

        stack.Attributes["types"] = types.Clone();
    }

    internal static CompositeShape BoardCompositeShape(Block block, ITreeAttribute types)
    {
        string attachment = block.Variant?["attachment"] == "wall" ? "wall" : "ground";
        string count = types?.GetString("messageCount") ?? "0";
        if (count.Length != 1 || count[0] < '0' || count[0] > '6')
            count = "0";
        return new CompositeShape
        {
            Base = new AssetLocation("noticeboard", $"block/noticeboard-{attachment}-{count}"),
            rotateY = block.Variant?["side"] switch
            {
                "east" => 270f,
                "south" => 180f,
                "west" => 90f,
                _ => 0f,
            }
        };
    }

    private static string BuildDefaultName(Block block, ITreeAttribute types)
    {
        string woodName = GetVariantDisplayName(types?.GetString("wood"));
        string metalName = GetVariantDisplayName(types?.GetString("metal"));

        if (woodName == null || metalName == null)
            return Lang.Get(block.Code.Domain + ":block-" + block.Code.Path);

        return Lang.Get("noticeboard:block-noticeboard-name-with-material", woodName, metalName);
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        isWallBoard = Variant["attachment"] == "wall";

        interactions = ObjectCacheUtil.GetOrCreate(
            api,
            "noticeBoardInteraction",
            () =>
            {
                return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "noticeboard:blockhelp-open",
                        MouseButton = EnumMouseButton.Right,
                    },
                };
            }
        );
    }

    public override void OnBeforeRender(
        ICoreClientAPI capi,
        ItemStack itemstack,
        EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo
    )
    {
        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

        BlockBehaviorShapeTexturesFromAttributes beh =
            GetBehavior<BlockBehaviorShapeTexturesFromAttributes>();
        if (beh == null || itemstack == null)
            return;

        ItemSlot slot = renderinfo.InSlot ?? new DummySlot(itemstack);
        Dictionary<string, MultiTextureMeshRef> meshRefs = ObjectCacheUtil.GetOrCreate(
            capi,
            "noticeboard-gui-meshrefs",
            () => new Dictionary<string, MultiTextureMeshRef>()
        );
        string key = beh.GetMeshCacheKey(slot);
        if (!meshRefs.TryGetValue(key, out MultiTextureMeshRef meshref))
        {
            MeshData mesh = beh.GenGuiMesh(slot, BoardCompositeShape(this, GetTypes(itemstack)));
            meshref = capi.Render.UploadMultiTextureMesh(mesh);
            meshRefs[key] = meshref;
        }
        renderinfo.ModelRef = meshref;
        renderinfo.NormalShaded = true;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
        IWorldAccessor world,
        BlockSelection selection,
        IPlayer forPlayer
    )
    {
        NoticeBoardBlockEntity helpBe = world.Api.GetNoticeBoardEntity(selection.Position);
        if (helpBe == null || !PaperPinController.IsWithinInteractDistance(forPlayer, helpBe.Pos))
            return Array.Empty<WorldInteraction>();

        var extra = new List<WorldInteraction>();

        if (helpBe.HasNotices)
        {
            extra.Add(new WorldInteraction()
            {
                MouseButton = EnumMouseButton.None,
                HotKeyCode = "noticeboardpreview",
                ActionLangCode = "noticeboard:hotkey-preview",
            });
        }

        if (helpBe.Inventory != null)
        {
            bool hasLeft = helpBe.Inventory.Count > 5 && !helpBe.Inventory[5].Empty;
            bool hasRight = helpBe.Inventory.Count > 6 && !helpBe.Inventory[6].Empty;
            bool room = (helpBe.Inventory.Count > 5 && helpBe.Inventory[5].Empty)
                || (helpBe.Inventory.Count > 6 && helpBe.Inventory[6].Empty);

            if (room)
            {
                extra.Add(new WorldInteraction()
                {
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "ctrl",
                    ActionLangCode = "noticeboard:lantern-help-attach",
                });
            }

            if (hasLeft || hasRight)
            {
                extra.Add(new WorldInteraction()
                {
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "ctrl",
                    ActionLangCode = "noticeboard:lantern-help-remove",
                });
            }
        }

        return interactions
            .Append(extra.ToArray())
            .Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }

    public void InitializeNoticeBoard(
        IWorldAccessor world,
        IPlayer byPlayer,
        ItemStack itemstack,
        BlockSelection blockSel,
        ref string failureCode
    )
    {
        if (
            world.BlockAccessor.GetBlockEntity(blockSel.Position)
            is NoticeBoardBlockEntity blockEntity
        )
        {
            if (itemstack != null && itemstack.Attributes.HasAttribute("uniqueID"))
            {
                blockEntity.uniqueID = itemstack.Attributes.GetString("uniqueID");
            }
            else if (string.IsNullOrEmpty(blockEntity.uniqueID))
            {
                if (world.Side == EnumAppSide.Server)
                {
                    blockEntity.GenerateUniqueID();
                }
            }

            if (itemstack != null && itemstack.Attributes.HasAttribute("inventory"))
            {
                ITreeAttribute invTree = itemstack.Attributes.GetTreeAttribute("inventory");
                blockEntity.Inventory.FromTreeAttributes(invTree);
            }

            if (itemstack != null && itemstack.Attributes.HasAttribute("restoreWood"))
                blockEntity.restoreWood = itemstack.Attributes.GetString("restoreWood");
            if (itemstack != null && itemstack.Attributes.HasAttribute("restoreMetal"))
                blockEntity.restoreMetal = itemstack.Attributes.GetString("restoreMetal");

            ITreeAttribute stackTypes = GetTypes(itemstack);
            if (stackTypes != null)
                blockEntity.SetTypes(stackTypes);
            blockEntity.EnsureDefaultTypes();

            blockEntity.MarkDirty(true);
        }
    }

    public override bool TryPlaceBlock(
        IWorldAccessor world,
        IPlayer byPlayer,
        ItemStack itemstack,
        BlockSelection blockSel,
        ref string failureCode
    )
    {
        BlockPos supportingPos = blockSel.Position.AddCopy(blockSel.Face.Opposite);

        Block supportingBlock = world.BlockAccessor.GetBlock(supportingPos);

        bool blockPlaced = false;

        if (
            blockSel.Face.IsHorizontal
            && (
                supportingBlock.CanAttachBlockAt(
                    world.BlockAccessor,
                    this,
                    supportingPos,
                    blockSel.Face
                )
                || supportingBlock
                    .GetAttributes(world.BlockAccessor, supportingPos)
                    ?.IsTrue("partialAttachable") == true
            )
        )
        {
            Block wallblock = world.BlockAccessor.GetBlock(
                CodeWithVariants(
                    new[] { "attachment", "side" },
                    new[] { "wall", blockSel.Face.Opposite.Code }
                )
            );

            if (!wallblock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;

            world.BlockAccessor.SetBlock(wallblock.BlockId, blockSel.Position, itemstack);

            blockPlaced = true;
        }
        else
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;
            BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);
            AssetLocation blockCode = CodeWithVariant("side", horVer[0].Code);
            Block block = world.BlockAccessor.GetBlock(blockCode);
            world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position, itemstack);
            blockPlaced = true;
        }

        if (blockPlaced)
        {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is NoticeBoardBlockEntity be)
            {
                InitializeNoticeBoard(world, byPlayer, itemstack, blockSel, ref failureCode);

                if (world.Side == EnumAppSide.Server)
                {
                    NoticeBoard.Database.SQLiteHandler db =
                        new NoticeBoard.Database.SQLiteHandler();

                    PlayerCreateNoticeBoard creationData = new PlayerCreateNoticeBoard
                    {
                        PlayerId = byPlayer.PlayerUID,

                        BoardId = be.uniqueID,

                        Pos = blockSel.Position.ToString(),
                    };

                    NoticeBoardObject noticeBoard = db.GetBoardData(be.uniqueID);

                    if (noticeBoard != null && noticeBoard.BoardId == be.uniqueID)
                    {
                        db.UpdateNoticeBoard(creationData);
                    }
                    else
                    {
                        db.CreateNoticeBoard(creationData);
                    }

                    bool isWall = be.Block?.Variant?["attachment"] == "wall";
                    db.SyncMessagePinsToAttachment(be.uniqueID, isWall);
                    be.RefreshPaperVisuals();
                }
            }
        }

        return blockPlaced;
    }

    public override bool OnBlockInteractStart(
       IWorldAccessor world,
       IPlayer byPlayer,
       BlockSelection blockSel
   )
    {
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
            return false;

        if (world.Side == EnumAppSide.Client && PaperPinController.Instance != null && PaperPinController.Instance.IsActive)
            return PaperPinController.Instance.TryConfirm(world, byPlayer, blockSel);

        NoticeBoardBlockEntity reachBe = world.Api.GetNoticeBoardEntity(blockSel.Position);
        if (reachBe != null && !PaperPinController.IsWithinInteractDistance(byPlayer, reachBe.Pos))
            return false;

        if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Block is NoticeBoardBlock)
            return base.OnBlockInteractStart(world, byPlayer, blockSel);

        if (
            world.BlockAccessor.GetBlockEntity(blockSel.Position)
            is NoticeBoardBlockEntity blockEntity
        )
        {
            if (string.IsNullOrEmpty(blockEntity.uniqueID))
                return false;

            if (byPlayer.Entity.Controls.CtrlKey && TryLanternInteract(world, byPlayer, blockEntity))
                return true;

            if (world.Side == EnumAppSide.Client)
            {
                RequestAllMessages requestPacket = new RequestAllMessages
                {
                    BoardId = blockEntity.uniqueID,
                    PlayerId = byPlayer.PlayerUID,
                };

                NoticeBoardModSystem
                    .getCAPI()
                    .Network.GetChannel("noticeboard")
                    .SendPacket(requestPacket);

            }

            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override void OnBlockPlaced(
        IWorldAccessor world,
        BlockPos blockPos,
        ItemStack byItemStack = null
    )
    {
        base.OnBlockPlaced(world, blockPos, byItemStack);
    }

    public override void GetHeldItemInfo(
        ItemSlot inSlot,
        StringBuilder dsc,
        IWorldAccessor world,
        bool withDebugInfo
    )
    {
        if (inSlot.Itemstack.Attributes.HasAttribute("uniqueID"))
        {
            dsc.AppendLine(
                Lang.Get($"<font color=\"#99c9f9\"><i>It has some messages attached</i></font> \n")
            );
        }

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }

    public override void OnCreatedByCrafting(
        ItemSlot[] allInputslots,
        ItemSlot outputSlot,
        IRecipeBase byRecipe
    )
    {
        base.OnCreatedByCrafting(allInputslots, outputSlot, byRecipe);
        outputSlot.Itemstack?.Attributes?.RemoveAttribute("uniqueID");
    }

    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity blockEntity)
        {
            world.Logger.Debug($"[NoticeBoard] BoardProperties: {blockEntity.BoardProperties != null}, BoardName: '{blockEntity.BoardProperties?.BoardName}'");

            if (!string.IsNullOrEmpty(blockEntity.BoardProperties?.BoardName))
                return blockEntity.BoardProperties.BoardName;

            return BuildDefaultName(world.BlockAccessor.GetBlock(pos), blockEntity.GetTypes());
        }

        return BuildDefaultName(world.BlockAccessor.GetBlock(pos), null);
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        return BuildDefaultName(itemStack.Block, GetTypes(itemStack));
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity blockEntity
            && blockEntity.BoardProperties != null)
        {
            var props = blockEntity.BoardProperties;
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(props.BoardName))
                sb.AppendLine($"Name: {props.BoardName}");

            if (!string.IsNullOrEmpty(props.PlayerName))
                sb.AppendLine($"Owner: {props.PlayerName}");

            string modeLabel = props.PermissionMode switch
            {
                (int)BoardPermissionMode.All => Lang.Get("noticeboard:blockinfo-mode-all"),
                (int)BoardPermissionMode.Locked => Lang.Get("noticeboard:blockinfo-mode-locked"),
                _ => Lang.Get("noticeboard:blockinfo-mode-default"),
            };
            sb.AppendLine(modeLabel);

            return sb.ToString().TrimEnd();
        }

        return string.Empty;
    }

    public void SpawnUnreadParticles(IWorldAccessor world, BlockPos pos)
    {
        if (world.Side != EnumAppSide.Client)
            return;

        Block block = world.BlockAccessor.GetBlock(pos);
        string facing = block?.Variant?["side"] ?? "north";
        double yOffset = isWallBoard ? 1.5 : 2.5;
        Vec3d center = pos.ToVec3d().Add(0.5, yOffset, 0.5);

        switch (facing)
        {
            case "north":
                center.X += 1.0;
                break;

            case "south":
                center.X -= 1.0;
                break;

            case "east":
                center.Z += 1.0;
                break;

            case "west":
                center.Z -= 1.0;
                break;
        }

        int count = 42;

        Vec3d sharedPos = new Vec3d();

        Vec3f sharedVel = new Vec3f();

        SimpleParticleProperties props = new SimpleParticleProperties(
            1,
            1,
            0,
            sharedPos,
            sharedPos,
            sharedVel,
            sharedVel,
            4.5f,
            -0.001f,
            0.14f,
            0.14f,
            EnumParticleModel.Quad
        );

        props.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.14f);

        double frameWidth = 2.6;
        double frameHeight = 2;
        double verticalOffset = -0.35;
        double fuzziness = 0.25;

        for (int i = 0; i < count; i++)
        {
            sharedPos.Set(center.X, center.Y + verticalOffset, center.Z);

            int edge = world.Rand.Next(4);

            double offsetX = 0;
            double offsetY = 0;

            if (edge == 0)
            {
                offsetX = (world.Rand.NextDouble() - 0.5) * frameWidth;
                offsetY = frameHeight / 2.0;
            }
            else if (edge == 1)
            {
                offsetX = (world.Rand.NextDouble() - 0.5) * frameWidth;
                offsetY = -frameHeight / 2.0;
            }
            else if (edge == 2)
            {
                offsetX = -frameWidth / 2.0;
                offsetY = (world.Rand.NextDouble() - 0.5) * frameHeight;
            }
            else
            {
                offsetX = frameWidth / 2.0;
                offsetY = (world.Rand.NextDouble() - 0.5) * frameHeight;
            }

            offsetX += (world.Rand.NextDouble() - 0.5) * fuzziness;
            offsetY += (world.Rand.NextDouble() - 0.5) * fuzziness;

            if (facing == "north" || facing == "south")
            {
                sharedPos.X += offsetX;
            }
            else
            {
                sharedPos.Z += offsetX;
            }

            sharedPos.Y += offsetY;
            double forwardOffsetBase = isWallBoard ? 0.25 : -0.1;
            double forwardOffset = forwardOffsetBase + (world.Rand.NextDouble() * 0.15);

            switch (facing)
            {
                case "north":
                    sharedPos.Z -= forwardOffset;
                    break;
                case "south":
                    sharedPos.Z += forwardOffset;
                    break;
                case "east":
                    sharedPos.X += forwardOffset;
                    break;
                case "west":
                    sharedPos.X -= forwardOffset;
                    break;
            }

            props.Color = GetRandomGoldColor(world.Rand);

            sharedVel.Set(
                (float)(world.Rand.NextDouble() - 0.5) * 0.15f,
                (float)(world.Rand.NextDouble() * 0.12f - 0.15f),
                (float)(world.Rand.NextDouble() - 0.5) * 0.15f
            );

            world.SpawnParticles(props);
        }
    }

    private int GetRandomGoldColor(Random rand)
    {
        int variation = (int)rand.NextInt64(5);

        return variation switch
        {
            0 => ColorUtil.ToRgba(240, 255, 220, 60),
            1 => ColorUtil.ToRgba(235, 255, 200, 50),
            2 => ColorUtil.ToRgba(245, 255, 180, 70),
            3 => ColorUtil.ToRgba(230, 255, 140, 40),
            _ => ColorUtil.ToRgba(250, 255, 230, 90),
        };
    }

    public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor ba, BlockPos pos, Vec3i offsetInv)
        => OffsetedIfIntersects(SelectionBoxes, offsetInv);

    public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor ba, BlockPos pos, Vec3i offsetInv)
        => OffsetedIfIntersects(CollisionBoxes, offsetInv);

    public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos) => true;

    private static Cuboidf[] OffsetedIfIntersects(Cuboidf[] boxes, Vec3i offsetInv)
    {
        if (boxes == null || boxes.Length == 0)
            return Array.Empty<Cuboidf>();

        var result = new List<Cuboidf>(boxes.Length);
        foreach (Cuboidf box in boxes)
        {
            Cuboidf copy = box.Clone();
            copy.Offset(offsetInv.X, offsetInv.Y, offsetInv.Z);
            if (IntersectsUnitCube(copy))
                result.Add(copy);
        }

        return result.Count > 0 ? result.ToArray() : Array.Empty<Cuboidf>();
    }

    private static bool IntersectsUnitCube(Cuboidf box)
    {
        float minX = Math.Min(box.X1, box.X2);
        float maxX = Math.Max(box.X1, box.X2);
        float minY = Math.Min(box.Y1, box.Y2);
        float maxY = Math.Max(box.Y1, box.Y2);
        float minZ = Math.Min(box.Z1, box.Z2);
        float maxZ = Math.Max(box.Z1, box.Z2);
        return maxX > 0 && minX < 1 && maxY > 0 && minY < 1 && maxZ > 0 && minZ < 1;
    }

    private static bool TryLanternInteract(
        IWorldAccessor world,
        IPlayer byPlayer,
        NoticeBoardBlockEntity blockEntity
    )
    {
        if (blockEntity.Inventory == null || blockEntity.Inventory.Count <= 6)
            return false;

        ItemSlot left = blockEntity.Inventory[5];
        ItemSlot right = blockEntity.Inventory[6];
        ItemSlot hotbar = byPlayer.InventoryManager.ActiveHotbarSlot;
        bool emptyHand = hotbar == null || hotbar.Empty;

        if (LanternHolderSlot.IsSmallLantern(hotbar?.Itemstack))
        {
            ItemSlot dest = left.Empty ? left : (right.Empty ? right : null);
            if (dest == null)
                return false;

            int moved = hotbar.TryPutInto(world, dest, 1);
            if (moved <= 0)
                return false;

            PlayLanternSound(world, blockEntity.Pos, byPlayer);
            blockEntity.MarkDirty(true);
            world.BlockAccessor.MarkBlockDirty(blockEntity.Pos);
            return true;
        }

        if (!emptyHand)
            return false;

        ItemSlot src = !right.Empty ? right : (!left.Empty ? left : null);
        if (src == null)
            return false;

        ItemStack taken = src.TakeOutWhole();
        if (taken != null && !byPlayer.InventoryManager.TryGiveItemstack(taken, true))
            world.SpawnItemEntity(taken, blockEntity.Pos.ToVec3d().Add(0.5, 0.5, 0.5));

        src.MarkDirty();
        PlayLanternSound(world, blockEntity.Pos, byPlayer);
        blockEntity.MarkDirty(true);
        world.BlockAccessor.MarkBlockDirty(blockEntity.Pos);
        return true;
    }

    private static void PlayLanternSound(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
    {
        world.PlaySoundAt(new AssetLocation("sounds/block/planks"), pos, 0, byPlayer);
    }

    public override void OnBlockBroken(
        IWorldAccessor world,
        BlockPos pos,
        IPlayer byPlayer,
        float dropQuantityMultiplier = 1
    )
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity blockEntity)
        {
            world.Logger.Debug("Destroyed the block with ID: " + blockEntity.uniqueID);
        }

        if (
            world.Side == EnumAppSide.Server
            && byPlayer != null
            && byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative
        )
        {
            ItemStack[] drops = GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
            if (drops != null)
            {
                for (int i = 0; i < drops.Length; i++)
                {
                    if (drops[i] == null)
                        continue;
                    world.SpawnItemEntity(drops[i].Clone(), pos, null);
                }
            }
        }

        SpawnBlockBrokenParticles(pos, byPlayer);

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override ItemStack[] GetDrops(
        IWorldAccessor world,
        BlockPos pos,
        IPlayer byPlayer,
        float dropQuantityMultiplier = 1
    )
    {
        if (
            world.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity blockEntity
            && world.BlockAccessor.GetBlock(pos) is Block noticeBoardBlock
        )
        {
            Block dropBlock = world.GetBlock(
                noticeBoardBlock.CodeWithVariants(
                    new[] { "attachment", "side" },
                    new[] { "ground", "north" }
                )
            );
            if (dropBlock == null)
                dropBlock = noticeBoardBlock;

            ItemStack dropStack = new ItemStack(dropBlock);

            ITreeAttribute invTree = new TreeAttribute();
            blockEntity.Inventory?.ToTreeAttributes(invTree);
            dropStack.Attributes["inventory"] = invTree;
            dropStack.Attributes.SetString("uniqueID", blockEntity.uniqueID);
            if (!string.IsNullOrEmpty(blockEntity.restoreWood))
                dropStack.Attributes.SetString("restoreWood", blockEntity.restoreWood);
            if (!string.IsNullOrEmpty(blockEntity.restoreMetal))
                dropStack.Attributes.SetString("restoreMetal", blockEntity.restoreMetal);
            CopyTypesOnto(dropStack, blockEntity.GetTypes());

            return new ItemStack[] { dropStack };
        }

        return base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
