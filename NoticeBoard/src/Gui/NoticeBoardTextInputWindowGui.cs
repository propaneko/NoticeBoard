using NoticeBoard;
using System.Linq;
using Vintagestory.API.Client;
using NoticeBoard.Packets;
using Vintagestory.API.Config;

public class NoticeBoardTextInputWindowGui : GuiDialog
{
    private string boardId;
    private string playerId;
    private string mode;
    private int messageId;
    private string message;

    private NoticeBoardMainWindowGui parentContext;


    public NoticeBoardTextInputWindowGui(ICoreClientAPI capi, NoticeBoardMainWindowGui context, string boardId, string playerId, string mode, int messageId = -1, string message = "")
        : base(capi)
    {
        this.boardId = boardId;
        this.playerId = playerId;
        this.mode = mode;
        this.parentContext = context;
        if (mode == "edit")
        {
            this.message = message;
            this.messageId = messageId;
        }
    }

    public override void OnGuiOpened()
    {

        base.OnGuiOpened();

        int insetWidth = 550;
        int insetHeight = 75;
        int insetDepth = 3;
        int rowHeight = 100;

        // Auto-sized dialog at the center of the screen
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterBottom).WithFixedOffset(0, -120);

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

        ElementBounds buttonBounds = insetBounds.RightCopy().WithFixedWidth(120).WithFixedHeight(40).WithFixedOffset(17, 0); // Button position and size

        // Dialog background bounds
        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds, buttonBounds);

        // Create the dialog
        SingleComposer = capi.Gui.CreateCompo("addNoticeGui", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("noticeboard:add-notice-window-title"), OnTitleBarClose)
            .AddSmallButton(Lang.Get("noticeboard:add-notice-window-pin-button"), OnSendButtonClicked, buttonBounds)
            .AddInset(insetBounds, insetDepth)
                .BeginClip(clipBounds)
                 .AddTextArea(containerBounds, OnTextChanged, CairoFont.WhiteDetailText(), "messageInput")
                .EndClip();

        SingleComposer.Compose();
        if (this.mode == "edit")
        {
            base.SingleComposer.GetTextArea("messageInput").SetValue(message);
        }
    }

    private void OnTextChanged(string text)
    {
        string[] lines = text.Split('\n');

        int maxLines = 4;

        if (lines.Length > maxLines)
        {
            string limitedText = string.Join("\n", lines.Take(maxLines));

            SingleComposer.GetTextArea("messageInput").SetValue(limitedText);
        }

        if (text.Length > 292)
        {
            string limitedText = text.Substring(0, 292);
            SingleComposer.GetTextArea("messageInput").SetValue(limitedText);
        }
    }

    private bool OnSendButtonClicked()
    {
        if (mode == "edit")
        {
            string text = base.SingleComposer.GetTextArea("messageInput").GetText();
            PlayerEditMessage playerEditMessage = new PlayerEditMessage
            {
                Id = messageId,
                Message = text
            };
            NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket<PlayerEditMessage>(playerEditMessage);
            parentContext.GetMessages();
            TryClose();
            return true;
        }
        string text2 = base.SingleComposer.GetTextArea("messageInput").GetText();
        PlayerSendMessage playerSendMessage = new PlayerSendMessage
        {
            Message = text2,
            BoardId = boardId,
            PlayerId = playerId
        };
        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket<PlayerSendMessage>(playerSendMessage);
        parentContext.GetMessages();
        TryClose();
        return true;
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