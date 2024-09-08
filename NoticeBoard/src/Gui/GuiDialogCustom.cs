using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using static SQLiteDatabase;

public class GuiDialogCustom : GuiDialog
{
    private string id;
    private SQLiteDatabase database;
    private List<Message> messages;

    public GuiDialogCustom(string dialogTitle, string id, ICoreClientAPI capi) : base(capi)
    {
        this.id = id;
        database = new SQLiteDatabase();  // Initialize the database
        messages = database.GetAllMessages();  // Load messages from the database
    }

    private bool AddMessage(string message)
    {
        database.InsertMessage(message);  // Add to database
        RefreshMessageList();
        return true;
    }

    //public GuiDialogCustom(string dialogTitle, string id, ICoreClientAPI capi) : base(capi)
    //{
    //    this.id = id;
    //    // Define the size and layout of the GUI window
    //    //ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

    //    ElementBounds buttonBounds = ElementBounds.Fixed(10, 40, 80, 20); // Button position and size
    //    ElementBounds labelBounds = ElementBounds.Fixed(10, 80, 300, 20); // Label position and size

    //    ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
    //    ElementBounds listBounds = ElementBounds.Fixed(0, 0, 300, 200).WithFixedPadding(10);

    //    // Create the GUI components
    //    //var composer = capi.Gui.CreateCompo("messageListDialog", dialogBounds)
    //    //    .AddShadedDialogBG(ElementBounds.Fill)
    //    //    .AddDialogTitleBar("Message List", OnCloseDialog);

    //SingleComposer = capi.Gui
    //    .CreateCompo("myCustomGui", dialogBounds)
    //        .AddShadedDialogBG(ElementBounds.Fill)
    //        .AddDialogTitleBar("Movable GUI", OnTitleBarClose)
    //        .AddStaticText(id, CairoFont.WhiteDetailText(), labelBounds)
    //        .AddButton("Click me", OnButtonClick, buttonBounds)
    //        .Compose();
    //}

    public override void OnGuiOpened()
    {
        database = new SQLiteDatabase();  // Initialize the database
        messages = database.GetAllMessages();  // Load messages from the database

        base.OnGuiOpened();
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        ElementBounds buttonBounds = ElementBounds.Fixed(10, 40, 80, 20); // Button position and size
        ElementBounds labelBounds = ElementBounds.Fixed(10, 80, 300, 20).WithAlignment(EnumDialogArea.LeftBottom).WithFixedPadding(10); // Label position and size

        // Create the dialog and title bar
        //SingleComposer = capi.Gui
        //    .CreateCompo("myCustomGui", dialogBounds)
        //    .AddShadedDialogBG(ElementBounds.Fill)
        //    .AddDialogTitleBar("Movable GUI", OnTitleBarClose)
        //    .AddStaticText(id, CairoFont.WhiteDetailText(), labelBounds)
        //    .AddButton("Click me", OnButtonClick, buttonBounds)
        //    .Compose();

        var composer = capi.Gui.CreateCompo("messageListDialog", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill)
            .AddDialogTitleBar("Message List", OnTitleBarClose)
            .AddStaticText(id, CairoFont.WhiteDetailText(), labelBounds)
            .AddSmallButton("Add random string", () => AddMessage(RandomString(20)), buttonBounds);

        //// Add message text to the composer
        AddMessageText(composer);

        //// Complete the dialog composition
        SingleComposer = composer.Compose();
    }

    //private void AddMessageButtons(GuiComposer composer)
    //{
    //    messageButtons.Clear();

    //    // Create a button for each message
    //    for (int i = 0; i < messages.Count; i++)
    //    {
    //        string message = messages[i];
    //        int index = i;  // Capture the current index

    //        // Set bounds for each button (stacked vertically)
    //        ElementBounds buttonBounds = ElementBounds.Fixed(0, 30 * i, 300, 25).WithFixedPadding(5);

    //        // Create a text button with the message, and attach a click event
    //        var button = new GuiElementTextButton(capi, message, buttonBounds, () => RemoveMessage(index), CairoFont.WhiteSmallText());
    //        composer.AddInteractiveElement(button);

    //        // Add the button to the list for future reference
    //        messageButtons.Add(button);
    //    }
    //}


    //public void GuiMessageList(ICoreClientAPI capi) : base(capi)
    //{
    //    messages = new List<string> { "Welcome!", "This is a message.", "You can remove messages." };
    //    messageButtons = new List<GuiElementTextButton>();
    //}

    //public override void OnGuiOpened()
    //{

    //    // Define the dialog size
    //    ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

    //    // Create the dialog and title bar
    //    var composer = capi.Gui.CreateCompo("messageListDialog", dialogBounds)
    //        .AddShadedDialogBG(ElementBounds.Fill)
    //        .AddDialogTitleBar("Message List", OnCloseDialog);

    //    // Add message text to the composer
    //    AddMessageText(composer);

    //    // Complete the dialog composition
    //    SingleComposer = composer.Compose();

    //    base.OnGuiOpened();
    //}

    private void AddMessageText(GuiComposer composer)
    {
        // Define the initial Y position for the first message
        int yOffset = 70;

        // Loop through each message and add it to the composer
        for (int i = 0; i < messages.Count; i++)
        {
            string message = messages[i].Text;
            int id = messages[i].Id;

            int index = i;  // Capture the current index

            // Set bounds for each message and delete button
            ElementBounds messageBounds = ElementBounds.Fixed(0, yOffset, 250, 25);  // Fixed size for text
            ElementBounds buttonBounds = ElementBounds.Fixed(260, yOffset, 30, 25);  // Fixed size for the delete button

            // Add static text displaying the message
            composer.AddStaticText(message, CairoFont.WhiteSmallText(), messageBounds);

            // Add a button to remove the message
            composer.AddSmallButton("X", () => RemoveMessage(id), buttonBounds);

            // Increase yOffset for the next message to stack them vertically
            yOffset += 30;
        }
    }

    private bool RemoveMessage(int id)
    {
            // Delete the message from the database and refresh the list
        database.DeleteMessage(id);  // Assuming message IDs are 1-based
        RefreshMessageList();               // Refresh the GUI
        return true;
    }

    private void RefreshMessageList()
    {
        // Clear the current composition and rebuild the GUI
        SingleComposer?.Dispose();
        database.Close();
        // Reopen and rebuild the GUI with the updated content
        OnGuiOpened();
    }

    private void OnCloseDialog()
    {
        TryClose();
        database.Close();
    }

    private void OnTitleBarClose()
    {
        TryClose(); // Close the GUI when the title bar close button is clicked
        database.Close();
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
        capi.ShowChatMessage($"Button clicked! board id: {this.id}");
        return true;
    }

    public override string ToggleKeyCombinationCode => null;
}