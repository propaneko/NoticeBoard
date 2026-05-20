using NoticeBoard.Database;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace NoticeBoard.BlockType;

public class NoticeBoardSlot : ItemSlot
{
    public NoticeBoardSlot(InventoryBase inventory) : base(inventory){}
    public override bool CanHold(ItemSlot sourceSlot)
    {
        if (sourceSlot?.Itemstack == null) return false;

        return sourceSlot.Itemstack.Collectible.Code.Path == "paper-parchment";
    }

    public override int MaxSlotStackSize => 64;
}

public class NoticeBoardBlockEntity : BlockEntityOpenableContainer
{
    public string uniqueID;
    private double actionInterval = 1;
    private long lastParticleSpawnTime = 0;
    private long listener;

    public int quantitySlots = 1;
    public string inventoryClassName = "noticeboard";
    public string dialogTitleLangCode = "noticeboardcontents";

    private readonly SQLiteHandler db = new();

    private InventoryGeneric inventory;


    public override InventoryBase Inventory
    {
        get { return inventory; }
    }

    public override string InventoryClassName
    {
        get { return inventoryClassName; }
    }

    public NoticeBoardBlockEntity()
    {
    }
    public override void Initialize(ICoreAPI api)
    {
        bool isNewlyplaced = inventory == null;

        if (isNewlyplaced)
        {
            InitInventory(Block, api);
        }

        base.Initialize(api);

        if (!Api.World.Side.IsServer()) return;
        listener = RegisterGameTickListener(OnPerformAction, (int)(actionInterval * 500));
    }

    private void InitInventory(Block Block, ICoreAPI api)
    {
        if (Block?.Attributes != null)
        {
            inventoryClassName = Block.Attributes["inventoryClassName"].AsString(inventoryClassName);
            dialogTitleLangCode = Block.Attributes["dialogTitleLangCode"].AsString(dialogTitleLangCode);
            quantitySlots = Block.Attributes["quantitySlots"].AsInt(quantitySlots);
        }

        string myInvId = InventoryClassName + "-" + Pos;
        inventory = new InventoryGeneric(
            quantitySlots,
            myInvId,
            api,
            (slotId, inv) => new NoticeBoardSlot(inv)
        );

        inventory.OnInventoryClosed += OnInvClosed;
        inventory.OnInventoryOpened += OnInvOpened;
        inventory.SlotModified += OnSlotModified;
    }

    private void OnSlotModified(int slot)
    {
        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
    }

    protected virtual void OnInvOpened(IPlayer player)
    {
        inventory.PutLocked = player.WorldData.CurrentGameMode != EnumGameMode.Creative;
    }

    protected virtual void OnInvClosed(IPlayer player)
    {
        invDialog?.Dispose();
        invDialog = null;
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetid, byte[] data)
    {
        if (packetid == 999)
        {
            player.InventoryManager.CloseInventory(this.Inventory);
            return;
        }

        if (this.Inventory != null)
        {
            base.OnReceivedClientPacket(player, packetid, data);
        }
    }
    public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (Api.World.Side.IsServer())
        {
            if (Api.World.BlockAccessor.GetBlockEntity(Pos) is NoticeBoardBlockEntity)
            {
                byPlayer.InventoryManager.OpenInventory(this.Inventory);

                var data = BlockEntityContainerOpen.ToBytes("BlockEntityInventory", Lang.Get(dialogTitleLangCode), 4, inventory);
                ((ICoreServerAPI)Api).Network.SendBlockEntityPacket(
                    (IServerPlayer)byPlayer,
                    Pos,
                    (int)EnumBlockContainerPacketId.OpenInventory,
                    data
                );
            }
        }

        return true;
    }

    public string GenerateUniqueID()
    {
        uniqueID = System.Guid.NewGuid().ToString();
        return uniqueID;
    }

    private void OnPerformAction(float dt)
    {
        double messageCount = new SQLiteHandler().CountMessageElementsByBoardId(uniqueID);
        double divisionMessageNumbers = messageCount / NoticeBoardModSystem.getConfig().DivisionForPapersOnBoard;
        divisionMessageNumbers = divisionMessageNumbers > 6 ? 6 : divisionMessageNumbers;
        divisionMessageNumbers = Math.Floor(divisionMessageNumbers);

        NoticeBoardBlock myBlock = Block as NoticeBoardBlock;
        if (myBlock != null)
        {
            myBlock.ChangeBlockShape(Api.World, Pos, (int)(messageCount == 1 ? messageCount : divisionMessageNumbers));
        }

        if (Api.World.Side.IsServer() && Api.World.ElapsedMilliseconds - lastParticleSpawnTime > 800)
        {
            lastParticleSpawnTime = Api.World.ElapsedMilliseconds;

            foreach (IServerPlayer player in Api.World.AllOnlinePlayers)
            {
                if (player.ConnectionState != EnumClientState.Playing) continue;

                double distanceSq = player.Entity.Pos.SquareDistanceTo(Pos.ToVec3d());
                if (distanceSq > 32 * 32) continue;

                if (HasUnreadMessagesForPlayer(player.PlayerUID) && ShouldParticlesSpawn())
                {
                    ((NoticeBoardBlock)Block)?.SpawnUnreadParticles(Api.World, Pos);
                    break;
                }
            }
        }
    }
    public bool ShouldParticlesSpawn()
    {
        if (string.IsNullOrEmpty(uniqueID))
            return false;

        return db.GetBoardData(uniqueID)?.enableParticles == 1;
    }

    public bool HasUnreadMessagesForPlayer(string playerId)
    {
        if (string.IsNullOrEmpty(uniqueID) || string.IsNullOrEmpty(playerId))
            return false;

        return db.HasUnreadMessages(playerId, uniqueID);
    }

    public override void OnBlockRemoved()
    {
        UnregisterGameTickListener(listener);
        base.OnBlockRemoved();
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        if (inventory == null)
        {
            if (tree.HasAttribute("forBlockId"))
            {
                InitInventory(worldAccessForResolve.GetBlock((ushort)tree.GetInt("forBlockId")), Api);
            }
        }
        base.FromTreeAttributes(tree, worldAccessForResolve);
        uniqueID = tree.GetString("uniqueID", uniqueID);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetString("uniqueID", uniqueID);
        if (Block != null) tree.SetInt("forBlockId", Block.BlockId);
    }
}
