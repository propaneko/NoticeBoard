using NoticeBoard.src;
using NoticeBoard.src.Packets;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace NoticeBoard
{
    public class NoticeBoardBlockEntity : BlockEntityOpenableContainer
    {
        

        public string uniqueID;
        private ICoreAPI api;
        private double actionInterval = 1;
        internal InventoryGeneric inventory;

        private NoticeBoardMainWindowGui noticeBoardDialog;



        public NoticeBoardBlockEntity()
        {
            this.inventory = new InventoryGeneric(1, null, null);
            this.inventory.SlotModified += this.OnSlotModifid;
        }

        private void OnSlotModifid(int slotid)
        {
            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.MarkDirty(this.Api.Side == EnumAppSide.Server, null);
            //if (this.Api is ICoreClientAPI && this.noticeBoardDialog != null)
            //{
            //    this.SetDialogValues(this.noticeBoardDialog.Attributes);
            //}
        }

        private void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetString("uniqueID", uniqueID);
        }

        public override string InventoryClassName
        {
            get
            {
                return "noticeboard";
            }
        }

        public override InventoryBase Inventory
        {
            get
            {
                return this.inventory;
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.inventory.Pos = this.Pos;
            this.inventory.LateInitialize(string.Concat(new string[]
            {
                "smelting-",
                this.Pos.X.ToString(),
                "/",
                this.Pos.Y.ToString(),
                "/",
                this.Pos.Z.ToString()
            }), api);

            this.api = api;

            if (!Api.World.Side.IsServer()) return;

            if (uniqueID == null)
            {
                uniqueID = System.Guid.NewGuid().ToString();
            }

            this.RegisterGameTickListener(OnPerformAction, (int)(actionInterval * 1000));

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

        public class DialogPacket
        {
            public List<Message> Messages { get; set; }
            public string UniqueID { get; set; }

        }
        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
           
            List<Message> messages = new SQLiteHandler().GetAllMessages(uniqueID);
            DialogPacket packet = new DialogPacket()
            {
                Messages = messages,
                UniqueID = uniqueID,
            };
            if (this.Api.Side == EnumAppSide.Client)
            {
                if (noticeBoardDialog == null || !noticeBoardDialog.IsOpened())
                {
                    noticeBoardDialog = new NoticeBoardMainWindowGui("mainNoticeBoardDialog", this.inventory, packet, this.Api as ICoreClientAPI);
                    noticeBoardDialog.TryOpen();
                }
            }
            return true;
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            api.Logger.Debug(packetid.ToString());
            base.OnReceivedClientPacket(player, packetid, data);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
            api.Logger.Debug(packetid.ToString());
            base.OnReceivedServerPacket(packetid, data);
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
