using System;
using System.Text;
using NoticeBoard.Configs;
using NoticeBoard.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace NoticeBoard.BlockType;

public class NoticeBoardBlock : Block, IClaimTraverseable
{
    WorldInteraction[] interactions;

    protected bool isWallBoard;

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
                        ActionLangCode = "blockhelp-sign-write",
                        MouseButton = EnumMouseButton.Right,
                    },
                };
            }
        );
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
        IWorldAccessor world,
        BlockSelection selection,
        IPlayer forPlayer
    )
    {
        return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
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
                CodeWithParts("wall", blockSel.Face.Opposite.Code)
            );

            if (!wallblock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;

            world.BlockAccessor.SetBlock(wallblock.BlockId, blockSel.Position);

            blockPlaced = true;
        }
        else
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
                return false;
            BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);
            AssetLocation blockCode = CodeWithParts(horVer[0].Code);
            Block block = world.BlockAccessor.GetBlock(blockCode);
            world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);
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

        if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Block is NoticeBoardBlock)
            return base.OnBlockInteractStart(world, byPlayer, blockSel);

        if (
            world.BlockAccessor.GetBlockEntity(blockSel.Position)
            is NoticeBoardBlockEntity blockEntity
        )
        {
            if (string.IsNullOrEmpty(blockEntity.uniqueID))
                return false;

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

            //blockEntity.OnPlayerRightClick(byPlayer, blockSel);

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

    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity blockEntity)
        {
            world.Logger.Debug($"[NoticeBoard] BoardProperties: {blockEntity.BoardProperties != null}, BoardName: '{blockEntity.BoardProperties?.BoardName}'");

            if (!string.IsNullOrEmpty(blockEntity.BoardProperties?.BoardName))
                return blockEntity.BoardProperties.BoardName;
        }


        return base.GetPlacedBlockName(world, pos);
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

    public void ChangeBlockShape(IWorldAccessor world, BlockPos pos, int messageCount)
    {
        Block currentBlock = world.BlockAccessor.GetBlock(pos);

        BlockEntity currentEntity = world.BlockAccessor.GetBlockEntity(pos);

        if (currentEntity == null)
            return;

        TreeAttribute blockEntityData = new TreeAttribute();

        currentEntity.ToTreeAttributes(blockEntityData);

        Block newBlock = world.GetBlock(
            new AssetLocation(
                "noticeboard",
                $"noticeboard-{messageCount}-{currentBlock.Variant["attachment"]}-{currentBlock.Variant["side"]}"
            )
        );

        if (newBlock != null && newBlock.BlockId != currentBlock.BlockId)
        {
            world.BlockAccessor.SetBlock(newBlock.BlockId, pos);

            BlockEntity newEntity = world.BlockAccessor.GetBlockEntity(pos);

            if (newEntity != null)
            {
                newEntity.FromTreeAttributes(blockEntityData, world);
                newEntity.MarkDirty(true);
            }
        }
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

            if (edge == 0) // Top Edge
            {
                offsetX = (world.Rand.NextDouble() - 0.5) * frameWidth;
                offsetY = frameHeight / 2.0;
            }
            else if (edge == 1) // Bottom Edge
            {
                offsetX = (world.Rand.NextDouble() - 0.5) * frameWidth;
                offsetY = -frameHeight / 2.0;
            }
            else if (edge == 2) // Left Edge
            {
                offsetX = -frameWidth / 2.0;
                offsetY = (world.Rand.NextDouble() - 0.5) * frameHeight;
            }
            else // Right Edge
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

            if (world.Side == EnumAppSide.Server)
            {
                Block block = world.BlockAccessor.GetBlock(CodeWithParts("ground", "north"));

                if (block == null)
                    block = world.BlockAccessor.GetBlock(CodeWithParts("wall", "north"));

                if (block != null)
                {
                    ItemStack dropStack = new ItemStack(block);
                    ITreeAttribute invTree = new TreeAttribute();
                    blockEntity.Inventory?.ToTreeAttributes(invTree);
                    dropStack.Attributes["inventory"] = invTree;
                    dropStack.Attributes.SetString("uniqueID", blockEntity.uniqueID);
                    world.SpawnItemEntity(dropStack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }
            }
        }

        SpawnBlockBrokenParticles(pos, byPlayer);

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
