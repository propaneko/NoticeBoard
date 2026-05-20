using NoticeBoard;
using NoticeBoard.BlockType;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace NoticeBoard.Gui;

public class NoticeBoardMainWindowGui : GuiDialog
{
    private readonly string boardId;
    private readonly BlockPos boardPos;
    private readonly string boardPlayerId;
    private readonly string boardPlayerName;

    private List<Message> messages;
    private List<PlayerEntry> players;

    private readonly ResponseAllMessages noticeBoardPacket;

    private NoticeBoardTextInputWindowGui textInputGui;
    private bool isLocked;
    private bool enableParticles;
    private bool enableParchment;
    private string selectedPlayerUid;

    private int activeTab = 0;
    private double lastCalculatedContentHeight = 0.0;

    public override string ToggleKeyCombinationCode => null;

    public NoticeBoardMainWindowGui(string dialogTitle, ResponseAllMessages packet, ICoreClientAPI capi) : base(capi)
    {
        this.boardId = packet.BoardProperties.BoardId;
        this.boardPlayerId = packet.BoardProperties.PlayerId;
        this.boardPlayerName = packet.BoardProperties.PlayerName;
        this.boardPos = PositionHelper.FromString(packet.BoardProperties.Pos);

        this.isLocked = packet.BoardProperties.isLocked != 0;
        this.enableParticles = packet.BoardProperties.enableParticles != 0;
        this.enableParchment = packet.BoardProperties.enableParchment != 0;

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

    private bool OpenTextInput(string mode, int messageId, string currentText)
    {
        if (this.textInputGui != null && this.textInputGui.IsOpened())
        {
            this.textInputGui.TryClose();
        }

        this.textInputGui = new NoticeBoardTextInputWindowGui(this.capi, this, this.noticeBoardPacket, mode, messageId, currentText);
        this.textInputGui.TryOpen();
        return true;
    }

    private bool AddMessage() {
        if (!enableParchment)
        {
            return OpenTextInput("add", -1, "");
        }

        NoticeBoardBlockEntity blockEntity =
         capi.World.BlockAccessor.GetBlockEntity(this.boardPos) as NoticeBoardBlockEntity;

        if (blockEntity == null)
        {
            capi.TriggerIngameError(this, "missing_board", "Notice board inventory missing!");
            return false;
        }

        ItemSlot slot = blockEntity.Inventory[0];

        if (slot.Empty)
        {
            capi.TriggerIngameError(this, "no_item", "Place a parchment in the slot first!");
            return false;
        }

        ItemStack stack = slot.Itemstack;

        if (stack?.Collectible?.Code == null)
        {
            capi.TriggerIngameError(this, "invalid_item", "Invalid item!");
            return false;
        }

        string path = stack.Collectible.Code.Path;

        bool isParchment =
            path == "paper-parchment" ||
            path.StartsWith("paper-parchment");

        if (!isParchment)
        {
            capi.TriggerIngameError(this, "wrong_item", "You must use parchment!");
            return false;
        }

        return OpenTextInput("add", -1, "");
    }

    private bool EditMessage(int id)
    {
        Message message = this.messages.Find(m => m.Id == id);
        return message != null && OpenTextInput("edit", message.Id, message.Text);
    }

    private bool RemoveMessage(int id)
    {
        PlayerRemoveMessage removeMessage = new PlayerRemoveMessage { MessageId = id };
        capi.Network.GetChannel("noticeboard").SendPacket(removeMessage);
        this.GetMessages();
        return true;
    }

    private void OnToggleLock(bool lockedState)
    {
        this.isLocked = lockedState;
        EditIsLocked editPacket = new EditIsLocked { BoardId = this.boardId, isLocked = this.isLocked };
        capi.Network.GetChannel("noticeboard").SendPacket(editPacket);
    }

    private void OnToggleParticles(bool enabledState)
    {
        this.enableParticles = enabledState;
        EditEnableParticles editPacket = new EditEnableParticles { BoardId = this.boardId, enableParticles = this.enableParticles };
        capi.Network.GetChannel("noticeboard").SendPacket(editPacket);
    }

    private void OnToggleParchment(bool enabledState)
    {
        this.enableParchment = enabledState;
        EditEnableParchment editPacket = new EditEnableParchment { BoardId = this.boardId, enableParchment = this.enableParchment };
        capi.Network.GetChannel("noticeboard").SendPacket(editPacket);
    }

    public override void OnGuiOpened() => ComposeGui();

    private void OnTabClicked(int tabIndex)
    {
        if (this.activeTab == tabIndex) return;
        this.activeTab = tabIndex;
        this.RefreshMessageList();
    }

    public void ComposeGui()
    {
        capi.Network.GetChannel("noticeboard").SendPacket(new RequestAllPlayers());
        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);
        double insetWidth = 575.0;
        double insetHeight = 550.0;
        int insetDepth = 3;

        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithFixedWidth(860.0)
            .WithAlignment(EnumDialogArea.CenterTop)
            .WithFixedOffset(0.0, 40.0);

        ElementBounds insetBounds = ElementBounds.Fixed(0.0, GuiStyle.TitleBarHeight, insetWidth, insetHeight);
        ElementBounds scrollbarBounds = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(20.0);
        ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds buttonBoundsAddNotice = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(120.0).WithFixedHeight(40.0).WithFixedOffset(36.0, 0.0);

        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds, buttonBoundsAddNotice);

