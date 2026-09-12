using System;
using System.Collections.Generic;
using AttributeRenderingLibrary;
using NoticeBoard.Database;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Rendering;
using NoticeBoard.Utils;
using NoticeBoard;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
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

public class LanternHolderSlot : ItemSlot
{
    public LanternHolderSlot(InventoryBase inventory)
        : base(inventory) { }

    public static bool IsSmallLantern(ItemStack stack)
    {
        string path = stack?.Block?.Code?.Path;
        return path != null && path.StartsWith("lantern-small");
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        return IsSmallLantern(sourceSlot?.Itemstack);
    }

    public override int MaxSlotStackSize => 1;
}

public class NoticeBoardBlockEntity : BlockEntityOpenableContainer
{
    public string uniqueID;
    public string restoreWood;
    public string restoreMetal;
    private ITreeAttribute types;
    public NoticeBoardObject BoardProperties { get; private set; }

    private readonly double actionInterval = 1;
    private long lastParticleSpawnTime = 0;
    private long listener;

    public int quantitySlots = 7;
    public string inventoryClassName = "noticeboard";
    public string dialogTitleLangCode = "noticeboardcontents";

    private int[] visualMessageIds = Array.Empty<int>();
    private Dictionary<int, MessageVisualData> visualMessageTexts = new Dictionary<int, MessageVisualData>();
    private PaperPlacement[] lastPlacements = Array.Empty<PaperPlacement>();
    private int lastSyncedMaxPapers = -1;
    private readonly Dictionary<string, MeshData> customPaperMeshes = new();
    private MeshData nailUnitMesh;
    private MeshData nailUnitMeshClient;
    private Dictionary<int, int> paperHeightUnitsById = new Dictionary<int, int>();
    private NoticeBoardPaperTextRenderer textRenderer;
    private NoticeBoardLanternRenderer lanternRenderer;
    private string lastPinSyncKey;

    private Dictionary<string, bool> unreadCache = new Dictionary<string, bool>();
    private long lastUnreadCacheUpdate = 0;

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

    public ITreeAttribute GetTypes()
    {
        return types;
    }

    public void SetTypes(ITreeAttribute value)
    {
        types = value == null ? null : (ITreeAttribute)value.Clone();
    }

    private ITreeAttribute GetOrCreateTypes()
    {
        if (types == null)
            types = new TreeAttribute();
        return types;
    }

    public void EnsureDefaultTypes()
    {
        ITreeAttribute next = types ?? new TreeAttribute();
        JsonObject jsonTypes = Block?.Attributes?["types"];
        if (string.IsNullOrEmpty(next.GetString("wood")))
            next.SetString("wood", jsonTypes?["wood"].AsString("aged") ?? "aged");
        if (string.IsNullOrEmpty(next.GetString("metal")))
            next.SetString("metal", jsonTypes?["metal"].AsString("brass") ?? "brass");
        if (string.IsNullOrEmpty(next.GetString("messageCount")))
            next.SetString("messageCount", jsonTypes?["messageCount"].AsString("0") ?? "0");
        types = next;
        SyncArlVariants();
    }

    private void SyncArlVariants()
    {
        BlockEntityBehaviorShapeTexturesFromAttributes beh =
            GetBehavior<BlockEntityBehaviorShapeTexturesFromAttributes>();
        if (beh == null || types == null)
            return;
        string wood = types.GetString("wood");
        string metal = types.GetString("metal");
        string messageCount = types.GetString("messageCount");
        if (!string.IsNullOrEmpty(wood))
            beh.Variants.Set("wood", wood);
        if (!string.IsNullOrEmpty(metal))
            beh.Variants.Set("metal", metal);
        if (!string.IsNullOrEmpty(messageCount))
            beh.Variants.Set("messageCount", messageCount);
    }

        public override void Initialize(ICoreAPI api)
        {
            bool isNewlyPlaced = inventory == null;

            if (isNewlyPlaced)
                InitInventory(Block, api);

            base.Initialize(api);

            EnsureDefaultTypes();

            if (Api.Side == EnumAppSide.Client)
            {
                textRenderer?.Dispose();
                textRenderer = new NoticeBoardPaperTextRenderer((ICoreClientAPI)Api, Pos);
                lanternRenderer?.Dispose();
                lanternRenderer = new NoticeBoardLanternRenderer((ICoreClientAPI)Api, Pos);
                UpdateTextRenderer();
                return;
            }

            if (NoticeBoardModSystem.getModInstance()?.getDatabaseHandler() == null)
            {
                Api.Logger.Warning("[NoticeBoard] BlockEntity initialized before database was ready.");
                return;
            }

            db = new SQLiteHandler();

            if (!string.IsNullOrEmpty(uniqueID))
            {
                bool isWall = Block?.Variant?["attachment"] == "wall";
                db.SyncMessagePinsToAttachment(uniqueID, isWall);
                BoardProperties = db.GetBoardData(uniqueID);
                RefreshPaperVisuals();
                MarkDirty(true);
            }

            listener = RegisterGameTickListener(OnPerformAction, (int)(actionInterval * 1000));
            SyncLanternGlow();
        }

