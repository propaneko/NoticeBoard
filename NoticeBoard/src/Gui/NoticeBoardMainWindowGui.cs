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
    private string playerId;
    private List<Message> messages;
    private ResponseAllMessages noticeBoardPacket;

    public NoticeBoardMainWindowGui context;
    private NoticeBoardTextInputWindowGui textInputGui;

    public NoticeBoardMainWindowGui(string dialogTitle, ResponseAllMessages packet, ICoreClientAPI capi) : base(capi)
    {
        this.noticeBoardPacket = packet;
        this.context = this;
    }

    public void UpdateMessages(List<Message> messages)
    {
        this.noticeBoardPacket.Messages = messages;
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
        Message message = this.noticeBoardPacket.Messages.Find((Message m) => m.Id == id);
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

    public override void OnGuiOpened()
    {
        int insetWidth = 575;
        int insetHeight = 300;
        int insetDepth = 3;
        int rowHeight = 80;
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithFixedWidth(860.0).WithAlignment((EnumDialogArea)5).WithFixedOffset(0.0, 40.0);
        ElementBounds insetBounds = ElementBounds.Fixed(0.0, GuiStyle.TitleBarHeight, (double)insetWidth, (double)insetHeight);
        ElementBounds.Fixed(0.0, GuiStyle.TitleBarHeight, (double)insetWidth, (double)insetHeight);
        ElementBounds scrollbarBounds = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(20.0);
        ElementBounds.Fixed(0.0, 40.0, 300.0, 100.0);
        ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerRowBounds = ElementBounds.Fixed(0.0, 0.0, (double)insetWidth, (double)rowHeight);
        ElementBounds containerRowBoundsButton = ElementBounds.Fixed(870.0, 0.0, 20.0, 20.0);
        ElementBounds editButtonBounds = ElementBounds.Fixed(870.0, 0.0, 20.0, 20.0);
        ElementBounds buttonBounds = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(120.0).WithFixedHeight(40.0).WithFixedOffset(36.0, 0.0);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing((ElementSizing)2).WithChildren(new ElementBounds[]
        {
            insetBounds,
            scrollbarBounds,
            buttonBounds
        });
        base.SingleComposer = GuiComposerHelpers.AddSmallButton(GuiComposerHelpers.AddVerticalScrollbar(GuiElementClipHelpler.EndClip(GuiComposerHelpers.AddContainer(GuiElementClipHelpler.BeginClip(GuiElementInsetHelper.AddInset(GuiComposerHelpers.AddDialogTitleBar(GuiComposerHelpers.AddShadedDialogBG(this.capi.Gui.CreateCompo("demoScrollGui", dialogBounds), bgBounds, true, 5.0, 0.75f), Lang.Get("noticeboard:main-window-title-bar", Array.Empty<object>()), new Action(this.OnTitleBarClose), null, null), insetBounds, insetDepth, 0.85f), clipBounds), containerBounds, "scroll-content")), new Action<float>(this.OnNewScrollbarValue), scrollbarBounds, "scrollbar"), Lang.Get("noticeboard:main-window-add-notice-button", Array.Empty<object>()), () => this.AddMessage(), buttonBounds, (EnumButtonStyle)2, null);
        GuiElementContainer scrollArea = GuiComposerHelpers.GetContainer(base.SingleComposer, "scroll-content");
        scrollArea.Add(new GuiElementStaticText(this.capi, "", (EnumTextOrientation)2, containerRowBounds, CairoFont.WhiteDetailText()), -1);
        if (this.noticeBoardPacket.Messages != null && this.noticeBoardPacket.Messages.Count > 0)
        {
            for (int i = 0; i < this.noticeBoardPacket.Messages.Count; i++)
            {
                string message = this.noticeBoardPacket.Messages[i].Text;
                int id = this.noticeBoardPacket.Messages[i].Id;
                this.CalculateTextHeight(message, (double)insetWidth);
                containerRowBounds.WithFixedHeight((double)rowHeight);
                containerRowBounds.WithFixedWidth(500.0);
                containerRowBounds.WithFixedPadding(0.0, 8.0);
                containerRowBoundsButton = containerRowBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedPosition(530.0, containerRowBounds.fixedY + 8.0).WithFixedHeight(10.0).WithFixedWidth(30.0);
                containerRowBoundsButton.WithFixedMargin(0.0, 8.0);
                editButtonBounds = containerRowBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedPosition(530.0, containerRowBounds.fixedY + 48.0).WithFixedHeight(10.0).WithFixedWidth(30.0);
                editButtonBounds.WithFixedMargin(0.0, 8.0);
                GuiElementStaticText textElement = new GuiElementStaticText(this.capi, message, 0, containerRowBounds, CairoFont.WhiteDetailText());
                scrollArea.Add(textElement, -1);
                scrollArea.Add(new GuiElementTextButton(this.capi, "X", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.RemoveMessage(id), containerRowBoundsButton, (EnumButtonStyle)2), -1);
                scrollArea.Add(new GuiElementTextButton(this.capi, "E", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.EditMessage(id), editButtonBounds, (EnumButtonStyle)2), -1);
                containerRowBounds = containerRowBounds.BelowCopy(0.0, 0.0, 0.0, 0.0);
                containerRowBoundsButton = containerRowBoundsButton.BelowCopy(0.0, 0.0, 0.0, 0.0);
                editButtonBounds = editButtonBounds.BelowCopy(0.0, 0.0, 0.0, 0.0);
            }
        }
        base.SingleComposer.Compose(true);
        List<Message> messages = this.noticeBoardPacket.Messages;
        int messagesCount = (messages != null) ? messages.Count : 0;
        float scrollVisibleHeight = (float)clipBounds.fixedHeight;
        float scrollTotalHeight = (float)((rowHeight + 16) * messagesCount);
        GuiComposerHelpers.GetScrollbar(base.SingleComposer, "scrollbar").SetHeights(scrollVisibleHeight, scrollTotalHeight);
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
            BoardId = this.noticeBoardPacket.BoardId
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