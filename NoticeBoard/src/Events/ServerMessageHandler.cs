using NoticeBoard.Packets;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using NoticeBoard.Extensions;
using System.Linq;


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

        private void OnPlayerSendMessage(IServerPlayer player, PlayerSendMessage packet)
        {
            string rpNickName = NoticeBoardModSystem.getSAPI().GetPlayerByUID(player.PlayerUID).GetModData("BASIC_NICKNAME", player.PlayerName);

            db.InsertMessage(packet, rpNickName);

            var proximityGroup = NoticeBoardModSystem.getSAPI().Groups.GetPlayerGroupByName("Proximity").Uid;
            if (proximityGroup != 0)
            {
                NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);
                string message = $"<strong>{rpNickName}</strong> {Lang.Get("noticeboard:new-notice-proximity-message")} ({noticeBoard.Pos})";
                SendLocalChatByPlayer(player, message);
            }
        }

        private void OnPlayerEditMessage(IServerPlayer player, PlayerEditMessage packet)
        {
            db.EditMessageById(packet.Id, packet.Message);
        }

        private void SendLocalChatByPlayer(IServerPlayer byPlayer, string message, int distanceToBroadcast = 100, EnumChatType chatType = EnumChatType.OthersMessage, string data = null)
        {
            var proximityGroup = NoticeBoardModSystem.getSAPI().Groups.GetPlayerGroupByName("Proximity");
            foreach (var player in NoticeBoardModSystem.getSAPI().World.AllOnlinePlayers.Where(x =>
                         x.Entity.Pos.AsBlockPos.ManhattenDistance(byPlayer.Entity.Pos.AsBlockPos) < distanceToBroadcast))
            {
                var serverPlayer = player as IServerPlayer;

                serverPlayer.SendMessage(proximityGroup.Uid, message, chatType, data);
            }
        }

        private void OnPlayerRequestAllMessages(IServerPlayer player, RequestAllMessages packet)
        {
            List<Message> messages = db.GetAllMessages(packet.BoardId);
            messages.Reverse();

            ResponseAllMessages responsePacket = new ResponseAllMessages
            {
                BoardId = packet.BoardId,
                PlayerId = packet.PlayerId,
                Messages = messages
            };

            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SendPacket(responsePacket, player);
        }
    }
}
