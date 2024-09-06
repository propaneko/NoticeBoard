using Vintagestory.API.Common;
using Vintagestory.API.Client;    // For client-side interaction, if needed (e.g., custom block rendering).
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace NoticeBoard.src.NoticeBoardBlock
{
    public class NoticeBoardBlock : Block
    {
        string blockUniqueId;

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockPos blockPos = blockSel.Position;

            //ChangeBlockShape(world, blockPos);

            if (world.BlockAccessor.GetBlockEntity(blockPos) is NoticeBoardBlockEntity blockEntity)
            {
                blockUniqueId = blockEntity.uniqueID;
                //world.Logger.Debug("Unique ID of the block: " + blockUniqueId);
            }

            if (world.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (byPlayer as IClientPlayer)?.Entity?.World?.Api as ICoreClientAPI;
                if (capi != null)
                {
                    GuiDialogCustom myGui = new GuiDialogCustom("Custom GUI", blockUniqueId, capi);
                    myGui.TryOpen();
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public void ChangeBlockShape(IWorldAccessor world, BlockPos pos)
        {
            Block currentBlock = world.BlockAccessor.GetBlock(pos);
            BlockEntity currentEntity = world.BlockAccessor.GetBlockEntity(pos);

            if (currentEntity == null) return;

            TreeAttribute blockEntityData = new TreeAttribute();
            currentEntity.ToTreeAttributes(blockEntityData);

            if (currentBlock.Code.Path.Contains("default"))
            {
                // Get the block variant with the different shape
                string[] splitPath = currentBlock.Code.Path.Split("-");
                Block newBlock = world.GetBlock(new AssetLocation("noticeboard", $"noticeboard-active-{splitPath[splitPath.Length - 1]}"));

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
            else if (currentBlock.Code.Path.Contains("active"))
            {
                // Get the other block variant
                string[] splitPath = currentBlock.Code.Path.Split("-");
                Block newBlock = world.GetBlock(new AssetLocation("noticeboard", $"noticeboard-default-{splitPath[splitPath.Length - 1]}"));

                // Replace the block with the original variant
                world.BlockAccessor.SetBlock(newBlock.BlockId, pos);
                BlockEntity newEntity = world.BlockAccessor.GetBlockEntity(pos);
                if (newEntity != null)
                {
                    // Restore the entity data to the new BlockEntity
                    newEntity.FromTreeAttributes(blockEntityData, world);
                    newEntity.MarkDirty(true);
                }
            }
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            
       
            base.OnBlockPlaced(world, blockPos, byItemStack);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {


            if (world.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity blockEntity)
            {
                blockUniqueId = blockEntity.uniqueID;
                world.Logger.Debug("Destroyed the block with ID: " + blockUniqueId);
            }

            // Your custom code goes here, for example:
            world.Api.Logger.Notification("Block at {0} was destroyed by {1}", pos, byPlayer.PlayerName);

            // You can also spawn custom items or trigger other events
            // Example: Drop a custom item when the block is destroyed
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            if (!world.Side.IsServer()) return; // Ensure only the server handles dropping items
            //ItemStack drop = new ItemStack(world.GetItem(new AssetLocation("noticeboard:block-noticeboard-north")));
            //world.SpawnItemEntity(drop, pos.ToVec3d());
        }
    }
}
