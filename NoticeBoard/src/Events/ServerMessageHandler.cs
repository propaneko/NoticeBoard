using NoticeBoard.Packets;
using System.Collections.Generic;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using NoticeBoard.Utils;
using NoticeBoard.Extensions;
using NoticeBoard.Database;

namespace NoticeBoard.Events
{
    internal class ServerMessageHandler
    {
        private readonly SQLiteHandler db = new SQLiteHandler();
        public void SetMessageHandlers()
        {
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<RequestAllMessages>(OnPlayerRequestAllMessages);
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<PlayerSendMessage>(OnPlayerSendMessage);
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<PlayerEditMessage>(OnPlayerEditMessage);
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<PlayerRemoveMessage>(OnPlayerRemoveMessage);
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<PlayerDestroyNoticeBoard>(OnPlayerDestroyNoticeBoard);
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<PlayerCreateNoticeBoard>(OnPlayerCreateNoticeBoard);
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<EditIsLocked>(OnPlayerEditIsLocked);

        }

        private void OnPlayerDestroyNoticeBoard(IServerPlayer player, PlayerDestroyNoticeBoard packet)
        {
            db.DeleteNoticeBoard(packet.BoardId);
        }


        private void OnPlayerCreateNoticeBoard(IServerPlayer player, PlayerCreateNoticeBoard packet)
        {
            db.CreateNoticeBoard(packet);
        }

        private void OnPlayerRemoveMessage(IServerPlayer player, PlayerRemoveMessage packet)
        {
            db.DeleteMessage(packet.MessageId);
        }

        private void OnPlayerEditMessage(IServerPlayer player, PlayerEditMessage packet)
        {
            db.EditMessageById(packet.Id, packet.Message);
        }

        private void OnPlayerEditIsLocked(IServerPlayer player, EditIsLocked packet)
        {
            db.EditIsLocked(packet);
        }

        private void OnPlayerSendMessage(IServerPlayer player, PlayerSendMessage packet)
        {

            db.InsertMessage(packet, player.PlayerName);

            if (NoticeBoardModSystem.getConfig().SendProximityMessage && NoticeBoardModSystem.getSAPI().Groups.GetPlayerGroupByName(NoticeBoardModSystem.getConfig().ProximityGroupName) != null)
            {
                var proximityGroup = NoticeBoardModSystem.getSAPI().Groups.GetPlayerGroupByName(NoticeBoardModSystem.getConfig().ProximityGroupName).Uid;
                if (proximityGroup != 0)
                {
                    NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);
                    string rpNickName = NoticeBoardModSystem.getSAPI().GetPlayerByUID(player.PlayerUID).GetModData("BASIC_NICKNAME", player.PlayerName);

                    string message = $"<strong>{rpNickName}</strong> {Lang.Get("noticeboard:new-notice-proximity-message")} ({noticeBoard.Pos})";
                    Proximity.SendLocalChatByPlayer(player, message, NoticeBoardModSystem.getConfig().ProximityMessageDistance);
                }
            }
        }

      
        private void OnPlayerRequestAllMessages(IServerPlayer player, RequestAllMessages packet)
        {
            List<Message> messages = db.GetAllMessages(packet.BoardId);
            messages.Reverse();

            NoticeBoardObject tableProperties = db.GetBoardData(packet.BoardId);

            ResponseAllMessages responsePacket = new ResponseAllMessages
            {
                Messages = messages,
                BoardProperties = tableProperties
            };

            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SendPacket(responsePacket, player);
        }
    }
}
