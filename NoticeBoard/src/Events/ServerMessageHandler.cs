using NoticeBoard.BlockType;
using NoticeBoard.Database;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

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
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<EditEnableParticles>(OnPlayerEditEnableParticles);
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<EditEnableParchment>(OnPlayerEditEnableParchment);
            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SetMessageHandler<RequestAllPlayers>(OnRequestAllPlayers);
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

        private void OnPlayerEditEnableParticles(IServerPlayer player, EditEnableParticles packet)
        {
            db.EditEnableParticles(packet);
        }

        private void OnPlayerEditEnableParchment(IServerPlayer player, EditEnableParchment packet)
        {
            db.EditEnableParchment(packet);
        }

        private void OnPlayerSendMessage(IServerPlayer player, PlayerSendMessage packet)
        {
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);

            db.InsertMessage(packet, player.PlayerName);

            //db.MarkBoardAsRead(player.PlayerUID, packet.BoardId);

            NoticeBoardBlockEntity be = NoticeBoardModSystem.getSAPI().World.BlockAccessor.GetBlockEntity(PositionHelper.FromString(noticeBoard.Pos)) as NoticeBoardBlockEntity;

            if (be == null || noticeBoard.enableParchment == 0) return;

            ItemSlot slot = be.Inventory[0];
            if (slot.Empty || slot.Itemstack?.Collectible?.Code == null)
                return;

            string path = slot.Itemstack.Collectible.Code.Path;

            bool isParchment =
                path == "paper-parchment" ||
                path.StartsWith("paper-parchment-");

            if (!isParchment)
                return;

            slot.TakeOut(1);
            slot.MarkDirty();
            be.MarkDirty(true);

            if (NoticeBoardModSystem.getConfig().SendProximityMessage && NoticeBoardModSystem.getSAPI().Groups.GetPlayerGroupByName(NoticeBoardModSystem.getConfig().ProximityGroupName) != null)
            {
                var proximityGroup = NoticeBoardModSystem.getSAPI().Groups.GetPlayerGroupByName(NoticeBoardModSystem.getConfig().ProximityGroupName).Uid;
                if (proximityGroup != 0)
                {
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

            //db.MarkBoardAsRead(player.PlayerUID, packet.BoardId);

            ResponseAllMessages responsePacket = new ResponseAllMessages
            {
                Messages = messages,
                BoardProperties = tableProperties,
            };

            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard").SendPacket(responsePacket, player);
        }

        private void OnRequestAllPlayers(IServerPlayer player, RequestAllPlayers packet)
        {
            List<PlayerEntry> players = new List<PlayerEntry>();

            foreach (var p in NoticeBoardModSystem.getSAPI().World.AllPlayers)
            {
                players.Add(new PlayerEntry
                {
                    PlayerUID = p.PlayerUID,
                    PlayerName = p.PlayerName
                });
            }

            // ALSO include offline players from server player data
            foreach (var uid in NoticeBoardModSystem.getSAPI().PlayerData.PlayerDataByUid)
            {
                var pdata = NoticeBoardModSystem.getSAPI().PlayerData.GetPlayerDataByUid(uid.Value.PlayerUID);
                if (pdata == null) continue;

                if (!players.Exists(x => x.PlayerUID == uid.Value.PlayerUID))
                {
                    players.Add(new PlayerEntry
                    {
                        PlayerUID = uid.Value.PlayerUID,
                        PlayerName = pdata.LastKnownPlayername
                    });
                }
            }

            NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard")
                .SendPacket(new ResponseAllPlayers { Players = players }, player);
        }
    }
}
