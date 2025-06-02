using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using NoticeBoard;
using NoticeBoard.Packets;

namespace NoticeBoard.Database;
public class SQLiteHandler
{
    private readonly SQLiteDatabase SQLiteDatabase = NoticeBoardModSystem.getModInstance().getDatabaseHandler();
    private readonly SqliteConnection SQLiteConnection = NoticeBoardModSystem.getModInstance().getDatabaseHandler().getSQLiteConnection();

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

            string insertQuery = "INSERT INTO messages (message, boardId, playerId) VALUES (@message, @boardId, @playerId)";
            using (var command = new SqliteCommand(insertQuery, SQLiteConnection))
            {
                command.Parameters.AddWithValue("@message", packet.Message);
                command.Parameters.AddWithValue("@boardId", packet.BoardId);
                command.Parameters.AddWithValue("@playerId", packet.PlayerId);
                command.ExecuteNonQuery();
            }
        } catch
        {
            NoticeBoardModSystem.getSAPI().Logger.Error("Couldnt send message");
        }
        
    }

    public void EditMessageById(int id, string message)
    {
        this.SQLiteDatabase.TryOpenConnection();
        try
        {
            using (SqliteCommand sqliteCommand = new SqliteCommand("UPDATE messages SET message = @message WHERE id = @id", this.SQLiteConnection))
            {
                sqliteCommand.Parameters.AddWithValue("@id", id);
                sqliteCommand.Parameters.AddWithValue("@message", message);
                sqliteCommand.ExecuteNonQuery();
            }
        } catch
        {
            NoticeBoardModSystem.getSAPI().Logger.Error("Couldnt edit message");
        }
       
    }

    public void CreateNoticeBoard(PlayerCreateNoticeBoard packet)
    {
        SQLiteDatabase.TryOpenConnection();

        string insertQuery = "INSERT OR IGNORE INTO noticeBoard (boardId, pos) VALUES (@boardId, @pos)";
        using (var command = new SqliteCommand(insertQuery, SQLiteConnection))
        {
            command.Parameters.AddWithValue("@boardId", packet.BoardId);
            command.Parameters.AddWithValue("@pos", packet.Pos);

            command.ExecuteNonQuery();
        }
    }

    public void DeleteNoticeBoard(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        string deleteNoticeBoardAndMessages = "DELETE FROM noticeBoard WHERE boardId = @boardId;";
        using (var command = new SqliteCommand(deleteNoticeBoardAndMessages, SQLiteConnection))
        {
            command.Parameters.AddWithValue("@boardId", boardId);
            command.ExecuteNonQuery();
        }
    }

    public NoticeBoardObject GetBoardData(string boardId)
    {
        SQLiteDatabase.TryOpenConnection();

        NoticeBoardObject noticeBoard = new NoticeBoardObject();

        string selectQuery = "SELECT boardId, pos FROM noticeBoard WHERE boardId = @boardId";
        using (var command = new SqliteCommand(selectQuery, SQLiteConnection))
        {
            command.Parameters.AddWithValue("@boardId", boardId);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    noticeBoard.BoardId = reader.GetString(0);
                    noticeBoard.Pos = reader.GetString(1);
                }
            }
        }

        return noticeBoard;
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