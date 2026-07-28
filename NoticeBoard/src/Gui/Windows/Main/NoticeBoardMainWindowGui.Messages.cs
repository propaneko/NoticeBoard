using NoticeBoard.BlockType;
using NoticeBoard.Configs;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace NoticeBoard.src.Gui.Windows;

public partial class NoticeBoardMainWindowGui
{
    private bool OpenTextInput(string mode, int messageId, string currentText, int isAnonymous = 0)
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
            isAnonymous
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
        NoticeBoardBlockEntity blockEntity =
            capi.GetNoticeBoardEntity(this.boardPos);
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

        var networkChannel = capi.Network.GetChannel("noticeboard");
        networkChannel.SendPacket(
            new PlayerSendDocument
            {
                Document = "",
                BoardId = noticeBoardPacket.BoardProperties.BoardId,
                PlayerId = capi.World.Player.PlayerUID,
            }
        );

        GetMessages();

        return true;
    }

    private bool EditMessage(int id)
    {
        Message message = this.messages.Find(m => m.Id == id);
        return message != null && OpenTextInput("edit", message.Id, message.Text, message.IsAnonymous);
    }

    private bool RemoveMessage(int id)
    {
        capi.Network.GetChannel("noticeboard")
            .SendPacket(new PlayerRemoveMessage { MessageId = id, BoardId = this.boardId });
        this.GetMessages();
        return true;
    }

    //private bool TakeMessage(int id)
    //{
    //    capi.Network.GetChannel("noticeboard")
    //        .SendPacket(new PlayerTakeMessage { MessageId = id, BoardId = this.boardId });
    //    this.GetMessages();
    //    return true;
    //}

    private bool BumpMessage(int id)
    {
        capi.Network.GetChannel("noticeboard").SendPacket(new PlayerBumpMessage { MessageId = id, BoardId = this.boardId });
        this.GetMessages();
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
        string[] monthNames = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];


        float hoursPerDay = calendar.HoursPerDay;
        int daysPerMonth = calendar.DaysPerMonth;
        int monthsPerYear = monthNames.Length; 
        int daysPerYear = daysPerMonth * monthsPerYear;
        int totalDays = (int)(savedTotalHours / hoursPerDay);

        double hourOfDayRaw = savedTotalHours % hoursPerDay;
        int hour = (int)hourOfDayRaw;
        int minute = (int)((hourOfDayRaw - hour) * 60);

        string amPm = hour >= 12 ? "PM" : "AM";
        int displayHour = hour % 12;
        if (displayHour == 0) displayHour = 12;

        int year = (totalDays / daysPerYear);
        int dayOfYear = totalDays % daysPerYear;
        int monthIndex = dayOfYear / daysPerMonth;
        int dayOfMonth = (dayOfYear % daysPerMonth) + 1;

        string monthName = monthNames[Math.Clamp(monthIndex, 0, monthNames.Length - 1)];

        return $"Day {dayOfMonth} of {monthName}, Year {year} at {displayHour}:{minute:D2} {amPm}";
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
            for (int i = 0; i < this.messages.Count; i++)
            {
                Message message = this.messages[i];
                int id = message.Id;
                string messageText = message.Text;
                string authorName = message.PlayerName;
                double totalHours = message.TotalHours;
                int isAnonymous = message.IsAnonymous;

                string immersiveDate = GetAbsoluteGameDate(totalHours);

                CairoFont nameFont = inkFont.Clone().WithFontSize(16f);
                CairoFont dateFont = inkFont
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
                    inkFont,
                    onLinkClicked
                );

                foreach (RichTextComponentBase component in bodyVtml)
                {
                    if (component is LinkTextComponent linkComponent)
                    {
                        linkComponent.Font = linkComponent.Font.Clone().WithColor(theme.LinkColor);
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
                }

                ElementBounds texturePaperBound = ElementBounds
                    .Fixed(0.0, containerRowBounds.fixedY, totalPaperWidth + 20, rowHeight)
                    .WithFixedMargin(4.0);

                scrollArea.Add(
                    new ProceduralPaperGuiElement(this.capi, id, texturePaperBound, theme),
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
