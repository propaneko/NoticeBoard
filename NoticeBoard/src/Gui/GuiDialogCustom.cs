using Vintagestory.API.Client;

public class GuiDialogCustom : GuiDialog
{
    private string id;
    public GuiDialogCustom(string dialogTitle, string id, ICoreClientAPI capi) : base(capi)
    {
        this.id = id;
        // Define the size and layout of the GUI window
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        ElementBounds buttonBounds = ElementBounds.Fixed(10, 40, 80, 20); // Button position and size
        ElementBounds labelBounds = ElementBounds.Fixed(10, 80, 300, 20); // Label position and size

        // Create the GUI components
        SingleComposer = capi.Gui
            .CreateCompo("myCustomGui", dialogBounds)
            .AddShadedDialogBG(ElementBounds.Fill)
            .AddDialogTitleBar("Movable GUI", OnTitleBarClose)
            .AddStaticText(id, CairoFont.WhiteDetailText(), labelBounds)
            .AddButton("Click me", OnButtonClick, buttonBounds)
            .Compose();
    }

    private void OnTitleBarClose()
    {
        TryClose(); // Close the GUI when the title bar close button is clicked
    }
    private bool OnButtonClick()
    {
        capi.ShowChatMessage($"Button clicked! board id: {this.id}");
        return true;
    }

    public override string ToggleKeyCombinationCode => null;
}