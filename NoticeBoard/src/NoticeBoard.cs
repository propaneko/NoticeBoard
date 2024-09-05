using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;    // For client-side interaction, if needed (e.g., custom block rendering).
using Vintagestory.API.MathTools; // For block position and math utilities.

namespace NoticeBoard.src
{
    public class NoticeBoardBlockEntity : BlockEntity
    {
        public string uniqueID;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Generate a unique ID for this block instance
            if (uniqueID == null)
            {
                uniqueID = System.Guid.NewGuid().ToString();
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            uniqueID = tree.GetString("uniqueID", uniqueID);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("uniqueID", uniqueID);
        }
    }

    public class NoticeBoardBlock : Block
    {
        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockPos blockPos = blockSel.Position;

            

            if (world.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (byPlayer as IClientPlayer)?.Entity?.World?.Api as ICoreClientAPI;
                if (capi != null)
                {
                    if (world.BlockAccessor.GetBlockEntity(blockPos) is NoticeBoardBlockEntity blockEntity)
                    {
                        string blockUniqueId = blockEntity.uniqueID;
                        world.Logger.Debug("Unique ID of the block: " + blockUniqueId);

                        GuiDialogCustom myGui = new GuiDialogCustom("Custom GUI", blockUniqueId, capi);
                        myGui.TryOpen();
                    }
                    // Open the custom GUI
                    
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }


}
