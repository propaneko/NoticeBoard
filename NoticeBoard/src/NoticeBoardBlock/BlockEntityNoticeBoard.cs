using NoticeBoard.src;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace NoticeBoard
{
    public class NoticeBoardBlockEntity : BlockEntity
    {
        public string uniqueID;
        private ICoreAPI api;
        private double actionInterval = 1;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.api = api;

            if (!Api.World.Side.IsServer()) return;

            if (uniqueID == null)
            {
                uniqueID = System.Guid.NewGuid().ToString();
            }

            RegisterGameTickListener(OnPerformAction, (int)(actionInterval * 1000));

        }

        private void OnPerformAction(float dt)
        {
            int messageCount = new SQLiteHandler().CountMessageElementsByBoardId(uniqueID);
           
            NoticeBoardBlock myBlock = Block as NoticeBoardBlock;
            if (myBlock != null)
            {
                myBlock.ChangeBlockShape(Api.World, Pos, messageCount);
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
}
