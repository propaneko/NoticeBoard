using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NoticeBoard.BlockType;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace NoticeBoard.src.Gui.Windows;

public class NoticeBoardMainWindowGui : GuiDialog
{
    private readonly string boardId;
    private readonly BlockPos boardPos;
    private readonly string boardPlayerId;
    private readonly string boardPlayerName;

    private List<Message> messages;
    private AssetLocation paperTexture;
    private List<PlayerEntry> players;

    private readonly ResponseAllMessages noticeBoardPacket;

    private NoticeBoardTextInputWindowGui textInputGui;
    private string boardName;
    private string boardFont;
    private bool isLocked;
    private bool enableParticles;
    private bool enableParchment;
    private bool enableProximityMessage;
    private string proximityChannel;
    private double proximityDistance;
    private string pendingOwnerUid;
    private int pendingDistance;
    private bool isDirty = false;

    private int activeTab = 0;
    private double lastCalculatedContentHeight = 0.0;

    public override string ToggleKeyCombinationCode => null;

    public NoticeBoardMainWindowGui(
        string dialogTitle,
        ResponseAllMessages packet,
        ICoreClientAPI capi
    )
        : base(capi)
    {
        this.boardId = packet.BoardProperties.BoardId;
        this.boardName = packet.BoardProperties.BoardName;

        this.boardPlayerId = packet.BoardProperties.PlayerId;
        this.boardPlayerName = packet.BoardProperties.PlayerName;
        this.boardPos = PositionHelper.FromString(packet.BoardProperties.Pos);

        this.isLocked = packet.BoardProperties.IsLocked != 0;
        this.enableParticles = packet.BoardProperties.EnableParticles != 0;
        this.enableParchment = packet.BoardProperties.EnableParchment != 0;

        this.enableProximityMessage = packet.BoardProperties.EnableProximity != 0;
        this.proximityChannel = packet.BoardProperties.ProximityChannel;
        this.proximityDistance = packet.BoardProperties.ProximityDistance;

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

        this.textInputGui = new NoticeBoardTextInputWindowGui(
            this.capi,
            this,
            this.noticeBoardPacket,
            mode,
            messageId,
            currentText
        );
        this.textInputGui.TryOpen();
        return true;
    }

