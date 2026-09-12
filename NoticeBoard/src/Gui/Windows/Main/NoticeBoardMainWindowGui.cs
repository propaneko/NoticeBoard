using System;
using System.Collections.Generic;
using System.Linq;
using NoticeBoard.BlockType;
using NoticeBoard.Configs;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Rendering;
using NoticeBoard.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace NoticeBoard.src.Gui.Windows;

public partial class NoticeBoardMainWindowGui : GuiDialog
{
    private readonly string boardId;
    private readonly BlockPos boardPos;
    private readonly string boardPlayerId;
    private readonly string boardPlayerName;

    private List<Message> messages;
    private List<PlayerEntry> players;
    private int hoveredMessageId = -1;

    private readonly ResponseAllMessages noticeBoardPacket;

    private NoticeBoardTextInputWindowGui textInputGui;
    private string boardName;
    private string boardFont;
    private string boardTheme;

    private int permissionMode;
    private bool enableParticles;
    private bool enableNoticeAging;
    private int pendingNoticeAgingDays;
    private bool enableParchment;
    private bool enableManualPin;
    private bool enableDiscord;
    private string pendingDiscordWebhook = "";
    private bool pendingClearDiscordWebhook;
    private bool enableLegacyBoard;
    private bool enableProximityMessage;
    private string proximityChannel;
    private double proximityDistance;
    private string pendingOwnerUid;
    private int pendingDistance;
    private float boardFontSize;
    private float pendingFontSize;
    private int pendingMaxPapers;
    private int pendingTextSharpness;
    private int pendingSwayStrength;
    private bool isDirty = false;
    private string messageFilter = "";
    private int messageSort = 0;

