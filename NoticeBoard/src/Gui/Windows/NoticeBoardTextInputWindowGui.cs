using Cairo;
using NoticeBoard.Packets;
using System;
using System.Collections.Generic;
using System.Reflection;
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

        private List<string> textHistory = new List<string>();
        private int historyIndex = -1;
        private bool isHistoryAction = false;

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

            textHistory.Add(this.message ?? "");
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

            ParchmentPalette theme = ThemeManager.GetCurrentTheme(this.noticeBoardPacket.BoardProperties.BoardTheme);

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
            ElementBounds inputClipBounds = inputInsetBounds.ForkContainingChild(3.0, 3.0, 3.0, 3.0);
            ElementBounds inputContainerBounds = inputClipBounds.ForkContainingChild(0.0, 0.0, 0.0, 0.0).WithFixedPadding(0);
            ElementBounds inputScrollbarBounds = inputInsetBounds.RightCopy().WithFixedWidth(20.0).WithFixedOffset(5.0, 0.0);

            ElementBounds previewLabelBounds = ElementBounds.Fixed(0.0, row2Y + 215.0, insetWidth, 25.0);
            ElementBounds previewInsetBounds = ElementBounds.Fixed(0.0, row2Y + 240.0, insetWidth - 25, 170.0);
            ElementBounds previewClipBounds = previewInsetBounds.ForkContainingChild(3.0, 3.0, 3.0, 3.0);
            ElementBounds previewContainerBounds = previewClipBounds.ForkContainingChild(0.0, 0.0, 0.0, 0.0).WithFixedPadding(0);
            ElementBounds previewScrollbarBounds = previewInsetBounds.RightCopy().WithFixedWidth(20.0).WithFixedOffset(5.0, 0.0);

            ElementBounds buttonBounds = ElementBounds.Fixed(insetWidth - 120.0, row2Y + 420.0, 120.0, 40.0);
            ElementBounds previewSwitchBounds = ElementBounds.Fixed(insetWidth - 140.0, row2Y + 445.0, 120.0, 40.0);
            ElementBounds previewTextBounds = ElementBounds.Fixed(insetWidth - 250.0, row2Y + 452.5, 120.0, 40.0);

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
                    boldBtn, italicBtn, largeBtn, smallBtn, linkBtn, handbookBtn, commandBtn, iconBtn, itemBtn, helpBtn,
                    inkBtn, redBtn, blueBtn, greenBtn, goldBtn
                );

            CairoFont inkFont = CairoFont.WhiteDetailText().WithColor(theme.InkColor).WithFont(this.noticeBoardPacket.BoardProperties.BoardFont).WithFontSize(18f);

            GuiComposer dialogComposer = capi.Gui.CreateCompo("addNoticeGui", dialogBounds);

            dialogComposer.AddShadedDialogBG(bgBounds, true, 5.0, 0.75f);
            dialogComposer.AddDialogTitleBar(Lang.Get("noticeboard:add-notice-window-title"), OnTitleBarClose);

            dialogComposer.AddSmallButton("B", () => InsertFormatTag("<strong>", "</strong>", dialogComposer), boldBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("I", () => InsertFormatTag("<i>", "</i>", dialogComposer), italicBtn, EnumButtonStyle.Normal);
            //dialogComposer.AddSmallButton("U", () => InsertFormatTag("<u>", "</u>", dialogComposer), underBtn, EnumButtonStyle.Normal);
            //dialogComposer.AddSmallButton("Del", () => InsertFormatTag("<del>", "</del>", dialogComposer), strikeBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Big", () => InsertFormatTag("<font size=\"24\">", "</font>", dialogComposer), largeBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Sml", () => InsertFormatTag("<font size=\"12\">", "</font>", dialogComposer), smallBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Link", () => InsertFormatTag("<a href=\"https://example.com\">", "</a>", dialogComposer), linkBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Hdbk", () => InsertFormatTag("<a href='handbook://item-flint'>", "</a>", dialogComposer), handbookBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Cmd", () => InsertFormatTag("<a href='command:///kill'>", "</a>", dialogComposer), commandBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Icon", () => InsertFormatTag("<icon name=dice>", "</icon>", dialogComposer), iconBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Item", () => InsertFormatTag("<itemstack floattype=\"left\" type=\"block\" code=\"packeddirt\" rsize=\"1\" offx=\"0\" offy=\"0\">", "", dialogComposer), itemBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("VTML Help", () => { capi.Gui.OpenLink("https://wiki.vintagestory.at/VTML"); return true; }, helpBtn, EnumButtonStyle.Normal);

            dialogComposer.AddSmallButton("Ink", () => InsertFormatTag("<font color=\"#332211\">", "</font>", dialogComposer), inkBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Red", () => InsertFormatTag("<font color=\"#b22222\">", "</font>", dialogComposer), redBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Blue", () => InsertFormatTag("<font color=\"#2a52be\">", "</font>", dialogComposer), blueBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Green", () => InsertFormatTag("<font color=\"#228b22\">", "</font>", dialogComposer), greenBtn, EnumButtonStyle.Normal);
            dialogComposer.AddSmallButton("Gold", () => InsertFormatTag("<font color=\"#ffd700\">", "</font>", dialogComposer), goldBtn, EnumButtonStyle.Normal);

            dialogComposer.AddSmallButton(Lang.Get("noticeboard:add-notice-window-pin-button"), () => OnSendButtonClicked(dialogComposer), buttonBounds, EnumButtonStyle.Normal);
            dialogComposer.AddStaticText(Lang.Get("noticeboard:add-notice-window-preview-switch"), CairoFont.WhiteDetailText(), previewTextBounds);

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
                dialogComposer.AddStaticText("Preview:", CairoFont.WhiteSmallText().WithWeight(FontWeight.Bold), previewLabelBounds);

                dialogComposer.AddInset(previewInsetBounds, insetDepth, 0.85f);

                ElementBounds paperBounds = previewInsetBounds.ForkContainingChild(2.0, 2.0, 2.0, 2.0);

                dialogComposer.BeginClip(previewClipBounds);
                dialogComposer.AddRichtext("", inkFont, previewContainerBounds, "previewInput");
                dialogComposer.EndClip();
                dialogComposer.AddVerticalScrollbar(
                    (value) => OnPreviewScroll(value, dialogComposer),
                    previewScrollbarBounds,
                    "previewScroll"
                );

                dialogComposer.AddInteractiveElement(new ProceduralPaperGuiElement(this.capi, 12345, paperBounds, theme, false));
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

            bool hasSelection = _cachedSelStart.HasValue && _cachedSelStart.Value != _cachedCaretPos;

            if (hasSelection)
            {
                int selStart = Math.Clamp(Math.Min(_cachedSelStart.Value, _cachedCaretPos), 0, currentText.Length);
                int selEnd = Math.Clamp(Math.Max(_cachedSelStart.Value, _cachedCaretPos), 0, currentText.Length);

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
                    textHistory.RemoveRange(historyIndex + 1, textHistory.Count - (historyIndex + 1));
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

                var scheme = href.Contains("://") ? href.Split(new[] { "://" }, 2, StringSplitOptions.None)[0] : null;
                if (scheme != null && this.capi.LinkProtocols.ContainsKey(scheme))
                    this.capi.LinkProtocols[scheme].Invoke(link);
                else
                    this.capi.Gui.OpenLink(href);
            };

            ParchmentPalette theme = ThemeManager.GetCurrentTheme(this.noticeBoardPacket.BoardProperties.BoardTheme);
            CairoFont inkFont = CairoFont.WhiteDetailText().WithColor(theme.InkColor).WithFont(this.noticeBoardPacket.BoardProperties.BoardFont).WithFontSize(18f);

            var previewRichtext = dialogComposer.GetRichtext("previewInput");

            if (previewRichtext != null)
            {
                RichTextComponentBase[] bodyVtml = VtmlUtil.Richtextify(capi, text, inkFont, onLinkClicked);

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
            if (previewRichtext == null) return;
            previewRichtext.Bounds.fixedY = -value;
            previewRichtext.Bounds.CalcWorldBounds();
        }

        private void UpdateScrollbars(GuiComposer dialogComposer)
        {
            var textArea = dialogComposer.GetTextArea("messageInput");
            var inputScroll = dialogComposer.GetScrollbar("inputScroll");

            string text = textArea.GetText();

            float visibleInputHeight = this.enablePreview ? 164.0f : 380.0f;

            double textHeight = Math.Max((text.Split('\n').Length + (text.Length / 35)) * 24.0, visibleInputHeight);

            textArea.Bounds.fixedHeight = textHeight;
            textArea.Bounds.CalcWorldBounds();

            inputScroll.SetHeights(visibleInputHeight, (float)textHeight);

            if (this.enablePreview)
            {
                var previewRichtext = dialogComposer.GetRichtext("previewInput");
                var previewScroll = dialogComposer.GetScrollbar("previewScroll");

                if (previewRichtext == null) return;

                var recomposeMethod = typeof(GuiElementRichtext).GetMethod("RecomposeText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
                    bool hasSelection = _cachedSelStart.HasValue && _cachedSelStart.Value != _cachedCaretPos;
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

            if (string.IsNullOrEmpty(currentText)) return;

            int caretPos = Math.Clamp(_cachedCaretPos, 0, currentText.Length);

            int lineStart = currentText.LastIndexOf('\n', Math.Max(0, caretPos - 1));
            lineStart = (lineStart == -1) ? 0 : lineStart + 1;

            int lineEnd = currentText.IndexOf('\n', caretPos);
            if (lineEnd == -1) lineEnd = currentText.Length;

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

        private bool OnSendButtonClicked(GuiComposer dialogComposer)
        {
            string currentText = dialogComposer.GetTextArea("messageInput").GetText();
            var networkChannel = NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard");

            if (this.mode == "edit")
            {
                networkChannel.SendPacket(
                    new PlayerEditMessage { Id = this.messageId, Message = currentText, BoardId = noticeBoardPacket.BoardProperties.BoardId }
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

        private void OnTitleBarClose() => TryClose();

        public override string ToggleKeyCombinationCode => null;
    }
}