    private bool AddMessage()
    {
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

        bool isParchment = path == "paper-parchment" || path.StartsWith("paper-parchment");

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

    private bool BumpMessage(int id)
    {
        PlayerBumpMessage bumpMessage = new PlayerBumpMessage { MessageId = id };
        capi.Network.GetChannel("noticeboard").SendPacket(bumpMessage);
        this.GetMessages();
        return true;
    }

    private void UpdateDirtyState()
    {
        var p = this.noticeBoardPacket.BoardProperties;

        // Check if any value is different from the original packet
        this.isDirty =
            (this.isLocked != (p.IsLocked != 0))
            || (this.enableParticles != (p.EnableParticles != 0))
            || (this.enableParchment != (p.EnableParchment != 0))
            || (this.enableProximityMessage != (p.EnableProximity != 0))
            || (this.proximityChannel != p.ProximityChannel)
            || (this.boardName != p.BoardName)
            || (this.boardFont != p.BoardFont)
            || (this.pendingDistance != p.ProximityDistance)
            || (!string.IsNullOrEmpty(this.pendingOwnerUid) && this.pendingOwnerUid != p.PlayerId);

        // Update the button state if it exists
        if (this.SingleComposer != null)
        {
            var btn = this.SingleComposer.GetButton("btnSaveAll");
            if (btn != null)
                btn.Enabled = this.isDirty;
        }
    }

    private bool OnSaveAllSettingsClick()
    {
        var originalProps = this.noticeBoardPacket.BoardProperties;
        var channel = capi.Network.GetChannel("noticeboard");

        if (this.isLocked != (originalProps.IsLocked != 0))
        {
            channel.SendPacket(
                new EditIsLocked { BoardId = this.boardId, IsLocked = this.isLocked }
            );
            originalProps.IsLocked = this.isLocked ? 1 : 0;
        }

        if (this.enableParticles != (originalProps.EnableParticles != 0))
        {
            channel.SendPacket(
                new EditEnableParticles
                {
                    BoardId = this.boardId,
                    EnableParticles = this.enableParticles,
                }
            );
            originalProps.EnableParticles = this.enableParticles ? 1 : 0;
        }

        if (this.enableParchment != (originalProps.EnableParchment != 0))
        {
            channel.SendPacket(
                new EditEnableParchment
                {
                    BoardId = this.boardId,
                    EnableParchment = this.enableParchment,
                }
            );
            originalProps.EnableParchment = this.enableParchment ? 1 : 0;
        }

        if (this.boardName != originalProps.BoardName)
        {
            channel.SendPacket(
                new EditBoardName { BoardId = this.boardId, BoardName = this.boardName }
            );
            originalProps.BoardName = this.boardName;
        }

        if (
            !string.IsNullOrEmpty(this.pendingOwnerUid)
            && this.pendingOwnerUid != originalProps.PlayerId
        )
        {
            channel.SendPacket(
                new EditBoardOwner { BoardId = this.boardId, NewOwnerUid = this.pendingOwnerUid }
            );
            originalProps.PlayerId = this.pendingOwnerUid;
        }

        if (this.enableProximityMessage != (originalProps.EnableProximity != 0))
        {
            channel.SendPacket(
                new EditEnableProximity
                {
                    BoardId = this.boardId,
                    EnableProximity = this.enableProximityMessage,
                }
            );
            originalProps.EnableProximity = this.enableProximityMessage ? 1 : 0;
        }

        if (this.proximityChannel != originalProps.ProximityChannel)
        {
            channel.SendPacket(
                new EditProximityChannel
                {
                    BoardId = this.boardId,
                    ChannelName = this.proximityChannel ?? "",
                }
            );
            originalProps.ProximityChannel = this.proximityChannel;
        }

        if (this.pendingDistance != originalProps.ProximityDistance)
        {
            channel.SendPacket(
                new EditProximityDistance
                {
                    BoardId = this.boardId,
                    Distance = this.pendingDistance,
                }
            );
            originalProps.ProximityDistance = this.pendingDistance;
        }

        this.isDirty = false;
        this.SingleComposer.GetButton("btnSaveAll").Enabled = false;

        TryClose();
        return true;
    }

    public override void OnGuiOpened()
    {
        if (this.paperTexture == null)
        {
            this.paperTexture = new AssetLocation("noticeboard", "textures/gui/paper2.jpg");
        }
        ComposeGui();
    }

    private void OnTabClicked(int tabIndex)
    {
        if (this.activeTab == 1 && tabIndex == 0)
        {
            ResetSettings();
        }

        if (this.activeTab == tabIndex)
            return;
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

        ElementBounds dialogBounds = ElementStdBounds
            .AutosizedMainDialog.WithFixedWidth(860.0)
            .WithAlignment(EnumDialogArea.CenterTop)
            .WithFixedOffset(0.0, 40.0);

        ElementBounds insetBounds = ElementBounds.Fixed(
            0.0,
            GuiStyle.TitleBarHeight,
            insetWidth,
            insetHeight
        );
        ElementBounds scrollbarBounds = insetBounds
            .RightCopy(0.0, 0.0, 0.0, 0.0)
            .WithFixedWidth(20.0);
        ElementBounds clipBounds = insetBounds.ForkContainingChild(
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding
        );
        ElementBounds containerBounds = insetBounds.ForkContainingChild(
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding,
            GuiStyle.HalfPadding
        );
        ElementBounds buttonBoundsAddNotice = insetBounds
            .RightCopy(0.0, 0.0, 0.0, 0.0)
            .WithFixedWidth(120.0)
            .WithFixedHeight(40.0)
            .WithFixedOffset(36.0, 0.0);

        ElementBounds bgBounds = ElementBounds
            .Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren)
            .WithChildren(insetBounds, scrollbarBounds, buttonBoundsAddNotice);

        GuiComposer dialogComposer = capi.Gui.CreateCompo("noticeBoardScrollGui", dialogBounds);
        GuiComposerHelpers.AddShadedDialogBG(dialogComposer, bgBounds, true, 5.0, 0.75f);
        GuiComposerHelpers.AddDialogTitleBar(
            dialogComposer,
            Lang.Get("noticeboard:main-window-title-bar") + $" - Owner: {this.boardPlayerName}",
            this.OnTitleBarClose
        );

        ElementBounds tabBounds = ElementBounds.Fixed(0.0, -32, insetWidth, 35.0);
        GuiTab[] tabs = new GuiTab[]
        {
            new GuiTab() { Name = Lang.Get("Messages"), DataInt = 0 },
            isOwner
                ? new GuiTab() { Name = Lang.Get("Settings"), DataInt = 1 }
                : null,
        };

        dialogComposer.AddHorizontalTabs(
            tabs,
            tabBounds,
            this.OnTabClicked,
            CairoFont.WhiteSmallText(),
            CairoFont.WhiteSmallText(),
            "topTabs"
        );
        dialogComposer.GetHorizontalTabs("topTabs").SetValue(this.activeTab);

        if (this.activeTab == 0)
        {
            NoticeBoardBlockEntity blockEntity =
                capi.World.BlockAccessor.GetBlockEntity(this.boardPos) as NoticeBoardBlockEntity;

            if (blockEntity != null && this.enableParchment)
            {
                ElementBounds slotBounds = buttonBoundsAddNotice
                    .RightCopy(-120.0, 48.0, 0.0, 0.0)
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

            GuiComposerHelpers.AddVerticalScrollbar(
                dialogComposer,
                this.OnNewScrollbarValue,
                scrollbarBounds,
                "scrollbar"
            );
            GuiComposerHelpers.AddSmallButton(
                dialogComposer,
                Lang.Get("noticeboard:main-window-add-notice-button"),
                () => this.AddMessage(),
                buttonBoundsAddNotice,
                EnumButtonStyle.Normal,
                "addNoticeButton"
            );

            PopulateMessagesTab(dialogComposer, insetBounds);
        }
        else if (this.activeTab == 1)
        {
            ElementBounds settingsInsetBounds = ElementBounds.Fixed(
                20,
                GuiStyle.TitleBarHeight + 20,
                insetWidth + 155,
                insetHeight
            );
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
                scrollbar.SetHeights(
                    (float)insetBounds.InnerHeight,
                    (float)this.lastCalculatedContentHeight
                );
            }

            if (this.isLocked && !isOwner)
            {
                var addBtn = GuiComposerHelpers.GetButton(base.SingleComposer, "addNoticeButton");
                if (addBtn != null)
                    addBtn.Enabled = false;
            }
        }
    }

