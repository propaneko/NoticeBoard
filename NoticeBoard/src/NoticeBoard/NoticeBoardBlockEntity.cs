using System;
using System.Collections.Generic;
using System.Text;
using NoticeBoard.Database;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace NoticeBoard.BlockType;

public class NoticeBoardSlot : ItemSlot
{
    public NoticeBoardSlot(InventoryBase inventory)
        : base(inventory) { }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        if (sourceSlot?.Itemstack == null)
            return false;

        return sourceSlot.Itemstack.Collectible.Code.Path == "paper-parchment"
            || sourceSlot.Itemstack.Collectible.Code.Path == "papyrus-paper";
    }

    public override int MaxSlotStackSize => 64;
}

public class NoticeBoardBlockEntity : BlockEntityOpenableContainer
{
    public string uniqueID;
    public NoticeBoardObject BoardProperties { get; private set; }

    private readonly double actionInterval = 1;
    private long lastParticleSpawnTime = 0;
    private long listener;

    public int quantitySlots = 5;
    public string inventoryClassName = "noticeboard";
    public string dialogTitleLangCode = "noticeboardcontents";

    private int cachedMessageCount = -1;
    private long lastCacheUpdate = 0;

    private readonly TimeSpan cacheValidity = TimeSpan.FromSeconds(15);
    private Dictionary<string, bool> unreadCache = new Dictionary<string, bool>();
    private long lastUnreadCacheUpdate = 0;
    private long lastBoardPropertiesCacheUpdate = 0;

    private SQLiteHandler db;

    private InventoryGeneric inventory;

    public override InventoryBase Inventory
    {
        get { return inventory; }
    }

    public override string InventoryClassName
    {
        get { return inventoryClassName; }
    }

    public NoticeBoardBlockEntity() { }

        public override void Initialize(ICoreAPI api)
        {
            bool isNewlyPlaced = inventory == null;

            if (isNewlyPlaced)
                InitInventory(Block, api);

            base.Initialize(api);

            if (!Api.World.Side.IsServer())
                return;

            if (NoticeBoardModSystem.getModInstance()?.getDatabaseHandler() == null)
            {
                Api.Logger.Warning("[NoticeBoard] BlockEntity initialized before database was ready.");
                return;
            }

            db = new SQLiteHandler();

            if (!string.IsNullOrEmpty(uniqueID))
            {
                BoardProperties = db.GetBoardData(uniqueID);
                MarkDirty(true);
            }

            listener = RegisterGameTickListener(OnPerformAction, (int)(actionInterval * 1000));
        }

