using System;
using System.Collections.Generic;
using NoticeBoard.BlockType;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
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
    private float boardFontSize;
    private float pendingFontSize;
    private bool isDirty = false;

    private int activeTab = 0;
    private double lastCalculatedContentHeight = 0.0;
    private float currentScrollY = 0f;
    private bool isComposing = false;
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
        this.boardFontSize = packet.BoardProperties.BoardFontSize;
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
        this.isComposing = true;
        capi.Network.GetChannel("noticeboard").SendPacket(new RequestAllPlayers());
        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);
        NoticeBoardBlockEntity blockEntity = capi.GetNoticeBoardEntity(this.boardPos);

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
            140.0,
            40.0
        );
        ElementBounds slotBounds = ElementBounds.Fixed(
            148.0,
            GuiStyle.TitleBarHeight + 1.0,
            200.0,
            48.0
        );

        ElementBounds buttonBoundsAddDocument = ElementBounds.Fixed(
            listWidth - 100,
            GuiStyle.TitleBarHeight + 25.0,
            140.0,
            40.0
        );

        ElementBounds documentSlotBounds = ElementBounds.Fixed(
            listWidth - 158.0,
            GuiStyle.TitleBarHeight + 20.0,
            48.0,
            48.0
        );

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

        containerBounds.fixedY = -this.currentScrollY;

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
        List<GuiTab> tabList = new List<GuiTab>
        {
            new GuiTab() { Name = Lang.Get("noticeboard:main-window-tab-messages"), DataInt = 0 },
        };

        if (isOwner)
        {
            tabList.Add(
                new GuiTab()
                {
                    Name = Lang.Get("noticeboard:main-window-tab-settings"),
                    DataInt = 1,
                }
            );
        }

        GuiTab[] tabs = tabList.ToArray();

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
            if (blockEntity != null && this.enableParchment)
            {
                dialogComposer.AddItemSlotGrid(
                    blockEntity.Inventory,
                    (packet) =>
                        capi.Network.SendBlockEntityPacket(
                            boardPos.X,
                            boardPos.Y,
                            boardPos.Z,
                            packet
                        ),
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
                    Lang.Get("noticeboard:main-window-add-document-button"),
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
                (value) => this.OnNewScrollbarValue(value),
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
                insetHeight + 60
            );
            GuiElementInsetHelper.AddInset(dialogComposer, settingsInsetBounds, insetDepth, 0.85f);
            GuiComposerHelpers.AddContainer(dialogComposer, containerBounds, "settings-content");

            PopulateSettingsTab(dialogComposer, settingsInsetBounds);
        }

        base.SingleComposer = dialogComposer.Compose();

        var slotGrid = base.SingleComposer.GetSlotGrid("costSlot");
        if (slotGrid != null)
        {
            slotGrid.DrawIconHandler = (cr, slotId, x, y, w, h, color) =>
            {
                if (blockEntity.Inventory[4].Empty)
                {
                    capi.Gui.Icons.DrawIcon(
                        cr,
                        "circle",
                        x + 4,
                        y + 4,
                        w - 8,
                        h - 8,
                        new double[] { 1, 1, 1, 0.2 }
                    );
                }
            };
        }

        if (this.activeTab == 0)
        {
            var scrollbar = GuiComposerHelpers.GetScrollbar(base.SingleComposer, "scrollbar");
            if (scrollbar != null)
            {
                scrollbar.SetHeights(
                    (float)insetBounds.fixedHeight,
                    (float)this.lastCalculatedContentHeight
                );

                float maxScroll = Math.Max(
                    0,
                    (float)this.lastCalculatedContentHeight - (float)insetBounds.fixedHeight
                );
                this.currentScrollY = Math.Min(this.currentScrollY, maxScroll);

                scrollbar.CurrentYPosition = this.currentScrollY;

                OnNewScrollbarValue(this.currentScrollY);
            }

            if (this.isLocked && !isOwner)
            {
                GuiComposerHelpers
                    .GetButton(base.SingleComposer, "addNoticeButton")
                    ?.Enabled = false;
            }
        }
        this.isComposing = false;
    }
}
