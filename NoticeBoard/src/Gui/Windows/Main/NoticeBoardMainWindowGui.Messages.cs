using NoticeBoard;
using NoticeBoard.BlockType;
using NoticeBoard.Configs;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Rendering;
using NoticeBoard.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace NoticeBoard.src.Gui.Windows;

public partial class NoticeBoardMainWindowGui
{
        public bool OpenTextInput(string mode, int messageId, string currentText, int isAnonymous = 0, int holder = 0, string paperTheme = "", bool hasWaypoint = false, float waypointX = 0, float waypointZ = 0, string waypointTitle = "", string waypointIcon = "", string waypointColor = "")
        {
            if (this.textInputGui != null && this.textInputGui.IsOpened())
                this.textInputGui.TryClose();

            this.textInputGui = new NoticeBoardTextInputWindowGui(
                this.capi,
                this,
                this.noticeBoardPacket,
                mode,
                messageId,
                currentText,
                isAnonymous,
                holder,
                paperTheme,
                hasWaypoint,
                waypointX,
                waypointZ,
                waypointTitle,
                waypointIcon,
                waypointColor
            );
        this.textInputGui.TryOpen();
        return true;
    }

    private bool OnAddMessageClick()
    {
        if (!enableParchment)
            return OpenTextInput("add", -1, "");

        if (
            capi.GetNoticeBoardEntity(this.boardPos)
            is not NoticeBoardBlockEntity blockEntity
        )
        {
            capi.TriggerIngameError(this, "missing_board", Lang.Get("noticeboard:messages-error-missing-board"));
            return false;
        }

        ItemSlot validSlot = null;

        foreach (ItemSlot slot in blockEntity.Inventory)
        {
            if (!slot.Empty && (slot.Itemstack.Collectible.Code.Path.StartsWith("paper-parchment") || slot.Itemstack.Collectible.Code.Path == "papyrus-paper"))
            {
                validSlot = slot;
                break; 
            }
        }

        if (validSlot == null)
        {
            capi.TriggerIngameError(this, "no_item", Lang.Get("noticeboard:messages-error-no-item-empty"));
            return false;
        }

        return OpenTextInput("add", -1, "");
    }

    private bool OnPostDocumentClick()
    {
        if (
            capi.GetNoticeBoardEntity(this.boardPos)
            is not NoticeBoardBlockEntity blockEntity
        )
        {
            capi.TriggerIngameError(this, "missing_board", Lang.Get("noticeboard:messages-error-missing-board"));
            return false;
        }

        ItemSlot writtenSlot = blockEntity.Inventory[4]; // The 5th slot

        if (writtenSlot.Empty)
        {
            capi.TriggerIngameError(this, "no_item", Lang.Get("noticeboard:messages-error-no-item-written"));
            return false;
        }

        string textContent = writtenSlot.Itemstack.Attributes.GetString("text", "");

        if (string.IsNullOrEmpty(textContent))
        {
            capi.TriggerIngameError(this, "no_text", Lang.Get("noticeboard:messages-error-no-text"));
            return false;
        }

        var props = noticeBoardPacket.BoardProperties;
        bool manual = props.EnableManualPin != 0 && props.EnableLegacyBoard == 0;
        if (manual)
        {
            int units = PaperSize.MeasureHeightUnits(capi, textContent, props.BoardFont, props.BoardFontSize, hasAuthor: true);
            NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(this.boardPos);
            bool isWall = be?.Block?.Variant?["attachment"] == "wall";
            float rotateYDeg = NoticeBoardBlockEntity.GetRotateYDeg(be?.Block);
            TryClose();
            PaperPinController.Instance.BeginDocument(
                props.BoardId,
                this.boardPos,
                isWall,
                rotateYDeg,
                units,
                textContent
            );
            return true;
        }

        var networkChannel = capi.Network.GetChannel("noticeboard");
        networkChannel.SendPacket(
            new PlayerSendDocument
            {
                Document = textContent,
                BoardId = noticeBoardPacket.BoardProperties.BoardId,
                PlayerId = capi.World.Player.PlayerUID,
                PaperSeed = MessageVisualData.NewPaperSeed(),
            }
        );

        GetMessages();

        return true;
    }

