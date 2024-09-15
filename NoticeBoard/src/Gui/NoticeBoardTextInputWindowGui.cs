using NoticeBoard.src;
using System.Linq;
using Vintagestory.API.Client;
using NoticeBoard.src.Packets;
using Vintagestory.API.Config;

public class NoticeBoardTextInputWindowGui : GuiDialog
{
    private string boardId;
    private string playerId;

    private NoticeBoardMainWindowGui parentContext;


    public NoticeBoardTextInputWindowGui(NoticeBoardMainWindowGui context, string boardId, string playerId, ICoreClientAPI capi) : base(capi)
    {
        this.boardId = boardId;
        this.playerId = playerId;
        parentContext = context;
    }

    public override void OnGuiOpened()
    {

        base.OnGuiOpened();
        ElementBounds dialogBounds = ElementBounds.Fixed(0, 0, 500, 120).WithAlignment(EnumDialogArea.CenterBottom);

        ElementBounds inputBounds = ElementBounds.Fixed(16, 40, 468, 70);
        ElementBounds buttonBounds = ElementBounds.Fixed(-106, 40, 100, 30);

        SingleComposer = capi.Gui.CreateCompo("messageinputdialog", dialogBounds)
            .AddDialogTitleBar(Lang.Get("noticeboard:add-notice-window-title"), OnTitleBarClose)
            .AddShadedDialogBG(ElementBounds.Fill)
            .AddTextArea(inputBounds, OnTextChanged, CairoFont.WhiteDetailText(), "messageInput")
            .AddSmallButton(Lang.Get("noticeboard:add-notice-window-pin-button"), OnSendButtonClicked, buttonBounds)
            .Compose();
    }

    private void OnTextChanged(string text)
    {
        string[] lines = text.Split('\n');

        // Define the maximum number of lines
        int maxLines = 4;

        if (lines.Length > maxLines)
        {
            // Restrict the text to the first maxLines lines
            string limitedText = string.Join("\n", lines.Take(maxLines));

            // Update the TextArea with the limited text
            SingleComposer.GetTextArea("messageInput").SetValue(limitedText);
        }
        // Optional: Handle text input changes, if needed
    }

    private bool OnSendButtonClicked()
    {
        string message = SingleComposer.GetTextArea("messageInput").GetText();

        PlayerSendMessage sendMessage = new PlayerSendMessage
        {
            Message = message,
            BoardId = boardId,
            PlayerId = playerId
        };

        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(sendMessage);
        // Optionally close the dialog after submission
        parentContext.GetMessages();
        TryClose();

        return true;
    }


    private void OnCloseDialog()
    {
        TryClose();
        //NoticeBoardModSystem.getModInstance().getDatabaseHandler().Close();
    }

    private void OnTitleBarClose()
    {
        TryClose(); // Close the GUI when the title bar close button is clicked
        //NoticeBoardModSystem.getModInstance().getDatabaseHandler().Close();
    }

    public override string ToggleKeyCombinationCode => null;
}