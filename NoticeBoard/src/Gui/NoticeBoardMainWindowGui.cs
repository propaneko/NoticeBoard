using NoticeBoard;
using System.Collections.Generic;
using Vintagestory.API.Client;
using NoticeBoard.Packets;
using Vintagestory.API.Config;

public class NoticeBoardMainWindowGui : GuiDialog
{
    private string boardId;
    private string playerId;
    private List<Message> messages;

    public NoticeBoardMainWindowGui context;
    private NoticeBoardTextInputWindowGui textInputGui;

    public NoticeBoardMainWindowGui(string dialogTitle, ResponseAllMessages packet, ICoreClientAPI capi) : base(capi)
    {
        this.boardId = packet.BoardId;
        this.playerId = packet.PlayerId;
        this.messages = packet.Messages;
        context = this;
    }

    public void UpdateMessages(List<Message> messages)
    {
        this.messages = messages;
        RefreshMessageList();
    }

    private bool AddMessage()
    {

        if (textInputGui == null || !textInputGui.IsOpened())
        {
            textInputGui = new NoticeBoardTextInputWindowGui(this.capi, this.context, this.boardId, this.playerId, "add");
            textInputGui.TryOpen();
        } else
        {
            textInputGui.TryClose();
            textInputGui.TryOpen();
        }

        return true;
    }

    private bool RemoveMessage(int id)
    {


        PlayerRemoveMessage removeMessage = new PlayerRemoveMessage
        {
            MessageId = id,
        };

        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(removeMessage);
        GetMessages();
        return true;
    }

    private bool EditMessage(int id)
    {
        Message message = this.messages.Find((Message m) => m.Id == id);
        if (this.textInputGui == null || !this.textInputGui.IsOpened())
        {
            this.textInputGui = new NoticeBoardTextInputWindowGui(this.capi, this.context, this.boardId, this.playerId, "edit", message.Id, message.Text);
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

        // Auto-sized dialog at the center of the screen
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithFixedWidth(860).WithAlignment(EnumDialogArea.CenterTop).WithFixedOffset(0, 40);

        // Bounds of main inset for scrolling content in the GUI
        ElementBounds insetBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, insetWidth, insetHeight);
        ElementBounds insetBounds1 = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, insetWidth, insetHeight);

        ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(20);

        ElementBounds textBounds = ElementBounds.Fixed(0, 40, 300, 100);

        // Create child elements bounds for within the inset
        ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerRowBounds = ElementBounds.Fixed(0, 0, insetWidth, rowHeight);
        ElementBounds containerRowBoundsButton = ElementBounds.Fixed(870, 0, 20, 20);
        ElementBounds editButtonBounds = ElementBounds.Fixed(870, 0, 20, 20);

        ElementBounds buttonBounds = insetBounds.RightCopy().WithFixedWidth(120).WithFixedHeight(40).WithFixedOffset(36, 0); // Button position and size

        // Dialog background bounds
        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds, buttonBounds);

        // Create the dialog
        SingleComposer = capi.Gui.CreateCompo("demoScrollGui", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("noticeboard:main-window-title-bar"), OnTitleBarClose)
            .AddInset(insetBounds, insetDepth)
                .BeginClip(clipBounds)
                    .AddContainer(containerBounds, "scroll-content")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
            .AddSmallButton(Lang.Get("noticeboard:main-window-add-notice-button"), () => AddMessage(), buttonBounds);
                
            


        GuiElementContainer scrollArea = SingleComposer.GetContainer("scroll-content");
        scrollArea.Add(new GuiElementStaticText(capi, "", EnumTextOrientation.Center, containerRowBounds, CairoFont.WhiteDetailText()));
        if (messages != null && messages.Count > 0)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                string message = messages[i].Text;
                int id = messages[i].Id;
                double height = CalculateTextHeight(message, insetWidth);
                containerRowBounds.WithFixedHeight(rowHeight);
                //totalHeight +=  height + 16;
                containerRowBounds.WithFixedWidth(500);
                containerRowBounds.WithFixedPadding(0 ,8);


                containerRowBoundsButton = containerRowBounds.RightCopy().WithFixedPosition(530, containerRowBounds.fixedY + 8).WithFixedHeight(10).WithFixedWidth(30);
                containerRowBoundsButton.WithFixedMargin(0, 8);

                editButtonBounds = containerRowBounds.RightCopy().WithFixedPosition(530, containerRowBounds.fixedY + 48).WithFixedHeight(10).WithFixedWidth(30);
                editButtonBounds.WithFixedMargin(0, 8);

                //containerRowBoundsButton.WithFixedMargin(0, 8);

                GuiElementStaticText textElement = new GuiElementStaticText(capi, message, EnumTextOrientation.Left, containerRowBounds, CairoFont.WhiteDetailText());

                scrollArea.Add(textElement);
                scrollArea.Add(new GuiElementTextButton(capi, "X", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => RemoveMessage(id), containerRowBoundsButton));
                scrollArea.Add(new GuiElementTextButton(this.capi, "E", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => this.EditMessage(id), editButtonBounds, EnumButtonStyle.Normal), -1);

                containerRowBounds = containerRowBounds.BelowCopy();
                containerRowBoundsButton = containerRowBoundsButton.BelowCopy();
                editButtonBounds = editButtonBounds.BelowCopy();

            }
        }

        SingleComposer.Compose();

        int messagesCount = messages?.Count ?? 0;
        float scrollVisibleHeight = (float)clipBounds.fixedHeight;
        float scrollTotalHeight = (rowHeight + 16) * messagesCount;
        SingleComposer.GetScrollbar("scrollbar").SetHeights(scrollVisibleHeight, scrollTotalHeight);
    }
    private double CalculateTextHeight(string text, double width)
    {
        CairoFont font = CairoFont.WhiteDetailText();
        return capi.Gui.Text.GetMultilineTextHeight(font, text, width);
    }
    private void OnNewScrollbarValue(float value)
    {
        ElementBounds bounds = SingleComposer.GetContainer("scroll-content").Bounds;
        bounds.fixedY = 5 - value;
        bounds.CalcWorldBounds();
    }

    public void GetMessages()
    {
        RequestAllMessages requestPacket = new RequestAllMessages
        {
            BoardId = boardId
        };

        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(requestPacket);
    }

    public void RefreshMessageList()
    {
   
        SingleComposer?.Dispose();
        OnGuiOpened();
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