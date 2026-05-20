using NoticeBoard.Packets;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace NoticeBoard.BlockType;
public class NoticeBoardBlock : Block
{
    WorldInteraction[] interactions;

    protected bool isWallBoard;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        isWallBoard = Variant["attachment"] == "wall";

        interactions = ObjectCacheUtil.GetOrCreate(api, "noticeBoardInteraction", () =>
        {
            return new WorldInteraction[] { new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-sign-write",
                        MouseButton = EnumMouseButton.Right,
                    }
                };
        });
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }

    public void InitializeNoticeBoard(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is NoticeBoardBlockEntity blockEntity)
        {
            if (blockEntity.uniqueID != null)
            {
                return;
            }

            if (itemstack != null && itemstack.Attributes.HasAttribute("uniqueID"))
            {
                string uniqueIdFromItemStack = itemstack.Attributes.GetString("uniqueID");
                blockEntity.uniqueID = uniqueIdFromItemStack;
                blockEntity.MarkDirty(true); // Ensure the block entity updates
            } else if (blockEntity.uniqueID == null)
            {
                blockEntity.GenerateUniqueID();
            }
        }
    }
    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        BlockPos supportingPos = blockSel.Position.AddCopy(blockSel.Face.Opposite);
        Block supportingBlock = world.BlockAccessor.GetBlock(supportingPos);

        NoticeBoardBlockEntity bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as NoticeBoardBlockEntity;

        if (blockSel.Face.IsHorizontal && (supportingBlock.CanAttachBlockAt(world.BlockAccessor, this, supportingPos, blockSel.Face) || supportingBlock.GetAttributes(world.BlockAccessor, supportingPos)?.IsTrue("partialAttachable") == true))
        {
            Block wallblock = world.BlockAccessor.GetBlock(CodeWithParts("wall", blockSel.Face.Opposite.Code));

            if (!wallblock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            world.BlockAccessor.SetBlock(wallblock.BlockId, blockSel.Position);

            InitializeNoticeBoard(world, byPlayer, itemstack, blockSel, ref failureCode);

            return true;
        }

        if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
        {
            return false;
        }

        BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);
        AssetLocation blockCode = CodeWithParts(horVer[0].Code);
        Block block = world.BlockAccessor.GetBlock(blockCode);
        world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);

        if (bect != null)
        {
            BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
            double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
            double dz = (float)byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
            float angleHor = (float)Math.Atan2(dx, dz);

            float deg45 = GameMath.PIHALF / 2;
            float roundRad = ((int)Math.Round(angleHor / deg45)) * deg45;
        }

        InitializeNoticeBoard(world, byPlayer, itemstack, blockSel, ref failureCode);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockPos absolutePos = blockSel.Position;
        string absolutePosString = absolutePos.ToString();

        if (world.BlockAccessor.GetBlockEntity(absolutePos) is NoticeBoardBlockEntity blockEntity)
        {
            if (world.Side == EnumAppSide.Client)
            {

                PlayerCreateNoticeBoard sendPacket = new PlayerCreateNoticeBoard
                {
                    PlayerId = byPlayer.PlayerUID,
                    BoardId = blockEntity.uniqueID,
                    Pos = absolutePosString,
                };

                NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(sendPacket);

                RequestAllMessages requestPacket = new RequestAllMessages
                {
                    BoardId = blockEntity.uniqueID,
                    PlayerId = byPlayer.PlayerUID
                };

                NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(requestPacket);
            }
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(world, blockPos, byItemStack);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        if (inSlot.Itemstack.Attributes.HasAttribute("uniqueID"))
        {
            dsc.AppendLine(Lang.Get($"<font color=\"#99c9f9\"><i>It has some messages attached</i></font> \n"));
        }

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }

    public void ChangeBlockShape(IWorldAccessor world, BlockPos pos, int messageCount)
    {
        Block currentBlock = world.BlockAccessor.GetBlock(pos);
        BlockEntity currentEntity = world.BlockAccessor.GetBlockEntity(pos);
        if (currentEntity == null) return;

        TreeAttribute blockEntityData = new TreeAttribute();
        currentEntity.ToTreeAttributes(blockEntityData);
       
        string[] splitPath = currentBlock.Code.Path.Split("-");
        Block newBlock = world.GetBlock(new AssetLocation("noticeboard", $"noticeboard-{messageCount}-{currentBlock.Variant["attachment"]}-{currentBlock.Variant["side"]}"));

        if (newBlock != null)
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
        if (world.Side != EnumAppSide.Server) return;

        Block block = world.BlockAccessor.GetBlock(pos);
        string facing = block?.Variant?["side"] ?? "north";

        Vec3d center = pos.ToVec3d().Add(0.5, 2.50, 0.5);

        switch (facing)
        {
            case "north": center.X += 1.0; break;
            case "south": center.X -= 1.0; break;
            case "east": center.Z += 1.0; break;
            case "west": center.Z -= 1.0; break;
        }

        int count = 32;

        Vec3d sharedPos = new Vec3d();
        Vec3f sharedVel = new Vec3f();

        SimpleParticleProperties props = new SimpleParticleProperties(
            1, 1,
            0,                    
            sharedPos, sharedPos,
            sharedVel, sharedVel,
            4.5f,
            -0.004f,
            0.14f, 0.14f,
            EnumParticleModel.Quad
        );

        props.SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.14f);

        for (int i = 0; i < count; i++)
        {
            sharedPos.Set(center.X, center.Y, center.Z);

            sharedPos.Y += 0.65 + (world.Rand.NextDouble() - 0.5) * 0.3;

            if (facing == "north" || facing == "south")
            {
                sharedPos.X += (world.Rand.NextDouble() - 0.5) * 2.8;
            }
            else
            {
                sharedPos.Z += (world.Rand.NextDouble() - 0.5) * 2.8;
            }

            sharedPos.Z += (world.Rand.NextDouble() - 0.5) * 0.3;

            double forwardOffset = -0.1 + world.Rand.NextDouble() * 0.25;

            switch (facing)
            {
                case "north": sharedPos.Z -= forwardOffset; break;
                case "south": sharedPos.Z += forwardOffset; break;
                case "east": sharedPos.X += forwardOffset; break;
                case "west": sharedPos.X -= forwardOffset; break;
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
            _ => ColorUtil.ToRgba(250, 255, 230, 90)
        };
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity blockEntity)
        {
            world.Logger.Debug("Destroyed the block with ID: " + blockEntity.uniqueID);

            if (world.Side == EnumAppSide.Server)
            {
                Block block = world.BlockAccessor.GetBlock(CodeWithParts("ground", "north"));
                if (block == null) block = world.BlockAccessor.GetBlock(CodeWithParts("wall", "north"));
                ItemStack[] dropStacks = new ItemStack[] { new ItemStack(block) };

                if (dropStacks != null)
                {
                    foreach (ItemStack stack in dropStacks)
                    {
                        if (stack != null)
                        {
                            stack.Attributes.SetString("uniqueID", blockEntity.uniqueID);
                            world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.5, 0.5));
                        }
                    }
                }
            }
        }

        SpawnBlockBrokenParticles(pos, byPlayer);
        world.BlockAccessor.SetBlock(0, pos);
        if (!world.Side.IsServer()) return; // Ensure only the server handles dropping items
    }
}
