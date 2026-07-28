using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using NoticeBoard.Packets;

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
        var player = new PlayerEntry();

        try
        {
            string query =
                @"SELECT p.playerId, p.playerName FROM players p WHERE p.playerId = @playerId";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerId", playerId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
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
                .Logger.Error($"[NoticeBoard] Could not get player by id: {e.Message}");
        }

        return player;
    }

    public PlayerEntry GetPlayerByName(string playerName)
    {
        SQLiteDatabase.TryOpenConnection();
        var player = new PlayerEntry();

        try
        {
            string query =
                @"SELECT p.playerId, p.playerName FROM players p WHERE p.playerName = @playerName";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerName", playerName);

            using var reader = command.ExecuteReader();
            while (reader.Read())
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
                .Logger.Error($"[NoticeBoard] Could not get player by name: {e.Message}");
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

            string query =
                @"
                INSERT OR IGNORE INTO noticeBoard (boardId, ownerPlayerId, pos, permissionMode) 
                VALUES (@boardId, @playerId, @pos, 0)";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@playerId", packet.PlayerId);
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

    public void DeleteNoticeBoard(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                @"
                DELETE FROM playerBoardReads WHERE boardId = @boardId;
                DELETE FROM messages WHERE boardId = @boardId;
                DELETE FROM noticeBoard WHERE boardId = @boardId;";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not delete noticeboard: {e.Message}");
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
                       nb.enableProximity, nb.proximityChannel, nb.proximityDistance
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
                INSERT INTO messages (boardId, senderPlayerId, message, totalHours, isAnonymous ) 
                VALUES (@boardId, @playerId, @message, @totalHours, @isAnonymous)";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@playerId", packet.PlayerId);
            command.Parameters.AddWithValue("@message", packet.Message);
            command.Parameters.AddWithValue("@totalHours", packet.TotalHours);
            command.Parameters.AddWithValue("@isAnonymous", packet.IsAnonymous);

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
                INSERT INTO messages (boardId, senderPlayerId, message, totalHours ) 
                VALUES (@boardId, @playerId, @message, @totalHours)";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@playerId", packet.PlayerId);
            command.Parameters.AddWithValue("@message", packet.Document);
            command.Parameters.AddWithValue("@totalHours", packet.TotalHours);
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
        var message = new Message();

        try
        {
            string query =
                @"
                SELECT m.id, m.message, m.senderPlayerId, m.totalHours, p.playerName, m.createdAt
                FROM messages m
                LEFT JOIN players p ON p.playerId = m.senderPlayerId
                WHERE m.id = @messageId
                ";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@messageId", messageId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                message.Id = reader.GetInt32(0);
                message.Text = reader.GetString(1);
                message.PlayerId = reader.GetString(2);
                message.TotalHours = reader.GetDouble(3);
                message.PlayerName = reader.IsDBNull(4) ? "Unknown" : reader.GetString(4);
                message.CreatedAt = reader.GetDateTime(5);
            }
            return message;
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not get message: {e.Message}");
            return null;
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
                SELECT m.id, m.message, m.senderPlayerId, m.totalHours, p.playerName, m.createdAt, m.isAnonymous
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

    public void EditMessageById(int id, string message, int isAnonymous)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE messages SET message = @message, updatedAt = CURRENT_TIMESTAMP, isAnonymous = @isAnonymous WHERE id = @id";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@message", message);
            command.Parameters.AddWithValue("@isAnonymous", isAnonymous);

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
