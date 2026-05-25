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
    #endregion

    #region NoticeBoard
    public void CreateNoticeBoard(PlayerCreateNoticeBoard packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                @"
                INSERT OR IGNORE INTO noticeBoard (boardId, ownerPlayerId, pos, isLocked) 
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

    public void EditIsLocked(EditIsLocked packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE noticeBoard SET isLocked = @isLocked WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@isLocked", packet.IsLocked ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not update lock status: {e.Message}");
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
        var noticeBoard = new NoticeBoardObject();

        try
        {
            string query =
                @"
                SELECT nb.boardId, nb.boardName, nb.boardFont, nb.ownerPlayerId, nb.pos, nb.isLocked, nb.enableParticles, nb.enableParchment, p.playerName,
                       nb.enableProximity, nb.proximityChannel, nb.proximityDistance
                FROM noticeBoard nb
                LEFT JOIN players p ON p.playerId = nb.ownerPlayerId
                WHERE nb.boardId = @boardId";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                noticeBoard.BoardId = reader.GetString(0);
                noticeBoard.BoardName = reader.GetString(1);
                noticeBoard.BoardFont = reader.GetString(2);
                noticeBoard.PlayerId = reader.GetString(3);
                noticeBoard.Pos = reader.GetString(4);
                noticeBoard.IsLocked = reader.GetInt16(5);
                noticeBoard.EnableParticles = reader.GetInt16(6);
                noticeBoard.EnableParchment = reader.GetInt16(7);
                noticeBoard.PlayerName = reader.IsDBNull(8) ? "Unknown" : reader.GetString(8);
                noticeBoard.EnableProximity = reader.IsDBNull(9) ? 0 : reader.GetInt16(9);
                noticeBoard.ProximityChannel = reader.IsDBNull(10)
                    ? "Proximity"
                    : reader.GetString(10);
                noticeBoard.ProximityDistance = reader.IsDBNull(11) ? 100 : reader.GetInt16(11);
            }

            return noticeBoard;
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
                INSERT INTO messages (boardId, senderPlayerId, message) 
                VALUES (@boardId, @playerId, @message)";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@playerId", packet.PlayerId);
            command.Parameters.AddWithValue("@message", packet.Message);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not insert message: {e.Message}");
        }
    }

    public void InsertMessage(PlayerSendDocument packet, string playerName)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            AddPlayerToDatabase(packet.PlayerId, playerName);

            string query =
                @"
                INSERT INTO messages (boardId, senderPlayerId, message) 
                VALUES (@boardId, @playerId, @message)";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@playerId", packet.PlayerId);
            command.Parameters.AddWithValue("@message", packet.Document);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem
                .getSAPI()
                .Logger.Error($"[NoticeBoard] Could not insert message: {e.Message}");
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
                SELECT m.id, m.message, m.senderPlayerId, p.playerName, m.createdAt
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
                        PlayerName = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3),
                        CreatedAt = reader.GetDateTime(4),
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

    public void EditMessageById(int id, string message)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query =
                "UPDATE messages SET message = @message, updatedAt = CURRENT_TIMESTAMP WHERE id = @id";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@message", message);
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
                        (SELECT MAX(id) FROM messages WHERE boardId = @boardId),
                        CURRENT_TIMESTAMP)
                ON CONFLICT(playerId, boardId) 
                DO UPDATE SET 
                    lastReadMessageId = (SELECT MAX(id) FROM messages WHERE boardId = @boardId),
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
                SELECT MAX(m.id) > COALESCE(pbr.lastReadMessageId, 0)
                FROM noticeBoard nb
                LEFT JOIN messages m ON m.boardId = nb.boardId
                LEFT JOIN playerBoardReads pbr ON pbr.boardId = nb.boardId AND pbr.playerId = @playerId
                WHERE nb.boardId = @boardId;";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerId", playerId);
            command.Parameters.AddWithValue("@boardId", boardId);

            var result = command.ExecuteScalar();
            return result != null && Convert.ToBoolean(result);
        }
        catch
        {
            return false;
        }
    }
    #endregion
}
