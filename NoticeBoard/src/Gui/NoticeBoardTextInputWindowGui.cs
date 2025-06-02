using NoticeBoard;
using System.Linq;
using Vintagestory.API.Client;
using NoticeBoard.Packets;
using Vintagestory.API.Config;
using System;
using Vintagestory.API.Common;

namespace NoticeBoard.Gui;
public class NoticeBoardTextInputWindowGui : GuiDialog
{
    private string boardId;
    private string playerId;
    private string mode;
    private int messageId;
    private string message;

    private NoticeBoardMainWindowGui parentContext;
    private ResponseAllMessages noticeBoardPacket;

    public NoticeBoardTextInputWindowGui(ICoreClientAPI capi, NoticeBoardMainWindowGui context, ResponseAllMessages noticeBoardPacket, string mode, int messageId = -1, string message = "") : base(capi)
    {
        this.noticeBoardPacket = noticeBoardPacket;
        this.mode = mode;
        this.parentContext = context;
        if (mode == "edit")
        {
            this.message = message;
            this.messageId = messageId;
        }
        this.Compose();
    }
    private void Compose()
    {
        int insetWidth = 550;
        int insetHeight = 75;
        int insetDepth = 3;
        int rowHeight = 100;
        ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment((EnumDialogArea)7).WithFixedOffset(0.0, -120.0);
        ElementBounds insetBounds = ElementBounds.Fixed(0.0, GuiStyle.TitleBarHeight, (double)insetWidth, (double)insetHeight);
        ElementBounds.Fixed(0.0, GuiStyle.TitleBarHeight, (double)insetWidth, (double)insetHeight);
        ElementBounds scrollbarBounds = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(20.0);
        ElementBounds.Fixed(0.0, 40.0, 300.0, 100.0);
        ElementBounds clipBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds containerBounds = insetBounds.ForkContainingChild(GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding, GuiStyle.HalfPadding);
        ElementBounds.Fixed(0.0, 0.0, (double)insetWidth, (double)rowHeight);
        ElementBounds.Fixed(870.0, 0.0, 20.0, 20.0);
        ElementBounds buttonBounds = insetBounds.RightCopy(0.0, 0.0, 0.0, 0.0).WithFixedWidth(120.0).WithFixedHeight(40.0).WithFixedOffset(17.0, 0.0);
        ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding).WithSizing((ElementSizing)2).WithChildren(new ElementBounds[]
        {
            insetBounds,
            scrollbarBounds,
            buttonBounds
        });
        base.SingleComposer = GuiElementClipHelpler.EndClip(GuiComposerHelpers.AddTextArea(GuiElementClipHelpler.BeginClip(GuiElementInsetHelper.AddInset(GuiComposerHelpers.AddSmallButton(GuiComposerHelpers.AddDialogTitleBar(GuiComposerHelpers.AddShadedDialogBG(this.capi.Gui.CreateCompo("addNoticeGui", dialogBounds), bgBounds, true, 5.0, 0.75f), Lang.Get("noticeboard:add-notice-window-title", Array.Empty<object>()), new Action(this.OnTitleBarClose), null, null), Lang.Get("noticeboard:add-notice-window-pin-button", Array.Empty<object>()), new ActionConsumable(this.OnSendButtonClicked), buttonBounds, (EnumButtonStyle)2, null), insetBounds, insetDepth, 0.85f), clipBounds), containerBounds, new Action<string>(this.OnTextChanged), CairoFont.WhiteDetailText(), "messageInput"));
        base.SingleComposer.Compose(true);
        if (this.mode == "edit")
        {
            GuiComposerHelpers.GetTextArea(base.SingleComposer, "messageInput").SetValue(this.message, true);
        }
    }

    private void OnTextChanged(string text)
    {
        string[] lines = text.Split('\n', StringSplitOptions.None);
        int maxLines = 4;
        if (lines.Length > maxLines)
        {
            string limitedText = string.Join("\n", lines.Take(maxLines));
            GuiComposerHelpers.GetTextArea(base.SingleComposer, "messageInput").SetValue(limitedText, true);
        }
        if (text.Length > 292)
        {
            string limitedText2 = text.Substring(0, 292);
            GuiComposerHelpers.GetTextArea(base.SingleComposer, "messageInput").SetValue(limitedText2, true);
        }
    }

    private bool OnSendButtonClicked()
    {
        if (this.mode == "edit")
        {
            string text = GuiComposerHelpers.GetTextArea(base.SingleComposer, "messageInput").GetText();
            PlayerEditMessage playerEditMessage = new PlayerEditMessage
            {
                Id = this.messageId,
                Message = text
            };
            NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket<PlayerEditMessage>(playerEditMessage);
            this.parentContext.GetMessages();
            this.TryClose();
            return true;
        }
        string text2 = GuiComposerHelpers.GetTextArea(base.SingleComposer, "messageInput").GetText();
        PlayerSendMessage playerSendMessage = new PlayerSendMessage
        {
            Message = text2,
            BoardId = this.noticeBoardPacket.BoardId,
            PlayerId = this.noticeBoardPacket.PlayerId
        };
        NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SendPacket<PlayerSendMessage>(playerSendMessage);
        this.parentContext.GetMessages();
        this.TryClose();
        return true;
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