using NoticeBoard;
using System.Collections.Generic;
using Vintagestory.API.Client;
using NoticeBoard.Packets;
using Vintagestory.API.Config;
using System;

namespace NoticeBoard.Gui;

public class NoticeBoardMainWindowGui : GuiDialog
{
    private string boardId;
    private string boardPlayerId;
    private string boardPlayerName;

    private List<Message> messages;
    private ResponseAllMessages noticeBoardPacket;

    public NoticeBoardMainWindowGui context;
    private NoticeBoardTextInputWindowGui textInputGui;
    private bool isLocked;


    public NoticeBoardMainWindowGui(string dialogTitle, ResponseAllMessages packet, ICoreClientAPI capi) : base(capi)
    {
        this.boardId = packet.BoardProperties.BoardId;
        this.boardPlayerId = packet.BoardProperties.PlayerId;
        this.boardPlayerName = packet.BoardProperties.PlayerName;

        this.isLocked = packet.BoardProperties.isLocked == 0 ? false : true;
        this.messages = packet.Messages;
        this.noticeBoardPacket = packet;
        this.context = this;
    }

    public void UpdateMessages(List<Message> messages)
    {
        this.messages = messages;
        this.RefreshMessageList();
    }

    private bool AddMessage()
    {
        if (this.textInputGui == null || !this.textInputGui.IsOpened())
        {
            this.textInputGui = new NoticeBoardTextInputWindowGui(this.capi, this.context, this.noticeBoardPacket, "add", -1, "");
            this.textInputGui.TryOpen();
        }
        else
        {
            this.textInputGui.TryClose();
            this.textInputGui.TryOpen();
        }
        return true;
    }

    private bool RemoveMessage(int id)
    {
        PlayerRemoveMessage removeMessage = new PlayerRemoveMessage
        {
            MessageId = id
        };
        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket<PlayerRemoveMessage>(removeMessage);
        this.GetMessages();
        return true;
    }

    private bool EditMessage(int id)
    {
        Message message = this.messages.Find((Message m) => m.Id == id);
        if (this.textInputGui == null || !this.textInputGui.IsOpened())
        {
            this.textInputGui = new NoticeBoardTextInputWindowGui(this.capi, this.context, this.noticeBoardPacket, "edit", message.Id, message.Text);
            this.textInputGui.TryOpen();
        }
        else
        {
            this.textInputGui.TryClose();
            this.textInputGui.TryOpen();
        }
        return true;
    }

    private bool LockNoticeBoard()
    {
        isLocked = !isLocked;
        if (this.boardPlayerId == capi.World.Player.PlayerUID)
        {
            GuiComposerHelpers.GetButton(base.SingleComposer, "addNoticeButton").Enabled = true;
        } else
        {
            GuiComposerHelpers.GetButton(base.SingleComposer, "addNoticeButton").Enabled = false;
        }

        GuiComposerHelpers.GetButton(base.SingleComposer, "lockBoard").Text = isLocked ? "Unlock" : "Lock";

        this.RefreshMessageList();
        EditIsLocked editIsLocked = new EditIsLocked
        {
            BoardId = this.boardId,
            isLocked = isLocked
        };
        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket<EditIsLocked>(editIsLocked);
        return true;
    }


    //public override void OnGuiOpened()
    //{
    //    double insetWidth = 575.0;
    //    double insetHeight = 300.0;

    //    int insetDepth = 3;
    //    double minRowHeight = 80.0;

    //    //int rowHeight = 80;
    //    ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithFixedWidth(860.0).WithAlignment((EnumDialogArea)5).WithFixedOffset(0.0, 40.0);
    //    ElementBounds insetBounds = ElementBounds.Fixed(0.0, GuiStyle.TitleBarHeight, insetWidth, insetHeight);
    //    ElementBounds.Fixed(0.0, GuiStyle.TitleBarHeight, insetWidth, insetHeight);
    //    ElementBounds scrollbarBounds = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(20.0);
    //    ElementBounds.Fixed(0.0, 40.0, 300.0, 100.0);
    //    ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
    //    ElementBounds containerBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
    //    ElementBounds containerRowBounds = ElementBounds.Fixed(0.0, 0.0, insetWidth, minRowHeight);
    //    ElementBounds containerRowBoundsButton = ElementBounds.Fixed(870.0, 0.0, 20.0, 20.0);
    //    ElementBounds editButtonBounds = ElementBounds.Fixed(870.0, 0.0, 20.0, 20.0);
    //    ElementBounds buttonBoundsAddNotice = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(120.0).WithFixedHeight(40.0).WithFixedOffset(36.0, 0.0);
    //    ElementBounds buttonBoundsLock = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(120.0).WithFixedHeight(40.0).WithFixedOffset(36.0, 48.0);

