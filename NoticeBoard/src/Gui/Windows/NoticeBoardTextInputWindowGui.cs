using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Cairo;
using NoticeBoard;
using NoticeBoard.BlockType;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Rendering;
using NoticeBoard.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace NoticeBoard.src.Gui.Windows
{
    public class NoticeBoardTextInputWindowGui : GuiDialog
    {
        private string mode;
        private int messageId;
        private string message;
        private bool enablePreview;
        private int isAnonymous;
        private int holder;
        private string paperTheme;
        private bool attachWaypoint;
        private float wpX;
        private float wpZ;
        private string wpTitle;
        private string wpIcon;
        private string wpColor;

        private NoticeBoardMainWindowGui parentContext;
        private ResponseAllMessages noticeBoardPacket;

        private int _cachedCaretPos = 0;
        private int? _cachedSelStart = null;
        private FieldInfo _selStartField;

        private List<string> textHistory = new List<string>();
        private int historyIndex = -1;
        private bool isHistoryAction = false;

        public NoticeBoardTextInputWindowGui(
            ICoreClientAPI capi,
            NoticeBoardMainWindowGui context,
            ResponseAllMessages noticeBoardPacket,
            string mode,
            int messageId = -1,
            string message = "",
            int isAnonymous = 0,
            int holder = 0,
            string paperTheme = "",
            bool hasWaypoint = false,
            float waypointX = 0,
            float waypointZ = 0,
            string waypointTitle = "",
            string waypointIcon = "",
            string waypointColor = ""
        )
            : base(capi)
        {
            this.noticeBoardPacket = noticeBoardPacket;
            this.mode = mode;
            this.parentContext = context;
            this.enablePreview = false;
            this.holder = MessageHolder.Clamp(holder);
            this.paperTheme = paperTheme ?? "";
            this.message = message ?? "";
            this.messageId = messageId;
            this.isAnonymous = isAnonymous;
            this.attachWaypoint = hasWaypoint;
            this.wpTitle = waypointTitle ?? "";
            this.wpIcon = WaypointPin.SanitizeIcon(waypointIcon);
            this.wpColor = WaypointPin.SanitizeColor(waypointColor);
            if (hasWaypoint)
            {
                EntityPos spawn = capi.World.DefaultSpawnPosition;
                this.wpX = PositionHelper.WorldToHud(waypointX, spawn.X);
                this.wpZ = PositionHelper.WorldToHud(waypointZ, spawn.Z);
            }
            else
            {
                this.wpX = 0;
                this.wpZ = 0;
            }

            textHistory.Add(this.message);
            historyIndex = 0;
        }

        public override void OnGuiOpened()
        {
            Compose();
        }

        private void Compose()
        {
            int insetWidth = 680;
            int insetDepth = 3;

            ParchmentPalette theme = ThemeManager.Resolve(
                this.paperTheme,
                this.noticeBoardPacket.BoardProperties.BoardTheme
            );

            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterBottom)
                .WithFixedOffset(0.0, -120.0);

            double row1Y = GuiStyle.TitleBarHeight;
            ElementBounds boldBtn = ElementBounds.Fixed(0.0, row1Y, 35.0, 30.0);
            ElementBounds italicBtn = ElementBounds.Fixed(40.0, row1Y, 35.0, 30.0);
            ElementBounds largeBtn = ElementBounds.Fixed(80.0, row1Y, 35.0, 30.0);
            ElementBounds smallBtn = ElementBounds.Fixed(120.0, row1Y, 45.0, 30.0);
            ElementBounds linkBtn = ElementBounds.Fixed(170.0, row1Y, 50.0, 30.0);
            ElementBounds handbookBtn = ElementBounds.Fixed(225.0, row1Y, 55.0, 30.0);
            ElementBounds commandBtn = ElementBounds.Fixed(285.0, row1Y, 60.0, 30.0);
            ElementBounds iconBtn = ElementBounds.Fixed(350.0, row1Y, 60, 30.0);
            ElementBounds itemBtn = ElementBounds.Fixed(415.0, row1Y, 60, 30.0);
            ElementBounds helpBtn = ElementBounds.Fixed(580.0, row1Y, 100, 30.0);

            double row2Y = row1Y + 35.0;
            ElementBounds inkBtn = ElementBounds.Fixed(0.0, row2Y, 50.0, 30.0);
            ElementBounds redBtn = ElementBounds.Fixed(55.0, row2Y, 50.0, 30.0);
            ElementBounds blueBtn = ElementBounds.Fixed(110.0, row2Y, 50.0, 30.0);
            ElementBounds greenBtn = ElementBounds.Fixed(165.0, row2Y, 55.0, 30.0);
            ElementBounds goldBtn = ElementBounds.Fixed(225.0, row2Y, 50.0, 30.0);

            ElementBounds inputInsetBounds = ElementBounds.Fixed(
                0.0,
                row2Y + 35.0,
                insetWidth - 25,
                this.enablePreview ? 170.0 : 375.0
            );
            ElementBounds inputClipBounds = inputInsetBounds.ForkContainingChild(
                3.0,
                3.0,
                3.0,
                3.0
            );
            ElementBounds inputContainerBounds = inputClipBounds
                .ForkContainingChild(0.0, 0.0, 0.0, 0.0)
                .WithFixedPadding(0);
            ElementBounds inputScrollbarBounds = inputInsetBounds
                .RightCopy()
                .WithFixedWidth(20.0)
                .WithFixedOffset(5.0, 0.0);

            ElementBounds previewLabelBounds = ElementBounds.Fixed(
                0.0,
                row2Y + 215.0,
                insetWidth,
                25.0
            );
            ElementBounds previewInsetBounds = ElementBounds.Fixed(
                0.0,
                row2Y + 240.0,
                insetWidth - 25,
                170.0
            );
            ElementBounds previewClipBounds = previewInsetBounds.ForkContainingChild(
                3.0,
                3.0,
                3.0,
                3.0
            );
            ElementBounds previewContainerBounds = previewClipBounds
                .ForkContainingChild(0.0, 0.0, 0.0, 0.0)
                .WithFixedPadding(0);
            ElementBounds previewScrollbarBounds = previewInsetBounds
                .RightCopy()
                .WithFixedWidth(20.0)
                .WithFixedOffset(5.0, 0.0);

            double optionsY = row2Y + 420.0;

            ElementBounds themeLabelBounds = ElementBounds.Fixed(0, optionsY + 8.0, 55, 20);
            ElementBounds themeDropBounds = ElementBounds.Fixed(55, optionsY, 180, 30);
            ElementBounds holderLabelBounds = ElementBounds.Fixed(250, optionsY + 8.0, 70, 20);
            ElementBounds holderDropBounds = ElementBounds.Fixed(320, optionsY, 180, 30);

            double switchesY = optionsY + 46.0;
            double waypointY = optionsY + 92.0;
            double coordsY = optionsY + 138.0;
            double nameY = optionsY + 184.0;
            double iconY = optionsY + 230.0;
            double pinY = optionsY;

            ElementBounds buttonBounds = ElementBounds.Fixed(insetWidth - 120.0, pinY, 120.0, 40.0);

            ElementBounds previewSwitchBounds = ElementBounds.Fixed(320, switchesY, 40.0, 40.0);
            ElementBounds previewTextBounds = ElementBounds.Fixed(364, switchesY + 8.0, 300.0, 24.0);

            ElementBounds anonymousSwitchBounds = ElementBounds.Fixed(0, switchesY, 40.0, 40.0);
            ElementBounds anonymousTextBounds = ElementBounds.Fixed(44, switchesY + 8.0, 260.0, 24.0);

            ElementBounds waypointSwitchBounds = ElementBounds.Fixed(0, waypointY, 40.0, 40.0);
            ElementBounds waypointTextBounds = ElementBounds.Fixed(44, waypointY + 8.0, 280.0, 24.0);
            ElementBounds waypointXLabel = ElementBounds.Fixed(0, coordsY + 8.0, 55, 20);
            ElementBounds waypointXInput = ElementBounds.Fixed(55, coordsY, 250, 30);
            ElementBounds waypointZLabel = ElementBounds.Fixed(320, coordsY + 8.0, 70, 20);
            ElementBounds waypointZInput = ElementBounds.Fixed(390, coordsY, 265, 30);
            ElementBounds waypointNameLabel = ElementBounds.Fixed(0, nameY + 8.0, 55, 20);
            ElementBounds waypointNameInput = ElementBounds.Fixed(55, nameY, 600, 30);
            ElementBounds waypointIconLabel = ElementBounds.Fixed(0, iconY + 8.0, 55, 20);
            ElementBounds waypointIconDrop = ElementBounds.Fixed(55, iconY, 250, 30);
            ElementBounds waypointColorLabel = ElementBounds.Fixed(320, iconY + 8.0, 70, 20);
            ElementBounds waypointColorDrop = ElementBounds.Fixed(390, iconY, 265, 30);

            ElementBounds bgBounds = ElementBounds
                .Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding)
                .WithSizing((ElementSizing)2)
                .WithChildren(
                    inputInsetBounds,
                    inputScrollbarBounds,
                    previewLabelBounds,
                    previewInsetBounds,
                    previewScrollbarBounds,
                    buttonBounds,
                    boldBtn,
                    italicBtn,
                    largeBtn,
                    smallBtn,
                    linkBtn,
                    handbookBtn,
                    commandBtn,
                    iconBtn,
                    itemBtn,
                    helpBtn,
                    inkBtn,
                    redBtn,
                    blueBtn,
                    greenBtn,
                    goldBtn,
                    themeLabelBounds,
                    themeDropBounds,
                    holderLabelBounds,
                    holderDropBounds,
                    anonymousSwitchBounds,
                    anonymousTextBounds,
                    previewSwitchBounds,
                    previewTextBounds,
                    waypointSwitchBounds,
                    waypointTextBounds
                );
            if (this.attachWaypoint)
            {
                bgBounds.WithChildren(
                    waypointXLabel,
                    waypointXInput,
                    waypointZLabel,
                    waypointZInput,
                    waypointNameLabel,
                    waypointNameInput,
                    waypointIconLabel,
                    waypointIconDrop,
                    waypointColorLabel,
                    waypointColorDrop
                );
            }

            CairoFont inkFont = CairoFont
                .WhiteDetailText()
                .WithColor(theme.InkColor)
                .WithFont(this.noticeBoardPacket.BoardProperties.BoardFont)
                .WithFontSize(18f);

            GuiComposer dialogComposer = capi.Gui.CreateCompo("addNoticeGui", dialogBounds);

            dialogComposer.AddShadedDialogBG(bgBounds, true, 5.0, 0.75f);
            dialogComposer.AddDialogTitleBar(
                Lang.Get("noticeboard:add-notice-window-title"),
                OnTitleBarClose
            );

            dialogComposer.AddSmallButton(
                "B",
                () => InsertFormatTag("<strong>", "</strong>", dialogComposer),
                boldBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "I",
                () => InsertFormatTag("<i>", "</i>", dialogComposer),
                italicBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Big",
                () => InsertFormatTag("<font size=\"24\">", "</font>", dialogComposer),
                largeBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Sml",
                () => InsertFormatTag("<font size=\"12\">", "</font>", dialogComposer),
                smallBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Link",
                () => InsertFormatTag("<a href=\"https://example.com\">", "</a>", dialogComposer),
                linkBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Hdbk",
                () => InsertFormatTag("<a href='handbook://item-flint'>", "</a>", dialogComposer),
                handbookBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Cmd",
                () => InsertFormatTag("<a href='command:///kill'>", "</a>", dialogComposer),
                commandBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Icon",
                () => InsertFormatTag("<icon name=dice>", "</icon>", dialogComposer),
                iconBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Item",
                () =>
                    InsertFormatTag(
                        "<itemstack floattype=\"left\" type=\"block\" code=\"packeddirt\" rsize=\"1\" offx=\"0\" offy=\"0\">",
                        "</itemstack>",
                        dialogComposer
                    ),
                itemBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "VTML Help",
                () =>
                {
                    capi.Gui.OpenLink("https://wiki.vintagestory.at/VTML");
                    return true;
                },
                helpBtn,
                EnumButtonStyle.Normal
            );

            dialogComposer.AddSmallButton(
                "Ink",
                () => InsertFormatTag("<font color=\"#332211\">", "</font>", dialogComposer),
                inkBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Red",
                () => InsertFormatTag("<font color=\"#b22222\">", "</font>", dialogComposer),
                redBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Blue",
                () => InsertFormatTag("<font color=\"#2a52be\">", "</font>", dialogComposer),
                blueBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Green",
                () => InsertFormatTag("<font color=\"#228b22\">", "</font>", dialogComposer),
                greenBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Gold",
                () => InsertFormatTag("<font color=\"#ffd700\">", "</font>", dialogComposer),
                goldBtn,
                EnumButtonStyle.Normal
            );

            dialogComposer.AddSmallButton(
                Lang.Get("noticeboard:add-notice-window-pin-button"),
                () => OnSendButtonClicked(dialogComposer),
                buttonBounds,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddStaticText(
                Lang.Get("noticeboard:add-notice-window-preview-switch"),
                CairoFont.WhiteDetailText(),
                previewTextBounds
            );

            dialogComposer.AddSwitch(
                (state) =>
                {
                    this.enablePreview = state;
                    RefreshInputGui();
                },
                previewSwitchBounds,
                "previewSwitch"
            );
            dialogComposer.GetSwitch("previewSwitch").On = this.enablePreview;

            dialogComposer.AddStaticText(
                Lang.Get("noticeboard:add-notice-window-anonymous-switch"),
                CairoFont.WhiteDetailText(),
                anonymousTextBounds
            );

            dialogComposer.AddSwitch(
                (state) =>
                {
                    this.isAnonymous = state ? 1 : 0;
                    RefreshInputGui();
                },
                anonymousSwitchBounds,
                "anonymousSwitch"
            );

            dialogComposer.GetSwitch("anonymousSwitch").On = this.isAnonymous == 1;

            dialogComposer.AddStaticText(
                Lang.Get("noticeboard:add-notice-window-waypoint-switch"),
                CairoFont.WhiteDetailText(),
                waypointTextBounds
            );
            dialogComposer.AddSwitch(
                (state) =>
                {
                    this.attachWaypoint = state;
                    if (state && this.wpX == 0 && this.wpZ == 0)
                    {
                        EntityPos spawn = capi.World.DefaultSpawnPosition;
                        EntityPos pos = capi.World.Player.Entity.Pos;
                        this.wpX = (int)PositionHelper.WorldToHud(pos.X, spawn.X);
                        this.wpZ = (int)PositionHelper.WorldToHud(pos.Z, spawn.Z);
                    }
                    if (state && string.IsNullOrWhiteSpace(this.wpTitle))
                        this.wpTitle = this.noticeBoardPacket.BoardProperties.BoardName ?? "Notice Board";
                    if (state && string.IsNullOrWhiteSpace(this.wpIcon))
                        this.wpIcon = WaypointPin.DefaultIcon;
                    if (state && string.IsNullOrWhiteSpace(this.wpColor))
                        this.wpColor = WaypointPin.DefaultColor;
                    RefreshInputGui();
                },
                waypointSwitchBounds,
                "waypointSwitch"
            );
            dialogComposer.GetSwitch("waypointSwitch").On = this.attachWaypoint;

            if (this.attachWaypoint)
            {
                dialogComposer.AddStaticText(
                    Lang.Get("noticeboard:add-notice-window-waypoint-x"),
                    CairoFont.WhiteSmallText(),
                    waypointXLabel
                );
                dialogComposer.AddNumberInput(waypointXInput, _ => { }, CairoFont.WhiteSmallText(), "waypointX");
                dialogComposer.GetNumberInput("waypointX").SetValue(this.wpX.ToString("0.##", CultureInfo.InvariantCulture));
                dialogComposer.AddStaticText(
                    Lang.Get("noticeboard:add-notice-window-waypoint-z"),
                    CairoFont.WhiteSmallText(),
                    waypointZLabel
                );
                dialogComposer.AddNumberInput(waypointZInput, _ => { }, CairoFont.WhiteSmallText(), "waypointZ");
                dialogComposer.GetNumberInput("waypointZ").SetValue(this.wpZ.ToString("0.##", CultureInfo.InvariantCulture));

                dialogComposer.AddStaticText(
                    Lang.Get("noticeboard:add-notice-window-waypoint-name"),
                    CairoFont.WhiteSmallText(),
                    waypointNameLabel
                );
                dialogComposer.AddTextInput(
                    waypointNameInput,
                    text => { this.wpTitle = text ?? ""; },
                    CairoFont.WhiteSmallText(),
                    "waypointTitle"
                );
                dialogComposer.GetTextInput("waypointTitle").SetValue(this.wpTitle ?? "");

                string[] iconCodes = WaypointPin.Icons;
                string[] iconLabels = new string[iconCodes.Length];
                int selectedIcon = 0;
                for (int i = 0; i < iconCodes.Length; i++)
                {
                    iconLabels[i] = WaypointPin.Label(iconCodes[i]);
                    if (iconCodes[i] == this.wpIcon) selectedIcon = i;
                }
                dialogComposer.AddStaticText(
                    Lang.Get("noticeboard:add-notice-window-waypoint-icon"),
                    CairoFont.WhiteSmallText(),
                    waypointIconLabel
                );
                dialogComposer.AddDropDown(
                    iconCodes,
                    iconLabels,
                    selectedIcon,
                    (code, selected) => { this.wpIcon = WaypointPin.SanitizeIcon(code); },
                    waypointIconDrop,
                    "waypointIcon"
                );

                string[] colorCodes = WaypointPin.Colors;
                string[] colorLabels = new string[colorCodes.Length];
                int selectedColor = 0;
                for (int i = 0; i < colorCodes.Length; i++)
                {
                    colorLabels[i] = WaypointPin.Label(colorCodes[i]);
                    if (colorCodes[i] == this.wpColor) selectedColor = i;
                }
                dialogComposer.AddStaticText(
                    Lang.Get("noticeboard:add-notice-window-waypoint-color"),
                    CairoFont.WhiteSmallText(),
                    waypointColorLabel
                );
                dialogComposer.AddDropDown(
                    colorCodes,
                    colorLabels,
                    selectedColor,
                    (code, selected) => { this.wpColor = WaypointPin.SanitizeColor(code); },
                    waypointColorDrop,
                    "waypointColor"
                );
            }

            string[] themeNames = ThemeManager.GetThemeNames();
            string[] themeCodes = new string[themeNames.Length + 1];
            string[] themeLabels = new string[themeNames.Length + 1];
            themeCodes[0] = "";
            themeLabels[0] = Lang.Get("noticeboard:add-notice-window-paper-theme-board");
            for (int i = 0; i < themeNames.Length; i++)
            {
                themeCodes[i + 1] = themeNames[i];
                themeLabels[i + 1] = themeNames[i];
            }
            int selectedTheme = 0;
            string currentTheme = this.paperTheme ?? "";
            for (int i = 0; i < themeCodes.Length; i++)
            {
                if (themeCodes[i] == currentTheme)
                {
                    selectedTheme = i;
                    break;
                }
            }

            dialogComposer.AddStaticText(
                Lang.Get("noticeboard:add-notice-window-paper-theme"),
                CairoFont.WhiteDetailText(),
                themeLabelBounds
            );
            dialogComposer.AddDropDown(
                themeCodes,
                themeLabels,
                selectedTheme,
                (code, selected) =>
                {
                    string next = code ?? "";
                    if (next == (this.paperTheme ?? ""))
                        return;
                    this.paperTheme = next;
                    RefreshInputGui();
                },
                themeDropBounds,
                "paperThemeDropdown"
            );

            string[] holderCodes = new string[MessageHolder.All.Length];
            string[] holderNames = new string[MessageHolder.All.Length];
            for (int i = 0; i < MessageHolder.All.Length; i++)
            {
                holderCodes[i] = MessageHolder.All[i].Id.ToString();
                holderNames[i] = Lang.Get(MessageHolder.All[i].LangKey);
            }

            dialogComposer.AddStaticText(
                Lang.Get("noticeboard:add-notice-window-holder"),
                CairoFont.WhiteDetailText(),
                holderLabelBounds
            );
            dialogComposer.AddDropDown(
                holderCodes,
                holderNames,
                MessageHolder.Clamp(this.holder),
                (code, selected) =>
                {
                    if (int.TryParse(code, out int parsed))
                        this.holder = MessageHolder.Clamp(parsed);
                },
                holderDropBounds,
                "holderDropdown"
            );

            dialogComposer.AddInset(inputInsetBounds, insetDepth, 0.85f);
            dialogComposer.BeginClip(inputClipBounds);
            dialogComposer.AddTextArea(
                inputContainerBounds,
                (text) => OnTextChanged(text, dialogComposer),
                CairoFont.WhiteDetailText(),
                "messageInput"
            );
            dialogComposer.EndClip();
            dialogComposer.AddVerticalScrollbar(
                (value) => OnInputScroll(value, dialogComposer),
                inputScrollbarBounds,
                "inputScroll"
            );

            if (this.enablePreview)
            {
                dialogComposer.AddStaticText(
                    "Preview:",
                    CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold),
                    previewLabelBounds
                );

                dialogComposer.AddInset(previewInsetBounds, insetDepth, 0.85f);

                ElementBounds paperBounds = previewInsetBounds.ForkContainingChild(
                    2.0,
                    2.0,
                    2.0,
                    2.0
                );

                dialogComposer.BeginClip(previewClipBounds);
                dialogComposer.AddRichtext("", inkFont, previewContainerBounds, "previewInput");
                dialogComposer.EndClip();
                dialogComposer.AddVerticalScrollbar(
                    (value) => OnPreviewScroll(value, dialogComposer),
                    previewScrollbarBounds,
                    "previewScroll"
                );

                dialogComposer.AddInteractiveElement(
                    new ProceduralPaperGuiElement(this.capi, this.messageId, paperBounds, theme, jaggedEdges: false)
                );
            }

            base.SingleComposer = dialogComposer.Compose();

            _selStartField = typeof(GuiElementEditableTextBase).GetField(
                "selectedTextStart",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            var textArea = dialogComposer.GetTextArea("messageInput");
            textArea.OnCursorMoved = (x, y) =>
            {
                _cachedCaretPos = textArea.CaretPosWithoutLineBreaks;
                _cachedSelStart = (int?)_selStartField?.GetValue(textArea);
            };

            isHistoryAction = true;
            string initialText = this.message ?? "";
            textArea.SetValue(initialText, true);
            OnTextChanged(initialText, dialogComposer);
            isHistoryAction = false;
        }

        public void RefreshInputGui()
        {
            if (base.SingleComposer != null)
            {
                this.message = base.SingleComposer.GetTextArea("messageInput").GetText();
                ReadWaypointFields(base.SingleComposer);
            }

            int savedCaret = _cachedCaretPos;

            base.SingleComposer?.Dispose();
            this.OnGuiOpened();

            if (base.SingleComposer != null)
            {
                var textArea = base.SingleComposer.GetTextArea("messageInput");
                textArea?.SetCaretPos(savedCaret);
            }
        }

        private bool InsertFormatTag(string openTag, string closeTag, GuiComposer dialogComposer)
        {
            var textArea = dialogComposer.GetTextArea("messageInput");
            string currentText = textArea.GetText();

            string newText;
            int newCaretPos;

            bool hasSelection =
                _cachedSelStart.HasValue && _cachedSelStart.Value != _cachedCaretPos;

            if (hasSelection)
            {
                int selStart = Math.Clamp(
                    Math.Min(_cachedSelStart.Value, _cachedCaretPos),
                    0,
                    currentText.Length
                );
                int selEnd = Math.Clamp(
                    Math.Max(_cachedSelStart.Value, _cachedCaretPos),
                    0,
                    currentText.Length
                );

                string before = currentText[..selStart];
                string selected = currentText[selStart..selEnd];
                string after = currentText[selEnd..];

                newText = before + openTag + selected + closeTag + after;
                newCaretPos = selStart + openTag.Length + selected.Length + closeTag.Length;
            }
            else
            {
                int caretPos = Math.Clamp(_cachedCaretPos, 0, currentText.Length);
                string before = currentText.Substring(0, caretPos);
                string after = currentText.Substring(caretPos);

                newText = before + openTag + closeTag + after;
                newCaretPos = caretPos + openTag.Length;
            }

            textArea.SetValue(newText, false);
            textArea.SetCaretPos(newCaretPos);

            OnTextChanged(newText, dialogComposer);

            return true;
        }

        private void OnTextChanged(string text, GuiComposer dialogComposer)
        {
            if (text.Length > 10000)
            {
                text = text.Substring(0, 10000);
                dialogComposer.GetTextArea("messageInput").SetValue(text, true);
            }

            this.message = text;

            if (!isHistoryAction)
            {
                if (historyIndex < textHistory.Count - 1)
                {
                    textHistory.RemoveRange(
                        historyIndex + 1,
                        textHistory.Count - (historyIndex + 1)
                    );
                }

                if (textHistory.Count == 0 || textHistory[textHistory.Count - 1] != text)
                {
                    textHistory.Add(text);
                    historyIndex++;

                    if (textHistory.Count > 50)
                    {
                        textHistory.RemoveAt(0);
                        historyIndex--;
                    }
                }
            }

            Action<LinkTextComponent> onLinkClicked = (link) =>
            {
                var href = link.Href;

                var scheme = href.Contains("://")
                    ? href.Split(new[] { "://" }, 2, StringSplitOptions.None)[0]
                    : null;
                if (scheme != null && this.capi.LinkProtocols.ContainsKey(scheme))
                    this.capi.LinkProtocols[scheme].Invoke(link);
                else
                    this.capi.Gui.OpenLink(href);
            };

            ParchmentPalette theme = ThemeManager.Resolve(
                this.paperTheme,
                this.noticeBoardPacket.BoardProperties.BoardTheme
            );
            CairoFont inkFont = CairoFont
                .WhiteDetailText()
                .WithColor(theme.InkColor)
                .WithFont(this.noticeBoardPacket.BoardProperties.BoardFont)
                .WithFontSize(18f);

            var previewRichtext = dialogComposer.GetRichtext("previewInput");

            if (previewRichtext != null)
            {
                RichTextComponentBase[] bodyVtml = VtmlUtil.Richtextify(
                    capi,
                    text,
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
                previewRichtext.Components = bodyVtml;
                previewRichtext.CalcHeightAndPositions();
            }

            UpdateScrollbars(dialogComposer);
        }

        private void OnInputScroll(float value, GuiComposer dialogComposer)
        {
            var textArea = dialogComposer.GetTextArea("messageInput");
            textArea.Bounds.fixedY = -value;
            textArea.Bounds.CalcWorldBounds();
        }

        private void OnPreviewScroll(float value, GuiComposer dialogComposer)
        {
            var previewRichtext = dialogComposer.GetRichtext("previewInput");
            if (previewRichtext == null)
                return;
            previewRichtext.Bounds.fixedY = -value;
            previewRichtext.Bounds.CalcWorldBounds();
        }

        private void UpdateScrollbars(GuiComposer dialogComposer)
        {
            var textArea = dialogComposer.GetTextArea("messageInput");
            var inputScroll = dialogComposer.GetScrollbar("inputScroll");

            string text = textArea.GetText();

            float visibleInputHeight = this.enablePreview ? 164.0f : 380.0f;

            double textHeight = Math.Max(
                (text.Split('\n').Length + (text.Length / 35)) * 24.0,
                visibleInputHeight
            );

            textArea.Bounds.fixedHeight = textHeight;
            textArea.Bounds.CalcWorldBounds();

            inputScroll.SetHeights(visibleInputHeight, (float)textHeight);

            if (this.enablePreview)
            {
                var previewRichtext = dialogComposer.GetRichtext("previewInput");
                var previewScroll = dialogComposer.GetScrollbar("previewScroll");

                if (previewRichtext == null)
                    return;

                var recomposeMethod = typeof(GuiElementRichtext).GetMethod(
                    "RecomposeText",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                );
                recomposeMethod?.Invoke(previewRichtext, null);

                double previewHeight = Math.Max(previewRichtext.Bounds.fixedHeight, 164.0);
                previewRichtext.Bounds.fixedHeight = previewHeight;
                previewRichtext.Bounds.CalcWorldBounds();

                previewScroll?.SetHeights(164.0f, (float)previewHeight);
            }
        }

        public override void OnKeyDown(KeyEvent args)
        {
            if (args.CtrlPressed)
            {
                if (args.KeyCode == (int)GlKeys.Z)
                {
                    PerformUndo();
                    args.Handled = true;
                    return;
                }
                if (args.KeyCode == (int)GlKeys.Y)
                {
                    PerformRedo();
                    args.Handled = true;
                    return;
                }
                if (args.KeyCode == (int)GlKeys.X)
                {
                    bool hasSelection =
                        _cachedSelStart.HasValue && _cachedSelStart.Value != _cachedCaretPos;
                    if (!hasSelection && SingleComposer != null)
                    {
                        DeleteCurrentLine();
                        args.Handled = true;
                        return;
                    }
                }
            }
            base.OnKeyDown(args);
        }

        private void DeleteCurrentLine()
        {
            var textArea = SingleComposer.GetTextArea("messageInput");
            string currentText = textArea.GetText();

            if (string.IsNullOrEmpty(currentText))
                return;

            int caretPos = Math.Clamp(_cachedCaretPos, 0, currentText.Length);

            int lineStart = currentText.LastIndexOf('\n', Math.Max(0, caretPos - 1));
            lineStart = (lineStart == -1) ? 0 : lineStart + 1;

            int lineEnd = currentText.IndexOf('\n', caretPos);
            if (lineEnd == -1)
                lineEnd = currentText.Length;

            int removeStart = lineStart;
            int removeLength = lineEnd - lineStart;

            if (lineEnd < currentText.Length && currentText[lineEnd] == '\n')
            {
                removeLength += 1;
            }
            else if (lineStart > 0 && currentText[lineStart - 1] == '\n')
            {
                removeStart -= 1;
                removeLength += 1;
            }

            string newText = currentText.Remove(removeStart, removeLength);
            textArea.SetValue(newText, false);

            int newCaretPos = Math.Clamp(lineStart, 0, newText.Length);
            textArea.SetCaretPos(newCaretPos);
            _cachedCaretPos = newCaretPos;

            OnTextChanged(newText, SingleComposer);
        }

        private void PerformUndo()
        {
            if (historyIndex > 0)
            {
                historyIndex--;
                ApplyHistoryState();
            }
        }

        private void PerformRedo()
        {
            if (historyIndex < textHistory.Count - 1)
            {
                historyIndex++;
                ApplyHistoryState();
            }
        }

        private void ApplyHistoryState()
        {
            if (historyIndex >= 0 && historyIndex < textHistory.Count && SingleComposer != null)
            {
                isHistoryAction = true;
                string stateText = textHistory[historyIndex];
                this.message = stateText;

                var textArea = SingleComposer.GetTextArea("messageInput");
                textArea.SetValue(stateText, true);
                textArea.SetCaretPos(stateText.Length);

                OnTextChanged(stateText, SingleComposer);
                isHistoryAction = false;
            }
        }

        private void ReadWaypointFields(GuiComposer dialogComposer)
        {
            if (dialogComposer == null)
                return;

            var xInput = dialogComposer.GetNumberInput("waypointX");
            var zInput = dialogComposer.GetNumberInput("waypointZ");
            if (xInput != null && float.TryParse(xInput.GetText(), CultureInfo.InvariantCulture, out float parsedX))
                this.wpX = parsedX;
            if (zInput != null && float.TryParse(zInput.GetText(), CultureInfo.InvariantCulture, out float parsedZ))
                this.wpZ = parsedZ;
            var titleInput = dialogComposer.GetTextInput("waypointTitle");
            if (titleInput != null)
                this.wpTitle = titleInput.GetText() ?? "";
        }

        private bool OnSendButtonClicked(GuiComposer dialogComposer)
        {
            string currentText = dialogComposer.GetTextArea("messageInput").GetText();

            if (string.IsNullOrWhiteSpace(currentText))
            {
                capi.TriggerIngameError(this, "empty_message", Lang.Get("noticeboard:messages-error-empty-notice"));
                return true;
            }

            ReadWaypointFields(dialogComposer);

            EntityPos spawn = capi.World.DefaultSpawnPosition;
            float worldX = this.attachWaypoint ? PositionHelper.HudToWorld(this.wpX, spawn.X) : this.wpX;
            float worldZ = this.attachWaypoint ? PositionHelper.HudToWorld(this.wpZ, spawn.Z) : this.wpZ;

            var networkChannel = NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard");

            if (this.mode == "edit")
            {
                networkChannel.SendPacket(
                    new PlayerEditMessage
                    {
                        Id = this.messageId,
                        Message = currentText,
                        BoardId = noticeBoardPacket.BoardProperties.BoardId,
                        TotalHours = capi.World.Calendar.TotalHours,
                        IsAnonymous = isAnonymous,
                        Holder = holder,
                        PaperTheme = paperTheme,
                        HasWaypoint = this.attachWaypoint,
                        WaypointX = worldX,
                        WaypointZ = worldZ,
                        WaypointTitle = this.wpTitle,
                        WaypointIcon = this.wpIcon,
                        WaypointColor = this.wpColor,
                    }
                );

                var props = noticeBoardPacket.BoardProperties;
                bool manual = props.EnableManualPin != 0 && props.EnableLegacyBoard == 0;
                if (manual)
                {
                    int units = PaperSize.MeasureHeightUnits(
                        capi, currentText, props.BoardFont, props.BoardFontSize, hasAuthor: isAnonymous == 0);
                    BlockPos origin = PositionHelper.FromString(props.Pos);
                    NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(origin);
                    bool isWall = be?.Block?.Variant?["attachment"] == "wall";
                    float rotateYDeg = NoticeBoardBlockEntity.GetRotateYDeg(be?.Block);
                    this.TryClose();
                    this.parentContext.TryClose();
                    PaperPinController.Instance.BeginReposition(
                        props.BoardId,
                        origin,
                        isWall,
                        rotateYDeg,
                        units,
                        currentText,
                        isAnonymous,
                        holder,
                        paperTheme,
                        props.BoardFont,
                        this.messageId
                    );
                    return true;
                }
            }
            else
            {
                var props = noticeBoardPacket.BoardProperties;
                bool manual = props.EnableManualPin != 0 && props.EnableLegacyBoard == 0;
                if (manual)
                {
                    int units = PaperSize.MeasureHeightUnits(
                        capi, currentText, props.BoardFont, props.BoardFontSize, hasAuthor: isAnonymous == 0);
                    BlockPos origin = PositionHelper.FromString(props.Pos);
                    NoticeBoardBlockEntity be = capi.GetNoticeBoardEntity(origin);
                    bool isWall = be?.Block?.Variant?["attachment"] == "wall";
                    float rotateYDeg = NoticeBoardBlockEntity.GetRotateYDeg(be?.Block);
                    this.TryClose();
                    this.parentContext.TryClose();
                    PaperPinController.Instance.BeginCompose(
                        props.BoardId,
                        origin,
                        isWall,
                        rotateYDeg,
                        units,
                        currentText,
                        isAnonymous,
                        holder,
                        paperTheme,
                        props.BoardFont,
                        this.attachWaypoint,
                        worldX,
                        worldZ,
                        this.wpTitle,
                        this.wpIcon,
                        this.wpColor
                    );
                    return true;
                }

                networkChannel.SendPacket(
                    new PlayerSendMessage
                    {
                        Message = currentText,
                        BoardId = props.BoardId,
                        PlayerId = capi.World.Player.PlayerUID,
                        TotalHours = capi.World.Calendar.TotalHours,
                        IsAnonymous = isAnonymous,
                        Holder = holder,
                        PaperTheme = paperTheme,
                        PaperSeed = MessageVisualData.NewPaperSeed(),
                        HasWaypoint = this.attachWaypoint,
                        WaypointX = worldX,
                        WaypointZ = worldZ,
                        WaypointTitle = this.wpTitle,
                        WaypointIcon = this.wpIcon,
                        WaypointColor = this.wpColor,
                    }
                );
            }
            this.parentContext.GetMessages();
            this.TryClose();
            return true;
        }

        private void OnTitleBarClose() => TryClose();

        public override double DrawOrder => 0.3;

        public override double InputOrder => 0.1;
        public override string ToggleKeyCombinationCode => null;
    }
}