    private int activeTab = 0;
    private double lastCalculatedContentHeight = 0.0;
    private float currentScrollY = 0f;
    private bool isComposing = false;
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
        this.boardFont = packet.BoardProperties.BoardFont;
        this.boardFontSize = packet.BoardProperties.BoardFontSize;
        this.boardTheme = packet.BoardProperties.BoardTheme;
        this.boardPlayerId = packet.BoardProperties.PlayerId;
        this.boardPlayerName = packet.BoardProperties.PlayerName;
        this.boardPos = PositionHelper.FromString(packet.BoardProperties.Pos);
        this.permissionMode = packet.BoardProperties.PermissionMode;
        this.enableParticles = packet.BoardProperties.EnableParticles != 0;
        this.enableNoticeAging = packet.BoardProperties.EnableNoticeAging != 0;
        this.pendingNoticeAgingDays = GameDateFormatter.ClampLifeDays(
            packet.BoardProperties.NoticeAgingDays
        );
        this.enableParchment = packet.BoardProperties.EnableParchment != 0;
        this.enableManualPin = packet.BoardProperties.EnableManualPin != 0;
        this.enableDiscord = packet.BoardProperties.EnableDiscord != 0;
        this.enableLegacyBoard = packet.BoardProperties.EnableLegacyBoard != 0;
        this.enableProximityMessage = packet.BoardProperties.EnableProximity != 0;
        this.proximityChannel = packet.BoardProperties.ProximityChannel;
        this.proximityDistance = packet.BoardProperties.ProximityDistance;
        this.pendingMaxPapers = NoticeBoard.Rendering.NoticeBoardPaperLayout.ClampMax(
            packet.BoardProperties.MaxPapersOnBoard
        );
        this.pendingTextSharpness = NoticeBoard.Rendering.PaperSize.ResolveSharpness(
            packet.BoardProperties.TextSharpness
        );
        this.pendingSwayStrength = NoticeBoard.Rendering.PaperSize.ClampSwayStrength(
            packet.BoardProperties.SwayStrength
        );
        this.messages = packet.Messages;
        this.noticeBoardPacket = packet;
    }

    public string BoardId => this.boardId;

    public void UpdateMessages(List<Message> newMessages)
    {
        this.messages = newMessages;
        this.RefreshMessageList();
    }

    public void UpdatePlayersList(List<PlayerEntry> players)
    {
        this.players = players;
    }

    public override void OnGuiOpened()
    {
        ComposeGui();
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();

        if (this.boardPos == null)
            return;

        this.hoveredMessageId = -1;
        NoticeBoardPreviewOverlay.Instance?.HidePinned();
        capi.GetNoticeBoardEntity(this.boardPos)?.SetHighlightedMessage(-1);

        if (
            capi.World.BlockAccessor.GetBlockEntity(this.boardPos)
            is NoticeBoardBlockEntity blockEntity
        )
        {
            capi.World.Player.InventoryManager.CloseInventory(blockEntity.Inventory);
        }

        capi.Network.SendBlockEntityPacket(this.boardPos, 999, null);
    }

    private void OnTabClicked(int tabIndex)
    {
        if (this.activeTab == 1 && tabIndex == 0)
            ResetSettings();

        if (this.activeTab == tabIndex)
            return;

        this.activeTab = tabIndex;
        this.RefreshMessageList();
    }

    private void OnTitleBarClose() => TryClose();

    public void ComposeGui()
    {
        this.hoveredMessageId = -1;
        if (this.boardPos != null)
            capi.GetNoticeBoardEntity(this.boardPos)?.SetHighlightedMessage(-1);

        this.isComposing = true;
        capi.Network.GetChannel("noticeboard").SendPacket(new RequestAllPlayers());
        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);
        NoticeBoardBlockEntity blockEntity = this.boardPos != null
            ? capi.GetNoticeBoardEntity(this.boardPos)
            : null;

        if (this.boardPos == null)
        {
            capi.Logger.Warning(
                "[NoticeBoard] Opening GUI without a valid board position; inventory slots will be unavailable."
            );
        }

        double dialogWidth = 800;
        double listWidth = 720;
        double insetHeight = 456.0;
        int insetDepth = 3;

        ElementBounds dialogBounds = ElementStdBounds
            .AutosizedMainDialog.WithFixedWidth(dialogWidth)
            .WithAlignment(EnumDialogArea.CenterTop)
            .WithFixedOffset(0.0, 40.0);

        ElementBounds buttonBoundsAddNotice = ElementBounds.Fixed(
            0,
            GuiStyle.TitleBarHeight + 5.0,
            140.0,
            40.0
        );
        ElementBounds slotBounds = ElementBounds.Fixed(
            148.0,
            GuiStyle.TitleBarHeight + 1.0,
            200.0,
            48.0
        );

        ElementBounds buttonBoundsAddDocument = ElementBounds.Fixed(
            listWidth - 100,
            GuiStyle.TitleBarHeight + 25.0,
            140.0,
            40.0
        );

        ElementBounds documentSlotBounds = ElementBounds.Fixed(
            listWidth - 158.0,
            GuiStyle.TitleBarHeight + 20.0,
            48.0,
            48.0
        );

        ElementBounds searchBounds = ElementBounds.Fixed(
            0.0,
            GuiStyle.TitleBarHeight + 62.0,
            500.0,
            28.0
        );
        ElementBounds sortBounds = ElementBounds.Fixed(
            508.0,
            GuiStyle.TitleBarHeight + 62.0,
            232.0,
            28.0
        );
        ElementBounds insetBounds = ElementBounds.Fixed(
            0.0,
            GuiStyle.TitleBarHeight + 104.0,
            listWidth,
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

        containerBounds.fixedY = -this.currentScrollY;

        // Messages tab's own bgBounds contribution extends 20px past insetBounds for the
        // scrollbar, so match that total width here to keep both tabs the same size.
        // Inset starts below the Settings-only Board ID row (8 + 30 + 10 = 48 from title bar).
        ElementBounds settingsInsetBounds = ElementBounds.Fixed(
            0,
            GuiStyle.TitleBarHeight + 48,
            listWidth + 20,
            532
        );

        double settingsIdRowY = GuiStyle.TitleBarHeight + 8;
        ElementBounds settingsIdLabelBounds = ElementBounds.Fixed(0, settingsIdRowY, 120, 30);
        ElementBounds settingsIdValueBounds = ElementBounds.Fixed(120, settingsIdRowY, 620, 30);

        ElementBounds bgBounds = ElementBounds
            .Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding)
            .WithSizing(ElementSizing.FitToChildren);

        if (this.activeTab == 0)
            bgBounds.WithChildren(
                insetBounds,
                scrollbarBounds,
                buttonBoundsAddNotice,
                slotBounds,
                searchBounds,
                sortBounds
            );
        else
            bgBounds.WithChildren(
                settingsIdLabelBounds,
                settingsIdValueBounds,
                settingsInsetBounds
            );

        GuiComposer dialogComposer = capi.Gui.CreateCompo("noticeBoardScrollGui", dialogBounds);
        GuiComposerHelpers.AddShadedDialogBG(dialogComposer, bgBounds, true, 5.0, 0.75f);
        GuiComposerHelpers.AddDialogTitleBar(
            dialogComposer,
            Lang.Get("noticeboard:main-window-title-bar") + $" - Owner: {this.boardPlayerName}",
            this.OnTitleBarClose
        );

        ElementBounds tabBounds = ElementBounds.Fixed(0.0, -32, listWidth, 35.0);
        List<GuiTab> tabList = new List<GuiTab>
        {
            new GuiTab() { Name = Lang.Get("noticeboard:main-window-tab-messages"), DataInt = 0 },
        };

        if (isOwner)
        {
            tabList.Add(
                new GuiTab()
                {
                    Name = Lang.Get("noticeboard:main-window-tab-settings"),
                    DataInt = 1,
                }
            );
        }

        GuiTab[] tabs = tabList.ToArray();

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
            if (blockEntity != null && this.enableParchment)
            {
            dialogComposer.AddItemSlotGrid(
                    blockEntity.Inventory,
                    (packet) => capi.Network.SendBlockEntityPacket(boardPos.X, boardPos.Y, boardPos.Z, packet),
                    1,
                    new int[] { 4 },
                    documentSlotBounds,
                    "writtenSlot"
                );

                dialogComposer.AddItemSlotGrid(
                    blockEntity.Inventory,
                    (packet) => capi.Network.SendBlockEntityPacket(boardPos.X, boardPos.Y, boardPos.Z, packet),
                    4,
                    new int[] { 0, 1, 2, 3 },
                    slotBounds,
                    "costSlot"
                );

                GuiComposerHelpers.AddSmallButton(
                    dialogComposer,
                    Lang.Get("noticeboard:main-window-add-document-button"),
                    () => this.OnPostDocumentClick(),
                    buttonBoundsAddDocument,
                    EnumButtonStyle.Normal,
                    "addDocumentButton"
                );
            }

            GuiElementInsetHelper.AddInset(dialogComposer, insetBounds, insetDepth, 0.85f);
            GuiElementClipHelpler.BeginClip(dialogComposer, clipBounds);
            GuiComposerHelpers.AddContainer(dialogComposer, containerBounds, "scroll-content");
            GuiElementClipHelpler.EndClip(dialogComposer);
            GuiComposerHelpers.AddVerticalScrollbar(
                dialogComposer,
                (value) => this.OnNewScrollbarValue(value),
                scrollbarBounds,
                "scrollbar"
            );

            GuiComposerHelpers.AddSmallButton(
                dialogComposer,
                Lang.Get("noticeboard:main-window-add-notice-button"),
                () => this.OnAddMessageClick(),
                buttonBoundsAddNotice,
                EnumButtonStyle.Normal,
                "addNoticeButton"
            );

            dialogComposer.AddTextInput(
                searchBounds,
                text => { this.messageFilter = text ?? ""; },
                CairoFont.WhiteSmallText(),
                "messageSearch"
            );
            string[] sortCodes = { "0", "1", "2" };
            string[] sortLabels =
            {
                Lang.Get("noticeboard:messages-sort-newest"),
                Lang.Get("noticeboard:messages-sort-oldest"),
                Lang.Get("noticeboard:messages-sort-author"),
            };
            dialogComposer.AddDropDown(
                sortCodes,
                sortLabels,
                this.messageSort,
                (code, selected) =>
                {
                    if (!int.TryParse(code, out int next))
                        return;
                    if (next == this.messageSort)
                        return;
                    this.messageSort = next;
                    this.RefreshMessageList();
                },
                sortBounds,
                "messageSort"
            );

            PopulateMessagesTab(dialogComposer, insetBounds);
        }
        else if (this.activeTab == 1)
        {
            dialogComposer.AddStaticText(
                Lang.Get("noticeboard:settings-board-id"),
                CairoFont.WhiteSmallText(),
                settingsIdLabelBounds
            );
            dialogComposer.AddStaticText(
                this.boardId ?? "Unknown",
                CairoFont.WhiteSmallText(),
                settingsIdValueBounds
            );

            GuiElementInsetHelper.AddInset(dialogComposer, settingsInsetBounds, insetDepth, 0.85f);

            PopulateSettingsTab(dialogComposer, settingsInsetBounds);
        }

        base.SingleComposer = dialogComposer.Compose();

        var slotGrid = base.SingleComposer.GetSlotGrid("costSlot");
        if (slotGrid != null)
        {
            slotGrid.DrawIconHandler = (cr, slotId, x, y, w, h, color) =>
            {
                if (blockEntity.Inventory[4].Empty)
                {
                    capi.Gui.Icons.DrawIcon(
                        cr,
                        "circle",
                        x + 4,
                        y + 4,
                        w - 8,
                        h - 8,
                        new double[] { 1, 1, 1, 0.2 }
                    );
                }
            };
        }

        if (this.activeTab == 0)
        {
            var search = base.SingleComposer.GetTextInput("messageSearch");
            if (search != null)
            {
                search.SetValue(this.messageFilter ?? "");
                search.SetPlaceHolderText(Lang.Get("noticeboard:messages-search-placeholder"));
            }

            var scrollbar = GuiComposerHelpers.GetScrollbar(base.SingleComposer, "scrollbar");
            if (scrollbar != null)
            {
                scrollbar.SetHeights(
                    (float)insetBounds.fixedHeight,
                    (float)this.lastCalculatedContentHeight
                );

                float maxScroll = Math.Max(
                    0,
                    (float)this.lastCalculatedContentHeight - (float)insetBounds.fixedHeight
                );
                this.currentScrollY = Math.Min(this.currentScrollY, maxScroll);

                scrollbar.CurrentYPosition = this.currentScrollY;

                OnNewScrollbarValue(this.currentScrollY);
            }

            if (this.permissionMode == (int)BoardPermissionMode.Locked && !isOwner)
            {
                GuiComposerHelpers
                    .GetButton(base.SingleComposer, "addNoticeButton")
                    ?.Enabled = false;
            }
        }
        this.isComposing = false;
    }

    public override void OnKeyDown(KeyEvent args)
    {
        if (this.activeTab == 0 && args.KeyCode == (int)GlKeys.Enter)
        {
            var search = SingleComposer?.GetTextInput("messageSearch");
            if (search != null && search.HasFocus)
            {
                args.Handled = true;
                RefreshMessageList();
                return;
            }
        }

        base.OnKeyDown(args);
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (ForwardOpenSortDropDown(args, down: true))
            return;
        base.OnMouseDown(args);
    }

    public override void OnMouseUp(MouseEvent args)
    {
        if (ForwardOpenSortDropDown(args, down: false))
            return;
        base.OnMouseUp(args);
    }

    private bool ForwardOpenSortDropDown(MouseEvent args, bool down)
    {
        if (this.activeTab != 0)
            return false;
        var dd = SingleComposer?.GetDropDown("messageSort");
        if (dd == null || !dd.listMenu.IsOpened)
            return false;
        if (down)
            dd.OnMouseDown(capi, args);
        else
            dd.OnMouseUp(capi, args);
        return args.Handled;
    }
}