    //    ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing((ElementSizing)2).WithChildren(new ElementBounds[]
    //    {
    //        insetBounds,
    //        scrollbarBounds,
    //        buttonBoundsAddNotice,
    //        buttonBoundsLock
    //    });

    //    var dialogComposer = capi.Gui.CreateCompo("unconsciousScrollGui", dialogBounds);
    //    var shadedBackground = GuiComposerHelpers.AddShadedDialogBG(dialogComposer, bgBounds, true, 5.0, 0.75f);
    //    var titleBar = GuiComposerHelpers.AddDialogTitleBar(dialogComposer,Lang.Get("noticeboard:main-window-title-bar", Array.Empty<object>()), new Action(this.OnTitleBarClose), null, null);
    //    var insetContent = GuiElementInsetHelper.AddInset(dialogComposer, insetBounds, insetDepth, 0.85f);
    //    var clippedContent = GuiElementClipHelpler.BeginClip(dialogComposer, clipBounds);
    //    var contentContainer = GuiComposerHelpers.AddContainer(dialogComposer, containerBounds, "scroll-content");
    //    var finalizedContent = GuiElementClipHelpler.EndClip(dialogComposer);
    //    var scrollableContent = GuiComposerHelpers.AddVerticalScrollbar(dialogComposer, new Action<float>(this.OnNewScrollbarValue), scrollbarBounds, "scrollbar");
    //    var addNoticeButton = GuiComposerHelpers.AddSmallButton(dialogComposer, Lang.Get("noticeboard:main-window-add-notice-button",Array.Empty<object>()), () => this.AddMessage(), buttonBoundsAddNotice, EnumButtonStyle.Normal, "addNoticeButton");
    //    var finalComposer = GuiComposerHelpers.AddSmallButton(dialogComposer, Lang.Get("Lock", Array.Empty<object>()), () => this.LockNoticeBoard(), buttonBoundsLock, EnumButtonStyle.Normal, "lockBoard");
    //    base.SingleComposer = dialogComposer;


    //    GuiElementContainer scrollArea = GuiComposerHelpers.GetContainer(base.SingleComposer, "scroll-content");
    //    scrollArea.Add(new GuiElementStaticText(this.capi, "", (EnumTextOrientation)2, containerRowBounds, CairoFont.WhiteDetailText()), -1);
    //    if (this.messages != null && this.messages.Count > 0)
    //    {
    //        for (int i = 0; i < this.messages.Count; i++)
    //        {
    //            string message = this.messages[i].Text;
    //            int id = this.messages[i].Id;
    //            double textHeight = this.CalculateTextHeight(message, (double)(insetWidth - 80)); // Adjust width for padding/buttons
    //            double rowHeight = Math.Max(textHeight + 16.0, minRowHeight); // Ensure minimum height for buttons
    //            containerRowBounds.WithFixedHeight((double)rowHeight);
    //            containerRowBounds.WithFixedWidth(500.0);
    //            containerRowBounds.WithFixedPadding(0.0, 8.0);
    //            containerRowBoundsButton = containerRowBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedPosition(530.0, containerRowBounds.fixedY + 8.0).WithFixedHeight(10.0).WithFixedWidth(30.0);
    //            containerRowBoundsButton.WithFixedMargin(0.0, 8.0);
    //            editButtonBounds = containerRowBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedPosition(530.0, containerRowBounds.fixedY + 48.0).WithFixedHeight(10.0).WithFixedWidth(30.0);
    //            editButtonBounds.WithFixedMargin(0.0, 8.0);
    //            GuiElementStaticText textElement = new GuiElementStaticText(this.capi, message, 0, containerRowBounds, CairoFont.WhiteDetailText());
    //            scrollArea.Add(textElement, -1);
    //            scrollArea.Add(new GuiElementTextButton(this.capi, "X", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.RemoveMessage(id), containerRowBoundsButton, EnumButtonStyle.Normal), -1);
    //            scrollArea.Add(new GuiElementTextButton(this.capi, "E", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.EditMessage(id), editButtonBounds, EnumButtonStyle.Normal), -1);
    //            containerRowBounds = containerRowBounds.BelowCopy(0.0, 0.0, 0.0, 0.0);
    //            containerRowBoundsButton = containerRowBoundsButton.BelowCopy(0.0, 0.0, 0.0, 0.0);
    //            editButtonBounds = editButtonBounds.BelowCopy(0.0, 0.0, 0.0, 0.0);
    //        }
    //    }
    //    base.SingleComposer.Compose(true);
    //    List<Message> messages = this.messages;
    //    int messagesCount = (messages != null) ? messages.Count : 0;
    //    float scrollVisibleHeight = (float)clipBounds.fixedHeight;
    //    float scrollTotalHeight = (float)((rowHeight + 16) * messagesCount);
    //    GuiComposerHelpers.GetScrollbar(base.SingleComposer, "scrollbar").SetHeights(scrollVisibleHeight, scrollTotalHeight);

