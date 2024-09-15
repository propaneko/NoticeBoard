using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using NoticeBoard.src;
using NoticeBoard.src.Packets;

namespace NoticeBoard
{
    public class NoticeBoardBlock : Block
    {
        string blockUniqueId;

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockPos blockPos = blockSel.Position;

            if (world.BlockAccessor.GetBlockEntity(blockPos) is NoticeBoardBlockEntity blockEntity)
            {
                blockUniqueId = blockEntity.uniqueID;
            }

            if (world.Side == EnumAppSide.Client)
            {
                PlayerCreateNoticeBoard sendPacket = new PlayerCreateNoticeBoard
                {
                    BoardId = blockUniqueId,
                    Pos = blockPos.ToLocalPosition(NoticeBoardModSystem.getCAPI()).ToString(),
                };

                NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(sendPacket);

                RequestAllMessages requestPacket = new RequestAllMessages
                {
                    BoardId = blockUniqueId,
                    PlayerId = byPlayer.PlayerUID
                };

                NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(requestPacket);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public void ChangeBlockShape(IWorldAccessor world, BlockPos pos, int messageCount)
        {
            Block currentBlock = world.BlockAccessor.GetBlock(pos);
            BlockEntity currentEntity = world.BlockAccessor.GetBlockEntity(pos);

            if (currentEntity == null) return;

            TreeAttribute blockEntityData = new TreeAttribute();
            currentEntity.ToTreeAttributes(blockEntityData);
           
            string[] splitPath = currentBlock.Code.Path.Split("-");
            Block newBlock = world.GetBlock(new AssetLocation("noticeboard", $"noticeboard-{messageCount}-{splitPath[splitPath.Length - 1]}"));

            // Replace the block with the new variant
            world.BlockAccessor.SetBlock(newBlock.BlockId, pos);
            BlockEntity newEntity = world.BlockAccessor.GetBlockEntity(pos);
            if (newEntity != null)
            {
                // Restore the entity data to the new BlockEntity
                newEntity.FromTreeAttributes(blockEntityData, world);
                newEntity.MarkDirty(true);
            }
        }
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity blockEntity)
            {
                blockUniqueId = blockEntity.uniqueID;
                world.Logger.Debug("Destroyed the block with ID: " + blockUniqueId);
            }

            if (world.Side == EnumAppSide.Client)
            {
                PlayerDestroyNoticeBoard sendPacket = new PlayerDestroyNoticeBoard
                {
                    BoardId = blockUniqueId,
                };

                NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(sendPacket);
            }

            // Your custom code goes here, for example:
            world.Api.Logger.Notification("Block at {0} was destroyed by {1}", pos, byPlayer.PlayerName);

            // You can also spawn custom items or trigger other events
            // Example: Drop a custom item when the block is destroyed
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            if (!world.Side.IsServer()) return; // Ensure only the server handles dropping items
            //ItemStack drop = new ItemStack(world.GetBlock(new AssetLocation("noticeboard:noticeboard-default-north")));
            //world.SpawnItemEntity(drop, pos.ToVec3d());
        }
    }
}