    private bool EditMessage(int id)
    {
        Message message = this.messages.Find(m => m.Id == id);
        return message != null && OpenTextInput("edit", message.Id, message.Text, message.IsAnonymous, message.Holder, message.PaperTheme, message.HasWaypoint, message.WaypointX, message.WaypointZ, message.WaypointTitle, message.WaypointIcon, message.WaypointColor);
    }

    private bool RemoveMessage(int id)
    {
        capi.Network.GetChannel("noticeboard")
            .SendPacket(new PlayerRemoveMessage { MessageId = id, BoardId = this.boardId });
        this.GetMessages();
        return true;
    }

    private bool BumpMessage(int id)
    {
        capi.Network.GetChannel("noticeboard").SendPacket(new PlayerBumpMessage { MessageId = id, BoardId = this.boardId });
        this.GetMessages();
        return true;
    }

    private void OnMessageRowHover(int id, bool inside)
    {
        if (inside)
            this.hoveredMessageId = id;
        else if (this.hoveredMessageId == id)
            this.hoveredMessageId = -1;

        capi.GetNoticeBoardEntity(this.boardPos)?.SetHighlightedMessage(this.hoveredMessageId);
    }

    private void OnMessageRowClicked(int id)
    {
        Message message = null;
        if (this.messages != null)
        {
            for (int i = 0; i < this.messages.Count; i++)
            {
                if (this.messages[i].Id == id)
                {
                    message = this.messages[i];
                    break;
                }
            }
        }
        if (message == null || string.IsNullOrWhiteSpace(message.Text))
            return;

        var data = new MessageVisualData(
            message.Text,
            message.IsAnonymous == 1 ? "" : message.PlayerName,
            GetAbsoluteGameDate(message.TotalHours),
            message.Holder,
            message.PaperTheme ?? ""
        )
        {
            TotalHours = message.TotalHours,
            PaperSeed = message.PaperSeed,
        };
        NoticeBoardPreviewOverlay.Instance?.ShowPinned(
            id,
            data,
            this.boardFont,
            this.boardTheme,
            this.boardFontSize,
            this.enableNoticeAging,
            this.noticeBoardPacket?.BoardProperties?.NoticeAgingDays ?? GameDateFormatter.DefaultLifeDays(this.capi.World.Calendar));
    }

    private bool MoveMessage(int id)
    {
        Message message = this.messages?.Find(m => m.Id == id);
        if (message == null)
            return false;

        var props = noticeBoardPacket.BoardProperties;
        if (props.EnableManualPin == 0 || props.EnableLegacyBoard != 0)
            return false;

        NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(this.boardPos);
        if (be == null)
        {
            capi.TriggerIngameError(this, "missing_board", Lang.Get("noticeboard:messages-error-missing-board"));
            return false;
        }

        int units = PaperSize.MeasureHeightUnits(
            capi, message.Text, props.BoardFont, props.BoardFontSize, hasAuthor: message.IsAnonymous == 0);
        bool isWall = be.Block?.Variant?["attachment"] == "wall";
        float rotateYDeg = NoticeBoardBlockEntity.GetRotateYDeg(be.Block);
        TryClose();
        PaperPinController.Instance.BeginReposition(
            props.BoardId,
            this.boardPos,
            isWall,
            rotateYDeg,
            units,
            message.Text,
            message.IsAnonymous,
            message.Holder,
            message.PaperTheme,
            props.BoardFont,
            id
        );
        return true;
    }

    public void GetMessages()
    {
        capi.Network.GetChannel("noticeboard")
            .SendPacket(new RequestAllMessages { BoardId = this.boardId });
    }

    public void RefreshMessageList()
    {
        base.SingleComposer?.Dispose();
        this.OnGuiOpened();
    }

