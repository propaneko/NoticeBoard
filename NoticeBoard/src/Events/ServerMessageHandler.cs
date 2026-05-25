using System.Collections.Generic;
using NoticeBoard.BlockType;
using NoticeBoard.Database;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
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
            var channel = NoticeBoardModSystem.getSAPI().Network.GetChannel("noticeboard");
            channel.SetMessageHandler<RequestAllMessages>(OnPlayerRequestAllMessages);
            channel.SetMessageHandler<PlayerSendMessage>(OnPlayerSendMessage);
            channel.SetMessageHandler<PlayerSendDocument>(OnPlayerSendDocument);
            channel.SetMessageHandler<PlayerEditMessage>(OnPlayerEditMessage);
            channel.SetMessageHandler<PlayerBumpMessage>(OnPlayerBumpMessage);
            channel.SetMessageHandler<PlayerRemoveMessage>(OnPlayerRemoveMessage);
            channel.SetMessageHandler<PlayerDestroyNoticeBoard>(OnPlayerDestroyNoticeBoard);
            channel.SetMessageHandler<PlayerCreateNoticeBoard>(OnPlayerCreateNoticeBoard);
            channel.SetMessageHandler<EditIsLocked>(OnPlayerEditIsLocked);
            channel.SetMessageHandler<EditEnableParticles>(OnPlayerEditEnableParticles);
            channel.SetMessageHandler<EditEnableParchment>(OnPlayerEditEnableParchment);
            channel.SetMessageHandler<RequestAllPlayers>(OnRequestAllPlayers);
            channel.SetMessageHandler<EditBoardName>(OnPlayerEditBoardName);
            channel.SetMessageHandler<EditBoardFont>(OnPlayerEditBoardFont);
            channel.SetMessageHandler<EditBoardOwner>(OnPlayerEditBoardOwner);
            channel.SetMessageHandler<EditEnableProximity>(OnPlayerEditEnableProximity);
            channel.SetMessageHandler<EditProximityChannel>(OnPlayerEditProximityChannel);
            channel.SetMessageHandler<EditProximityDistance>(OnPlayerEditProximityDistance);
        }

        private void OnPlayerDestroyNoticeBoard(
            IServerPlayer player,
            PlayerDestroyNoticeBoard packet
        )
        {
            db.DeleteNoticeBoard(packet.BoardId);
        }

        private void OnPlayerCreateNoticeBoard(IServerPlayer player, PlayerCreateNoticeBoard packet)
        {
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);

            if (noticeBoard != null && noticeBoard.BoardId == packet.BoardId)
            {
                db.UpdateNoticeBoard(packet);
            }
            else
            {
                db.CreateNoticeBoard(packet);
            }
        }

        private void OnPlayerRemoveMessage(IServerPlayer player, PlayerRemoveMessage packet)
        {
            db.DeleteMessage(packet.MessageId);
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);
            BlockPos boardPos = PositionHelper.FromString(noticeBoard.Pos);
            NoticeBoardModSystem
                .getSAPI()
                .World.PlaySoundAt(
                    new AssetLocation("noticeboard:sounds/effect/delete.ogg"),
                    boardPos.X,
                    boardPos.Y,
                    boardPos.Z,
                    null,
                    true,
                    32.0f,
                    0.1f + (float)NoticeBoardModSystem.getSAPI().World.Rand.NextDouble() * 0.2f
                );
        }

        private void OnPlayerEditMessage(IServerPlayer player, PlayerEditMessage packet)
        {
            db.EditMessageById(packet.Id, packet.Message);
        }

        private void OnPlayerBumpMessage(IServerPlayer player, PlayerBumpMessage packet)
        {
            db.BumpMessageById(packet.MessageId);
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

        private void OnPlayerEditBoardOwner(IServerPlayer player, EditBoardOwner packet)
        {
            db.EditBoardOwner(packet);
        }

        private void OnPlayerEditBoardName(IServerPlayer player, EditBoardName packet)
        {
            db.EditBoardName(packet);
        }

        private void OnPlayerEditBoardFont(IServerPlayer player, EditBoardFont packet)
        {
            db.EditBoardFont(packet);
        }

        private void OnPlayerEditEnableProximity(IServerPlayer player, EditEnableProximity packet)
        {
            db.EditEnableProximity(packet);
        }

        private void OnPlayerEditProximityChannel(IServerPlayer player, EditProximityChannel packet)
        {
            db.EditProximityChannel(packet);
        }

        private void OnPlayerEditProximityDistance(
            IServerPlayer player,
            EditProximityDistance packet
        )
        {
            db.EditProximityDistance(packet);
        }

        private void OnPlayerSendMessage(IServerPlayer player, PlayerSendMessage packet)
        {
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);

            db.InsertMessage(packet, player.PlayerName);

            db.MarkBoardAsRead(player.PlayerUID, packet.BoardId);

            BlockPos boardPos = PositionHelper.FromString(noticeBoard.Pos);

            NoticeBoardBlockEntity be =
                NoticeBoardModSystem
                    .getSAPI()
                    .World.BlockAccessor.GetBlockEntity(PositionHelper.FromString(noticeBoard.Pos))
                as NoticeBoardBlockEntity;

            if (be != null && noticeBoard.EnableParchment == 1)
            {
                bool parchmentFound = false;

                for (int i = be.Inventory.Count - 1; i >= 0; i--)
                {
                    ItemSlot slot = be.Inventory[i];

                    if (slot.Empty || slot.Itemstack?.Collectible?.Code == null)
                        continue;

                    string path = slot.Itemstack.Collectible.Code.Path;
                    bool isParchment =
                        path == "paper-parchment" || path.StartsWith("paper-parchment-");

                    if (isParchment)
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                        be.MarkDirty(true);
                        parchmentFound = true;
                        break;
                    }
                }

                if (!parchmentFound)
                    return;
            }

            NoticeBoardModSystem
                .getSAPI()
                .World.PlaySoundAt(
                    new AssetLocation("noticeboard:sounds/effect/new_message.ogg"),
                    boardPos.X,
                    boardPos.Y,
                    boardPos.Z,
                    null,
                    true,
                    32.0f,
                    0.7f + (float)NoticeBoardModSystem.getSAPI().World.Rand.NextDouble() * 0.2f
                );

            if (
                noticeBoard.EnableProximity == 1
                && NoticeBoardModSystem
                    .getSAPI()
                    .Groups.GetPlayerGroupByName(noticeBoard.ProximityChannel) != null
            )
            {
                var proximityGroup = NoticeBoardModSystem
                    .getSAPI()
                    .Groups.GetPlayerGroupByName(noticeBoard.ProximityChannel)
                    .Uid;
                if (proximityGroup != 0)
                {
                    string rpNickName = NoticeBoardModSystem
                        .getSAPI()
                        .GetPlayerByUID(player.PlayerUID)
                        .GetModData("BASIC_NICKNAME", player.PlayerName);

                    string message =
                        $"<strong>{rpNickName}</strong> {Lang.Get("noticeboard:new-notice-proximity-message")} ({noticeBoard.BoardName})";
                    Proximity.SendLocalChatByPlayer(
                        player,
                        message,
                        noticeBoard.ProximityChannel,
                        (int)noticeBoard.ProximityDistance
                    );
                }
            }
        }

        private void OnPlayerSendDocument(IServerPlayer player, PlayerSendDocument packet)
        {
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);
            BlockPos boardPos = PositionHelper.FromString(noticeBoard.Pos);

            NoticeBoardBlockEntity be =
                NoticeBoardModSystem.getSAPI().World.BlockAccessor.GetBlockEntity(boardPos)
                as NoticeBoardBlockEntity;

            if (be == null)
                return;

            string documentText = "";
            string authorName = player.PlayerName;

            if (noticeBoard.EnableParchment == 1)
            {
                ItemSlot writtenSlot = be.Inventory[4]; 

                if (writtenSlot.Empty || writtenSlot.Itemstack?.Collectible?.Code == null)
                    return;

                string path = writtenSlot.Itemstack.Collectible.Code.Path;
                bool isParchment = path == "paper-parchment" || path.StartsWith("paper-parchment-");

                if (!isParchment)
                    return;

                documentText = writtenSlot.Itemstack.Attributes.GetString("text", "");
                authorName = writtenSlot.Itemstack.Attributes.GetString(
                    "signedby",
                    player.PlayerName
                );

                if (string.IsNullOrEmpty(documentText))
                    return;
                writtenSlot.TakeOut(1);
                writtenSlot.MarkDirty();
                be.MarkDirty(true);
            }
            else
            {
                return;
            }

            packet.Document = documentText;

            db.InsertMessage(packet, authorName);

            NoticeBoardModSystem
                .getSAPI()
                .World.PlaySoundAt(
                    new AssetLocation("noticeboard:sounds/effect/new_message.ogg"),
                    boardPos.X,
                    boardPos.Y,
                    boardPos.Z,
                    null,
                    true,
                    32.0f,
                    0.7f + (float)NoticeBoardModSystem.getSAPI().World.Rand.NextDouble() * 0.2f
                );

            if (
                noticeBoard.EnableProximity == 1
                && NoticeBoardModSystem
                    .getSAPI()
                    .Groups.GetPlayerGroupByName(noticeBoard.ProximityChannel) != null
            )
            {
                var proximityGroup = NoticeBoardModSystem
                    .getSAPI()
                    .Groups.GetPlayerGroupByName(noticeBoard.ProximityChannel)
                    .Uid;

                if (proximityGroup != 0)
                {
                    string rpNickName = NoticeBoardModSystem
                        .getSAPI()
                        .GetPlayerByUID(player.PlayerUID)
                        .GetModData("BASIC_NICKNAME", authorName);

                    string message =
                        $"<strong>{rpNickName}</strong> {Lang.Get("noticeboard:new-notice-proximity-message")} ({noticeBoard.BoardName})";

                    Proximity.SendLocalChatByPlayer(
                        player,
                        message,
                        noticeBoard.ProximityChannel,
                        (int)noticeBoard.ProximityDistance
                    );
                }
            }
        }

        private void OnPlayerRequestAllMessages(IServerPlayer player, RequestAllMessages packet)
        {
            List<Message> messages = db.GetAllMessages(packet.BoardId);

            NoticeBoardObject tableProperties = db.GetBoardData(packet.BoardId);

            db.MarkBoardAsRead(player.PlayerUID, packet.BoardId);

            ResponseAllMessages responsePacket = new ResponseAllMessages
            {
                Messages = messages,
                BoardProperties = tableProperties,
            };

            NoticeBoardModSystem
                .getSAPI()
                .Network.GetChannel("noticeboard")
                .SendPacket(responsePacket, player);
        }

        private void OnRequestAllPlayers(IServerPlayer player, RequestAllPlayers packet)
        {
            List<PlayerEntry> players = new List<PlayerEntry>();

            foreach (var p in NoticeBoardModSystem.getSAPI().World.AllPlayers)
            {
                players.Add(new PlayerEntry { PlayerUID = p.PlayerUID, PlayerName = p.PlayerName });
            }

            foreach (var uid in NoticeBoardModSystem.getSAPI().PlayerData.PlayerDataByUid)
            {
                var pdata = NoticeBoardModSystem
                    .getSAPI()
                    .PlayerData.GetPlayerDataByUid(uid.Value.PlayerUID);
                if (pdata == null)
                    continue;

                if (!players.Exists(x => x.PlayerUID == uid.Value.PlayerUID))
                {
                    players.Add(
                        new PlayerEntry
                        {
                            PlayerUID = uid.Value.PlayerUID,
                            PlayerName = pdata.LastKnownPlayername,
                        }
                    );
                }
            }

            NoticeBoardModSystem
                .getSAPI()
                .Network.GetChannel("noticeboard")
                .SendPacket(new ResponseAllPlayers { Players = players }, player);
        }
    }
}
