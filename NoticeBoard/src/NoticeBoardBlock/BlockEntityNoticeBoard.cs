using NoticeBoard;
using NoticeBoard.Entity;
using NoticeBoard.Packets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace NoticeBoard
{
    public class NoticeBoardBlockEntity : BlockEntityOpenableContainer
    {
        

        public string uniqueID;
        private ICoreAPI api;
        private double actionInterval = 1;
        internal NoticeBoardInventory inventory;
        private NoticeBoardMainWindowGui noticeBoardDialog;
        private NoticeBoardTextInputWindowGui textInputDialog;
        List<Message> messages;

        public NoticeBoardBlockEntity()
        {
            this.inventory = new NoticeBoardInventory(null, null, this);
            this.inventory.SlotModified += this.OnSlotModifid;
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

        public ItemSlot InputSlot
        {
            get
            {
                return this.inventory[0];
            }
        }

        private void OnSlotModifid(int slotid)
        {
            base.Block = this.Api.World.BlockAccessor.GetBlock(this.Pos);
            this.MarkDirty(false, null);

            if (this.noticeBoardDialog != null && this.noticeBoardDialog.IsOpened())
            {
                this.noticeBoardDialog.SingleComposer.ReCompose();
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            this.inventory.Pos = this.Pos;
            this.inventory.LateInitialize(string.Concat(new string[]
            {
                "noticeboard-",
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

            if (api.Side == EnumAppSide.Server)
            {
                this.RegisterGameTickListener(OnPerformAction, (int)(actionInterval * 1000));
            }
        }

        private void OnPerformAction(float dt)
        {
            int messageCount = new SQLiteHandler().CountMessageElementsByBoardId(uniqueID);
           
            NoticeBoardBlock myBlock = Block as NoticeBoardBlock;
            if (myBlock != null)
            {
                myBlock.ChangeBlockShape(Api.World, Pos, messageCount);
            }

            List<Message> messages = new SQLiteHandler().GetAllMessages(uniqueID);
            messages.Reverse();
            this.messages = messages;

        }

        public override void OnPlacementBySchematic(ICoreServerAPI api, IBlockAccessor blockAccessor, BlockPos pos, Dictionary<int, Dictionary<int, int>> replaceBlocks, int centerrockblockid, Block layerBlock, bool resolveImports)
        {
            this.Pos = pos.Copy();
            for (int i = 0; i < this.Behaviors.Count; i++)
            {
                this.Behaviors[i].OnPlacementBySchematic(api, blockAccessor, pos, replaceBlocks, centerrockblockid, layerBlock, resolveImports);
            }
        }

        public class DialogPacket
        {
            public List<Message> Messages { get; set; }
            public string UniqueID { get; set; }
            public string PlayerId { get; set; }


        }

        protected void toggleInventoryDialogClient(IPlayer byPlayer)
        {
            if (this.noticeBoardDialog == null)
            {
                ICoreClientAPI capi = this.Api as ICoreClientAPI;
                List<Message> messages = new SQLiteHandler().GetAllMessages(uniqueID);
                messages.Reverse();

                DialogPacket packet = new DialogPacket()
                {
                    Messages = messages,
                    UniqueID = uniqueID,
                    PlayerId = byPlayer.PlayerUID
                };

                if (this.noticeBoardDialog == null || !this.noticeBoardDialog.IsOpened())
                {
                    this.noticeBoardDialog = new NoticeBoardMainWindowGui("mainNoticeBoardDialog", this.inventory, this.Pos, packet, this.Api as ICoreClientAPI);
                    this.noticeBoardDialog.OnClosed += delegate
                    {
                        this.noticeBoardDialog = null;
                        capi.Network.SendBlockEntityPacket(this.Pos.X, this.Pos.Y, this.Pos.Z, 1001, null);
                        capi.Network.SendPacketClient(this.inventory.Close(byPlayer));
                    };
                    this.noticeBoardDialog.OpenSound = AssetLocation.Create("sounds/block/barrelopen", "game");
                    this.noticeBoardDialog.CloseSound = AssetLocation.Create("sounds/block/barrelclose", "game");
                    this.noticeBoardDialog.TryOpen();
                    capi.Network.SendPacketClient(this.Inventory.Open(byPlayer));
                    capi.Network.SendBlockEntityPacket(this.Pos.X, this.Pos.Y, this.Pos.Z, 1000, null);
                    this.MarkDirty(false, null);
                    return;
                }
            }
            this.noticeBoardDialog.TryClose();
        }
        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (this.Api.Side == EnumAppSide.Client)
            {
                this.toggleInventoryDialogClient(byPlayer);
            }
            return true;
        }

        public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
        {
            api.Logger.Debug(packetid.ToString());

            if (packetid == 1000)
            {
                this.MarkDirty(false, null);
                IPlayerInventoryManager inventoryManager = player.InventoryManager;
                if (inventoryManager != null)
                {
                    inventoryManager.OpenInventory(this.Inventory);
                }
            }

            if (packetid == 2138)
            {
                List<Message> messages = new SQLiteHandler().GetAllMessages(uniqueID);
                messages.Reverse();
                if (this.noticeBoardDialog != null)
                {
                    this.noticeBoardDialog.UpdateMessages(messages);
                }
            }

            if (packetid == 2137)
            {
                IPlayerInventoryManager inventoryManager = player.InventoryManager;
                if (inventoryManager != null)
                {
                    inventoryManager.OpenInventory(this.Inventory);
                }
                this.inventory[0].TakeOut(1);
                this.inventory[0].MarkDirty();
            }
            base.OnReceivedClientPacket(player, packetid, data);
        }

        public override void OnReceivedServerPacket(int packetid, byte[] data)
        {
       
            base.OnReceivedServerPacket(packetid, data);
            api.Logger.Debug(packetid.ToString());

            //List<Message> messages = new SQLiteHandler().GetAllMessages(uniqueID);
            //if (noticeBoardDialog != null || noticeBoardDialog.IsOpened())
            //{
            //    noticeBoardDialog.UpdateMessages(messages);
            //}

            if (packetid == 1001)
            {
                (this.Api.World as IClientWorldAccessor).Player.InventoryManager.CloseInventory(this.Inventory);
                NoticeBoardMainWindowGui guinoticeBoardDialogBE = this.noticeBoardDialog;
                if (guinoticeBoardDialogBE != null)
                {
                    guinoticeBoardDialogBE.TryClose();
                }
                NoticeBoardMainWindowGui guinoticeBoardDialogBE2 = this.noticeBoardDialog;
                if (guinoticeBoardDialogBE2 != null)
                {
                    guinoticeBoardDialogBE2.Dispose();
                }
                this.noticeBoardDialog = null;
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            base.OnBlockBroken(byPlayer);
            NoticeBoardMainWindowGui guinoticeBoardDialogBE = this.noticeBoardDialog;
            if (guinoticeBoardDialogBE != null)
            {
                guinoticeBoardDialogBE.TryClose();
            }
            NoticeBoardMainWindowGui guinoticeBoardDialogBE2 = this.noticeBoardDialog;
            if (guinoticeBoardDialogBE2 != null)
            {
                guinoticeBoardDialogBE2.Dispose();
            }
            this.noticeBoardDialog = null;
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            this.Inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (this.Api != null)
            {
                this.Inventory.AfterBlocksLoaded(this.Api.World);
            }
            uniqueID = tree.GetString("uniqueID", uniqueID);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute treeAttribute = new TreeAttribute();
            this.Inventory.ToTreeAttributes(treeAttribute);
            tree["inventory"] = treeAttribute;
            tree.SetString("uniqueID", uniqueID);
        }
    }
}
