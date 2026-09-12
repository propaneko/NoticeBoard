using System.Collections.Generic;
using NoticeBoard;
using NoticeBoard.BlockType;
using NoticeBoard.Configs;
using NoticeBoard.Database;
using NoticeBoard.Extensions;
using NoticeBoard.Packets;
using NoticeBoard.Utils;
using NoticeBoard.Rendering;
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
            channel.SetMessageHandler<PlayerRepositionMessage>(OnPlayerRepositionMessage);
            channel.SetMessageHandler<PlayerBumpMessage>(OnPlayerBumpMessage);
            channel.SetMessageHandler<PlayerRemoveMessage>(OnPlayerRemoveMessage);
            channel.SetMessageHandler<EditPermissionMode>(OnPlayerEditPermissionMode);
            channel.SetMessageHandler<EditEnableParticles>(OnPlayerEditEnableParticles);
            channel.SetMessageHandler<EditEnableNoticeAging>(OnPlayerEditEnableNoticeAging);
            channel.SetMessageHandler<EditNoticeAgingDays>(OnPlayerEditNoticeAgingDays);
            channel.SetMessageHandler<EditEnableParchment>(OnPlayerEditEnableParchment);
            channel.SetMessageHandler<EditEnableManualPin>(OnPlayerEditEnableManualPin);
            channel.SetMessageHandler<PersistPaperPins>(OnPersistPaperPins);
            channel.SetMessageHandler<RequestAllPlayers>(OnRequestAllPlayers);
            channel.SetMessageHandler<EditBoardName>(OnPlayerEditBoardName);
            channel.SetMessageHandler<EditBoardFont>(OnPlayerEditBoardFont);
            channel.SetMessageHandler<EditBoardFontSize>(OnPlayerEditBoardFontSize);
            channel.SetMessageHandler<EditMaxPapersOnBoard>(OnPlayerEditMaxPapersOnBoard);
            channel.SetMessageHandler<EditEnableLegacyBoard>(OnPlayerEditEnableLegacyBoard);
            channel.SetMessageHandler<EditBoardTextSharpness>(OnPlayerEditBoardTextSharpness);
            channel.SetMessageHandler<EditBoardSwayStrength>(OnPlayerEditBoardSwayStrength);
            channel.SetMessageHandler<EditBoardTheme>(OnPlayerEditBoardTheme);
            channel.SetMessageHandler<EditBoardOwner>(OnPlayerEditBoardOwner);
            channel.SetMessageHandler<EditEnableProximity>(OnPlayerEditEnableProximity);
            channel.SetMessageHandler<EditProximityChannel>(OnPlayerEditProximityChannel);
            channel.SetMessageHandler<EditProximityDistance>(OnPlayerEditProximityDistance);
            channel.SetMessageHandler<EditEnableDiscord>(OnPlayerEditEnableDiscord);
            channel.SetMessageHandler<EditDiscordWebhook>(OnPlayerEditDiscordWebhook);
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
                var cal = sapi.World.Calendar;
                string date = GameDateFormatter.FormatImmersiveDate(
                    cal.HoursPerDay, cal.DaysPerMonth, messageData.TotalHours, includeTime: false);
                ItemStack parchmentStack = NoticeParchment.FromNotice(
                    sapi.World,
                    messageData.Text,
                    messageData.PlayerName,
                    messageData.IsAnonymous,
                    messageData.Holder,
                    messageData.PaperTheme,
                    messageData.PlayerId,
                    date
                );

                if (parchmentStack != null)
                {
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

            RefreshBoardVisuals(packet.BoardId);

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
            if (string.IsNullOrWhiteSpace(packet.Message))
                return;

            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);

            if (noticeBoard == null)
                return;

            Message messageData = db.GetMessageById(packet.Id);

            if (!CanEditMessage(player, noticeBoard, messageData))
                return;

            packet.Holder = MessageHolder.Clamp(packet.Holder);
            packet.PaperTheme = ThemeManager.Sanitize(packet.PaperTheme);
            packet.WaypointTitle = WaypointPin.SanitizeTitle(packet.WaypointTitle);
            packet.WaypointIcon = WaypointPin.SanitizeIcon(packet.WaypointIcon);
            packet.WaypointColor = WaypointPin.SanitizeColor(packet.WaypointColor);

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

            db.EditMessageById(
                packet.Id,
                packet.Message,
                packet.IsAnonymous,
                packet.Holder,
                packet.PaperTheme,
                packet.HasWaypoint,
                packet.WaypointX,
                packet.WaypointZ,
                packet.WaypointTitle,
                packet.WaypointIcon,
                packet.WaypointColor
            );

            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerRepositionMessage(IServerPlayer player, PlayerRepositionMessage packet)
        {
            if (packet == null || string.IsNullOrEmpty(packet.BoardId))
                return;

            NoticeBoardObject noticeBoard = db.GetBoardData(packet.BoardId);
            Message messageData = db.GetMessageById(packet.Id);
            if (noticeBoard == null || messageData == null || messageData.BoardId != packet.BoardId)
                return;

            if (noticeBoard.EnableLegacyBoard == 1 || noticeBoard.EnableManualPin == 0)
                return;

            if (!CanEditMessage(player, noticeBoard, messageData))
                return;

            NoticeBoardBlockEntity blockEntity = sapi.GetNoticeBoardEntity(
                PositionHelper.FromString(noticeBoard.Pos)
            );
            if (!TryAcceptPin(player, noticeBoard, blockEntity, true, packet.PinX, packet.PinY, out _))
                return;

            db.UpdateMessagePin(packet.Id, packet.BoardId, packet.PinX, packet.PinY, packet.PinRotZ, packet.PinLayer);

            RefreshBoardVisuals(packet.BoardId);

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

            RefreshBoardVisuals(packet.BoardId);

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
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditEnableParticles(IServerPlayer player, EditEnableParticles packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableParticles(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditEnableNoticeAging(IServerPlayer player, EditEnableNoticeAging packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableNoticeAging(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditNoticeAgingDays(IServerPlayer player, EditNoticeAgingDays packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            packet.NoticeAgingDays = GameDateFormatter.ClampLifeDays(packet.NoticeAgingDays);
            db.EditNoticeAgingDays(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditEnableParchment(IServerPlayer player, EditEnableParchment packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableParchment(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditEnableManualPin(IServerPlayer player, EditEnableManualPin packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableManualPin(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPersistPaperPins(IServerPlayer player, PersistPaperPins packet)
        {
            if (packet == null || string.IsNullOrEmpty(packet.BoardId))
                return;
            if (packet.Ids == null || packet.Xs == null || packet.Ys == null || packet.Rots == null)
                return;
            if (packet.Ids.Length != packet.Xs.Length || packet.Ids.Length != packet.Ys.Length || packet.Ids.Length != packet.Rots.Length)
                return;
            if (packet.Ids.Length < 1 || packet.Ids.Length > NoticeBoard.Rendering.NoticeBoardPaperLayout.MaxPapers)
                return;

            NoticeBoardObject board = db.GetBoardData(packet.BoardId);
            if (board == null || board.EnableLegacyBoard == 1)
                return;

            for (int i = 0; i < packet.Ids.Length; i++)
            {
                Message message = db.GetMessageById(packet.Ids[i]);
                if (message == null || message.BoardId != packet.BoardId)
                    return;
            }

            if (board.EnableLegacyBoard == 1 || board.EnableManualPin == 0)
                return;

            NoticeBoardBlockEntity be = sapi.GetNoticeBoardEntity(
                PositionHelper.FromString(board.Pos)
            );
            bool isWall = be?.Block?.Variant?["attachment"] == "wall";
            for (int i = 0; i < packet.Ids.Length; i++)
            {
                if (!NoticeBoardPaperLayout.IsTackOnCork(packet.Xs[i], packet.Ys[i], isWall, 1, 0f))
                    return;
            }

            db.PersistPaperPins(
                packet.BoardId,
                packet.Ids,
                packet.Xs,
                packet.Ys,
                packet.Rots,
                overwriteExisting: false
            );
        }

        private void OnPlayerEditBoardOwner(IServerPlayer player, EditBoardOwner packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardOwner(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditBoardName(IServerPlayer player, EditBoardName packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardName(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditBoardFont(IServerPlayer player, EditBoardFont packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardFont(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditBoardFontSize(IServerPlayer player, EditBoardFontSize packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardFontSize(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditMaxPapersOnBoard(IServerPlayer player, EditMaxPapersOnBoard packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            packet.MaxPapersOnBoard = NoticeBoard.Rendering.NoticeBoardPaperLayout.ClampMax(
                packet.MaxPapersOnBoard
            );
            db.EditMaxPapersOnBoard(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditBoardTextSharpness(
            IServerPlayer player,
            EditBoardTextSharpness packet
        )
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            packet.TextSharpness = NoticeBoard.Rendering.PaperSize.ClampSharpness(
                packet.TextSharpness
            );
            db.EditBoardTextSharpness(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditEnableLegacyBoard(IServerPlayer player, EditEnableLegacyBoard packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableLegacyBoard(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditBoardSwayStrength(
            IServerPlayer player,
            EditBoardSwayStrength packet
        )
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            packet.SwayStrength = NoticeBoard.Rendering.PaperSize.ClampSwayStrength(
                packet.SwayStrength
            );
            db.EditBoardSwayStrength(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditBoardTheme(IServerPlayer player, EditBoardTheme packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditBoardTheme(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditEnableProximity(IServerPlayer player, EditEnableProximity packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableProximity(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditProximityChannel(IServerPlayer player, EditProximityChannel packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditProximityChannel(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditProximityDistance(
            IServerPlayer player,
            EditProximityDistance packet
        )
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditProximityDistance(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditEnableDiscord(IServerPlayer player, EditEnableDiscord packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            db.EditEnableDiscord(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerEditDiscordWebhook(IServerPlayer player, EditDiscordWebhook packet)
        {
            if (!CanManageBoard(player, packet.BoardId))
                return;

            if (string.IsNullOrWhiteSpace(packet.WebhookUrl))
            {
                packet.WebhookUrl = "";
            }
            else
            {
                packet.WebhookUrl = packet.WebhookUrl.Trim();
                if (!DiscordNoticeBridge.IsWebhookUrl(packet.WebhookUrl))
                    return;
            }

            db.EditDiscordWebhook(packet);
            RefreshBoardVisuals(packet.BoardId);
        }

        private void OnPlayerSendMessage(IServerPlayer player, PlayerSendMessage packet)
        {
            if (string.IsNullOrWhiteSpace(packet.Message))
                return;

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

            if (!TryAcceptPin(player, noticeBoard, blockEntity, packet))
                return;

            bool parchmentConsumed = false;
            if (blockEntity != null && noticeBoard.EnableParchment == 1)
            {
                for (int i = 3; i >= 0; i--)
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

            packet.Holder = MessageHolder.Clamp(packet.Holder);
            packet.PaperTheme = ThemeManager.Sanitize(packet.PaperTheme);
            packet.WaypointTitle = WaypointPin.SanitizeTitle(packet.WaypointTitle);
            packet.WaypointIcon = WaypointPin.SanitizeIcon(packet.WaypointIcon);
            packet.WaypointColor = WaypointPin.SanitizeColor(packet.WaypointColor);

            packet.PlayerId = player.PlayerUID;
            string display = TheBasicsNick.Resolve(sapi, player, player.PlayerName);
            db.InsertMessage(packet, player.PlayerName);
            db.SetPlayerDisplayName(player.PlayerUID, display);

            db.MarkBoardAsRead(player.PlayerUID, packet.BoardId);

            RefreshBoardVisuals(packet.BoardId);

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

            NotifyNewNotice(player, noticeBoard, packet.IsAnonymous, display);
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

            if (!TryAcceptPin(player, noticeBoard, blockEntity, packet))
                return;

            string documentText = "";
            string authorName = player.PlayerName;
            string pinnerDisplay = TheBasicsNick.Resolve(sapi, player, player.PlayerName);
            bool returnedAnon = false;

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
                returnedAnon = writtenSlot.Itemstack.Attributes.GetInt("noticeboardAnonymous") == 1;
                string signedby = writtenSlot.Itemstack.Attributes.GetString("signedby", null);
                if (returnedAnon)
                    authorName = "";
                else
                    authorName = string.IsNullOrEmpty(signedby) ? pinnerDisplay : signedby;

                if (string.IsNullOrEmpty(documentText))
                    return;

                packet.Holder = MessageHolder.Clamp(writtenSlot.Itemstack.Attributes.GetInt("noticeboardHolder"));
                packet.PaperTheme = ThemeManager.Sanitize(writtenSlot.Itemstack.Attributes.GetString("noticeboardTheme", ""));
                packet.IsAnonymous = returnedAnon ? 1 : 0;

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
            db.AddPlayerToDatabase(player.PlayerUID, player.PlayerName);
            db.SetPlayerDisplayName(player.PlayerUID, pinnerDisplay);
            if (packet.PlayerId != player.PlayerUID)
            {
                IServerPlayer author = sapi.GetPlayerByUID(packet.PlayerId);
                if (author != null)
                    db.SetPlayerDisplayName(
                        author.PlayerUID,
                        TheBasicsNick.Resolve(sapi, author, author.PlayerName));
            }
            db.InsertMessage(packet);

            RefreshBoardVisuals(packet.BoardId);

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

            NotifyNewNotice(player, noticeBoard, packet.IsAnonymous, returnedAnon ? null : pinnerDisplay);
        }

        private void NotifyNewNotice(
            IServerPlayer player,
            NoticeBoardObject noticeBoard,
            int isAnonymous,
            string fallbackName
        )
        {
            string rpName = TheBasicsNick.Resolve(sapi, player, fallbackName);

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
                    string message =
                        $"<strong>{rpName}</strong> {Lang.Get("noticeboard:new-notice-proximity-message")} ({noticeBoard.BoardName})";

                    Proximity.SendLocalChatByPlayer(
                        player,
                        message,
                        noticeBoard.ProximityChannel,
                        (int)noticeBoard.ProximityDistance
                    );
                }
            }

            string discordName = isAnonymous != 1 ? rpName : null;
            DiscordNoticeBridge.TryNotify(sapi, db, noticeBoard, discordName);
        }

        private void OnPlayerRequestAllMessages(IServerPlayer player, RequestAllMessages packet)
        {
            NoticeBoardObject tableProperties = db.GetBoardData(packet.BoardId);
            BlockPos boardPos = tableProperties != null ? PositionHelper.FromString(tableProperties.Pos) : null;
            NoticeBoardBlockEntity be = boardPos != null ? sapi.GetNoticeBoardEntity(boardPos) : null;
            if (be != null)
            {
                be.ExpireAgedNotices();
            }
            else if (tableProperties?.EnableNoticeAging == 1 && sapi.World?.Calendar != null)
            {
                var cal = sapi.World.Calendar;
                List<ExpiredNoticeRow> expired = db.TakeExpiredMessages(
                    packet.BoardId,
                    cal.TotalHours,
                    cal.HoursPerDay,
                    GameDateFormatter.ClampLifeDays(tableProperties.NoticeAgingDays));
                if (expired.Count > 0 && boardPos != null)
                {
                    float rotateYDeg = NoticeBoardBlockEntity.GetRotateYDeg(
                        sapi.World.BlockAccessor.GetBlock(boardPos));
                    NoticeParchment.ExpireAndDrop(sapi, boardPos, packet.BoardId, expired, rotateYDeg);
                }
            }

            List<Message> messages = db.GetAllMessages(packet.BoardId);

            db.MarkBoardAsRead(player.PlayerUID, packet.BoardId);

            ResponseAllMessages responsePacket = new ResponseAllMessages
            {
                Messages = messages,
                BoardProperties = tableProperties ?? new NoticeBoardObject { BoardId = packet.BoardId, EnableManualPin = 1 },
            };

            sapi.Network.GetChannel("noticeboard").SendPacket(responsePacket, player);
        }

        private void OnRequestAllPlayers(IServerPlayer player, RequestAllPlayers packet)
        {
            List<PlayerEntry> players = db.GetAllPlayers();

            sapi.Network.GetChannel("noticeboard")
                .SendPacket(new ResponseAllPlayers { Players = players }, player);
        }

        private void RefreshBoardVisuals(string boardId)
        {
            NoticeBoardObject data = db.GetBoardData(boardId);
            if (data == null || string.IsNullOrEmpty(data.Pos))
                return;

            sapi.GetNoticeBoardEntity(PositionHelper.FromString(data.Pos))?.RefreshPaperVisuals();
        }

        private bool TryAcceptPin(IServerPlayer player, NoticeBoardObject board, NoticeBoardBlockEntity be, PlayerSendMessage packet)
        {
            if (!TryAcceptPin(player, board, be, packet.HasPin, packet.PinX, packet.PinY, out bool hasPin))
                return false;
            packet.HasPin = hasPin;
            return true;
        }

        private bool TryAcceptPin(IServerPlayer player, NoticeBoardObject board, NoticeBoardBlockEntity be, PlayerSendDocument packet)
        {
            if (!TryAcceptPin(player, board, be, packet.HasPin, packet.PinX, packet.PinY, out bool hasPin))
                return false;
            packet.HasPin = hasPin;
            return true;
        }

        private bool TryAcceptPin(
            IServerPlayer player,
            NoticeBoardObject board,
            NoticeBoardBlockEntity be,
            bool requested,
            float pinX,
            float pinY,
            out bool hasPin
        )
        {
            hasPin = requested;
            if (board.EnableManualPin == 0 || board.EnableLegacyBoard == 1)
            {
                hasPin = false;
                return true;
            }

            bool isWall = be?.Block?.Variant?["attachment"] == "wall";
            if (!requested || !NoticeBoardPaperLayout.IsTackOnCork(pinX, pinY, isWall, 1, 0f))
            {
                player.SendIngameError("pin_invalid", Lang.Get("noticeboard:pin-placement-invalid"));
                return false;
            }

            hasPin = true;
            return true;
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
