using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using NoticeBoard;
using NoticeBoard.Packets;
using NoticeBoard.Rendering;
using NoticeBoard.Utils;

namespace NoticeBoard.Database;

public class SQLiteHandler
{
    private readonly SQLiteDatabase SQLiteDatabase = NoticeBoardModSystem
        .getModInstance()
        .getDatabaseHandler();
    private readonly SqliteConnection SQLiteConnection = NoticeBoardModSystem
        .getModInstance()
        .getDatabaseHandler()
        .getSQLiteConnection();

    #region Players
    public void AddPlayerToDatabase(string playerId, string playerName)
    {
        try
        {
            string query =
                "INSERT OR IGNORE INTO players (playerId, playerName) VALUES (@playerId, @playerName)";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerId", playerId);
            command.Parameters.AddWithValue("@playerName", playerName);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"Couldnt insertPlayerQuery message: {e.Message}");
        }
    }

    public void SetPlayerDisplayName(string playerId, string displayName)
    {
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrWhiteSpace(displayName))
            return;
        SQLiteDatabase.TryOpenConnection();
        using var command = new SqliteCommand(
            "UPDATE players SET displayName = @displayName WHERE playerId = @playerId",
            SQLiteConnection);
        command.Parameters.AddWithValue("@playerId", playerId);
        command.Parameters.AddWithValue("@displayName", displayName.Trim());
        command.ExecuteNonQuery();
    }

    public List<PlayerEntry> GetAllPlayers()
    {
        SQLiteDatabase.TryOpenConnection();
        var players = new List<PlayerEntry>();

        try
        {
            string query = @"SELECT p.playerId, p.playerName FROM players p";

            using var command = new SqliteCommand(query, SQLiteConnection);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                players.Add(
                    new PlayerEntry
                    {
                        PlayerUID = reader.GetString(0),
                        PlayerName = reader.GetString(1),
                    }
                );
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get messages: {e.Message}");
        }

        return players;
    }

    public PlayerEntry GetPlayerBytId(string playerId)
    {
        SQLiteDatabase.TryOpenConnection();
        PlayerEntry player = null;

        try
        {
            string query =
                @"SELECT p.playerId, p.playerName FROM players p WHERE p.playerId = @playerId";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerId", playerId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                player = new PlayerEntry
                {
                    PlayerUID = reader.GetString(0),
                    PlayerName = reader.GetString(1),
                };
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get player by id: {e}");
        }

        return player;
    }

    public PlayerEntry GetPlayerByName(string playerName)
    {
        SQLiteDatabase.TryOpenConnection();
        PlayerEntry player = null;

        try
        {
            string query =
                @"SELECT p.playerId, p.playerName FROM players p WHERE p.playerName = @playerName";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerName", playerName);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                player = new PlayerEntry
                {
                    PlayerUID = reader.GetString(0),
                    PlayerName = reader.GetString(1),
                };
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get player by name: {e}");
        }

        return player;
    }

    #endregion

    #region NoticeBoard
    public void CreateNoticeBoard(PlayerCreateNoticeBoard packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string playerName = "Unknown";
            var player = NoticeBoardModSystem.getSAPI()?.World?.PlayerByUid(packet.PlayerId);
            if (player != null)
                playerName = player.PlayerName;

            AddPlayerToDatabase(packet.PlayerId, playerName);

            int lifeDays = GameDateFormatter.DefaultLifeDays(NoticeBoardModSystem.getSAPI()?.World?.Calendar);
            string query =
                @"
                INSERT OR IGNORE INTO noticeBoard (boardId, ownerPlayerId, pos, permissionMode, enableManualPin, noticeAgingDays)
                VALUES (@boardId, @playerId, @pos, 0, 1, @noticeAgingDays)";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@playerId", packet.PlayerId);
            command.Parameters.AddWithValue("@pos", packet.Pos);
            command.Parameters.AddWithValue("@noticeAgingDays", lifeDays);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not create noticeboard: {e.Message}");
        }
    }

    public void UpdateNoticeBoard(PlayerCreateNoticeBoard packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE noticeBoard SET pos = @pos WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@pos", packet.Pos);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not create noticeboard: {e.Message}");
        }
    }

    public void SyncMessagePinsToAttachment(string boardId, bool isWall)
    {
        if (string.IsNullOrEmpty(boardId))
            return;

        string now = isWall ? "wall" : "ground";
        string stored = GetCorkAttachment(boardId);
        bool? fromWall;
        if (stored == "wall")
            fromWall = true;
        else if (stored == "ground")
            fromWall = false;
        else
            fromWall = NoticeBoardPaperLayout.InferCorkIsWall(GetMessagePinYs(boardId));

        if (fromWall.HasValue)
        {
            float dy = NoticeBoardPaperLayout.PinYDelta(fromWall.Value, isWall);
            if (dy != 0f)
                ShiftMessagePinY(boardId, dy);
        }

        SetCorkAttachment(boardId, now);
    }

    private string GetCorkAttachment(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "SELECT corkAttachment FROM noticeBoard WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            object value = command.ExecuteScalar();
            if (value == null || value == DBNull.Value)
                return null;
            string stored = value as string;
            return string.IsNullOrEmpty(stored) ? null : stored;
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get cork attachment: {e.Message}");
            return null;
        }
    }

    private void SetCorkAttachment(string boardId, string attachment)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE noticeBoard SET corkAttachment = @a WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            command.Parameters.AddWithValue("@a", attachment);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not set cork attachment: {e.Message}");
        }
    }

    private float[] GetMessagePinYs(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();
        var ys = new List<float>();

        try
        {
            string query = "SELECT pinY FROM messages WHERE boardId = @boardId AND pinY IS NOT NULL";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0))
                    ys.Add(reader.GetFloat(0));
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get message pinY values: {e.Message}");
        }

        return ys.ToArray();
    }

    private void ShiftMessagePinY(string boardId, float dy)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE messages SET pinY = pinY + @dy WHERE boardId = @boardId AND pinY IS NOT NULL";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            command.Parameters.AddWithValue("@dy", dy);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not shift message pinY: {e.Message}");
        }
    }

    public void EditPermissionMode(EditPermissionMode packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE noticeBoard SET permissionMode = @permissionMode WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@permissionMode", packet.PermissionMode);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update permission mode: {e.Message}");
        }
    }

    public void EditEnableParticles(EditEnableParticles packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET enableParticles = @enableParticles WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableParticles", packet.EnableParticles ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update lock status: {e.Message}");
        }
    }

    public void EditEnableNoticeAging(EditEnableNoticeAging packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET enableNoticeAging = @enableNoticeAging WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableNoticeAging", packet.EnableNoticeAging ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update notice aging: {e.Message}");
        }
    }

    public void EditNoticeAgingDays(EditNoticeAgingDays packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET noticeAgingDays = @noticeAgingDays WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@noticeAgingDays", GameDateFormatter.ClampLifeDays(packet.NoticeAgingDays));
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update notice aging days: {e.Message}");
        }
    }

    public void EditEnableParchment(EditEnableParchment packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET enableParchment = @enableParchment WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableParchment", packet.EnableParchment ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update lock status: {e.Message}");
        }
    }

    public void EditEnableManualPin(EditEnableManualPin packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET enableManualPin = @enableManualPin WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableManualPin", packet.EnableManualPin ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update manual pin setting: {e.Message}");
        }
    }

    public void EditEnableLegacyBoard(EditEnableLegacyBoard packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET enableLegacyBoard = @enableLegacyBoard WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableLegacyBoard", packet.EnableLegacyBoard ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update legacy board setting: {e.Message}");
        }
    }

    public void EditBoardSwayStrength(EditBoardSwayStrength packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET swayStrength = @swayStrength WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@swayStrength", packet.SwayStrength);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update sway strength: {e.Message}");
        }
    }

    public void EditBoardName(EditBoardName packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE noticeBoard SET boardName = @boardName WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@boardName", packet.BoardName);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update board name: {e.Message}");
        }
    }

    public void EditBoardOwner(EditBoardOwner packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET ownerPlayerId = @ownerId WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@ownerId", packet.NewOwnerUid);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update board owner: {e.Message}");
        }
    }

    public void EditBoardFont(EditBoardFont packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE noticeBoard SET boardFont = @boardFont WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@boardFont", packet.BoardFont);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update board font: {e.Message}");
        }
    }

    public void EditBoardFontSize(EditBoardFontSize packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET boardFontSize = @boardFontSize WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@boardFontSize", packet.BoardFontSize);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update board font size: {e.Message}");
        }
    }

    public void EditMaxPapersOnBoard(EditMaxPapersOnBoard packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET maxPapersOnBoard = @maxPapersOnBoard WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@maxPapersOnBoard", packet.MaxPapersOnBoard);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update max papers: {e.Message}");
        }
    }

    public void EditBoardTextSharpness(EditBoardTextSharpness packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET textSharpness = @textSharpness WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@textSharpness", packet.TextSharpness);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update text sharpness: {e.Message}");
        }
    }

    public void EditBoardTheme(EditBoardTheme packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET boardTheme = @boardTheme WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@boardTheme", packet.BoardTheme);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update board name: {e.Message}");
        }
    }

    public void EditEnableProximity(EditEnableProximity packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET enableProximity = @enableProximity WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableProximity", packet.EnableProximity ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update proximity toggle: {e.Message}");
        }
    }

    public void EditProximityChannel(EditProximityChannel packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET proximityChannel = @channel WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@channel", packet.ChannelName ?? "");
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update proximity channel: {e.Message}");
        }
    }

    public void EditProximityDistance(EditProximityDistance packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET proximityDistance = @distance WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@distance", packet.Distance);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update proximity distance: {e.Message}");
        }
    }

    public void EditEnableDiscord(EditEnableDiscord packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE noticeBoard SET enableDiscord = @enableDiscord WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableDiscord", packet.EnableDiscord ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update discord toggle: {e.Message}");
        }
    }

    public string GetDiscordWebhook(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();
        try
        {
            using var command = new SqliteCommand(
                "SELECT discordWebhook FROM noticeBoard WHERE boardId = @boardId",
                SQLiteConnection
            );
            command.Parameters.AddWithValue("@boardId", boardId);
            object raw = command.ExecuteScalar();
            return raw as string;
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                ?.Logger.Error($"[NoticeBoard] Could not get discord webhook: {e.Message}");
            return null;
        }
    }

    public void EditDiscordWebhook(EditDiscordWebhook packet)
    {
        SQLiteDatabase.TryOpenConnection();
        try
        {
            using var command = new SqliteCommand(
                "UPDATE noticeBoard SET discordWebhook = @url WHERE boardId = @boardId",
                SQLiteConnection
            );
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@url", packet.WebhookUrl ?? "");
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                ?.Logger.Error($"[NoticeBoard] Could not update discord webhook: {e.Message}");
        }
    }

    public NoticeBoardObject GetBoardData(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                @"
                SELECT nb.boardId, nb.boardName, nb.boardFont, nb.boardFontSize, nb.boardTheme, nb.ownerPlayerId,
                       nb.pos, nb.permissionMode, nb.enableParticles, nb.enableParchment, p.playerName,
                       nb.enableProximity, nb.proximityChannel, nb.proximityDistance, nb.maxPapersOnBoard, nb.enableLegacyBoard,
                       nb.textSharpness, nb.swayStrength, nb.enableManualPin, nb.enableNoticeAging, nb.noticeAgingDays,
                       nb.enableDiscord,
                       CASE WHEN nb.discordWebhook IS NOT NULL AND TRIM(nb.discordWebhook) != '' THEN 1 ELSE 0 END
                FROM noticeBoard nb
                LEFT JOIN players p ON p.playerId = nb.ownerPlayerId
                WHERE nb.boardId = @boardId";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            return new NoticeBoardObject
            {
                BoardId = reader.GetString(0),
                BoardName = reader.GetString(1),
                BoardFont = reader.GetString(2),
                BoardFontSize = reader.GetFloat(3),
                BoardTheme = reader.GetString(4),
                PlayerId = reader.GetString(5),
                Pos = reader.GetString(6),
                PermissionMode = reader.GetInt16(7),
                EnableParticles = reader.GetInt16(8),
                EnableParchment = reader.GetInt16(9),
                PlayerName = reader.IsDBNull(10) ? "Unknown" : reader.GetString(10),
                EnableProximity = reader.IsDBNull(11) ? 0 : reader.GetInt16(11),
                ProximityChannel = reader.IsDBNull(12) ? "Proximity" : reader.GetString(12),
                ProximityDistance = reader.IsDBNull(13) ? 100 : reader.GetInt16(13),
                MaxPapersOnBoard = reader.IsDBNull(14) ? 20 : reader.GetInt32(14),
                EnableLegacyBoard = reader.IsDBNull(15) ? 0 : reader.GetInt16(15),
                TextSharpness = reader.IsDBNull(16)
                    ? PaperSize.DefaultTextSharpness
                    : reader.GetInt32(16),
                SwayStrength = reader.IsDBNull(17)
                    ? PaperSize.DefaultSwayStrength
                    : reader.GetInt32(17),
                EnableManualPin = reader.IsDBNull(18) ? 1 : reader.GetInt16(18),
                EnableNoticeAging = reader.IsDBNull(19) ? 0 : reader.GetInt16(19),
                NoticeAgingDays = reader.IsDBNull(20)
                    ? GameDateFormatter.DefaultLifeDays(NoticeBoardModSystem.getSAPI()?.World?.Calendar)
                    : GameDateFormatter.ClampLifeDays(reader.GetInt32(20)),
                EnableDiscord = reader.IsDBNull(21) ? 0 : reader.GetInt32(21),
                HasDiscordWebhook = reader.IsDBNull(22) ? 0 : reader.GetInt32(22),
            };
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get board data: {e.Message}");
            return null;
        }
    }
    #endregion

    #region Messages
    public void InsertMessage(PlayerSendMessage packet, string playerName)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            AddPlayerToDatabase(packet.PlayerId, playerName);

            string query =
                @"
                INSERT INTO messages (boardId, senderPlayerId, message, totalHours, isAnonymous, holder, paperTheme, paperSeed, pinX, pinY, pinRotZ, pinLayer, waypointX, waypointZ, waypointTitle, waypointIcon, waypointColor )
                VALUES (@boardId, @playerId, @message, @totalHours, @isAnonymous, @holder, @paperTheme, @paperSeed, @pinX, @pinY, @pinRotZ, @pinLayer, @waypointX, @waypointZ, @waypointTitle, @waypointIcon, @waypointColor)";

            using var command = new SqliteCommand(query, SQLiteConnection);
            int seed = packet.PaperSeed != 0 ? packet.PaperSeed : MessageVisualData.NewPaperSeed();
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@playerId", packet.PlayerId);
            command.Parameters.AddWithValue("@message", packet.Message);
            command.Parameters.AddWithValue("@totalHours", packet.TotalHours);
            command.Parameters.AddWithValue("@isAnonymous", packet.IsAnonymous);
            command.Parameters.AddWithValue("@holder", packet.Holder);
            command.Parameters.AddWithValue("@paperTheme", packet.PaperTheme ?? "");
            command.Parameters.AddWithValue("@paperSeed", seed);
            command.Parameters.AddWithValue("@pinX", packet.HasPin ? packet.PinX : DBNull.Value);
            command.Parameters.AddWithValue("@pinY", packet.HasPin ? packet.PinY : DBNull.Value);
            command.Parameters.AddWithValue("@pinRotZ", packet.HasPin ? packet.PinRotZ : DBNull.Value);
            command.Parameters.AddWithValue("@pinLayer", packet.HasPin ? NoticeBoardPaperLayout.ClampPinLayer(packet.PinLayer) : 0);
            command.Parameters.AddWithValue("@waypointX", packet.HasWaypoint ? packet.WaypointX : DBNull.Value);
            command.Parameters.AddWithValue("@waypointZ", packet.HasWaypoint ? packet.WaypointZ : DBNull.Value);
            command.Parameters.AddWithValue("@waypointTitle", packet.HasWaypoint ? (packet.WaypointTitle ?? "") : "");
            command.Parameters.AddWithValue("@waypointIcon", packet.HasWaypoint ? WaypointPin.SanitizeIcon(packet.WaypointIcon) : "");
            command.Parameters.AddWithValue("@waypointColor", packet.HasWaypoint ? WaypointPin.SanitizeColor(packet.WaypointColor) : "");

            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not insert message: {e.Message}");
        }
    }

    public void InsertMessage(PlayerSendDocument packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                @"
                INSERT INTO messages (boardId, senderPlayerId, message, totalHours, isAnonymous, holder, paperTheme, paperSeed, pinX, pinY, pinRotZ, pinLayer )
                VALUES (@boardId, @playerId, @message, @totalHours, @isAnonymous, @holder, @paperTheme, @paperSeed, @pinX, @pinY, @pinRotZ, @pinLayer)";

            using var command = new SqliteCommand(query, SQLiteConnection);
            int seed = packet.PaperSeed != 0 ? packet.PaperSeed : MessageVisualData.NewPaperSeed();
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@playerId", packet.PlayerId);
            command.Parameters.AddWithValue("@message", packet.Document);
            command.Parameters.AddWithValue("@totalHours", packet.TotalHours);
            command.Parameters.AddWithValue("@isAnonymous", packet.IsAnonymous);
            command.Parameters.AddWithValue("@holder", packet.Holder);
            command.Parameters.AddWithValue("@paperTheme", packet.PaperTheme ?? "");
            command.Parameters.AddWithValue("@paperSeed", seed);
            command.Parameters.AddWithValue("@pinX", packet.HasPin ? packet.PinX : DBNull.Value);
            command.Parameters.AddWithValue("@pinY", packet.HasPin ? packet.PinY : DBNull.Value);
            command.Parameters.AddWithValue("@pinRotZ", packet.HasPin ? packet.PinRotZ : DBNull.Value);
            command.Parameters.AddWithValue("@pinLayer", packet.HasPin ? NoticeBoardPaperLayout.ClampPinLayer(packet.PinLayer) : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not insert message: {e.Message}");
        }
    }

    public Message GetMessageById(int messageId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                @"
                SELECT m.id, m.message, m.senderPlayerId, m.totalHours, COALESCE(NULLIF(p.displayName, ''), p.playerName), m.createdAt, m.holder, m.paperTheme, m.boardId, m.paperSeed, m.isAnonymous
                FROM messages m
                LEFT JOIN players p ON p.playerId = m.senderPlayerId
                WHERE m.id = @messageId
                ";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@messageId", messageId);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;

            return new Message
            {
                Id = reader.GetInt32(0),
                Text = reader.GetString(1),
                PlayerId = reader.GetString(2),
                TotalHours = reader.GetDouble(3),
                PlayerName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                Holder = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                PaperTheme = reader.IsDBNull(7) ? "" : reader.GetString(7),
                BoardId = reader.IsDBNull(8) ? null : reader.GetString(8),
                PaperSeed = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
                IsAnonymous = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
            };
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get message: {e}");
            return null;
        }
    }

    public void PersistPaperPins(string boardId, int[] ids, float[] xs, float[] ys, float[] rots, bool overwriteExisting)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            using var tx = SQLiteConnection.BeginTransaction();
            string sql = overwriteExisting
                ? "UPDATE messages SET pinX = @x, pinY = @y, pinRotZ = @r WHERE id = @id AND boardId = @boardId"
                : "UPDATE messages SET pinX = @x, pinY = @y, pinRotZ = @r WHERE id = @id AND boardId = @boardId AND pinX IS NULL AND pinY IS NULL";
            for (int i = 0; i < ids.Length; i++)
            {
                using var command = new SqliteCommand(sql, SQLiteConnection, tx);
                command.Parameters.AddWithValue("@x", xs[i]);
                command.Parameters.AddWithValue("@y", ys[i]);
                command.Parameters.AddWithValue("@r", rots[i]);
                command.Parameters.AddWithValue("@id", ids[i]);
                command.Parameters.AddWithValue("@boardId", boardId);
                command.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not persist paper pins: {e.Message}");
        }
    }

    public List<ExpiredNoticeRow> TakeExpiredMessages(string boardId, double nowHours, float hoursPerDay, int lifeDays)
    {
        SQLiteDatabase.TryOpenConnection();
        var rows = new List<ExpiredNoticeRow>();
        try
        {
            double cutoff = nowHours - GameDateFormatter.LifeHours(hoursPerDay, lifeDays);
            using var tx = SQLiteConnection.BeginTransaction();
            using (var command = new SqliteCommand(
                @"SELECT m.id, m.message, m.totalHours, m.isAnonymous, COALESCE(NULLIF(p.displayName, ''), p.playerName), m.holder, m.paperTheme, m.pinX, m.pinY, m.pinRotZ, m.pinLayer, m.paperSeed, m.senderPlayerId
                FROM messages m
                LEFT JOIN players p ON p.playerId = m.senderPlayerId
                WHERE m.boardId = @boardId AND m.totalHours <= @cutoff",
                SQLiteConnection,
                tx))
            {
                command.Parameters.AddWithValue("@boardId", boardId);
                command.Parameters.AddWithValue("@cutoff", cutoff);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add(
                        new ExpiredNoticeRow
                        {
                            Id = reader.GetInt32(0),
                            Text = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            TotalHours = reader.GetDouble(2),
                            IsAnonymous = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                            PlayerName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4),
                            Holder = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                            PaperTheme = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            HasPin = !reader.IsDBNull(7) && !reader.IsDBNull(8),
                            PinX = reader.IsDBNull(7) ? 0 : reader.GetFloat(7),
                            PinY = reader.IsDBNull(8) ? 0 : reader.GetFloat(8),
                            PinRotZ = reader.IsDBNull(9) ? 0 : reader.GetFloat(9),
                            PinLayer = reader.IsDBNull(10) ? 0 : NoticeBoardPaperLayout.ClampPinLayer(reader.GetInt32(10)),
                            PaperSeed = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                            PlayerId = reader.IsDBNull(12) ? null : reader.GetString(12),
                        }
                    );
                }
            }

            if (rows.Count > 0)
            {
                var idParams = new List<string>(rows.Count);
                for (int i = 0; i < rows.Count; i++)
                    idParams.Add($"@id{i}");
                using var del = new SqliteCommand(
                    $"DELETE FROM messages WHERE boardId = @boardId AND id IN ({string.Join(",", idParams)})",
                    SQLiteConnection,
                    tx);
                del.Parameters.AddWithValue("@boardId", boardId);
                for (int i = 0; i < rows.Count; i++)
                    del.Parameters.AddWithValue(idParams[i], rows[i].Id);
                del.ExecuteNonQuery();
            }

            tx.Commit();
            return rows;
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not take expired messages: {e.Message}");
            return new List<ExpiredNoticeRow>();
        }
    }

    public List<Message> GetAllMessages(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();
        var messages = new List<Message>();

        try
        {
            string query =
                @"
                SELECT m.id, m.message, m.senderPlayerId, m.totalHours, COALESCE(NULLIF(p.displayName, ''), p.playerName), m.createdAt, m.isAnonymous, m.holder, m.paperTheme, m.waypointX, m.waypointZ, m.paperSeed, m.waypointTitle, m.waypointIcon, m.waypointColor
                FROM messages m
                LEFT JOIN players p ON p.playerId = m.senderPlayerId
                WHERE m.boardId = @boardId
                ORDER BY m.updatedAt DESC, m.id DESC";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(
                    new Message
                    {
                        Id = reader.GetInt32(0),
                        Text = reader.GetString(1),
                        PlayerId = reader.GetString(2),
                        TotalHours = reader.GetDouble(3),
                        PlayerName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5),
                        IsAnonymous = reader.GetInt32(6),
                        Holder = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                        PaperTheme = reader.IsDBNull(8) ? "" : reader.GetString(8),
                        HasWaypoint = !reader.IsDBNull(9) && !reader.IsDBNull(10),
                        WaypointX = reader.IsDBNull(9) ? 0 : reader.GetFloat(9),
                        WaypointZ = reader.IsDBNull(10) ? 0 : reader.GetFloat(10),
                        PaperSeed = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                        WaypointTitle = reader.IsDBNull(12) ? "" : reader.GetString(12),
                        WaypointIcon = reader.IsDBNull(13) ? "" : reader.GetString(13),
                        WaypointColor = reader.IsDBNull(14) ? "" : reader.GetString(14),
                    }
                );
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get messages: {e.Message}");
        }

        return messages;
    }

    public int[] GetRecentMessageIds(string boardId, int limit)
    {
        SQLiteDatabase.TryOpenConnection();
        var ids = new List<int>();

        try
        {
            string query =
                @"SELECT id FROM messages
                  WHERE boardId = @boardId
                  ORDER BY updatedAt DESC, id DESC
                  LIMIT @limit";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            command.Parameters.AddWithValue("@limit", Math.Max(0, limit));

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get recent message ids: {e.Message}");
        }

        return ids.ToArray();
    }

    public Dictionary<int, MessageVisualData> GetMessageTextsByIds(string boardId, int[] ids)
    {
        SQLiteDatabase.TryOpenConnection();
        var result = new Dictionary<int, MessageVisualData>();

        if (ids == null || ids.Length == 0 || string.IsNullOrEmpty(boardId))
            return result;

        try
        {
            var parameters = new List<string>();
            for (int i = 0; i < ids.Length; i++)
            {
                parameters.Add($"@id{i}");
            }

            string query = $@"
                SELECT m.id, m.message, m.totalHours, m.isAnonymous, COALESCE(NULLIF(p.displayName, ''), p.playerName), m.holder, m.paperTheme, m.pinX, m.pinY, m.pinRotZ, m.pinLayer, m.paperSeed
                FROM messages m
                LEFT JOIN players p ON p.playerId = m.senderPlayerId
                WHERE m.boardId = @boardId AND m.id IN ({string.Join(",", parameters)})
            ";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            for (int i = 0; i < ids.Length; i++)
            {
                command.Parameters.AddWithValue(parameters[i], ids[i]);
            }

            var calendar = NoticeBoardModSystem.getSAPI().World.Calendar;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                string message = reader.GetString(1);
                double totalHours = reader.GetDouble(2);
                string date = GameDateFormatter.FormatImmersiveDate(calendar.HoursPerDay, calendar.DaysPerMonth, totalHours, includeTime: false);
                int isAnon = reader.GetInt32(3);
                string playerName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4);
                if (isAnon == 1) playerName = "";

                int holder = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                string paperTheme = reader.IsDBNull(6) ? "" : reader.GetString(6);

                result[id] = new MessageVisualData(message, playerName, date, holder, paperTheme)
                {
                    HasPin = !reader.IsDBNull(7) && !reader.IsDBNull(8),
                    PinX = reader.IsDBNull(7) ? 0 : reader.GetFloat(7),
                    PinY = reader.IsDBNull(8) ? 0 : reader.GetFloat(8),
                    HasPinRotZ = !reader.IsDBNull(9),
                    PinRotZ = reader.IsDBNull(9) ? 0 : reader.GetFloat(9),
                    PinLayer = reader.IsDBNull(10) ? 0 : NoticeBoardPaperLayout.ClampPinLayer(reader.GetInt32(10)),
                    TotalHours = totalHours,
                    PaperSeed = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
                };
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                ?.Logger.Error($"[NoticeBoard] Could not get message texts: {e.Message}");
        }

        return result;
    }

    public int CountMessageElementsByBoardId(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "SELECT COUNT(*) FROM messages WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            return Convert.ToInt32(command.ExecuteScalar());
        }
        catch
        {
            return 0;
        }
    }

    public void DeleteMessage(int id)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "DELETE FROM messages WHERE id = @id";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not delete message: {e.Message}");
        }
    }

    public void UpdateMessagePin(int id, string boardId, float pinX, float pinY, float pinRotZ, int pinLayer)
    {
        SQLiteDatabase.TryOpenConnection();
        try
        {
            using var command = new SqliteCommand(
                "UPDATE messages SET pinX = @x, pinY = @y, pinRotZ = @r, pinLayer = @layer WHERE id = @id AND boardId = @boardId",
                SQLiteConnection
            );
            command.Parameters.AddWithValue("@x", pinX);
            command.Parameters.AddWithValue("@y", pinY);
            command.Parameters.AddWithValue("@r", pinRotZ);
            command.Parameters.AddWithValue("@layer", NoticeBoardPaperLayout.ClampPinLayer(pinLayer));
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@boardId", boardId);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not update message pin: {e.Message}");
        }
    }

    public void EditMessageById(int id, string message, int isAnonymous, int holder, string paperTheme, bool hasWaypoint, float waypointX, float waypointZ, string waypointTitle, string waypointIcon, string waypointColor)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = hasWaypoint
                ? "UPDATE messages SET message = @message, updatedAt = CURRENT_TIMESTAMP, isAnonymous = @isAnonymous, holder = @holder, paperTheme = @paperTheme, waypointX = @waypointX, waypointZ = @waypointZ, waypointTitle = @waypointTitle, waypointIcon = @waypointIcon, waypointColor = @waypointColor WHERE id = @id"
                : "UPDATE messages SET message = @message, updatedAt = CURRENT_TIMESTAMP, isAnonymous = @isAnonymous, holder = @holder, paperTheme = @paperTheme WHERE id = @id";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@message", message);
            command.Parameters.AddWithValue("@isAnonymous", isAnonymous);
            command.Parameters.AddWithValue("@holder", holder);
            command.Parameters.AddWithValue("@paperTheme", paperTheme ?? "");
            if (hasWaypoint)
            {
                command.Parameters.AddWithValue("@waypointX", waypointX);
                command.Parameters.AddWithValue("@waypointZ", waypointZ);
                command.Parameters.AddWithValue("@waypointTitle", waypointTitle ?? "");
                command.Parameters.AddWithValue("@waypointIcon", waypointIcon ?? "");
                command.Parameters.AddWithValue("@waypointColor", waypointColor ?? "");
            }

            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not edit message: {e.Message}");
        }
    }

    public void BumpMessageById(int id)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE messages SET updatedAt = CURRENT_TIMESTAMP WHERE id = @id";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not bump message: {e.Message}");
        }
    }
    #endregion

    #region Read Tracking
    public void MarkBoardAsRead(string playerId, string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                @"
            INSERT INTO playerBoardReads (playerId, boardId, lastReadMessageId, lastReadAt)
            VALUES (@playerId, @boardId, 
                    COALESCE((SELECT MAX(id) FROM messages WHERE boardId = @boardId), 0),
                    CURRENT_TIMESTAMP)
            ON CONFLICT(playerId, boardId) 
            DO UPDATE SET 
                lastReadMessageId = COALESCE((SELECT MAX(id) FROM messages WHERE boardId = @boardId), 0),
                lastReadAt = CURRENT_TIMESTAMP;";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerId", playerId);
            command.Parameters.AddWithValue("@boardId", boardId);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not mark board as read: {e.Message}");
        }
    }

    public bool HasUnreadMessages(string playerId, string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                @"
            SELECT 
                COALESCE((SELECT MAX(id) FROM messages WHERE boardId = @boardId), 0)
                > 
                COALESCE((SELECT lastReadMessageId FROM playerBoardReads WHERE boardId = @boardId AND playerId = @playerId), 0);";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerId", playerId);
            command.Parameters.AddWithValue("@boardId", boardId);

            var result = command.ExecuteScalar();

            return result != null && Convert.ToInt64(result) > 0;
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] HasUnreadMessages error: {e.Message}");
            return false;
        }
    }
    #endregion
}
