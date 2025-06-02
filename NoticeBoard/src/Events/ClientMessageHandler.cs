using NoticeBoard.Gui;
using NoticeBoard.Packets;

namespace NoticeBoard.Events
{
    internal class ClientMessageHandler
    {
        private NoticeBoardMainWindowGui messageBoardGui;

        public void SetMessageHandlers()
        {
            NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SetMessageHandler<ResponseAllMessages>(OnServerMessagesReceived);
        }

        private void OnServerMessagesReceived(ResponseAllMessages packet)
        {
            if (messageBoardGui == null || !messageBoardGui.IsOpened())
            {
                messageBoardGui = new NoticeBoardMainWindowGui("NoticeBoardGui", packet, NoticeBoardModSystem.getCAPI());
                messageBoardGui.TryOpen();
            }

            messageBoardGui.UpdateMessages(packet.Messages);
        }
    }
}
