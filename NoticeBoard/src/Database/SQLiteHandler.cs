using Microsoft.Data.Sqlite;
using NoticeBoard;
using NoticeBoard.Packets;
using System;
using System.Collections.Generic;
using Vintagestory.API.Server;

namespace NoticeBoard.Database;

public class SQLiteHandler
{
    private readonly SQLiteDatabase SQLiteDatabase = NoticeBoardModSystem.getModInstance().getDatabaseHandler();
    private readonly SqliteConnection SQLiteConnection = NoticeBoardModSystem.getModInstance().getDatabaseHandler().getSQLiteConnection();

    #region Players
    public void AddPlayerToDatabase(string playerId, string playerName)
    {
        try
        {
            string query = "INSERT OR IGNORE INTO players (playerId, playerName) VALUES (@playerId, @playerName)";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@playerId", playerId);
            command.Parameters.AddWithValue("@playerName", playerName);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldnt insertPlayerQuery message: {e.Message}");
        }
    }
    #endregion

    #region NoticeBoard
    public void CreateNoticeBoard(PlayerCreateNoticeBoard packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = @"
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
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not create noticeboard: {e.Message}");
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
            command.Parameters.AddWithValue("@isLocked", packet.isLocked ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not update lock status: {e.Message}");
        }
    }

    public void EditEnableParticles(EditEnableParticles packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE noticeBoard SET enableParticles = @enableParticles WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableParticles", packet.enableParticles ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not update lock status: {e.Message}");
        }
    }

    public void EditEnableParchment(EditEnableParchment packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE noticeBoard SET enableParchment = @enableParchment WHERE boardId = @boardId";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@enableParchment", packet.enableParchment ? 1 : 0);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not update lock status: {e.Message}");
        }
    }
    public void DeleteNoticeBoard(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            // Cascading delete is safer with foreign keys
            string query = @"
                DELETE FROM playerBoardReads WHERE boardId = @boardId;
                DELETE FROM messages WHERE boardId = @boardId;
                DELETE FROM noticeBoard WHERE boardId = @boardId;";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not delete noticeboard: {e.Message}");
        }
    }

    public NoticeBoardObject GetBoardData(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();
        var noticeBoard = new NoticeBoardObject();

        try
        {
            string query = @"
                SELECT nb.boardId, nb.ownerPlayerId, nb.pos, nb.isLocked, nb.enableParticles, nb.enableParchment, p.playerName 
                FROM noticeBoard nb
                LEFT JOIN players p ON p.playerId = nb.ownerPlayerId
                WHERE nb.boardId = @boardId";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                noticeBoard.BoardId = reader.GetString(0);
                noticeBoard.PlayerId = reader.GetString(1);
                noticeBoard.Pos = reader.GetString(2);
                noticeBoard.isLocked = reader.GetInt16(3);
                noticeBoard.enableParticles = reader.GetInt16(4);
                noticeBoard.enableParchment = reader.GetInt16(5);
                noticeBoard.PlayerName = reader.IsDBNull(6) ? "Unknown" : reader.GetString(6);
            }

            return noticeBoard;
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not get board data: {e.Message}");
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

            string query = @"
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
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not insert message: {e.Message}");
        }
    }


    public List<Message> GetAllMessages(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();
        var messages = new List<Message>();

        try
        {
            string query = @"
                SELECT m.id, m.message, m.senderPlayerId, p.playerName, m.createdAt
                FROM messages m
                LEFT JOIN players p ON p.playerId = m.senderPlayerId
                WHERE m.boardId = @boardId
                ORDER BY m.createdAt ASC, m.id ASC";

            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@boardId", boardId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                messages.Add(new Message
                {
                    Id = reader.GetInt32(0),
                    Text = reader.GetString(1),
                    PlayerId = reader.GetString(2),
                    PlayerName = reader.IsDBNull(3) ? "Unknown" : reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4)
                });
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not get messages: {e.Message}");
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
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not delete message: {e.Message}");
        }
    }

    public void EditMessageById(int id, string message)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = "UPDATE messages SET message = @message WHERE id = @id";
            using var command = new SqliteCommand(query, SQLiteConnection);
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@message", message);
            command.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not edit message: {e.Message}");
        }
    }
    #endregion

    #region Read Tracking
    public void MarkBoardAsRead(string playerId, string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = @"
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
            NoticeBoardModSystem.getSAPI().Logger.Error($"[NoticeBoard] Could not mark board as read: {e.Message}");
        }
    }

    public bool HasUnreadMessages(string playerId, string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string query = @"
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