    //    GuiComposerHelpers.GetButton(base.SingleComposer, "lockBoard").Enabled = false;
    //    if(this.boardPlayerId == capi.World.Player.PlayerUID)
    //    {
    //        GuiComposerHelpers.GetButton(base.SingleComposer, "lockBoard").Enabled = true;
    //    }

    //    if (this.isLocked && this.boardPlayerId != capi.World.Player.PlayerUID)
    //    {
    //        GuiComposerHelpers.GetButton(base.SingleComposer, "addNoticeButton").Enabled = false;
    //    }
    //}

    public override void OnGuiOpened()
    {
        double insetWidth = 575.0;
        int insetDepth = 3;
        double minRowHeight = 60.0; // Minimum row height to ensure buttons fit
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithFixedWidth(860.0).WithAlignment((EnumDialogArea)5).WithFixedOffset(0.0, 40.0);
        ElementBounds insetBounds = ElementBounds.Fixed(0.0, GuiStyle.TitleBarHeight, insetWidth, 300.0);
        ElementBounds scrollbarBounds = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(20.0);
        ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerRowBounds = ElementBounds.Fixed(0.0, 0.0, insetWidth, minRowHeight);
        ElementBounds buttonBoundsAddNotice = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(120.0).WithFixedHeight(40.0).WithFixedOffset(36.0, 0.0);
        ElementBounds buttonBoundsLock = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(120.0).WithFixedHeight(40.0).WithFixedOffset(36.0, 48.0);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing((ElementSizing)2).WithChildren(new ElementBounds[]
        {
        insetBounds,
        scrollbarBounds,
        buttonBoundsAddNotice,
        buttonBoundsLock
        });

        var dialogComposer = capi.Gui.CreateCompo("noticeBoardScrollGui", dialogBounds);
        var shadedBackground = GuiComposerHelpers.AddShadedDialogBG(dialogComposer, bgBounds, true, 5.0, 0.75f);
        var titleBar = GuiComposerHelpers.AddDialogTitleBar(dialogComposer, Lang.Get("noticeboard:main-window-title-bar") + $" - Owner: {this.boardPlayerName}", new Action(this.OnTitleBarClose), null, null);
        var insetContent = GuiElementInsetHelper.AddInset(dialogComposer, insetBounds, insetDepth, 0.85f);
        var clippedContent = GuiElementClipHelpler.BeginClip(dialogComposer, clipBounds);
        var contentContainer = GuiComposerHelpers.AddContainer(dialogComposer, containerBounds, "scroll-content");
        var finalizedContent = GuiElementClipHelpler.EndClip(dialogComposer);
        var scrollableContent = GuiComposerHelpers.AddVerticalScrollbar(dialogComposer, new Action<float>(this.OnNewScrollbarValue), scrollbarBounds, "scrollbar");
        var addNoticeButton = GuiComposerHelpers.AddSmallButton(dialogComposer, Lang.Get("noticeboard:main-window-add-notice-button", Array.Empty<object>()), () => this.AddMessage(), buttonBoundsAddNotice, EnumButtonStyle.Normal, "addNoticeButton");
        var finalComposer = GuiComposerHelpers.AddSmallButton(dialogComposer, Lang.Get(isLocked ? "Unlock" : "Lock", Array.Empty<object>()), () => this.LockNoticeBoard(), buttonBoundsLock, EnumButtonStyle.Normal, "lockBoard");
        base.SingleComposer = dialogComposer;
        GuiElementContainer scrollArea = GuiComposerHelpers.GetContainer(base.SingleComposer, "scroll-content");

        double totalContentHeight = 0.0; // Track total height for scrollbar
        if (this.messages != null && this.messages.Count > 0)
        {
            for (int i = 0; i < this.messages.Count; i++) {
                string message = this.messages[i].Text;
                int id = this.messages[i].Id;
                double textHeight = this.CalculateTextHeight(message, (double)(insetWidth - 80)); // Adjust width for padding/buttons
                double rowHeight = Math.Max(textHeight + 16.0, minRowHeight); // Ensure minimum height for buttons

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

                GuiElementStaticText textElement = new GuiElementStaticText(this.capi, message, 0, containerRowBounds, CairoFont.WhiteDetailText());
                scrollArea.Add(textElement, -1);

                if (this.isLocked && this.boardPlayerId == capi.World.Player.PlayerUID)
                {
                    scrollArea.Add(new GuiElementTextButton(this.capi, "X", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.RemoveMessage(id), containerRowBoundsButton, EnumButtonStyle.Normal), -1);
                    scrollArea.Add(new GuiElementTextButton(this.capi, "E", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.EditMessage(id), editButtonBounds, EnumButtonStyle.Normal), -1);
                } else if (!this.isLocked)
                {
                    scrollArea.Add(new GuiElementTextButton(this.capi, "X", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.RemoveMessage(id), containerRowBoundsButton, EnumButtonStyle.Normal), -1);
                    scrollArea.Add(new GuiElementTextButton(this.capi, "E", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.EditMessage(id), editButtonBounds, EnumButtonStyle.Normal), -1);
                }

                totalContentHeight += rowHeight + (4.0 * this.messages.Count); // Add row height plus padding
                containerRowBounds = containerRowBounds.BelowCopy(0.0, 16.0, 0.0, 0.0); // Add vertical spacing
                containerRowBoundsButton = containerRowBoundsButton.BelowCopy(0.0, 16.0, 0.0, 0.0);
                editButtonBounds = editButtonBounds.BelowCopy(0.0, 16.0, 0.0, 0.0);
            }
        }
        else
        {
            // Add placeholder text if no messages
            scrollArea.Add(new GuiElementStaticText(this.capi, "", (EnumTextOrientation)2, containerRowBounds, CairoFont.WhiteDetailText()), -1);
            totalContentHeight = minRowHeight; // Minimum height for empty state
        }

        base.SingleComposer.Compose(true);

        float scrollVisibleHeight = (float)clipBounds.fixedHeight;
        float scrollTotalHeight = (float)totalContentHeight;
        GuiComposerHelpers.GetScrollbar(base.SingleComposer, "scrollbar").SetHeights(scrollVisibleHeight, scrollTotalHeight);

        GuiComposerHelpers.GetButton(base.SingleComposer, "lockBoard").Enabled = (this.boardPlayerId == capi.World.Player.PlayerUID);
        if (this.isLocked && this.boardPlayerId != capi.World.Player.PlayerUID)
        {
            GuiComposerHelpers.GetButton(base.SingleComposer, "addNoticeButton").Enabled = false;
        }
    }
    private double CalculateTextHeight(string text, double width)
    {
        CairoFont font = CairoFont.WhiteDetailText();
        return capi.Gui.Text.GetMultilineTextHeight(font, text, width);
    }
    private void OnNewScrollbarValue(float value)
    {
        ElementBounds bounds = GuiComposerHelpers.GetContainer(base.SingleComposer, "scroll-content").Bounds;
        bounds.fixedY = (double)(5f - value);
        bounds.CalcWorldBounds();
    }

    public void GetMessages()
    {
        RequestAllMessages requestPacket = new RequestAllMessages
        {
            BoardId = this.boardId
        };
        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket<RequestAllMessages>(requestPacket);
    }

    public void RefreshMessageList()
    {
        GuiComposer singleComposer = base.SingleComposer;
        if (singleComposer != null)
        {
            singleComposer.Dispose();
        }
        this.OnGuiOpened();
    }

    private void OnCloseDialog()
    {
        TryClose();
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }
    public override string ToggleKeyCombinationCode => null;
}