    private void PopulateMessagesTab(GuiComposer composer, ElementBounds insetBounds)
    {
        double insetWidth = 575.0;
        double minRowHeight = 100.0;
        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);

        ElementBounds containerRowBounds = ElementBounds.Fixed(0.0, 0.0, insetWidth, minRowHeight);
        GuiElementContainer scrollArea = GuiComposerHelpers.GetContainer(
            composer,
            "scroll-content"
        );

        this.lastCalculatedContentHeight = 0.0;

        double[] inkColor = new double[] { 0.96, 0.94, 0.88, 1.0 };
        CairoFont inkFont = CairoFont
            .WhiteDetailText()
            .WithColor(inkColor)
            .WithFont("alagard")
            .WithFontSize(18f);
        CairoFont buttonFont = CairoFont.WhiteDetailText();

        if (this.messages != null && this.messages.Count > 0)
        {
            for (int i = 0; i < this.messages.Count; i++)
            {
                Message message = this.messages[i];
                int id = message.Id;
                string messageText = message.Text;
                string authorName = message.PlayerName;
                DateTime timeString = message.CreatedAt;

                CairoFont nameFont = inkFont.Clone().WithFontSize(14f);
                CairoFont dateFont = inkFont
                    .Clone()
                    .WithFontSize(12f)
                    .WithOrientation(EnumTextOrientation.Right);

                Action<LinkTextComponent> onLinkClicked = (link) =>
                {
                    this.capi.Gui.OpenLink(link.Href);
                };

                RichTextComponentBase[] nameVtml = VtmlUtil.Richtextify(
                    this.capi,
                    $"{authorName}",
                    nameFont,
                    null
                );
                RichTextComponentBase[] dateVtml = VtmlUtil.Richtextify(
                    this.capi,
                    $"<i>{timeString}</i>",
                    dateFont,
                    null
                );
                RichTextComponentBase[] bodyVtml = VtmlUtil.Richtextify(
                    this.capi,
                    messageText,
                    inkFont,
                    onLinkClicked
                );

                double totalPaperWidth = 520.0;
                double textPaddingLeft = 16.0;
                double textPaddingRight = 24.0;
                double actualTextWidth = totalPaperWidth - textPaddingLeft - textPaddingRight;

                double headerHeight = 20.0;
                double headerGap = 8.0;
                double bodyHeight = this.CalculateRichtextHeight(bodyVtml, actualTextWidth);

                double totalTextHeight = headerHeight + headerGap + bodyHeight;
                double rowPadding = 24.0;
                double rowHeight = Math.Max(totalTextHeight + rowPadding, minRowHeight);

                containerRowBounds = containerRowBounds
                    .WithFixedWidth(totalPaperWidth)
                    .WithFixedHeight(rowHeight);

                //AssetLocation paperTexture = textureList[random.Next(textureList.Count)];
                ElementBounds paperBounds = ElementBounds
                    .Fixed(0.0, containerRowBounds.fixedY, totalPaperWidth, rowHeight)
                    .WithFixedMargin(4.0);
                scrollArea.Add(new GuiElementImage(this.capi, paperBounds, this.paperTexture), -1);

                ElementBounds nameBounds = containerRowBounds
                    .FlatCopy()
                    .WithFixedOffset(textPaddingLeft, 12.0)
                    .WithFixedWidth(actualTextWidth / 2.0); // Covers the left half of the paper

                ElementBounds dateBounds = containerRowBounds
                    .FlatCopy()
                    .WithFixedOffset(textPaddingLeft, 12.0)
                    .WithFixedWidth(actualTextWidth); // Spans full width; right-orientation handles the rest

                ElementBounds bodyBounds = containerRowBounds
                    .FlatCopy()
                    .WithFixedOffset(textPaddingLeft, 12.0 + headerHeight + headerGap)
                    .WithFixedWidth(actualTextWidth);

                ElementBounds deleteButtonBounds = containerRowBounds
                    .RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(530.0, containerRowBounds.fixedY + 2.0)
                    .WithFixedHeight(30.0)
                    .WithFixedWidth(30.0);
                deleteButtonBounds.WithFixedMargin(0.0, 8.0);

                ElementBounds editButtonBounds = containerRowBounds
                    .RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(530.0, containerRowBounds.fixedY + 35.0)
                    .WithFixedHeight(30.0)
                    .WithFixedWidth(30.0);
                editButtonBounds.WithFixedMargin(0.0, 8.0);

                ElementBounds bumpButtonBounds = containerRowBounds
                    .RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(530.0, containerRowBounds.fixedY + 68.0)
                    .WithFixedHeight(30.0)
                    .WithFixedWidth(30.0);
                editButtonBounds.WithFixedMargin(0.0, 8.0);

                scrollArea.Add(new GuiElementRichtext(this.capi, nameVtml, nameBounds), -1);
                scrollArea.Add(new GuiElementRichtext(this.capi, dateVtml, dateBounds), -1);
                scrollArea.Add(new GuiElementRichtext(this.capi, bodyVtml, bodyBounds), -1);

                // Add Action Buttons
                bool isSender = (message.PlayerId == capi.World.Player.PlayerUID);
                bool canEdit = isOwner || (!this.isLocked && isSender);
                if (canEdit)
                {
                    scrollArea.Add(
                        new GuiElementTextButton(
                            this.capi,
                            "X",
                            buttonFont,
                            buttonFont,
                            () => this.RemoveMessage(id),
                            deleteButtonBounds,
                            EnumButtonStyle.Normal
                        ),
                        -1
                    );
                    scrollArea.Add(
                        new GuiElementTextButton(
                            this.capi,
                            "E",
                            buttonFont,
                            buttonFont,
                            () => this.EditMessage(id),
                            editButtonBounds,
                            EnumButtonStyle.Normal
                        ),
                        -1
                    );
                    scrollArea.Add(
                        new GuiElementTextButton(
                            this.capi,
                            "▲",
                            buttonFont,
                            buttonFont,
                            () => this.BumpMessage(id),
                            bumpButtonBounds,
                            EnumButtonStyle.Normal
                        ),
                        -1
                    );
                }

                ElementBounds separatorBounds = containerRowBounds
                    .BelowCopy(0.0, 8.0, 0.0, 0.0)
                    .WithFixedHeight(2.0)
                    .WithFixedWidth(totalPaperWidth);

                this.lastCalculatedContentHeight += rowHeight + 34.0f;
                containerRowBounds = separatorBounds.BelowCopy(0.0, 8.0, 0.0, 0.0);
            }
        }
        else
        {
            scrollArea.Add(
                new GuiElementStaticText(this.capi, "", 0, containerRowBounds, inkFont),
                -1
            );
            this.lastCalculatedContentHeight = minRowHeight + 60.0;
        }
    }

    private void PopulateSettingsTab(GuiComposer composer, ElementBounds insetBounds)
    {
        string fontsDir = Path.Combine(
            capi.ModLoader.GetMod("noticeboard").SourcePath,
            "assets/noticeboard/fonts"
        );

        string[] fontPaths = Directory.Exists(fontsDir)
            ? Directory
                .GetFiles(fontsDir, "*.ttf")
                .Concat(Directory.GetFiles(fontsDir, "*.otf"))
                .ToArray()
            : Array.Empty<string>();

        string[] fontFileNames = fontPaths.Select(Path.GetFileNameWithoutExtension).ToArray();
        string[] fontValues = fontPaths.Select(Path.GetFileNameWithoutExtension).ToArray();

        int checkboxWidth = 50;
        int inputWidth = 200;
        int sliderWidth = 200;

        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);

        bool isProximityLoaded = capi.ModLoader.IsModEnabled("modid");

        ElementBounds leftBounds = ElementBounds
            .FixedSize(280, 30)
            .WithFixedPosition(insetBounds.fixedX + 20, insetBounds.fixedY + 20);
        ElementBounds rightBounds = ElementBounds
            .FixedSize(inputWidth, 30)
            .WithFixedPosition(insetBounds.fixedX + 300, insetBounds.fixedY + 15);

        // 1. BoardId - Readonly field
        composer.AddStaticText("Board ID:", CairoFont.WhiteSmallText(), leftBounds);
        composer.AddStaticText(this.boardId ?? "Unknown", CairoFont.WhiteSmallText(), rightBounds);

        // 2. BoardName
        leftBounds = leftBounds.BelowCopy(0, 20);
        rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(inputWidth, 30);

        composer.AddStaticText("Board Name:", CairoFont.WhiteSmallText(), leftBounds);

        if (isOwner)
        {
            composer.AddTextInput(
                rightBounds,
                (text) =>
                {
                    this.boardName = text;
                    UpdateDirtyState();
                },
                CairoFont.WhiteSmallText(),
                "boardNameInput"
            );
            composer.GetTextInput("boardNameInput").SetValue(this.boardName ?? "");
        }
        else
        {
            composer.AddStaticText(
                Lang.Get("(Only the owner can edit this)"),
                CairoFont.WhiteSmallText(),
                rightBounds.FlatCopy().WithFixedWidth(300)
            );
        }

        // 3. BoardOwner
        leftBounds = leftBounds.BelowCopy(0, 20);
        rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(inputWidth, 30);

        composer.AddStaticText("Board Owner:", CairoFont.WhiteSmallText(), leftBounds);

        if (isOwner)
        {
            composer.AddDropDown(
                players.Select(p => p.PlayerUID).ToArray(),
                players.Select(p => p.PlayerName).ToArray(),
                players.FindIndex(p => p.PlayerUID == boardPlayerId),
                (code, selected) =>
                {
                    this.pendingOwnerUid = code;
                    UpdateDirtyState();
                },
                rightBounds,
                "ownerDropdown"
            );
        }
        else
        {
            composer.AddStaticText(
                Lang.Get("(Only the owner can edit this)"),
                CairoFont.WhiteSmallText(),
                rightBounds.FlatCopy().WithFixedWidth(300)
            );
        }

        // Font Selection
        leftBounds = leftBounds.BelowCopy(0, 20);
        rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(inputWidth, 30);

        composer.AddStaticText("Board Font:", CairoFont.WhiteSmallText(), leftBounds);

        if (isOwner)
        {
            if (fontValues.Length > 0)
            {
                int selectedFontIndex = Array.IndexOf(fontValues, this.boardFont ?? fontValues[0]);
                if (selectedFontIndex < 0) selectedFontIndex = 0;

                composer.AddDropDown(
                    fontValues,
                    fontFileNames,
                    selectedFontIndex,
                    (value, selected) =>
                    {
                        this.boardFont = value;
                        UpdateDirtyState();
                    },
                    rightBounds,
                    "fontDropdown"
                );
            }
            else
            {
                composer.AddStaticText(
                    "No fonts found.",
                    CairoFont.WhiteSmallText(),
                    rightBounds
                );
            }
        }
        else
        {
            composer.AddStaticText(
                Lang.Get("(Only the owner can edit this)"),
                CairoFont.WhiteSmallText(),
                rightBounds.FlatCopy().WithFixedWidth(300)
            );
        }

        // 4. Lock Notice Board
        leftBounds = leftBounds.BelowCopy(0, 20);
        rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(checkboxWidth, 30);

        composer.AddStaticText("Lock Notice Board:", CairoFont.WhiteSmallText(), leftBounds);

        if (isOwner)
        {
            composer.AddSwitch(
                (state) =>
                {
                    this.isLocked = state;
                    UpdateDirtyState();
                },
                rightBounds,
                "lockSwitch"
            );
            composer.GetSwitch("lockSwitch").On = this.isLocked;
        }
        else
        {
            composer.AddStaticText(
                Lang.Get("(Only the owner can lock this board)"),
                CairoFont.WhiteSmallText(),
                rightBounds.FlatCopy().WithFixedWidth(300)
            );
        }

        // 5. Enable Parchment
        leftBounds = leftBounds.BelowCopy(0, 20);
        rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(checkboxWidth, 30);

        composer.AddStaticText("Enable Parchment:", CairoFont.WhiteSmallText(), leftBounds);

        if (isOwner)
        {
            composer.AddSwitch(
                (state) =>
                {
                    this.enableParchment = state;
                    UpdateDirtyState();
                },
                rightBounds,
                "parchmentSwitch"
            );
            composer.GetSwitch("parchmentSwitch").On = this.enableParchment;
        }
        else
        {
            composer.AddStaticText(
                Lang.Get("(Only the owner can edit this)"),
                CairoFont.WhiteSmallText(),
                rightBounds.FlatCopy().WithFixedWidth(300)
            );
        }

        // 6. Enable Particles
        leftBounds = leftBounds.BelowCopy(0, 20);
        rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(checkboxWidth, 30);

        composer.AddStaticText("Enable Particles:", CairoFont.WhiteSmallText(), leftBounds);

        if (isOwner)
        {
            composer.AddSwitch(
                (state) =>
                {
                    this.enableParticles = state;
                    UpdateDirtyState();
                },
                rightBounds,
                "particlesSwitch"
            );
            composer.GetSwitch("particlesSwitch").On = this.enableParticles;
        }
        else
        {
            composer.AddStaticText(
                Lang.Get("(Only the owner can edit this)"),
                CairoFont.WhiteSmallText(),
                rightBounds.FlatCopy().WithFixedWidth(300)
            );
        }

        if (isProximityLoaded)
        {
            // 7. Enable Proximity
            leftBounds = leftBounds.BelowCopy(0, 20);
            rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(checkboxWidth, 30);

            composer.AddStaticText(
                "Enable Proximity Message:",
                CairoFont.WhiteSmallText(),
                leftBounds
            );

            if (isOwner)
            {
                composer.AddSwitch(
                    (state) =>
                    {
                        this.enableProximityMessage = state;
                        UpdateDirtyState();
                    },
                    rightBounds,
                    "proximitySwitch"
                );
                composer.GetSwitch("proximitySwitch").On = this.enableProximityMessage;
            }
            else
            {
                composer.AddStaticText(
                    Lang.Get("(Only the owner can edit this)"),
                    CairoFont.WhiteSmallText(),
                    rightBounds.FlatCopy().WithFixedWidth(300)
                );
            }

            // 8. Proximity Channel Name
            leftBounds = leftBounds.BelowCopy(0, 20);
            rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(inputWidth, 30);

            composer.AddStaticText(
                "Proximity Channel Name:",
                CairoFont.WhiteSmallText(),
                leftBounds
            );

            if (isOwner)
            {
                composer.AddTextInput(
                    rightBounds,
                    (text) =>
                    {
                        this.proximityChannel = text;
                        UpdateDirtyState();
                    },
                    CairoFont.WhiteSmallText(),
                    "channelInput"
                );
                composer.GetTextInput("channelInput").SetValue(this.proximityChannel ?? "");
            }
            else
            {
                composer.AddStaticText(
                    Lang.Get("(Only the owner can edit this)"),
                    CairoFont.WhiteSmallText(),
                    rightBounds.FlatCopy().WithFixedWidth(300)
                );
            }

            // 9. Proximity Distance
            leftBounds = leftBounds.BelowCopy(0, 20);
            rightBounds = rightBounds.BelowCopy(0, 20).WithFixedSize(sliderWidth, 30);

            this.pendingDistance = (int)Math.Max(1, this.proximityDistance);
            composer.AddDynamicText(
                $"Proximity Distance: {this.pendingDistance}",
                CairoFont.WhiteSmallText(),
                leftBounds,
                "distanceLabel"
            );

            if (isOwner)
            {
                composer.AddSlider(
                    (newValue) =>
                    {
                        this.pendingDistance = newValue;
                        composer
                            .GetDynamicText("distanceLabel")
                            .SetNewText($"Proximity Distance: {newValue}");
                        UpdateDirtyState();
                        return true;
                    },
                    rightBounds,
                    "distanceSlider"
                );

                composer.GetSlider("distanceSlider").SetValues(this.pendingDistance, 1, 1000, 1);
            }
            else
            {
                composer.AddStaticText(
                    Lang.Get("(Only the owner can edit this)"),
                    CairoFont.WhiteSmallText(),
                    rightBounds.FlatCopy().WithFixedWidth(300)
                );
            }
        }

        // 10. Save All Settings Button
        ElementBounds btnSaveAllBounds = leftBounds.BelowCopy(0, 20).WithFixedSize(160, 30);

        composer.AddSmallButton(
            Lang.Get("Save All Settings"),
            OnSaveAllSettingsClick,
            btnSaveAllBounds,
            EnumButtonStyle.Normal,
            "btnSaveAll"
        );
        composer.GetButton("btnSaveAll").Enabled = false;
    }

    private void ResetSettings()
    {
        var p = this.noticeBoardPacket.BoardProperties;

        this.isLocked = p.IsLocked != 0;
        this.enableParticles = p.EnableParticles != 0;
        this.enableParchment = p.EnableParchment != 0;
        this.enableProximityMessage = p.EnableProximity != 0;
        this.proximityChannel = p.ProximityChannel;
        this.pendingDistance = (int)p.ProximityDistance;
        this.pendingOwnerUid = p.PlayerId;

        this.isDirty = false;
    }

    private double CalculateRichtextHeight(RichTextComponentBase[] vtml, double width)
    {
        ElementBounds dummyBounds = ElementBounds.Fixed(0, 0, width, 0);
        dummyBounds.CalcWorldBounds();
        GuiElementRichtext dummyElement = new GuiElementRichtext(this.capi, vtml, dummyBounds);
        dummyElement.BeforeCalcBounds();
        return dummyBounds.fixedHeight;
    }

    private double CalculateTextHeight(string text, double width)
    {
        double[] inkColor = new double[] { 0.18, 0.14, 0.10, 1.0 }; // Faded dark brown
        CairoFont inkFont = CairoFont
            .WhiteDetailText()
            .WithColor(inkColor)
            .WithFont("MedievalSharp")
            .WithFontSize(18f);
        return capi.Gui.Text.GetMultilineTextHeight(inkFont, text, width);
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
        this.paperTexture = null;

        NoticeBoardBlockEntity blockEntity =
            capi.World.BlockAccessor.GetBlockEntity(this.boardPos) as NoticeBoardBlockEntity;
        if (blockEntity != null)
        {
            capi.World.Player.InventoryManager.CloseInventory(blockEntity.Inventory);
        }

        capi.Network.SendBlockEntityPacket(
            this.boardPos.X,
            this.boardPos.Y,
            this.boardPos.Z,
            999,
            null
        );
    }

    private void OnTitleBarClose() => TryClose();
}
