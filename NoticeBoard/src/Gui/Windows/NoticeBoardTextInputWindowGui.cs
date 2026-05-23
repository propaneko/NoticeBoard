using System;
using System.Reflection;
using Cairo;
using NoticeBoard.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace NoticeBoard.src.Gui.Windows
{
    public class NoticeBoardTextInputWindowGui : GuiDialog
    {
        private string mode;
        private int messageId;
        private string message;
        private bool enablePreview;

        private NoticeBoardMainWindowGui parentContext;
        private ResponseAllMessages noticeBoardPacket;

        private int _cachedCaretPos = 0;
        private int? _cachedSelStart = null;
        private FieldInfo _selStartField;

        public NoticeBoardTextInputWindowGui(
            ICoreClientAPI capi,
            NoticeBoardMainWindowGui context,
            ResponseAllMessages noticeBoardPacket,
            string mode,
            int messageId = -1,
            string message = ""
        )
            : base(capi)
        {
            this.noticeBoardPacket = noticeBoardPacket;
            this.mode = mode;
            this.parentContext = context;
            this.enablePreview = false;

            if (mode == "edit")
            {
                this.message = message;
                this.messageId = messageId;
            }

            //this.Compose();
        }

        public override void OnGuiOpened()
        {
            Compose();
        }

        private void Compose()
        {
            int insetWidth = 550;
            int insetHeight = 500;
            int insetDepth = 3;

            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterBottom)
                .WithFixedOffset(0.0, -120.0);

            double row1Y = GuiStyle.TitleBarHeight;
            ElementBounds boldBtn = ElementBounds.Fixed(0.0, row1Y, 35.0, 30.0);
            ElementBounds italicBtn = ElementBounds.Fixed(40.0, row1Y, 35.0, 30.0);
            ElementBounds underBtn = ElementBounds.Fixed(80.0, row1Y, 35.0, 30.0);
            ElementBounds strikeBtn = ElementBounds.Fixed(120.0, row1Y, 45.0, 30.0);
            ElementBounds largeBtn = ElementBounds.Fixed(170.0, row1Y, 50.0, 30.0);
            ElementBounds smallBtn = ElementBounds.Fixed(225.0, row1Y, 50.0, 30.0);
            ElementBounds linkBtn = ElementBounds.Fixed(280.0, row1Y, 60.0, 30.0);
            ElementBounds iconBtn = ElementBounds.Fixed(345.0, row1Y, 60, 30.0);
            ElementBounds itemBtn = ElementBounds.Fixed(405.0, row1Y, 60, 30.0);

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
                this.enablePreview ? 170.0 : 340.0
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

            ElementBounds buttonBounds = ElementBounds.Fixed(
                insetWidth - 120.0,
                row2Y + 420.0,
                120.0,
                40.0
            );

            ElementBounds previewSwitchBounds = ElementBounds.Fixed(
                insetWidth - 140.0,
                row2Y + 445.0,
                120.0,
                40.0
            );

            ElementBounds previewTextBounds = ElementBounds.Fixed(
               insetWidth - 250.0,
               row2Y + 452.5,
               120.0,
               40.0
           );

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
                    underBtn,
                    strikeBtn,
                    largeBtn,
                    smallBtn,
                    linkBtn,
                    iconBtn,
                    itemBtn,
                    inkBtn,
                    redBtn,
                    blueBtn,
                    greenBtn,
                    goldBtn
                );

            double[] inkColor = new double[] { 0.96, 0.94, 0.88, 1.0 };
            CairoFont inkFont = CairoFont
                .WhiteDetailText()
                .WithColor(inkColor)
                .WithFont("MedievalSharp")
                .WithFontSize(18f);

            Action<LinkTextComponent> onLinkClicked = (link) =>
            {
                this.capi.Gui.OpenLink(link.Href);
            };

            RichTextComponentBase[] bodyVtml = VtmlUtil.Richtextify(
                this.capi,
                "",
                inkFont,
                onLinkClicked
            );

            GuiComposer dialogComposer = capi.Gui.CreateCompo("addNoticeGui", dialogBounds);

            dialogComposer.AddShadedDialogBG(bgBounds, true, 5.0, 0.75f);
            dialogComposer.AddDialogTitleBar(
                Lang.Get("noticeboard:add-notice-window-title"),
                OnTitleBarClose
            );
            // Formatting Buttons
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
                "U",
                () => InsertFormatTag("<u>", "</u>", dialogComposer),
                underBtn,
                EnumButtonStyle.Normal
            );
            dialogComposer.AddSmallButton(
                "Del",
                () => InsertFormatTag("<del>", "</del>", dialogComposer),
                strikeBtn,
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
                        "",
                        dialogComposer
                    ),
                itemBtn,
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

            dialogComposer.AddStaticText("Enable Preview", CairoFont.WhiteDetailText(), previewTextBounds);

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
            // Text Input Area + Scrollbar
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
                (value) =>
                {
                    OnInputScroll(value, dialogComposer);
                },
                inputScrollbarBounds,
                "inputScroll"
            );
            // Live Preview Area + Scrollbar
            if (this.enablePreview)
            {
                dialogComposer.AddStaticText(
                    "Live Preview:",
                    CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold),
                    previewLabelBounds
                );
                dialogComposer.AddInset(previewInsetBounds, insetDepth, 0.85f);
                dialogComposer.BeginClip(previewClipBounds);
                dialogComposer.AddRichtext("", inkFont, previewContainerBounds, "previewInput");
                dialogComposer.EndClip();
                dialogComposer.AddVerticalScrollbar(
                    (value) =>
                    {
                        OnPreviewScroll(value, dialogComposer);
                    },
                    previewScrollbarBounds,
                    "previewScroll"
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

            // Initialize content
            if (this.mode == "edit")
            {
                textArea.SetValue(this.message, true);
            }

            // Trigger initial VTML parse and scrollbar setup
            OnTextChanged(this.mode == "edit" ? this.message : "", dialogComposer);
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

                string before = currentText.Substring(0, selStart);
                string selected = currentText.Substring(selStart, selEnd - selStart);
                string after = currentText.Substring(selEnd);

                newText = before + openTag + selected + closeTag + after;
                newCaretPos = selStart + openTag.Length + selected.Length + closeTag.Length;
            }
            else
            {
                int caretPos = Math.Clamp(_cachedCaretPos, 0, currentText.Length);

                string before = currentText.Substring(0, caretPos);
                string after = currentText.Substring(caretPos);

                newText = before + openTag + closeTag + after;
                newCaretPos = caretPos + openTag.Length + "text".Length;
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

            double[] inkColor = new double[] { 0.96, 0.94, 0.88, 1.0 };
            CairoFont inkFont = CairoFont
                .WhiteDetailText()
                .WithColor(inkColor)
                .WithFont("MedievalSharp")
                .WithFontSize(18f);

            var previewRichtext = dialogComposer.GetRichtext("previewInput");

            if (previewRichtext != null)
            {
                previewRichtext.Components = VtmlUtil.Richtextify(capi, text, inkFont);
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
            var previewRichtext = dialogComposer.GetRichtext("previewInput");
            var previewScroll = dialogComposer.GetScrollbar("previewScroll");

            string text = textArea.GetText();

            int lineCount = text.Split('\n').Length + (text.Length / 35);
            double textHeight = Math.Max(lineCount * 24.0, 164.0);

            textArea.Bounds.fixedHeight = textHeight;
            textArea.Bounds.CalcWorldBounds();
            inputScroll.SetHeights(164.0f, (float)textHeight);

            double[] inkColor = new double[] { 0.96, 0.94, 0.88, 1.0 };
            CairoFont inkFont = CairoFont
                .WhiteDetailText()
                .WithColor(inkColor)
                .WithFont("MedievalSharp")
                .WithFontSize(18f);
            if (previewRichtext == null)
                return;
            previewRichtext.Components = VtmlUtil.Richtextify(capi, text, inkFont);
            previewRichtext.CalcHeightAndPositions();

            var recomposeMethod = typeof(GuiElementRichtext).GetMethod("RecomposeText");
            if (recomposeMethod != null)
            {
                recomposeMethod.Invoke(previewRichtext, null);
            }

            double previewHeight = Math.Max(previewRichtext.Bounds.fixedHeight, 164.0);
            previewRichtext.Bounds.fixedHeight = previewHeight;
            previewRichtext.Bounds.CalcWorldBounds();

            previewScroll.SetHeights(164.0f, (float)previewHeight);
        }

        private bool OnSendButtonClicked(GuiComposer dialogComposer)
        {
            string currentText = dialogComposer.GetTextArea("messageInput").GetText();
            var networkChannel = NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard");

            if (this.mode == "edit")
            {
                networkChannel.SendPacket(
                    new PlayerEditMessage { Id = this.messageId, Message = currentText }
                );
            }
            else
            {
                networkChannel.SendPacket(
                    new PlayerSendMessage
                    {
                        Message = currentText,
                        BoardId = noticeBoardPacket.BoardProperties.BoardId,
                        PlayerId = capi.World.Player.PlayerUID,
                    }
                );
            }

            this.parentContext.GetMessages();
            this.TryClose();
            return true;
        }

        public void RefreshInputGui()
        {
            base.SingleComposer?.Dispose();
            this.OnGuiOpened();
        }

        private void OnTitleBarClose() => TryClose();

        public override string ToggleKeyCombinationCode => null;
    }
}