    private void InitInventory(Block Block, ICoreAPI api)
    {
        if (inventory != null)
        {
            inventory.OnInventoryClosed -= OnInvClosed;
            inventory.OnInventoryOpened -= OnInvOpened;
            inventory.SlotModified -= OnSlotModified;
        }

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

        if (quantitySlots < 7)
            quantitySlots = 7;

        string myInvId = InventoryClassName + "-" + Pos;

        inventory = new InventoryGeneric(
            quantitySlots,
            myInvId,
            api,
            (slotId, inv) => slotId == 5 || slotId == 6 ? new LanternHolderSlot(inv) : new NoticeBoardSlot(inv)
        );

        inventory.OnInventoryClosed += OnInvClosed;
        inventory.OnInventoryOpened += OnInvOpened;
        inventory.SlotModified += OnSlotModified;
    }

    private void OnSlotModified(int slot)
    {
        if (Api?.World == null)
            return;

        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        if (slot == 5 || slot == 6)
        {
            Api.World.BlockAccessor.MarkBlockDirty(Pos);
            SyncLanternGlow();
            if (Api.Side == EnumAppSide.Client)
                UpdateLanternRenderer();
        }
    }

    private void SyncLanternGlow()
    {
        if (Api == null || !Api.World.Side.IsServer() || Block == null)
            return;

        if (BoardProperties?.EnableLegacyBoard == 1)
            return;

        bool isWall = Block.Variant?["attachment"] == "wall";
        int up = isWall ? 1 : 2;

        SyncOneGlow(Pos.UpCopy(up), false);
        SyncOneGlow(RightGlowPos(Pos, Block, up), false);
    }

    private static BlockPos RightGlowPos(BlockPos origin, Block block, int up)
    {
        return block?.Variant?["side"] switch
        {
            "east" => origin.AddCopy(0, up, 2),
            "south" => origin.AddCopy(-2, up, 0),
            "west" => origin.AddCopy(0, up, -2),
            _ => origin.AddCopy(2, up, 0),
        };
    }

    private void SyncOneGlow(BlockPos glowPos, bool hasLantern)
    {
        Block current = Api.World.BlockAccessor.GetBlock(glowPos);
        if (current is not BlockMultiblock)
            return;

        string dx = MbOffset(glowPos.X - Pos.X);
        string dy = MbOffset(glowPos.Y - Pos.Y);
        string dz = MbOffset(glowPos.Z - Pos.Z);
        string prefix = hasLantern ? "noticeboard:lanternglow" : "game:multiblock";
        AssetLocation targetCode = new AssetLocation(prefix + "-monolithic-" + dx + "-" + dy + "-" + dz);

        Block target = Api.World.GetBlock(targetCode);
        if (target == null || current.Id == target.Id)
            return;

        Api.World.BlockAccessor.ExchangeBlock(target.BlockId, glowPos);
        Api.World.BlockAccessor.MarkBlockDirty(glowPos);
    }

