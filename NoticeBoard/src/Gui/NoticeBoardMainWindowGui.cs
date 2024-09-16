using NoticeBoard.src;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using NoticeBoard.src.Packets;
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
            textInputGui = new NoticeBoardTextInputWindowGui(context, boardId, playerId, capi);
            textInputGui.TryOpen();
        } else
        {
            textInputGui.TryClose();
            textInputGui.TryOpen();
        }

        GetMessages();
        return true;
    }

    private bool RemoveMessage(int id)
    {


        PlayerRemoveMessage removeMessage = new PlayerRemoveMessage
        {
            MessageId = id,
        };

        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(removeMessage);
        GetMessages();               // Refresh the GUI
        return true;
    }

    public override void OnGuiOpened()
    {

        int insetWidth = 900;
        int insetHeight = 300;
        int insetDepth = 3;
        int rowHeight = 100;

        ElementBounds buttonBounds = ElementBounds.Fixed(-136, 40, 100, 30); // Button position and size
        // Auto-sized dialog at the center of the screen
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        // Bounds of main inset for scrolling content in the GUI
        ElementBounds insetBounds = ElementBounds.Fixed(0, GuiStyle.TitleBarHeight, insetWidth, insetHeight);
        ElementBounds scrollbarBounds = insetBounds.RightCopy().WithFixedWidth(20);

        // Create child elements bounds for within the inset
        ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerRowBounds = ElementBounds.Fixed(0, 0, insetWidth, rowHeight);
        ElementBounds containerRowBoundsButton = ElementBounds.Fixed(870, 0, 20, 20);


        // Dialog background bounds
        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds);

        // Create the dialog
        SingleComposer = capi.Gui.CreateCompo("demoScrollGui", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("noticeboard:main-window-title-bar"), OnTitleBarClose)
            .AddSmallButton(Lang.Get("noticeboard:main-window-add-notice-button"), () => AddMessage(), buttonBounds)
            .BeginChildElements()
                .AddInset(insetBounds, insetDepth)
                .BeginClip(clipBounds)
                    .AddContainer(containerBounds, "scroll-content")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
            .EndChildElements();


        GuiElementContainer scrollArea = SingleComposer.GetContainer("scroll-content");
        scrollArea.Add(new GuiElementStaticText(capi, "", EnumTextOrientation.Center, containerRowBounds, CairoFont.WhiteDetailText()));

        if (messages != null && messages.Count > 0)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                string message = messages[i].Text;
                int id = messages[i].Id;
                //double height = CalculateTextHeight(message, insetWidth);
                //containerRowBounds.WithFixedHeight(height);
                //totalHeight +=  height + 16;
                containerRowBounds.WithFixedWidth(850);
                containerRowBounds.WithFixedPadding(0 ,8);


                containerRowBoundsButton.WithFixedPosition(860, containerRowBounds.fixedY + 8);
                containerRowBoundsButton.WithFixedMargin(0, 8);

                GuiElementStaticText textElement = new GuiElementStaticText(capi, message, EnumTextOrientation.Left, containerRowBounds, CairoFont.WhiteDetailText());

                scrollArea.Add(textElement);
                scrollArea.Add(new GuiElementTextButton(capi, "X", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => RemoveMessage(id), containerRowBoundsButton));

                containerRowBounds = containerRowBounds.BelowCopy();
                containerRowBoundsButton = containerRowBoundsButton.BelowCopy();
            }
        }

        // Compose the dialog
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
        TryClose(); // Close the GUI when the title bar close button is clicked
    }

    private static Random random = new Random();
    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
    private bool OnButtonClick()
    {
        capi.ShowChatMessage($"Button clicked! board id: {this.boardId}");
        return true;
    }

    public override string ToggleKeyCombinationCode => null;
}