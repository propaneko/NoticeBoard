using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace NoticeBoard.src.NoticeBoardBlock
{
    public class NoticeBoardBlockEntity : BlockEntity
    {
        public string uniqueID;
        private double actionInterval = 1;
        private double lastActionTime;


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            // Generate a unique ID for this block instance
            if (uniqueID == null)
            {
                uniqueID = System.Guid.NewGuid().ToString();
            }

            RegisterGameTickListener(OnPerformAction, (int)(actionInterval * 1000));
           
            //Api.Logger.Notification("UniqueID {0} at Pos {1}", uniqueID, Pos);
        }

        private void OnPerformAction(float dt)
        {
            // Custom action performed every actionInterval seconds
            Api.World.Logger.Notification("Timer action performed at block at " + Pos);

            NoticeBoardBlock myBlock = Block as NoticeBoardBlock;
            if (myBlock != null)
            {
                myBlock.ChangeBlockShape(Api.World, Pos);
            }

            // Example action: you can replace this with anything (e.g., spawn an item, change the block state, etc.)
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
}
