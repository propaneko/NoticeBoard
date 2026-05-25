using NoticeBoard.BlockType;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace NoticeBoard.src.Gui.Windows;

public partial class NoticeBoardMainWindowGui : GuiDialog
{
    private readonly string boardId;
    private readonly BlockPos boardPos;
    private readonly string boardPlayerId;
    private readonly string boardPlayerName;

    private List<Message> messages;
    private List<PlayerEntry> players;

    private readonly ResponseAllMessages noticeBoardPacket;

    private NoticeBoardTextInputWindowGui textInputGui;
    private string boardName;
    private string boardFont;
    private string boardTheme;

    private bool isLocked;
    private bool enableParticles;
    private bool enableParchment;
    private bool enableProximityMessage;
    private string proximityChannel;
    private double proximityDistance;
    private string pendingOwnerUid;
    private int pendingDistance;
    private bool isDirty = false;

    private int activeTab = 0;
    private double lastCalculatedContentHeight = 0.0;

    public override string ToggleKeyCombinationCode => null;

    public NoticeBoardMainWindowGui(
        string dialogTitle,
        ResponseAllMessages packet,
        ICoreClientAPI capi
    )
        : base(capi)
    {
        this.boardId = packet.BoardProperties.BoardId;
        this.boardName = packet.BoardProperties.BoardName;
        this.boardFont = packet.BoardProperties.BoardFont;
        this.boardTheme = packet.BoardProperties.BoardTheme;
        this.boardPlayerId = packet.BoardProperties.PlayerId;
        this.boardPlayerName = packet.BoardProperties.PlayerName;
        this.boardPos = PositionHelper.FromString(packet.BoardProperties.Pos);
        this.isLocked = packet.BoardProperties.IsLocked != 0;
        this.enableParticles = packet.BoardProperties.EnableParticles != 0;
        this.enableParchment = packet.BoardProperties.EnableParchment != 0;
        this.enableProximityMessage = packet.BoardProperties.EnableProximity != 0;
        this.proximityChannel = packet.BoardProperties.ProximityChannel;
        this.proximityDistance = packet.BoardProperties.ProximityDistance;
        this.messages = packet.Messages;
        this.noticeBoardPacket = packet;
    }

    public void UpdateMessages(List<Message> newMessages)
    {
        this.messages = newMessages;
        this.RefreshMessageList();
    }

    public void UpdatePlayersList(List<PlayerEntry> players)
    {
        this.players = players;
    }

    public override void OnGuiOpened()
    {
        ComposeGui();
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();

        if (
            capi.World.BlockAccessor.GetBlockEntity(this.boardPos)
            is NoticeBoardBlockEntity blockEntity
        )
        {
            capi.World.Player.InventoryManager.CloseInventory(blockEntity.Inventory);
        }

        capi.Network.SendBlockEntityPacket(
            this.boardPos.X,
            this.boardPos.Y,
            this.boardPos.Z,
            999,
            null
        );
    }

    private void OnTabClicked(int tabIndex)
    {
        if (this.activeTab == 1 && tabIndex == 0)
            ResetSettings();

        if (this.activeTab == tabIndex)
            return;

        this.activeTab = tabIndex;
        this.RefreshMessageList();
    }

    private void OnTitleBarClose() => TryClose();

    public void ComposeGui()
    {
        capi.Network.GetChannel("noticeboard").SendPacket(new RequestAllPlayers());
        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);

        double dialogWidth = 800;
        double listWidth = 720;
        double insetHeight = 500.0;
        int insetDepth = 3;

        ElementBounds dialogBounds = ElementStdBounds
            .AutosizedMainDialog.WithFixedWidth(dialogWidth)
            .WithAlignment(EnumDialogArea.CenterTop)
            .WithFixedOffset(0.0, 40.0);

        ElementBounds buttonBoundsAddNotice = ElementBounds.Fixed(
            0,
            GuiStyle.TitleBarHeight + 5.0,
            120.0,
            40.0
        );
        ElementBounds slotBounds = ElementBounds.Fixed(
            128.0,
            GuiStyle.TitleBarHeight + 1.0,
            200.0,
            48.0
        );

        ElementBounds buttonBoundsAddDocument = ElementBounds.Fixed(
            listWidth - 100 ,
            GuiStyle.TitleBarHeight + 25.0,
            140.0,
            40.0
        );

        ElementBounds documentSlotBounds = ElementBounds.Fixed(listWidth - 158.0, GuiStyle.TitleBarHeight + 20.0, 48.0, 48.0);

