using NoticeBoard.src.Packets;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace NoticeBoard.src.Events
{
    internal class ClientMessageHandler
    {
        private NoticeBoardModSystem modSystem = NoticeBoardModSystem.getModInstance();
        private NoticeBoardMainWindowGui messageBoardGui;

        public void SetMessageHandlers()
        {
            NoticeBoardModSystem.getCAPI().Network.GetChannel("noticeboard").SetMessageHandler<ResponseAllMessages>(OnServerMessagesReceived);
        }

        private void OnServerMessagesReceived(ResponseAllMessages packet)
        {
            // If the GUI is not open, create and open it
            //if (messageBoardGui == null || !messageBoardGui.IsOpened())
            //{
            //    messageBoardGui = new NoticeBoardMainWindowGui("NoticeBoardGui", packet, NoticeBoardModSystem.getCAPI());
            //    messageBoardGui.TryOpen();
            //}

            //messageBoardGui.UpdateMessages(packet.Messages);
        }
    }
}