    private void InitInventory(Block Block, ICoreAPI api)
    {
        if (Block?.Attributes != null)
        {
            inventoryClassName = Block
                .Attributes["inventoryClassName"]
                .AsString(inventoryClassName);

            dialogTitleLangCode = Block
                .Attributes["dialogTitleLangCode"]
                .AsString(dialogTitleLangCode);

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
            if (Api.GetNoticeBoardEntity(Pos) is not null)
            {
                byPlayer.InventoryManager.OpenInventory(this.Inventory);

                var data = BlockEntityContainerOpen.ToBytes(
                    "BlockEntityInventory",
                    Lang.Get(dialogTitleLangCode),
                    4,
                    inventory
                );

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
        if (string.IsNullOrEmpty(uniqueID))
            return;

        if (!Api.World.Side.IsServer())
            return;

        if (db == null)
            return;

        List<IServerPlayer> nearbyPlayers = new List<IServerPlayer>();
        foreach (IServerPlayer player in Api.World.AllOnlinePlayers)
        {
            if (player.ConnectionState != EnumClientState.Playing)
                continue;

            if (player.Entity.Pos.SquareDistanceTo(Pos.ToVec3d()) <= 32 * 32)
            {
                nearbyPlayers.Add(player);
            }
        }

        if (nearbyPlayers.Count == 0)
            return;

        UpdateMessageCountCache();
        UpdateBoardPropertiesCache();

        double messageCount = cachedMessageCount;
        double divisionMessageNumbers = Math.Min(Math.Floor(messageCount / NoticeBoardModSystem.getConfig().DivisionForPapersOnBoard), 6);

        if (Block is NoticeBoardBlock myBlock)
        {
            myBlock.ChangeBlockShape(Api.World, Pos, (int)(messageCount == 1 ? messageCount : divisionMessageNumbers));
        }

        if (Api.World.ElapsedMilliseconds - lastParticleSpawnTime > 800)
        {
            lastParticleSpawnTime = Api.World.ElapsedMilliseconds;

            if (ShouldParticlesSpawn())
            {
                UpdateUnreadCacheForPlayers(nearbyPlayers);

                foreach (var player in nearbyPlayers)
                {
                    if (HasUnreadCached(player.PlayerUID))
                    {
                        NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard")
                            .SendPacket(new UnreadParticlesPacket { Pos = Pos }, player);
                    }
                }
            }
        }
    }

    private void UpdateUnreadCacheForPlayers(List<IServerPlayer> playersToCheck)
    {
        long now = Api.World.ElapsedMilliseconds;
        if (now - lastUnreadCacheUpdate < 2000)
            return;

        unreadCache.Clear();

        foreach (IServerPlayer player in playersToCheck)
        {
            unreadCache[player.PlayerUID] = db.HasUnreadMessages(player.PlayerUID, uniqueID);
        }

        lastUnreadCacheUpdate = now;
    }

        private void UpdateBoardPropertiesCache()
        {
            long now = Api.World.ElapsedMilliseconds;

            if (now - lastBoardPropertiesCacheUpdate < 60000)
                return;

            lastBoardPropertiesCacheUpdate = now;

            var freshData = db.GetBoardData(uniqueID);

            if (freshData == null)
                return;

            bool changed =
                BoardProperties == null
                || freshData.BoardName != BoardProperties.BoardName
                || freshData.PlayerName != BoardProperties.PlayerName
                || freshData.PermissionMode != BoardProperties.PermissionMode
                || freshData.BoardFont != BoardProperties.BoardFont
                || freshData.EnableParticles != BoardProperties.EnableParticles
                || freshData.EnableParchment != BoardProperties.EnableParchment
                || Math.Abs(freshData.BoardFontSize - BoardProperties.BoardFontSize) > 0.001f
                || freshData.EnableProximity != BoardProperties.EnableProximity
                || freshData.ProximityChannel != BoardProperties.ProximityChannel
                || freshData.ProximityDistance != BoardProperties.ProximityDistance;

            if (changed)
            {
                BoardProperties = freshData;
                MarkDirty(true);
            }
        }

    private void UpdateMessageCountCache()
    {
        if (
            cachedMessageCount >= 0
            && Api.World.ElapsedMilliseconds - lastCacheUpdate < cacheValidity.TotalMilliseconds
        )
        {
            return;
        }

        cachedMessageCount = db.CountMessageElementsByBoardId(uniqueID);
        lastCacheUpdate = Api.World.ElapsedMilliseconds;
    }

    private void UpdateUnreadCacheIfNeeded()
    {
        long now = Api.World.ElapsedMilliseconds;

        if (now - lastUnreadCacheUpdate < 2000)
            return;

        unreadCache.Clear();

        foreach (IServerPlayer player in Api.World.AllOnlinePlayers)
        {
            if (player.ConnectionState == EnumClientState.Playing)
            {
                unreadCache[player.PlayerUID] = db.HasUnreadMessages(player.PlayerUID, uniqueID);
            }
        }

        lastUnreadCacheUpdate = now;
    }

    private bool HasUnreadCached(string playerId)
    {
        return unreadCache.TryGetValue(playerId, out bool hasUnread) && hasUnread;
    }

    public bool ShouldParticlesSpawn()
    {
        return BoardProperties?.EnableParticles == 1;
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
        listener = 0;

        base.OnBlockRemoved();
    }

    public override void FromTreeAttributes(
        ITreeAttribute tree,
        IWorldAccessor worldAccessForResolve
    )
    {
        if (inventory == null)
        {
            if (tree.HasAttribute("forBlockId"))
                InitInventory(
                    worldAccessForResolve.GetBlock((ushort)tree.GetInt("forBlockId")),
                    Api
                );
        }

        base.FromTreeAttributes(tree, worldAccessForResolve);

        uniqueID = tree.GetString("uniqueID", uniqueID);

        string boardPropertiesJson = tree.GetString("boardProperties");
        if (!string.IsNullOrEmpty(boardPropertiesJson))
            BoardProperties = JsonUtil.FromString<NoticeBoardObject>(boardPropertiesJson);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        if (!string.IsNullOrEmpty(uniqueID))
            tree.SetString("uniqueID", uniqueID);

        if (BoardProperties != null)
            tree.SetString("boardProperties", JsonUtil.ToString(BoardProperties));

        if (Block != null)
            tree.SetInt("forBlockId", Block.BlockId);
    }
}