        GuiComposer dialogComposer = capi.Gui.CreateCompo("noticeBoardScrollGui", dialogBounds);
        GuiComposerHelpers.AddShadedDialogBG(dialogComposer, bgBounds, true, 5.0, 0.75f);
        GuiComposerHelpers.AddDialogTitleBar(dialogComposer, Lang.Get("noticeboard:main-window-title-bar") + $" - Owner: {this.boardPlayerName}", this.OnTitleBarClose);

        ElementBounds tabBounds = ElementBounds.Fixed(0.0, -32, insetWidth, 35.0);
        GuiTab[] tabs = new GuiTab[]
        {
            new GuiTab() { Name = Lang.Get("Messages"), DataInt = 0 },
            isOwner ? new GuiTab() { Name = Lang.Get("Settings"), DataInt = 1 } : null
        };

        dialogComposer.AddHorizontalTabs(tabs, tabBounds, this.OnTabClicked, CairoFont.WhiteSmallText(), CairoFont.WhiteSmallText(), "topTabs");
        dialogComposer.GetHorizontalTabs("topTabs").SetValue(this.activeTab);

        if (this.activeTab == 0)
        {

            NoticeBoardBlockEntity blockEntity = capi.World.BlockAccessor.GetBlockEntity(this.boardPos) as NoticeBoardBlockEntity;

            if (blockEntity != null && this.enableParchment)
            {
                ElementBounds slotBounds = buttonBoundsAddNotice.RightCopy(-120.0, 48.0, 0.0, 0.0)
                    .WithFixedWidth(48.0)
                    .WithFixedHeight(48.0);

                dialogComposer.AddItemSlotGrid(
                    blockEntity.Inventory,
                    (packet) =>
                    {
                        capi.Network.SendBlockEntityPacket(
                            boardPos.X,
                            boardPos.Y,
                            boardPos.Z,
                            packet
                        );
                    },
                    1,
                    slotBounds,
                    "costSlot"
                );
            }

            GuiElementInsetHelper.AddInset(dialogComposer, insetBounds, insetDepth, 0.85f);
            GuiElementClipHelpler.BeginClip(dialogComposer, clipBounds);
            GuiComposerHelpers.AddContainer(dialogComposer, containerBounds, "scroll-content");
            GuiElementClipHelpler.EndClip(dialogComposer);

            GuiComposerHelpers.AddVerticalScrollbar(dialogComposer, this.OnNewScrollbarValue, scrollbarBounds, "scrollbar");
            GuiComposerHelpers.AddSmallButton(dialogComposer, Lang.Get("noticeboard:main-window-add-notice-button"), () => this.AddMessage(), buttonBoundsAddNotice, EnumButtonStyle.Normal, "addNoticeButton");

            PopulateMessagesTab(dialogComposer, insetBounds);
        }
        else if (this.activeTab == 1)
        {
            ElementBounds settingsInsetBounds = ElementBounds.Fixed(20, GuiStyle.TitleBarHeight + 20, insetWidth + 155, insetHeight);
            GuiElementInsetHelper.AddInset(dialogComposer, settingsInsetBounds, insetDepth, 0.85f);
            GuiComposerHelpers.AddContainer(dialogComposer, containerBounds, "settings-content");

            PopulateSettingsTab(dialogComposer, settingsInsetBounds);
        }

