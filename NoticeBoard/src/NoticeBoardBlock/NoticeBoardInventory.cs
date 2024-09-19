using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace NoticeBoard.Entity;
public class NoticeBoardInventory : InventoryBase, ISlotProvider
{
    public ItemSlot[] Slots
    {
        get
        {
            return this.slots;
        }
    }

    public NoticeBoardInventory(string inventoryID, ICoreAPI api, NoticeBoardBlockEntity entityNoticeBoard) : base(inventoryID, api)
    {
        this.slots = base.GenEmptySlots(1);
    }

    public override int Count
    {
        get
        {
            return 1;
        }
    }

    public override ItemSlot this[int slotId]
    {
        get
        {
            return this.slots[slotId];
        }
        set
        {
            this.slots[slotId] = value;
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        this.slots = this.SlotsFromTreeAttributes(tree, this.slots, null);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.SlotsToTreeAttributes(this.slots, tree);
    }

    public override bool CanPlayerAccess(IPlayer player, EntityPos position)
    {
        return base.CanPlayerAccess(player, position);
    }

    private ItemSlot[] slots;
}