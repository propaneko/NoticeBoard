using NoticeBoard;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using NoticeBoard.Packets;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using static NoticeBoard.NoticeBoardBlockEntity;
using System.Net.Sockets;
using Vintagestory.API.MathTools;
using System.ComponentModel;

public class NoticeBoardMainWindowGui : GuiDialogBlockEntity
{
    private string boardId;
    private string playerId;

    private List<Message> messages;

    public NoticeBoardMainWindowGui context;
    private NoticeBoardTextInputWindowGui textInputGui;

    public NoticeBoardMainWindowGui(string dialogTitle, InventoryBase Inventory, BlockPos BlockEntityPosition, DialogPacket packet, ICoreClientAPI capi) : base(dialogTitle, Inventory, BlockEntityPosition, capi)
    {
        if (base.IsDuplicate)
        {
            return;
        }
        this.boardId = packet.UniqueID;
        this.messages = packet.Messages;
        this.playerId = packet.PlayerId;
        context = this;
        capi.World.Player.InventoryManager.OpenInventory(Inventory);
        this.SetupDialog();
    }

    public void GetMessages()
    {
        NoticeBoardBlockEntity noticeBoardEntity = this.capi.World.BlockAccessor.GetBlockEntity(this.BlockEntityPosition) as NoticeBoardBlockEntity;
        this.capi.Network.SendBlockEntityPacket(noticeBoardEntity.Pos, 2138, null);
    }
    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        base.Inventory.SlotModified += this.OnInventorySlotModified;
    }

    public override void OnGuiClosed()
    {
        base.Inventory.SlotModified -= this.OnInventorySlotModified;
        base.OnGuiClosed();
    }

    private void OnInventorySlotModified(int slotid)
    {
        this.capi.Event.EnqueueMainThreadTask(new Action(this.SetupDialog), "setupnoticeboarddlg");
    }

    public void UpdateMessages(List<Message> messages)
    {
        this.messages = messages;
        RefreshMessageList();
    }

    private bool AddMessage()
    {
        //{
        onButtonPress();

        if (textInputGui == null || !textInputGui.IsOpened())
        {
            textInputGui = new NoticeBoardTextInputWindowGui(capi, context, boardId, playerId, base.BlockEntityPosition, "add" );
            textInputGui.TryOpen();
        } else
        {
            textInputGui.TryClose();
            textInputGui.TryOpen();
        }

        return true;
    }

    private bool EditMessage(int id)
    {
        Message message = this.messages.Find((Message m) => m.Id == id);
        if (this.textInputGui == null || !this.textInputGui.IsOpened())
        {
            this.textInputGui = new NoticeBoardTextInputWindowGui(capi, context, boardId, playerId, base.BlockEntityPosition, "edit", message.Id, message.Text);
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
            MessageId = id,
        };

        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket(removeMessage);

        GetMessages();
        return true;
    }

    private void SetupDialog()
    {
        ItemSlot itemSlot = this.capi.World.Player.InventoryManager.CurrentHoveredSlot;
        if (itemSlot != null && itemSlot.Inventory == base.Inventory)
        {
            this.capi.Input.TriggerOnMouseLeaveSlot(itemSlot);
        }
        else
        {
            itemSlot = null;
        }

        UpdateButtonState();

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
        ElementBounds editButtonBounds = ElementBounds.Fixed(870.0, 35.0, 20.0, 20.0);


        ElementBounds buttonBounds = insetBounds.RightCopy().WithFixedWidth(120).WithFixedHeight(40).WithFixedOffset(36, 0); // Button position and size

        ElementBounds inventoryBounds = insetBounds.RightCopy().WithFixedWidth(30).WithFixedHeight(30).WithFixedOffset(36, 60); // Button position and size


        // Dialog background bounds
        ElementBounds bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds, buttonBounds, inventoryBounds);

        // Create the dialog
        base.ClearComposers();
        SingleComposer = capi.Gui.CreateCompo("demoScrollGui", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("noticeboard:main-window-title-bar"), OnTitleBarClose)
            .AddInset(insetBounds, insetDepth)
                .BeginClip(clipBounds)
                    .AddContainer(containerBounds, "scroll-content")
                .EndClip()
                .AddVerticalScrollbar(OnNewScrollbarValue, scrollbarBounds, "scrollbar")
            .AddSmallButton(Lang.Get("noticeboard:main-window-add-notice-button"), () => AddMessage(), buttonBounds, EnumButtonStyle.Small, "addNoticeButton")
            .AddItemSlotGrid(base.Inventory, new Action<object>(this.DoSendPacket),1 , new int[1], inventoryBounds, "noticeBoardInventorySlots");
                
            


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

                editButtonBounds = containerRowBounds.RightCopy().WithFixedPosition(530, containerRowBounds.fixedY + 38).WithFixedHeight(10).WithFixedWidth(30);
                editButtonBounds.WithFixedMargin(0, 8);

                //containerRowBoundsButton.WithFixedMargin(0, 8);

                GuiElementStaticText textElement = new GuiElementStaticText(capi, message, EnumTextOrientation.Left, containerRowBounds, CairoFont.WhiteDetailText());

                scrollArea.Add(textElement);
                scrollArea.Add(new GuiElementTextButton(capi, "X", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => RemoveMessage(id), containerRowBoundsButton));
                scrollArea.Add(new GuiElementTextButton(capi, "E", CairoFont.WhiteDetailText(), CairoFont.WhiteDetailText(), () => EditMessage(id), editButtonBounds, EnumButtonStyle.Normal));

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
        if (itemSlot != null)
        {
            base.SingleComposer.OnMouseMove(new MouseEvent(this.capi.Input.MouseX, this.capi.Input.MouseY));
        }
    }
    private double CalculateTextHeight(string text, double width)
    {
        CairoFont font = CairoFont.WhiteDetailText();
        return capi.Gui.Text.GetMultilineTextHeight(font, text, width);
    }

    private void onButtonPress()
    {
        NoticeBoardBlockEntity noticeBoardEntity = this.capi.World.BlockAccessor.GetBlockEntity(this.BlockEntityPosition) as NoticeBoardBlockEntity;
        this.capi.Network.SendBlockEntityPacket(noticeBoardEntity.Pos, 2137, null);
    }

    private new void DoSendPacket(object p)
    {
        UpdateButtonState();
        NoticeBoardBlockEntity noticeBoardEntity = this.capi.World.BlockAccessor.GetBlockEntity(this.BlockEntityPosition) as NoticeBoardBlockEntity;
        GetMessages();
        this.capi.Network.SendBlockEntityPacket(base.BlockEntityPosition.X, base.BlockEntityPosition.Y, base.BlockEntityPosition.Z, p);
    }

    public void UpdateButtonState()
    {
        if (Inventory[0].Itemstack != null)
        {
            bool isParchment = Inventory[0].Itemstack.Item.Code.Path == "paper-parchment";
            this.capi.Logger.Debug(isParchment.ToString());
            SingleComposer.GetButton("addNoticeButton").Enabled = isParchment;
        }
    }

    private void OnNewScrollbarValue(float value)
    {
        ElementBounds bounds = SingleComposer.GetContainer("scroll-content").Bounds;
        bounds.fixedY = 5 - value;
        bounds.CalcWorldBounds();
    }

    public void RefreshMessageList()
    {
        SingleComposer?.Dispose();
        SetupDialog();
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