        base.SingleComposer = dialogComposer.Compose();

        if (this.activeTab == 0)
        {
            var scrollbar = GuiComposerHelpers.GetScrollbar(base.SingleComposer, "scrollbar");
            if (scrollbar != null)
            {
                scrollbar.SetHeights((float)insetBounds.InnerHeight, (float)this.lastCalculatedContentHeight);
            }

            if (this.isLocked && !isOwner)
            {
                var addBtn = GuiComposerHelpers.GetButton(base.SingleComposer, "addNoticeButton");
                if (addBtn != null) addBtn.Enabled = false;
            }
        }
    }

    private void PopulateMessagesTab(GuiComposer composer, ElementBounds insetBounds)
    {
        double insetWidth = 575.0;
        double minRowHeight = 60.0;
        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);

        ElementBounds containerRowBounds = ElementBounds.Fixed(0.0, 0.0, insetWidth, minRowHeight);
        GuiElementContainer scrollArea = GuiComposerHelpers.GetContainer(composer, "scroll-content");

        this.lastCalculatedContentHeight = 0.0;

        if (this.messages != null && this.messages.Count > 0)
        {
            for (int i = 0; i < this.messages.Count; i++)
            {
                Message message = this.messages[i];
                string messageText = message.Text;
                int id = this.messages[i].Id;
                double textHeight = this.CalculateTextHeight(messageText, insetWidth);
                double rowHeight = Math.Max(textHeight, minRowHeight);

                containerRowBounds.WithFixedHeight(rowHeight);
                containerRowBounds.WithFixedWidth(500.0);
                containerRowBounds.WithFixedPadding(0.0, 8.0);

                ElementBounds containerRowBoundsButton = containerRowBounds.RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(530.0, containerRowBounds.fixedY + 8.0)
                    .WithFixedHeight(20.0)
                    .WithFixedWidth(30.0);
                containerRowBoundsButton.WithFixedMargin(0.0, 8.0);

                ElementBounds editButtonBounds = containerRowBounds.RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(530.0, containerRowBounds.fixedY + 38.0)
                    .WithFixedHeight(20.0)
                    .WithFixedWidth(30.0);
                editButtonBounds.WithFixedMargin(0.0, 8.0);

                GuiElementStaticText textElement = new GuiElementStaticText(this.capi, messageText, 0, containerRowBounds, CairoFont.WhiteDetailText());
                scrollArea.Add(textElement, -1);

                bool isSender = (message.PlayerId == capi.World.Player.PlayerUID);
                bool canEdit = isOwner || (!this.isLocked && isSender);
                if (canEdit)
                {
                    scrollArea.Add(new GuiElementTextButton(this.capi, "X", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.RemoveMessage(id), containerRowBoundsButton, EnumButtonStyle.Normal), -1);
                    scrollArea.Add(new GuiElementTextButton(this.capi, "E", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.EditMessage(id), editButtonBounds, EnumButtonStyle.Normal), -1);
                }

                this.lastCalculatedContentHeight += rowHeight + 32.0f;
                containerRowBounds = containerRowBounds.BelowCopy(0.0, 16.0, 0.0, 0.0);
            }
        }
        else
        {
            scrollArea.Add(new GuiElementStaticText(this.capi, "", 0, containerRowBounds, CairoFont.WhiteDetailText()), -1);
            this.lastCalculatedContentHeight = minRowHeight + 60.0;
        }
    }

    private void PopulateSettingsTab(GuiComposer composer, ElementBounds insetBounds)
    {
        ElementBounds textBounds = ElementBounds.FixedSize(300, 30).WithFixedPosition(insetBounds.fixedX + 20, insetBounds.fixedY + 20);
        ElementBounds switchBounds = ElementBounds.FixedSize(50, 30).WithFixedPosition(insetBounds.fixedX + 300, insetBounds.fixedY + 15);

        composer.AddStaticText("Lock Notice Board:", CairoFont.WhiteSmallText(), textBounds);

        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);

        if (isOwner)
        {
            composer.AddSwitch(this.OnToggleLock, switchBounds, "lockSwitch");
            composer.GetSwitch("lockSwitch").On = this.isLocked;
        }
        else
        {
            ElementBounds infoBounds = ElementBounds.FixedSize(300, 30).WithFixedPosition(insetBounds.fixedX + 200, insetBounds.fixedY + 20);
            composer.AddStaticText(Lang.Get("(Only the owner can lock this board)"), CairoFont.WhiteSmallText(), infoBounds);
        }

        ElementBounds textBoundsParticles = ElementBounds.FixedSize(300, 30).WithFixedPosition(insetBounds.fixedX + 20, insetBounds.fixedY + 80);
        ElementBounds switchBoundsParticles = ElementBounds.FixedSize(50, 30).WithFixedPosition(insetBounds.fixedX + 300, insetBounds.fixedY + 75);

        composer.AddStaticText("Enable Particles:", CairoFont.WhiteSmallText(), textBoundsParticles);

        if (isOwner)
        {
            composer.AddSwitch(this.OnToggleParticles, switchBoundsParticles, "particlesSwitch");
            composer.GetSwitch("particlesSwitch").On = this.enableParticles;
        }
        else
        {
            ElementBounds infoBounds = ElementBounds.FixedSize(300, 30).WithFixedPosition(insetBounds.fixedX + 220, insetBounds.fixedY + 80);
            composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), infoBounds);
        }

        ElementBounds textBoundsParchment = ElementBounds.FixedSize(300, 30).WithFixedPosition(insetBounds.fixedX + 20, insetBounds.fixedY + 140);
        ElementBounds switchBoundsParchment = ElementBounds.FixedSize(50, 30).WithFixedPosition(insetBounds.fixedX + 300, insetBounds.fixedY + 135);

        composer.AddStaticText("Enable parchment:", CairoFont.WhiteSmallText(), textBoundsParchment);

        if (isOwner)
        {
            composer.AddSwitch(this.OnToggleParchment, switchBoundsParchment, "parchmentSwitch");
            composer.GetSwitch("parchmentSwitch").On = this.enableParchment;
        }
        else
        {
            ElementBounds infoBounds = ElementBounds.FixedSize(300, 30).WithFixedPosition(insetBounds.fixedX + 220, insetBounds.fixedY + 140);
            composer.AddStaticText(Lang.Get("(Only the owner can edit this)"), CairoFont.WhiteSmallText(), infoBounds);
        }

        ElementBounds textOwnerSelectBounds = ElementBounds.FixedSize(300, 30).WithFixedPosition(insetBounds.fixedX + 20, insetBounds.fixedY + 200);
        ElementBounds dropdownOwnerSelectBounds = ElementBounds.FixedSize(200, 30).WithFixedPosition(insetBounds.fixedX + 300, insetBounds.fixedY + 190);

        composer.AddStaticText("Owner:", CairoFont.WhiteSmallText(), textOwnerSelectBounds);
        composer.AddDropDown(
            players.Select(p => p.PlayerUID).ToArray(),
            players.Select(p => p.PlayerName).ToArray(),
            players.FindIndex(p => p.PlayerUID == boardPlayerId),
            (code, value) =>
            {
                selectedPlayerUid = code;
            },
            dropdownOwnerSelectBounds,
            "ownerDropdown"
        );
    }

    private double CalculateTextHeight(string text, double width)
    {
        return capi.Gui.Text.GetMultilineTextHeight(CairoFont.WhiteDetailText(), text, width);
    }

    private void OnNewScrollbarValue(float value)
    {
        var content = GuiComposerHelpers.GetContainer(base.SingleComposer, "scroll-content");
        if (content != null)
        {
            content.Bounds.fixedY = -value;
            content.Bounds.CalcWorldBounds();
        }
    }

    public void GetMessages()
    {
        RequestAllMessages requestPacket = new RequestAllMessages { BoardId = this.boardId };
        capi.Network.GetChannel("noticeboard").SendPacket(requestPacket);
    }

    public void RefreshMessageList()
    {
        base.SingleComposer?.Dispose();
        this.OnGuiOpened();
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();

        NoticeBoardBlockEntity blockEntity = capi.World.BlockAccessor.GetBlockEntity(this.boardPos) as NoticeBoardBlockEntity;
        if (blockEntity != null)
        {
            capi.World.Player.InventoryManager.CloseInventory(blockEntity.Inventory);
        }

        capi.Network.SendBlockEntityPacket(this.boardPos.X, this.boardPos.Y, this.boardPos.Z, 999, null);
    }

    private void OnTitleBarClose() => TryClose();
}