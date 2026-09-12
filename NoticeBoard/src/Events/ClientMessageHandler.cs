using NoticeBoard.BlockType;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.src.Gui.Windows;
using NoticeBoard.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
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
            capi.Network.GetChannel("noticeboard")
                .SetMessageHandler<UnreadParticlesPacket>(OnUnreadParticlesPacketReceived);
            capi.Network.GetChannel("noticeboard")
                .SetMessageHandler<ExpiredNoticesFall>(OnExpiredNoticesFall);
        }

        private void OnServerMessagesReceived(ResponseAllMessages packet)
        {
            string incomingBoardId = packet.BoardProperties?.BoardId;
            if (
                messageBoardGui == null
                || !messageBoardGui.IsOpened()
                || messageBoardGui.BoardId != incomingBoardId
            )
            {
                messageBoardGui?.TryClose();
                messageBoardGui = new NoticeBoardMainWindowGui("NoticeBoardGui", packet, capi);
                messageBoardGui.TryOpen();
            }

            messageBoardGui.UpdateMessages(packet.Messages);
            capi.Network.GetChannel("noticeboard").SendPacket(new RequestAllPlayers());
            PaperPinController.TryRestoreDraft(messageBoardGui);
        }

        private void OnPlayersReceived(ResponseAllPlayers packet)
        {
            messageBoardGui?.UpdatePlayersList(packet.Players);
        }

        private void OnUnreadParticlesPacketReceived(UnreadParticlesPacket packet)
        {
            if (capi.World.BlockAccessor.GetBlock(packet.Pos) is NoticeBoardBlock boardBlock)
            {
                boardBlock.SpawnUnreadParticles(capi.World, packet.Pos);
            }
        }

        private void OnExpiredNoticesFall(ExpiredNoticesFall packet)
        {
            if (packet?.Pos == null || packet.Notices == null)
                return;
            capi.GetNoticeBoardEntity(packet.Pos)?.AddExpiredFalls(packet.Notices);
        }
    }
}
