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

    public void AddPlayerToDatabase(IServerPlayer byPlayer)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string insertPlayerQuery = "INSERT OR IGNORE INTO players (playerId, playerName) VALUES (@playerId, @playerName)";
            using (var command = new SqliteCommand(insertPlayerQuery, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@playerId", byPlayer.PlayerUID);
                command.Parameters.AddWithValue("@playerName", byPlayer.PlayerName);
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldnt insertPlayerQuery message: {e.Message}");
        }
    }
    public void InsertMessage(PlayerSendMessage packet, string playerName)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string insertPlayerQuery = "INSERT OR IGNORE INTO players (playerId, playerName) VALUES (@playerId, @playerName)";
            using (var command = new SqliteCommand(insertPlayerQuery, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@playerId", packet.PlayerId);
                command.Parameters.AddWithValue("@playerName", playerName);
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldnt insertPlayerQuery message: {e.Message}");
        }

        try
        {
            string insertQuery = "INSERT INTO messages (message, boardId, playerId) VALUES (@message, @boardId, @playerId)";
            using (var command = new SqliteCommand(insertQuery, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@message", packet.Message);
                command.Parameters.AddWithValue("@boardId", packet.BoardId);
                command.Parameters.AddWithValue("@playerId", packet.PlayerId);
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldnt insertQuery message: {e.Message}, {packet.BoardId} {packet.PlayerId} ");
        }

    }

    public void EditMessageById(int id, string message)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            using (SqliteCommand sqliteCommand = new SqliteCommand("UPDATE messages SET message = @message WHERE id = @id", this.SQLiteConnection))
            {
                sqliteCommand.Parameters.AddWithValue("@id", id);
                sqliteCommand.Parameters.AddWithValue("@message", message);
                sqliteCommand.ExecuteNonQuery();
            }
        } catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldnt EditMessageById message: {e.Message}");
        }
       
    }

    public void CreateNoticeBoard(PlayerCreateNoticeBoard packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string insertQuery = "INSERT OR IGNORE INTO noticeBoard (boardId, playerId, pos, isLocked) VALUES (@boardId, @playerId, @pos, @isLocked)";
            using (var command = new SqliteCommand(insertQuery, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@boardId", packet.BoardId);
                command.Parameters.AddWithValue("@playerId", packet.PlayerId);
                command.Parameters.AddWithValue("@pos", packet.Pos);
                command.Parameters.AddWithValue("@isLocked", 0);
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldnt CreateNoticeBoard message: {e.Message}");
        }
    }

    public void EditIsLocked(EditIsLocked packet)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string insertQuery = "UPDATE noticeBoard SET isLocked = @isLocked WHERE boardId = @boardId";
            using (var command = new SqliteCommand(insertQuery, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@boardId", packet.BoardId);
                command.Parameters.AddWithValue("@isLocked", packet.isLocked ? 1 : 0);
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldnt CreateNoticeBoard message: {e.Message}");
        }
    }

    public void DeleteNoticeBoard(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            string deleteNoticeBoardAndMessages = "DELETE FROM noticeBoard WHERE boardId = @boardId;";
            using (var command = new SqliteCommand(deleteNoticeBoardAndMessages, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@boardId", boardId);
                command.ExecuteNonQuery();

            }
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldnt DeleteNoticeBoard message: {e.Message}");
        }

      
    }

    public NoticeBoardObject GetBoardData(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        try
        {
            NoticeBoardObject noticeBoard = new NoticeBoardObject();

            string selectQuery = "SELECT boardId, playerId, pos, isLocked FROM noticeBoard WHERE boardId = @boardId";
            using (var command = new SqliteCommand(selectQuery, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@boardId", boardId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        noticeBoard.BoardId = reader.GetString(0);
                        noticeBoard.PlayerId = reader.GetString(1);
                        noticeBoard.Pos = reader.GetString(2);
                        noticeBoard.isLocked = reader.GetInt16(3);
                    }
                }
            }

            string selectPlayerQuery = "SELECT playerName FROM players WHERE playerId = @playerId";
            using (var command = new SqliteCommand(selectPlayerQuery, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@playerId", noticeBoard.PlayerId);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        noticeBoard.PlayerName = reader.GetString(0);
                    }
                }
            }


            return noticeBoard;
        }
        catch (Exception e)
        {
            NoticeBoardModSystem.getSAPI().Logger.Error($"Couldn't GetBoardData message: {e.Message}");
            return null;
        }
        
    }

    public List<Message> GetAllMessages(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        List<Message> messages = new List<Message>();

        string selectQuery = "SELECT id, message FROM messages WHERE boardId = @boardId";
        using (var command = new SqliteCommand(selectQuery, SQLiteConnection))
        {
            command.Parameters.AddWithValue("@boardId", boardId);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string message = reader.GetString(1);
                    messages.Add(new Message
                    {
                        Id = id,
                        Text = message
                    });
                }
            }
        }


        return messages;
    }

    public int CountMessageElementsByBoardId(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();
        int rowCount = 0;

        string selectQuery = "SELECT COUNT(*) FROM messages WHERE boardId = @boardId";
        using (var command = new SqliteCommand(selectQuery, SQLiteConnection))
        {
            command.Parameters.AddWithValue("@boardId", boardId);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount = reader.GetInt32(0);
                }
            }
        }


        return rowCount;
    }

    public void DeleteMessage(int id)
    {
        SQLiteDatabase.TryOpenConnection();
        string deleteQuery = "DELETE FROM messages WHERE id = @id";
        using (var command = new SqliteCommand(deleteQuery, SQLiteConnection))
        {
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }

    }
}