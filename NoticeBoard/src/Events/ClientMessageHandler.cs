using NoticeBoard.Packets;
using NoticeBoard.src.Gui.Windows;

namespace NoticeBoard.Events
{
    internal class ClientMessageHandler
    {
        private NoticeBoardMainWindowGui messageBoardGui;

        public void SetMessageHandlers()
        {
            NoticeBoardModSystem
                .getCAPI()
                .Network.GetChannel("noticeboard")
                .SetMessageHandler<ResponseAllMessages>(OnServerMessagesReceived);
            NoticeBoardModSystem
                .getCAPI()
                .Network.GetChannel("noticeboard")
                .SetMessageHandler<ResponseAllPlayers>(OnPlayersReceived);
        }

        private void OnServerMessagesReceived(ResponseAllMessages packet)
        {
            if (messageBoardGui == null || !messageBoardGui.IsOpened())
            {
                messageBoardGui = new NoticeBoardMainWindowGui(
                    "NoticeBoardGui",
                    packet,
                    NoticeBoardModSystem.getCAPI()
                );
                messageBoardGui.TryOpen();
            }

            messageBoardGui.UpdateMessages(packet.Messages);
            NoticeBoardModSystem
                .getCAPI()
                .Network.GetChannel("noticeboard")
                .SendPacket(new RequestAllPlayers());
        }

        private void OnPlayersReceived(ResponseAllPlayers packet)
        {
            messageBoardGui.UpdatePlayersList(packet.Players);
        }
    }
}