    private static string MbOffset(int n)
    {
        if (n == 0) return "0";
        if (n < 0) return "n" + (-n);
        return "p" + n;
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

    public int GetPinLayer(int messageId)
    {
        if (visualMessageTexts != null && visualMessageTexts.TryGetValue(messageId, out MessageVisualData data))
            return NoticeBoardPaperLayout.ClampPinLayer(data.PinLayer);
        return 0;
    }

    public MessageVisualData GetVisualData(int messageId) =>
        visualMessageTexts != null && visualMessageTexts.TryGetValue(messageId, out MessageVisualData data)
            ? data
            : null;

    public bool HasNotices => visualMessageTexts != null && visualMessageTexts.Count > 0;

    // Which sheet the player's crosshair ray lands on, or -1. Same tack plane the pin ghost
    // uses, so the pick matches what the pointer visually covers.
    public int PickMessageAtLookRay(Vec3d eye, Vec3d dir)
    {
        if (BoardProperties?.EnableLegacyBoard == 1)
            return -1;
        bool isWall = Block?.Variant?["attachment"] == "wall";
        if (!NoticeBoardPaperLayout.TryLookRayToTack(
                eye, dir, Pos, GetRotateYDeg(Block), isWall, out float tackX, out float tackY))
            return -1;
        return NoticeBoardPaperLayout.PickSheetAt(lastPlacements, tackX, tackY);
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

        ExpireAgedNotices();

        RefreshPaperVisuals();

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

    public void ExpireAgedNotices()
    {
        if (Api == null || !Api.World.Side.IsServer())
            return;

        if (string.IsNullOrEmpty(uniqueID) || db == null)
            return;

        var props = db.GetBoardData(uniqueID) ?? BoardProperties;
        if (props?.EnableNoticeAging != 1)
            return;

        var cal = Api.World.Calendar;
        if (cal == null)
            return;

        List<ExpiredNoticeRow> expired = db.TakeExpiredMessages(
            uniqueID,
            cal.TotalHours,
            cal.HoursPerDay,
            GameDateFormatter.ClampLifeDays(props.NoticeAgingDays));
        if (expired == null || expired.Count == 0)
            return;

        foreach (ExpiredNoticeRow row in expired)
        {
            if (row.HasPin)
                continue;
            for (int i = 0; i < lastPlacements.Length; i++)
            {
                if (lastPlacements[i].MessageId != row.Id)
                    continue;
                row.PinX = lastPlacements[i].X;
                row.PinY = lastPlacements[i].Y;
                row.PinRotZ = lastPlacements[i].RotZDeg;
                row.HasPin = true;
                break;
            }
        }

        RefreshPaperVisuals();
        NoticeParchment.ExpireAndDrop((ICoreServerAPI)Api, Pos, uniqueID, expired, GetRotateYDeg(Block));
    }

    public void AddExpiredFalls(List<ExpiredNoticeFall> notices)
    {
        textRenderer?.AddExpiredFalls(notices);
    }

    public void RefreshPaperVisuals()
    {
        if (Api == null || !Api.World.Side.IsServer())
            return;

        if (string.IsNullOrEmpty(uniqueID) || db == null)
            return;

        var fresh = db.GetBoardData(uniqueID);
        if (fresh == null)
            return;

        bool propertiesChanged =
            BoardProperties == null
            || fresh.BoardName != BoardProperties.BoardName
            || fresh.PlayerName != BoardProperties.PlayerName
            || fresh.PermissionMode != BoardProperties.PermissionMode
            || fresh.BoardFont != BoardProperties.BoardFont
            || fresh.BoardTheme != BoardProperties.BoardTheme
            || fresh.EnableParticles != BoardProperties.EnableParticles
            || fresh.EnableNoticeAging != BoardProperties.EnableNoticeAging
            || fresh.NoticeAgingDays != BoardProperties.NoticeAgingDays
            || fresh.EnableParchment != BoardProperties.EnableParchment
            || Math.Abs(fresh.BoardFontSize - BoardProperties.BoardFontSize) > 0.001f
            || fresh.EnableProximity != BoardProperties.EnableProximity
            || fresh.ProximityChannel != BoardProperties.ProximityChannel
            || fresh.ProximityDistance != BoardProperties.ProximityDistance
            || fresh.EnableLegacyBoard != BoardProperties.EnableLegacyBoard
            || fresh.TextSharpness != BoardProperties.TextSharpness
            || fresh.SwayStrength != BoardProperties.SwayStrength
            || fresh.EnableManualPin != BoardProperties.EnableManualPin
            || fresh.EnableDiscord != BoardProperties.EnableDiscord
            || fresh.HasDiscordWebhook != BoardProperties.HasDiscordWebhook;

        BoardProperties = fresh;

        int max = NoticeBoardPaperLayout.ClampMax(
            BoardProperties?.MaxPapersOnBoard ?? NoticeBoardPaperLayout.DefaultMaxPapers
        );
        int[] ids = db.GetRecentMessageIds(uniqueID, max);
        var texts = db.GetMessageTextsByIds(uniqueID, ids);

        ApplyLegacyBoardVariant(ids.Length);

        bool messagesChanged =
            !IdsEqual(visualMessageIds, ids)
            || lastSyncedMaxPapers != max
            || !DictionariesEqual(visualMessageTexts, texts);

        visualMessageIds = ids;
        visualMessageTexts = texts;
        lastSyncedMaxPapers = max;

        if (propertiesChanged || messagesChanged)
            MarkDirty(true);
    }

    // Legacy boards swap ARL types to aged-brass and baked messageCount 0-6 instead of
    // relying on the dynamic renderer. Attachment and side stay on the block code.
    private void ApplyLegacyBoardVariant(int messageCount)
    {
        if (Block == null || Api?.Side != EnumAppSide.Server)
            return;

        bool legacy = BoardProperties?.EnableLegacyBoard == 1;
        ITreeAttribute boardTypes = GetOrCreateTypes();
        string currentWood = boardTypes.GetString("wood", "aged");
        string currentMetal = boardTypes.GetString("metal", "brass");
        string wood;
        string metal;
        int count;

        if (legacy)
        {
            if (string.IsNullOrEmpty(restoreWood))
            {
                restoreWood = currentWood;
                restoreMetal = currentMetal;
            }
            wood = "aged";
            metal = "brass";
            count = Math.Clamp(messageCount, 0, 6);
        }
        else
        {
            wood = string.IsNullOrEmpty(restoreWood) ? currentWood : restoreWood;
            metal = string.IsNullOrEmpty(restoreMetal) ? currentMetal : restoreMetal;
            count = 0;
        }

        int currentCount = int.TryParse(boardTypes.GetString("messageCount", "0"), out int parsed)
            ? parsed
            : -1;
        if (currentCount == count && currentWood == wood && currentMetal == metal)
            return;

        boardTypes.SetString("wood", wood);
        boardTypes.SetString("metal", metal);
        boardTypes.SetString("messageCount", count.ToString());
        SyncArlVariants();
        MarkDirty(true);
    }

    public int TextSharpness => PaperSize.ResolveSharpness(BoardProperties?.TextSharpness ?? 0);

    public int SwayStrength =>
        PaperSize.ClampSwayStrength(BoardProperties?.SwayStrength ?? PaperSize.DefaultSwayStrength);

    // What the renderer would actually rasterise at for a setting the player has picked but not
    // saved yet: a crowded board can force it below the request.
    public int PredictTextSuperSample(int requested) =>
        textRenderer?.PredictSuperSample(requested) ?? PaperSize.ClampSharpness(requested);

    public void RebuildClientPapers()
    {
        if (Api?.Side != EnumAppSide.Client)
            return;
        UpdateTextRenderer();
    }

    // Client-only hover feedback: no DB write, no packet, no chunk redraw.
    public void SetHighlightedMessage(int messageId)
    {
        textRenderer?.SetHighlightedMessage(messageId);
    }

    private void UpdateTextRenderer()
    {
        if (textRenderer == null)
            return;

        bool isLegacy = BoardProperties?.EnableLegacyBoard == 1;
        bool isWall = Block?.Variant?["attachment"] == "wall";
        int maxPapers = NoticeBoardPaperLayout.ClampMax(
            BoardProperties?.MaxPapersOnBoard ?? NoticeBoardPaperLayout.DefaultMaxPapers
        );

        // Legacy boards show their baked shape decorations instead: no per-message text, no
        // procedural permits, nothing for this renderer to draw.
        int[] idsToRender = isLegacy ? Array.Empty<int>() : visualMessageIds;
        var textsToRender = isLegacy ? new Dictionary<int, MessageVisualData>() : visualMessageTexts;

        int hideId = PaperPinController.HiddenMessageId(uniqueID);
        if (hideId >= 0 && idsToRender != null && idsToRender.Length > 0)
        {
            int kept = 0;
            for (int i = 0; i < idsToRender.Length; i++)
            {
                if (idsToRender[i] != hideId)
                    kept++;
            }
            if (kept != idsToRender.Length)
            {
                var filtered = new int[kept];
                int w = 0;
                for (int i = 0; i < idsToRender.Length; i++)
                {
                    if (idsToRender[i] == hideId)
                        continue;
                    filtered[w++] = idsToRender[i];
                }
                idsToRender = filtered;
            }
        }

        // OnTesselation reads this from the tessellation thread, so publish a fresh instance
        // rather than mutating the one already in flight.
        var heights = new Dictionary<int, int>();
        foreach (int id in idsToRender ?? Array.Empty<int>())
        {
            bool hasText = visualMessageTexts.TryGetValue(id, out MessageVisualData data)
                && !string.IsNullOrWhiteSpace(data.Text);

            heights[id] = !hasText
                ? PaperSize.FullHeightUnits
                : PaperSize.MeasureHeightUnits(
                    (ICoreClientAPI)Api,
                    data.Text,
                    BoardProperties?.BoardFont,
                    BoardProperties?.BoardFontSize ?? 0,
                    hasAuthor: !string.IsNullOrEmpty(data.Author));
        }
        paperHeightUnitsById = heights;

        bool freezePins = BoardProperties?.EnableManualPin != 0 && BoardProperties?.EnableLegacyBoard == 0;
        PaperPlacement[] placements = NoticeBoardPaperLayout.Place(
            idsToRender,
            isWall,
            maxPapers,
            heights,
            freezePins ? NoticeBoardPaperLayout.PinsFrom(textsToRender) : null
        );
        lastPlacements = placements;
        SyncLayoutPins(placements);

        textRenderer.Update(
            idsToRender,
            textsToRender,
            heights,
            isWall,
            maxPapers,
            GetRotateYDeg(Block),
            BoardProperties?.BoardFont,
            BoardProperties?.BoardFontSize ?? 0,
            BoardProperties?.BoardTheme,
            TextSharpness,
            SwayStrength,
            types?.GetString("metal") ?? "copper",
            freezePins
        );
        UpdateLanternRenderer();
    }

    private void SyncLayoutPins(PaperPlacement[] placements)
    {
        if (Api?.Side != EnumAppSide.Client || string.IsNullOrEmpty(uniqueID))
            return;
        if (BoardProperties?.EnableLegacyBoard == 1)
            return;
        if (placements == null || placements.Length == 0)
            return;

        var ids = new int[placements.Length];
        var xs = new float[placements.Length];
        var ys = new float[placements.Length];
        var rots = new float[placements.Length];
        var key = new System.Text.StringBuilder(placements.Length * 32);
        for (int i = 0; i < placements.Length; i++)
        {
            ids[i] = placements[i].MessageId;
            xs[i] = placements[i].X;
            ys[i] = placements[i].Y;
            rots[i] = placements[i].RotZDeg;
            key.Append(ids[i]).Append(',').Append(xs[i]).Append(',').Append(ys[i]).Append(',').Append(rots[i]).Append(';');
        }

        string syncKey = key.ToString();
        if (syncKey == lastPinSyncKey)
            return;
        lastPinSyncKey = syncKey;

        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(
            new PersistPaperPins { BoardId = uniqueID, Ids = ids, Xs = xs, Ys = ys, Rots = rots }
        );
    }

    private void UpdateLanternRenderer()
    {
        if (lanternRenderer == null)
            return;
        bool isLegacy = BoardProperties?.EnableLegacyBoard == 1;
        bool isWall = Block?.Variant?["attachment"] == "wall";
        ItemStack leftLantern = null;
        ItemStack rightLantern = null;
        if (!isLegacy && inventory != null)
        {
            if (inventory.Count > 5 && LanternHolderSlot.IsSmallLantern(inventory[5].Itemstack))
                leftLantern = inventory[5].Itemstack;
            if (inventory.Count > 6 && LanternHolderSlot.IsSmallLantern(inventory[6].Itemstack))
                rightLantern = inventory[6].Itemstack;
        }
        lanternRenderer.Update(isWall, GetRotateYDeg(Block), SwayStrength, leftLantern, rightLantern);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        BlockBehaviorShapeTexturesFromAttributes blockBeh =
            Block?.GetBehavior<BlockBehaviorShapeTexturesFromAttributes>();
        if (blockBeh == null)
            return base.OnTesselation(mesher, tessThreadTesselator);

        BlockEntityBehaviorShapeTexturesFromAttributes arlBe =
            GetBehavior<BlockEntityBehaviorShapeTexturesFromAttributes>();
        Variants variants = arlBe?.Variants ?? new Variants();
        if (!variants.Any && types != null)
        {
            string wood = types.GetString("wood");
            string metal = types.GetString("metal");
            string messageCount = types.GetString("messageCount");
            if (!string.IsNullOrEmpty(wood))
                variants.Set("wood", wood);
            if (!string.IsNullOrEmpty(metal))
                variants.Set("metal", metal);
            if (!string.IsNullOrEmpty(messageCount))
                variants.Set("messageCount", messageCount);
        }

        MeshData boardMesh = blockBeh.GetOrCreateMesh(
            variants,
            NoticeBoardBlock.BoardCompositeShape(Block, types),
            Pos,
            "nbshape"
        ).Clone();
        float rotateYDeg = GetRotateYDeg(Block);
        Vec3f blockOrigin = new Vec3f(0.5f, 0.5f, 0.5f);
        if (Math.Abs(rotateYDeg) > 0.01f)
            boardMesh.Rotate(blockOrigin, 0, rotateYDeg * GameMath.DEG2RAD, 0);
        mesher.AddMeshData(boardMesh);

        // Legacy boards carry their full decoration (papers and stamps alike) baked into the
        // ARL shape for types.messageCount; nothing to add on top.
        if (BoardProperties?.EnableLegacyBoard == 1)
            return true;

        int[] ids = visualMessageIds;
        bool isWall = Block?.Variant?["attachment"] == "wall";
        int maxPapers = NoticeBoardPaperLayout.ClampMax(
            BoardProperties?.MaxPapersOnBoard ?? NoticeBoardPaperLayout.DefaultMaxPapers
        );

        Dictionary<int, int> heights = paperHeightUnitsById;

        // Read once into a local: this runs on the tessellation thread, and the field is
        // replaced wholesale rather than mutated.
        var texts = visualMessageTexts;

        bool freezePins = BoardProperties?.EnableManualPin != 0 && BoardProperties?.EnableLegacyBoard == 0;
        IReadOnlyDictionary<int, PaperPin> pins = freezePins
            ? NoticeBoardPaperLayout.PinsFrom(texts)
            : null;

        if (ids != null && ids.Length > 0)
        {
            foreach (PaperPlacement placement in NoticeBoardPaperLayout.Place(
                ids,
                isWall,
                maxPapers,
                heights,
                pins
            ))
            {
                // Where the text renderer paints a sheet, the chunk mesh contributes the nail only.
                // Its quad already carries the parchment, and a sheet baked into the static chunk
                // mesh behind it could not follow the sway. A notice with no text to paint still
                // needs the modelled sheet.
                MessageVisualData data = null;
                bool hasText = texts != null
                    && texts.TryGetValue(placement.MessageId, out data)
                    && !string.IsNullOrWhiteSpace(data.Text);

                MessageHolderDef holderDef = data != null
                    ? MessageHolder.Get(data.Holder)
                    : MessageHolder.Get(MessageHolder.Nail);
                int leanSeed = data != null ? data.ResolveParchmentSeed(placement.MessageId) : placement.MessageId;

                if (!hasText)
                {
                    MeshData modelled = GetModelledPaperUnitMesh(tessThreadTesselator, "paperlabelsample");
                    if (modelled == null)
                        continue;
                    AddPlacement(mesher, modelled, placement, rotateYDeg, blockOrigin);
                    continue;
                }

                if (holderDef.UsesCornerNails)
                {
                    MeshData nail = GetNailUnitMesh(tessThreadTesselator);
                    if (nail == null)
                        continue;
                    PaperSize.TopCornerNailOffsets(
                        out float leftX, out float leftY, out float leftZ,
                        out float rightX, out float rightY, out float rightZ);
                    MeshData left = nail.Clone();
                    MessageHolder.HolderLean(leanSeed, 1, out float leftLx, out float leftLy, out float leftLz);
                    left.Rotate(
                        new Vec3f(0, 0, 0),
                        leftLx * GameMath.DEG2RAD,
                        leftLy * GameMath.DEG2RAD,
                        leftLz * GameMath.DEG2RAD
                    );
                    left.Translate(leftX, leftY, leftZ);
                    MeshData right = nail.Clone();
                    MessageHolder.HolderLean(leanSeed, 2, out float rightLx, out float rightLy, out float rightLz);
                    right.Rotate(
                        new Vec3f(0, 0, 0),
                        rightLx * GameMath.DEG2RAD,
                        rightLy * GameMath.DEG2RAD,
                        rightLz * GameMath.DEG2RAD
                    );
                    right.Translate(rightX, rightY, rightZ);
                    AddPlacement(mesher, left, placement, rotateYDeg, blockOrigin);
                    AddPlacement(mesher, right, placement, rotateYDeg, blockOrigin);
                    continue;
                }

                if (holderDef.UsesItemMesh)
                    continue;

                MeshData unit = GetNailUnitMesh(tessThreadTesselator);
                if (unit == null)
                    continue;
                MeshData tack = unit.Clone();
                MessageHolder.HolderLean(leanSeed, 0, out float lx, out float ly, out float lz);
                tack.Rotate(
                    new Vec3f(0, 0, 0),
                    lx * GameMath.DEG2RAD,
                    ly * GameMath.DEG2RAD,
                    lz * GameMath.DEG2RAD
                );
                tack.Translate(0, 0, -PaperSize.DefaultNailCorkEmbedZ);
                AddPlacement(mesher, tack, placement, rotateYDeg, blockOrigin);
            }
        }

        AddLanternMesh(mesher, blockBeh, variants, isWall, rotateYDeg, blockOrigin);

        return true;
    }

    private void AddLanternMesh(
        ITerrainMeshPool mesher,
        BlockBehaviorShapeTexturesFromAttributes blockBeh,
        Variants variants,
        bool isWall,
        float rotateYDeg,
        Vec3f blockOrigin
    )
    {
        float armY;
        float armZ;
        if (isWall)
        {
            armY = 1.94f;
            armZ = 0.24f;
        }
        else
        {
            armY = 3.06f;
            armZ = 0.61f;
        }

        AddOneArm(mesher, blockBeh, variants, 5, 0.12f, armY, armZ, rotateYDeg, blockOrigin);
        AddOneArm(mesher, blockBeh, variants, 6, 44.5f / 16f, armY, armZ, rotateYDeg, blockOrigin);
    }

    private void AddOneArm(
        ITerrainMeshPool mesher,
        BlockBehaviorShapeTexturesFromAttributes blockBeh,
        Variants variants,
        int slotId,
        float armX,
        float armY,
        float armZ,
        float rotateYDeg,
        Vec3f blockOrigin
    )
    {
        ItemStack stack = inventory != null && inventory.Count > slotId ? inventory[slotId].Itemstack : null;
        if (!LanternHolderSlot.IsSmallLantern(stack))
            return;

        MeshData arm = GetLanternArmMesh(blockBeh, variants);
        if (arm == null)
            return;

        AddRotatedUnitMesh(mesher, arm, armX, armY, armZ, rotateYDeg, blockOrigin);
    }

    private MeshData GetLanternArmMesh(
        BlockBehaviorShapeTexturesFromAttributes blockBeh,
        Variants variants
    )
    {
        if (blockBeh == null || Block == null)
            return null;

        MeshData mesh = blockBeh.GetOrCreateMesh(
            variants,
            new CompositeShape
            {
                Base = new AssetLocation("noticeboard", "block/noticeboard-lantern-arm")
            },
            Pos,
            "nbarm"
        );
        if (mesh == null)
            return null;
        return mesh;
    }

    private static void AddRotatedUnitMesh(
        ITerrainMeshPool mesher,
        MeshData unit,
        float x,
        float y,
        float z,
        float rotateYDeg,
        Vec3f blockOrigin
    )
    {
        MeshData copy = unit.Clone();
        copy.Translate(x, y, z);
        if (Math.Abs(rotateYDeg) > 0.01f)
            copy.Rotate(blockOrigin, 0, rotateYDeg * GameMath.DEG2RAD, 0);
        mesher.AddMeshData(copy);
    }

    private static void AddPlacement(
        ITerrainMeshPool mesher,
        MeshData unit,
        PaperPlacement placement,
        float rotateYDeg,
        Vec3f blockOrigin
    )
    {
        MeshData copy = unit.Clone();
        copy.Translate(placement.X, placement.Y, placement.Z);

        if (Math.Abs(placement.RotZDeg) > 0.01f)
        {
            copy.Rotate(
                new Vec3f(placement.X, placement.Y, placement.Z),
                0,
                0,
                placement.RotZDeg * GameMath.DEG2RAD
            );
        }

        if (Math.Abs(rotateYDeg) > 0.01f)
        {
            copy.Rotate(blockOrigin, 0, rotateYDeg * GameMath.DEG2RAD, 0);
        }

        mesher.AddMeshData(copy);
    }

    private const string PaperShapeName = "noticeboard-paper-unit";

    private static AssetLocation PaperShapeLocation() =>
        new AssetLocation("noticeboard:shapes/block/" + PaperShapeName + ".json");

    internal MeshData GetNailUnitMesh(ITesselatorAPI tesselator)
    {
        bool client = Api is ICoreClientAPI capi && tesselator == capi.Tesselator;
        MeshData cached = client ? nailUnitMeshClient : nailUnitMesh;
        if (cached != null)
            return cached;

        if (Block == null)
            return null;

        // Shape.TryGet deserializes the asset afresh on every call, so dropping the sheet from
        // this copy cannot reach the one GetModelledPaperUnitMesh tessellates.
        Shape shape = Shape.TryGet(Api, PaperShapeLocation());
        ShapeElement notice = shape?.Elements != null && shape.Elements.Length > 0
            ? shape.Elements[0]
            : null;
        if (notice?.Children == null)
            return null;

        var kept = new List<ShapeElement>(1);
        foreach (ShapeElement child in notice.Children)
        {
            if (child?.Name == "tackHead")
                kept.Add(child);
        }
        notice.Children = kept.ToArray();

        tesselator.TesselateShape(Block, shape, out MeshData mesh);
        if (mesh == null)
            return null;

        if (client)
            nailUnitMeshClient = mesh;
        else
            nailUnitMesh = mesh;
        return mesh;
    }

    private MeshData GetModelledPaperUnitMesh(ITesselatorAPI tesselator, string textureCode)
    {
        if (customPaperMeshes.TryGetValue(textureCode, out MeshData cached))
            return cached;

        if (Block == null)
            return null;

        Shape shape = Shape.TryGet(Api, PaperShapeLocation());
        if (shape == null)
            return null;

        var texSource = new PaperTextureSource(
            tesselator.GetTextureSource(Block),
            "paperlabel",
            textureCode
        );
        tesselator.TesselateShape(PaperShapeName, shape, out MeshData mesh, texSource);
        if (mesh == null)
            return null;

        customPaperMeshes[textureCode] = mesh;
        return mesh;
    }

    internal static float GetRotateYDeg(Block block)
    {
        return block?.Variant?["side"] switch
        {
            "east" => 270f,
            "south" => 180f,
            "west" => 90f,
            _ => 0f,
        };
    }

    private static bool IdsEqual(int[] a, int[] b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    private static bool DictionariesEqual(Dictionary<int, MessageVisualData> a, Dictionary<int, MessageVisualData> b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Count != b.Count)
            return false;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out MessageVisualData value) || value.Text != kvp.Value.Text || value.Author != kvp.Value.Author || value.Date != kvp.Value.Date || value.Holder != kvp.Value.Holder || value.PaperTheme != kvp.Value.PaperTheme || value.HasPin != kvp.Value.HasPin || value.PinX != kvp.Value.PinX || value.PinY != kvp.Value.PinY || value.HasPinRotZ != kvp.Value.HasPinRotZ || value.PinRotZ != kvp.Value.PinRotZ || value.PinLayer != kvp.Value.PinLayer || value.TotalHours != kvp.Value.TotalHours || value.PaperSeed != kvp.Value.PaperSeed)
                return false;
        }
        return true;
    }

    private static int[] ParseIdList(string value)
    {
        if (string.IsNullOrEmpty(value))
            return Array.Empty<int>();

        string[] parts = value.Split(',');
        var ids = new List<int>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i], out int id))
                ids.Add(id);
        }
        return ids.ToArray();
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

        textRenderer?.Dispose();
        textRenderer = null;
        lanternRenderer?.Dispose();
        lanternRenderer = null;

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

        if (inventory != null && inventory.Count < 7)
        {
            ItemStack[] kept = new ItemStack[inventory.Count];
            for (int i = 0; i < inventory.Count; i++)
                kept[i] = inventory[i].Itemstack?.Clone();

            quantitySlots = 7;
            InitInventory(Block ?? worldAccessForResolve.GetBlock((ushort)tree.GetInt("forBlockId")), Api);

            for (int i = 0; i < kept.Length; i++)
                inventory[i].Itemstack = kept[i];
        }

        uniqueID = tree.GetString("uniqueID", uniqueID);
        restoreWood = tree.GetString("restoreWood", restoreWood);
        restoreMetal = tree.GetString("restoreMetal", restoreMetal);

        ITreeAttribute loadedTypes = tree.GetTreeAttribute("types");
        if (loadedTypes != null)
            types = (ITreeAttribute)loadedTypes.Clone();

        visualMessageIds = ParseIdList(tree.GetString("visualMessageIds"));

        string textsJson = tree.GetString("visualMessageTexts", null);
        if (!string.IsNullOrEmpty(textsJson))
            visualMessageTexts = JsonUtil.FromString<Dictionary<int, MessageVisualData>>(textsJson) ?? new Dictionary<int, MessageVisualData>();
        else
            visualMessageTexts = new Dictionary<int, MessageVisualData>();

        string boardPropertiesJson = tree.GetString("boardProperties");
        if (!string.IsNullOrEmpty(boardPropertiesJson))
            BoardProperties = JsonUtil.FromString<NoticeBoardObject>(boardPropertiesJson);

        UpdateTextRenderer();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);

        if (!string.IsNullOrEmpty(uniqueID))
            tree.SetString("uniqueID", uniqueID);
        if (!string.IsNullOrEmpty(restoreWood))
            tree.SetString("restoreWood", restoreWood);
        if (!string.IsNullOrEmpty(restoreMetal))
            tree.SetString("restoreMetal", restoreMetal);
        if (types != null)
            tree["types"] = types.Clone();

        tree.SetString("visualMessageIds", string.Join(",", visualMessageIds ?? Array.Empty<int>()));

        if (visualMessageTexts != null && visualMessageTexts.Count > 0)
            tree.SetString("visualMessageTexts", JsonUtil.ToString(visualMessageTexts));

        if (BoardProperties != null)
            tree.SetString("boardProperties", JsonUtil.ToString(BoardProperties));

        if (Block != null)
            tree.SetInt("forBlockId", Block.BlockId);
    }
}