        ElementBounds insetBounds = ElementBounds.Fixed(
            0.0,
            GuiStyle.TitleBarHeight + 60.0,
            listWidth,
            insetHeight
        );
        ElementBounds scrollbarBounds = insetBounds
            .RightCopy(0.0, 0.0, 0.0, 0.0)
            .WithFixedWidth(20.0);
        ElementBounds clipBounds = insetBounds.ForkContainingChild(
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding
        );
        ElementBounds containerBounds = insetBounds.ForkContainingChild(
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding
        );

        ElementBounds bgBounds = ElementBounds
            .Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds, buttonBoundsAddNotice, slotBounds);

        GuiComposer dialogComposer = capi.Gui.CreateCompo("noticeBoardScrollGui", dialogBounds);
        GuiComposerHelpers.AddShadedDialogBG(dialogComposer, bgBounds, true, 5.0, 0.75f);
        GuiComposerHelpers.AddDialogTitleBar(
            dialogComposer,
            Lang.Get("noticeboard:main-window-title-bar") + $" - Owner: {this.boardPlayerName}",
            this.OnTitleBarClose
        );

        ElementBounds tabBounds = ElementBounds.Fixed(0.0, -32, listWidth, 35.0);
        GuiTab[] tabs =
        [
            new() { Name = Lang.Get("Messages"), DataInt = 0 },
            isOwner ? new GuiTab() { Name = Lang.Get("Settings"), DataInt = 1 } : null,
        ];

        dialogComposer.AddHorizontalTabs(
            tabs,
            tabBounds,
            this.OnTabClicked,
            CairoFont.WhiteSmallText(),
            CairoFont.WhiteSmallText(),
            "topTabs"
        );
        dialogComposer.GetHorizontalTabs("topTabs").SetValue(this.activeTab);

        if (this.activeTab == 0)
        {

            if (
                capi.World.BlockAccessor.GetBlockEntity(this.boardPos)
                    is NoticeBoardBlockEntity blockEntity
                && this.enableParchment
            )
            {
                dialogComposer.AddItemSlotGrid(
                    blockEntity.Inventory,
                    (packet) => capi.Network.SendBlockEntityPacket(boardPos.X, boardPos.Y, boardPos.Z, packet),
                    1,
                    new int[] { 4 },
                    documentSlotBounds,
                    "writtenSlot"
                );

                dialogComposer.AddItemSlotGrid(
                    blockEntity.Inventory,
                    (packet) =>
                        capi.Network.SendBlockEntityPacket(
                            boardPos.X,
                            boardPos.Y,
                            boardPos.Z,
                            packet
                        ),
                    4,
                    new int[] { 0, 1, 2, 3 },
                    slotBounds,
                    "costSlot"
                );

                GuiComposerHelpers.AddSmallButton(
                   dialogComposer,
                   "Add Document",
                   () => this.OnPostDocumentClick(),
                   buttonBoundsAddDocument,
                   EnumButtonStyle.Normal,
                   "addDocumentButton"
               );
            }

            GuiElementInsetHelper.AddInset(dialogComposer, insetBounds, insetDepth, 0.85f);
            GuiElementClipHelpler.BeginClip(dialogComposer, clipBounds);
            GuiComposerHelpers.AddContainer(dialogComposer, containerBounds, "scroll-content");
            GuiElementClipHelpler.EndClip(dialogComposer);
            GuiComposerHelpers.AddVerticalScrollbar(
                dialogComposer,
                this.OnNewScrollbarValue,
                scrollbarBounds,
                "scrollbar"
            );

            GuiComposerHelpers.AddSmallButton(
                dialogComposer,
                Lang.Get("noticeboard:main-window-add-notice-button"),
                () => this.OnAddMessageClick(),
                buttonBoundsAddNotice,
                EnumButtonStyle.Normal,
                "addNoticeButton"
            );

           

            PopulateMessagesTab(dialogComposer, insetBounds);
        }
        else if (this.activeTab == 1)
        {
            ElementBounds settingsInsetBounds = ElementBounds.Fixed(
                20,
                GuiStyle.TitleBarHeight + 20,
                listWidth + 20,
                insetHeight
            );
            GuiElementInsetHelper.AddInset(dialogComposer, settingsInsetBounds, insetDepth, 0.85f);
            GuiComposerHelpers.AddContainer(dialogComposer, containerBounds, "settings-content");

            PopulateSettingsTab(dialogComposer, settingsInsetBounds);
        }

        base.SingleComposer = dialogComposer.Compose();

        if (this.activeTab == 0)
        {
            var scrollbar = GuiComposerHelpers.GetScrollbar(base.SingleComposer, "scrollbar");
            scrollbar?.SetHeights(
                (float)insetBounds.InnerHeight,
                (float)this.lastCalculatedContentHeight
            );

            if (this.isLocked && !isOwner)
            {
                GuiComposerHelpers
                    .GetButton(base.SingleComposer, "addNoticeButton")
                    ?.Enabled = false;
            }
        }
    }
}
