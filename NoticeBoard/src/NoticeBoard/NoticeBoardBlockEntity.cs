using NoticeBoard.Database;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace NoticeBoard.BlockType;

public class NoticeBoardBlockEntity : BlockEntity
{
    public string uniqueID;
    private ICoreAPI api;
    private double actionInterval = 1;
    private long listener;


    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        this.api = api;

        if (!Api.World.Side.IsServer()) return;
        listener = RegisterGameTickListener(OnPerformAction, (int)(actionInterval * 500));
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
    }

    public override void OnBlockRemoved()
    {
        UnregisterGameTickListener(listener);
        base.OnBlockRemoved();
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
