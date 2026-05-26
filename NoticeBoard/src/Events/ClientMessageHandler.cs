using NoticeBoard.Packets;
using NoticeBoard.src.Gui.Windows;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace NoticeBoard.Events
{
    internal class ClientMessageHandler
    {
        private NoticeBoardMainWindowGui messageBoardGui;
        private ICoreClientAPI capi = NoticeBoardModSystem.getCAPI();

        public void SetMessageHandlers()
        {
            this.capi = NoticeBoardModSystem.getCAPI();
            capi.Network.GetChannel("noticeboard")
                .SetMessageHandler<ResponseAllMessages>(OnServerMessagesReceived);
            capi.Network.GetChannel("noticeboard")
                .SetMessageHandler<ResponseAllPlayers>(OnPlayersReceived);
        }

        private void OnServerMessagesReceived(ResponseAllMessages packet)
        {
            if (messageBoardGui == null || !messageBoardGui.IsOpened())
            {
                messageBoardGui = new NoticeBoardMainWindowGui("NoticeBoardGui", packet, capi);
                messageBoardGui.TryOpen();
            }

            messageBoardGui.UpdateMessages(packet.Messages);
            capi.Network.GetChannel("noticeboard").SendPacket(new RequestAllPlayers());
        }

        private void OnPlayersReceived(ResponseAllPlayers packet)
        {
            messageBoardGui.UpdatePlayersList(packet.Players);
        }
    }
}
