using Vintagestory.API.Common;

public class MyCustomInventory : InventoryGeneric
{
    public MyCustomInventory(int slotsCount, string className, ICoreAPI api) : base(slotsCount, className, api)
    {
    }

    // You can add custom behavior for your inventory here
}