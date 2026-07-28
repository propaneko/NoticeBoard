using System.Collections.Generic;
using NoticeBoard.BlockType;
using NoticeBoard.Configs;
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
        private ICoreServerAPI sapi = NoticeBoardModSystem.getSAPI();

        public void SetMessageHandlers()
        {
            this.sapi = NoticeBoardModSystem.getSAPI();
            var channel = this.sapi.Network.GetChannel("noticeboard");
            channel.SetMessageHandler<RequestAllMessages>(OnPlayerRequestAllMessages);
            channel.SetMessageHandler<PlayerSendMessage>(OnPlayerSendMessage);
            channel.SetMessageHandler<PlayerSendDocument>(OnPlayerSendDocument);
            channel.SetMessageHandler<PlayerEditMessage>(OnPlayerEditMessage);
            channel.SetMessageHandler<PlayerBumpMessage>(OnPlayerBumpMessage);
            channel.SetMessageHandler<PlayerRemoveMessage>(OnPlayerRemoveMessage);
            channel.SetMessageHandler<PlayerDestroyNoticeBoard>(OnPlayerDestroyNoticeBoard);
            channel.SetMessageHandler<PlayerCreateNoticeBoard>(OnPlayerCreateNoticeBoard);
            channel.SetMessageHandler<EditPermissionMode>(OnPlayerEditPermissionMode);
            channel.SetMessageHandler<EditEnableParticles>(OnPlayerEditEnableParticles);
            channel.SetMessageHandler<EditEnableParchment>(OnPlayerEditEnableParchment);
            channel.SetMessageHandler<RequestAllPlayers>(OnRequestAllPlayers);
            channel.SetMessageHandler<EditBoardName>(OnPlayerEditBoardName);
            channel.SetMessageHandler<EditBoardFont>(OnPlayerEditBoardFont);
            channel.SetMessageHandler<EditBoardFontSize>(OnPlayerEditBoardFontSize);
            channel.SetMessageHandler<EditBoardTheme>(OnPlayerEditBoardTheme);
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
            if (!CanManageBoard(player, packet.BoardId))
                return;

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
            Message messageData = db.GetMessageById(packet.MessageId);
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);

            if (noticeBoard == null)
                return;

            if (!CanEditMessage(player, noticeBoard, messageData))
                return;

            BlockPos boardPos = PositionHelper.FromString(noticeBoard.Pos);

            if (messageData != null && noticeBoard.EnableParchment != 0)
            {
                Item parchmentItem = sapi.World.GetItem(
                    new AssetLocation("game", "paper-parchment")
                );

                if (parchmentItem != null)
                {
                    ItemStack parchmentStack = new ItemStack(parchmentItem);
                    parchmentStack.Attributes.SetString("text", messageData.Text);
                    parchmentStack.Attributes.SetString("signedby", messageData.PlayerName);
                    player.InventoryManager.TryGiveItemstack(parchmentStack, true);

                    if (parchmentStack.StackSize > 0)
                    {
                        Vec3d dropPos = new Vec3d(
                            boardPos.X + 0.5,
                            boardPos.Y + 0.5,
                            boardPos.Z + 0.5
                        );
                        Vec3d velocity = new Vec3d(
                            (sapi.World.Rand.NextDouble() - 0.5) * 0.1,
                            0.1,
                            (sapi.World.Rand.NextDouble() - 0.5) * 0.1
                        );

                        sapi.World.SpawnItemEntity(parchmentStack, dropPos, velocity);
                    }
                }
            }

            db.DeleteMessage(packet.MessageId);

            sapi.World.PlaySoundAt(
                new AssetLocation("noticeboard:sounds/effect/delete.ogg"),
                boardPos.X,
                boardPos.Y,
                boardPos.Z,
                null,
                true,
                32.0f,
                0.1f + (float)sapi.World.Rand.NextDouble() * 0.2f
            );
        }

        private void OnPlayerEditMessage(IServerPlayer player, PlayerEditMessage packet)
        {
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);

            if (noticeBoard == null)
                return;

            Message messageData = db.GetMessageById(packet.Id);

            if (!CanEditMessage(player, noticeBoard, messageData))
                return;

            BlockPos boardPos = PositionHelper.FromString(noticeBoard.Pos);

            sapi.World.PlaySoundAt(
                new AssetLocation("game:sounds/effect/writing.ogg"),
                boardPos.X,
                boardPos.Y,
                boardPos.Z,
                null,
                true,
                32.0f,
                0.9f + (float)sapi.World.Rand.NextDouble() * 0.2f
            );

            db.EditMessageById(packet.Id, packet.Message, packet.IsAnonymous);
        }

        private void OnPlayerBumpMessage(IServerPlayer player, PlayerBumpMessage packet)
        {
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);

            if (noticeBoard == null)
                return;

            Message messageData = db.GetMessageById(packet.MessageId);

            if (!CanEditMessage(player, noticeBoard, messageData))
                return;

            BlockPos boardPos = PositionHelper.FromString(noticeBoard.Pos);

            db.BumpMessageById(packet.MessageId);

            sapi.World.PlaySoundAt(
               new AssetLocation("noticeboard:sounds/effect/bump.ogg"),
               boardPos.X,
               boardPos.Y,
               boardPos.Z,
               null,
               true,
               32.0f,
               0.1f + (float)sapi.World.Rand.NextDouble() * 0.2f
           );
        }

        private void OnPlayerEditPermissionMode(IServerPlayer player, EditPermissionMode packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditPermissionMode(packet);
        }

        private void OnPlayerEditEnableParticles(IServerPlayer player, EditEnableParticles packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableParticles(packet);
        }

        private void OnPlayerEditEnableParchment(IServerPlayer player, EditEnableParchment packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableParchment(packet);
        }

        private void OnPlayerEditBoardOwner(IServerPlayer player, EditBoardOwner packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardOwner(packet);
        }

        private void OnPlayerEditBoardName(IServerPlayer player, EditBoardName packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardName(packet);
        }

        private void OnPlayerEditBoardFont(IServerPlayer player, EditBoardFont packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardFont(packet);
        }

        private void OnPlayerEditBoardFontSize(IServerPlayer player, EditBoardFontSize packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardFontSize(packet);
        }

        private void OnPlayerEditBoardTheme(IServerPlayer player, EditBoardTheme packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardTheme(packet);
        }

        private void OnPlayerEditEnableProximity(IServerPlayer player, EditEnableProximity packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableProximity(packet);
        }

        private void OnPlayerEditProximityChannel(IServerPlayer player, EditProximityChannel packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditProximityChannel(packet);
        }

        private void OnPlayerEditProximityDistance(
            IServerPlayer player,
            EditProximityDistance packet
        )
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditProximityDistance(packet);
        }

        private void OnPlayerSendMessage(IServerPlayer player, PlayerSendMessage packet)
        {
            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);

            if (noticeBoard == null)
                return;

            if (noticeBoard.PermissionMode == (int)BoardPermissionMode.Locked && !CanManageBoard(player, noticeBoard.BoardId))
                return;

            BlockPos boardPos = PositionHelper.FromString(noticeBoard.Pos);
            packet.TotalHours = sapi.World.Calendar.TotalHours;

            NoticeBoardBlockEntity blockEntity = sapi.GetNoticeBoardEntity(
                PositionHelper.FromString(noticeBoard.Pos)
            );

            bool parchmentConsumed = false;
            if (blockEntity != null && noticeBoard.EnableParchment == 1)
            {
                for (int i = blockEntity.Inventory.Count - 1; i >= 0; i--)
                {
                    ItemSlot slot = blockEntity.Inventory[i];

                    if (slot.Empty || slot.Itemstack?.Collectible?.Code == null)
                        continue;

                    string path = slot.Itemstack.Collectible.Code.Path;
                    bool isParchment =
                        path == "paper-parchment" || path.StartsWith("paper-parchment-") || path == "papyrus-paper" || path.StartsWith("papyrus-paper-");

                    if (isParchment)
                    {
                        slot.TakeOut(1);
                        slot.MarkDirty();
                        blockEntity.MarkDirty(true);
                        parchmentConsumed = true;
                        break;
                    }
                }

                if (!parchmentConsumed)
                    return;
            }

            db.InsertMessage(packet, player.PlayerName);

            db.MarkBoardAsRead(player.PlayerUID, packet.BoardId);

            sapi.World.PlaySoundAt(
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
                && sapi.Groups.GetPlayerGroupByName(noticeBoard.ProximityChannel) != null
            )
            {
                var proximityGroup = sapi
                    .Groups.GetPlayerGroupByName(noticeBoard.ProximityChannel)
                    .Uid;
                if (proximityGroup != 0)
                {
                    string rpNickName = sapi.GetPlayerByUID(player.PlayerUID)
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

            if (noticeBoard == null)
                return;

            if (noticeBoard.PermissionMode == (int)BoardPermissionMode.Locked && !CanManageBoard(player, noticeBoard.BoardId))
                return;

            BlockPos boardPos = PositionHelper.FromString(noticeBoard.Pos);
            packet.TotalHours = sapi.World.Calendar.TotalHours;

            NoticeBoardBlockEntity blockEntity = sapi.GetNoticeBoardEntity(
                PositionHelper.FromString(noticeBoard.Pos)
            );

            if (blockEntity == null)
                return;

            string documentText = "";
            string authorName = player.PlayerName;

            if (noticeBoard.EnableParchment == 1)
            {
                ItemSlot writtenSlot = blockEntity.Inventory[4];

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
                blockEntity.MarkDirty(true);
            }
            else
            {
                return;
            }

            packet.Document = documentText;
            packet.PlayerId = db.GetPlayerByName(authorName)?.PlayerUID ?? player.PlayerUID;

            db.InsertMessage(packet);

            sapi.World.PlaySoundAt(
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
                && sapi.Groups.GetPlayerGroupByName(noticeBoard.ProximityChannel) != null
            )
            {
                var proximityGroup = sapi
                    .Groups.GetPlayerGroupByName(noticeBoard.ProximityChannel)
                    .Uid;

                if (proximityGroup != 0)
                {
                    string rpNickName = sapi.GetPlayerByUID(player.PlayerUID)
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
                BoardProperties = tableProperties ?? new NoticeBoardObject { BoardId = packet.BoardId },
            };

            sapi.Network.GetChannel("noticeboard").SendPacket(responsePacket, player);
        }

        private void OnRequestAllPlayers(IServerPlayer player, RequestAllPlayers packet)
        {
            List<PlayerEntry> players = db.GetAllPlayers();

            sapi.Network.GetChannel("noticeboard")
                .SendPacket(new ResponseAllPlayers { Players = players }, player);
        }

        private bool CanManageBoard(IServerPlayer player, string boardId)
        {
            NoticeBoardObject noticeBoard = db.GetBoardData(boardId);
            return noticeBoard != null
                && (noticeBoard.PlayerId == player.PlayerUID || player.Role.Code == "admin");
        }

        private bool CanEditMessage(IServerPlayer player, NoticeBoardObject noticeBoard, Message message)
        {
            if (noticeBoard == null)
                return false;

            if (noticeBoard.PlayerId == player.PlayerUID || player.Role.Code == "admin")
                return true;

            if (noticeBoard.PermissionMode == (int)BoardPermissionMode.Locked)
                return false;

            if (noticeBoard.PermissionMode == (int)BoardPermissionMode.All)
                return true;

            return message != null && message.PlayerId == player.PlayerUID;
        }
    }
}