    private void OnNewScrollbarValue(float value)
    {
        if (!this.isComposing)
        {
            this.currentScrollY = value;
        }

        var content = GuiComposerHelpers.GetContainer(base.SingleComposer, "scroll-content");
        if (content != null)
        {
            content.Bounds.fixedY = -value;
            content.Bounds.CalcWorldBounds();
        }
    }

    private double CalculateRichtextHeight(RichTextComponentBase[] vtml, double width)
    {
        ElementBounds dummyBounds = ElementBounds.Fixed(0, 0, width - 2.0, 0);

        dummyBounds.CalcWorldBounds();
        GuiElementRichtext dummyElement = new(this.capi, vtml, dummyBounds);
        dummyElement.BeforeCalcBounds();

        return Math.Ceiling(dummyBounds.fixedHeight) + 2.0;
    }

    private string GetAbsoluteGameDate(double savedTotalHours)
    {
        var calendar = this.capi.World.Calendar;
        return GameDateFormatter.FormatImmersiveDate(calendar.HoursPerDay, calendar.DaysPerMonth, savedTotalHours);
    }

    private void PopulateMessagesTab(GuiComposer composer, ElementBounds insetBounds)
    {
        double insetWidth = 720.0;
        double minRowHeight = 150.0;
        bool isOwner = (this.boardPlayerId == capi.World.Player.PlayerUID);

        ElementBounds containerRowBounds = ElementBounds.Fixed(0.0, 0.0, insetWidth, minRowHeight);
        GuiElementContainer scrollArea = GuiComposerHelpers.GetContainer(
            composer,
            "scroll-content"
        );

        this.lastCalculatedContentHeight = 0.0;

        ParchmentPalette theme = ThemeManager.GetCurrentTheme(this.boardTheme);

        CairoFont inkFont = CairoFont
            .WhiteDetailText()
            .WithColor(theme.InkColor)
            .WithFont(this.boardFont)
            .WithFontSize(this.boardFontSize);
        CairoFont buttonFont = CairoFont.WhiteDetailText();

        Action<LinkTextComponent> onLinkClicked = (link) =>
        {
            var href = link.Href;

            var scheme = href.Contains("://") ? href.Split(new[] { "://" }, 2, StringSplitOptions.None)[0] : null;
            if (scheme != null && this.capi.LinkProtocols.ContainsKey(scheme))
                this.capi.LinkProtocols[scheme].Invoke(link);
            else
                this.capi.Gui.OpenLink(href);
        };

        if (this.messages != null && this.messages.Count > 0)
        {
            IEnumerable<Message> rows = this.messages;
            string filter = (this.messageFilter ?? "").Trim();
            if (filter.Length > 0)
            {
                rows = rows.Where(m =>
                    (m.Text != null && m.Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    || (m.IsAnonymous == 0 && m.PlayerName != null
                        && m.PlayerName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0));
            }
            if (this.messageSort == 1)
                rows = rows.Reverse();
            else if (this.messageSort == 2)
                rows = rows.OrderBy(m => m.IsAnonymous == 1 ? "\uFFFF" : (m.PlayerName ?? ""), StringComparer.OrdinalIgnoreCase).ThenByDescending(m => m.Id);

            List<Message> visible = rows.ToList();
            for (int i = 0; i < visible.Count; i++)
            {
                Message message = visible[i];
                int id = message.Id;
                string messageText = message.Text;
                string authorName = message.PlayerName;
                double totalHours = message.TotalHours;
                int isAnonymous = message.IsAnonymous;

                string immersiveDate = GetAbsoluteGameDate(totalHours);

                ParchmentPalette rowTheme = ThemeManager.Resolve(message.PaperTheme, this.boardTheme);
                double wear01 = 0;
                if (this.enableNoticeAging)
                {
                    var cal = this.capi.World.Calendar;
                    double age01 = GameDateFormatter.Age01(
                        totalHours,
                        cal.TotalHours,
                        cal.HoursPerDay,
                        GameDateFormatter.ClampLifeDays(this.noticeBoardPacket.BoardProperties.NoticeAgingDays));
                    wear01 = GameDateFormatter.Wear01(age01);
                    rowTheme = ThemeManager.Age(rowTheme, age01);
                }
                CairoFont rowInkFont = CairoFont
                    .WhiteDetailText()
                    .WithColor(rowTheme.InkColor)
                    .WithFont(this.boardFont)
                    .WithFontSize(this.boardFontSize);

                CairoFont nameFont = rowInkFont.Clone().WithFontSize(16f);
                CairoFont dateFont = rowInkFont
                    .Clone()
                    .WithFontSize(14f)
                    .WithOrientation(EnumTextOrientation.Right);

                RichTextComponentBase[] nameVtml = VtmlUtil.Richtextify(
                    this.capi,
                    $"{(isAnonymous == 0 ? authorName : "")}",
                    nameFont,
                    null
                );
                RichTextComponentBase[] dateVtml = VtmlUtil.Richtextify(
                    this.capi,
                    $"<i>{immersiveDate}</i>",
                    dateFont,
                    null
                );
                RichTextComponentBase[] bodyVtml = VtmlUtil.Richtextify(
                    this.capi,
                    messageText,
                    rowInkFont,
                    onLinkClicked
                );

                foreach (RichTextComponentBase component in bodyVtml)
                {
                    if (component is LinkTextComponent linkComponent)
                    {
                        linkComponent.Font = linkComponent.Font.Clone().WithColor(rowTheme.LinkColor);
                    }
                }

                double totalPaperWidth = 690.0;
                double textPaddingLeft = 32.0;
                double textPaddingRight = 12.0;
                double actualTextWidth = totalPaperWidth - textPaddingLeft - textPaddingRight;
                double headerHeight = 40.0;
                double headerGap = 8.0;
                double bottomOffset = 40.0;
                double bodyHeight = this.CalculateRichtextHeight(bodyVtml, actualTextWidth);
                double totalTextHeight = headerHeight + headerGap + bodyHeight + bottomOffset;
                double rowPadding = 24.0;
                double rowHeight = Math.Max(totalTextHeight + rowPadding, minRowHeight);

                containerRowBounds = containerRowBounds
                    .WithFixedWidth(totalPaperWidth)
                    .WithFixedHeight(rowHeight);

                ElementBounds nameBounds = containerRowBounds
                    .FlatCopy()
                    .WithFixedOffset(textPaddingLeft, 28.0)
                    .WithFixedWidth(actualTextWidth / 2.0)
                    .WithFixedHeight(headerHeight);
                ElementBounds dateBounds = containerRowBounds
                    .FlatCopy()
                    .WithFixedOffset(textPaddingLeft, rowHeight - 40)
                    .WithFixedWidth(actualTextWidth)
                    .WithFixedHeight(headerHeight);
                ElementBounds bodyBounds = containerRowBounds
                    .FlatCopy()
                    .WithFixedOffset(textPaddingLeft, 12.0 + headerHeight + headerGap)
                    .WithFixedWidth(actualTextWidth);

                double buttonSize = 18.0;
                double buttonSpacing = 26.0;
                double buttonBaseX = totalPaperWidth - 28.0;
                double buttonY = containerRowBounds.fixedY + 22.0;

                ElementBounds deleteButtonBounds = containerRowBounds
                    .RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(buttonBaseX, buttonY)
                    .WithFixedHeight(buttonSize)
                    .WithFixedWidth(buttonSize);

                ElementBounds editButtonBounds = containerRowBounds
                    .RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(buttonBaseX - buttonSpacing, buttonY)
                    .WithFixedHeight(buttonSize)
                    .WithFixedWidth(buttonSize);

                ElementBounds bumpButtonBounds = containerRowBounds
                    .RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(buttonBaseX - 2.0 * buttonSpacing, buttonY)
                    .WithFixedHeight(buttonSize)
                    .WithFixedWidth(buttonSize);

                ElementBounds moveButtonBounds = containerRowBounds
                    .RightCopy(0.0, 0.0, 0.0, 0.0)
                    .WithFixedPosition(buttonBaseX - 3.0 * buttonSpacing, buttonY)
                    .WithFixedHeight(buttonSize)
                    .WithFixedWidth(buttonSize);

                scrollArea.Add(new GuiElementRichtext(this.capi, nameVtml, nameBounds), -1);
                scrollArea.Add(new GuiElementRichtext(this.capi, dateVtml, dateBounds), -1);
                scrollArea.Add(new GuiElementRichtext(this.capi, bodyVtml, bodyBounds), -1);

                bool isSender = (message.PlayerId == capi.World.Player.PlayerUID);
                bool canEdit = isOwner
                    || this.permissionMode == (int)BoardPermissionMode.All
                    || (this.permissionMode == (int)BoardPermissionMode.Default && isSender);
                if (canEdit)
                {
                    scrollArea.Add(
                        new GuiElementInkButton(
                            this.capi,
                            "delete",
                            () => this.RemoveMessage(id),
                            deleteButtonBounds,
                            theme
                        ),
                        -1
                    );
                    scrollArea.Add(
                        new GuiElementInkButton(
                            this.capi,
                            "edit",
                            () => this.EditMessage(id),
                            editButtonBounds,
                            theme
                        ),
                        -1
                    );
                    scrollArea.Add(
                        new GuiElementInkButton(
                            this.capi,
                            "bump",
                            () => this.BumpMessage(id),
                            bumpButtonBounds,
                            theme
                        ),
                        -1
                    );
                    if (this.enableManualPin && !this.enableLegacyBoard)
                    {
                        scrollArea.Add(
                            new GuiElementInkButton(
                                this.capi,
                                "move",
                                () => this.MoveMessage(id),
                                moveButtonBounds,
                                theme
                            ),
                            -1
                        );
                    }
                }

                if (message.HasWaypoint)
                {
                    int editCount = 0;
                    if (canEdit)
                        editCount = (this.enableManualPin && !this.enableLegacyBoard) ? 4 : 3;
                    double mapX = buttonBaseX - editCount * buttonSpacing;
                    float wpX = message.WaypointX;
                    float wpZ = message.WaypointZ;
                    ElementBounds mapButtonBounds = containerRowBounds
                        .RightCopy(0.0, 0.0, 0.0, 0.0)
                        .WithFixedPosition(mapX, buttonY)
                        .WithFixedHeight(buttonSize)
                        .WithFixedWidth(buttonSize);
                    scrollArea.Add(
                        new GuiElementInkButton(
                            this.capi,
                            "map",
                            () =>
                            {
                                string title = WaypointPin.ResolveTitle(message.WaypointTitle, this.noticeBoardPacket.BoardProperties.BoardName);
                                string icon = WaypointPin.SanitizeIcon(message.WaypointIcon);
                                string color = WaypointPin.SanitizeColor(message.WaypointColor);
                                EntityPos spawn = capi.World.DefaultSpawnPosition;
                                int hudX = (int)PositionHelper.WorldToHud(wpX, spawn.X);
                                int hudZ = (int)PositionHelper.WorldToHud(wpZ, spawn.Z);
                                capi.SendChatMessage($"/waypoint addati {icon} {hudX} 0 {hudZ} false {color} {title}");
                            },
                            mapButtonBounds,
                            theme
                        ),
                        -1
                    );
                }

                ElementBounds texturePaperBound = ElementBounds
                    .Fixed(0.0, containerRowBounds.fixedY, totalPaperWidth + 20, rowHeight)
                    .WithFixedMargin(4.0);

                scrollArea.Add(
                    new ProceduralPaperGuiElement(
                        this.capi,
                        id,
                        texturePaperBound,
                        rowTheme,
                        jaggedEdges: true,
                        wear01: wear01,
                        visibleBounds: insetBounds,
                        onHoverChanged: this.OnMessageRowHover,
                        onClicked: this.OnMessageRowClicked,
                        parchmentSeed: message.PaperSeed != 0 ? message.PaperSeed : id
                    ),
                    -1
                );

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
}
