using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

public class SQLiteDatabase
{
    private string dbFilePath;
    private SqliteConnection connection;

    public SQLiteDatabase(string databaseName = "noticeboard.db")
    {
        // Get the ModConfig directory path
        string modConfigDir = Path.Combine(GamePaths.ModConfig, "noticeboard");

        // Ensure the directory exists
        if (!Directory.Exists(modConfigDir))
        {
            Directory.CreateDirectory(modConfigDir);
        }

        // Set the database file path
        dbFilePath = Path.Combine(modConfigDir, databaseName);

        // Initialize the SQLite database
        InitializeDatabase();
    }

    // Initialize the database and create tables if they don't exist
    private void InitializeDatabase()
    {
        // Open the SQLite connection
        connection = new SqliteConnection($"Data Source={dbFilePath};");
        connection.Open();

        // Create a table for storing messages (if it doesn't exist)
        string createTableQuery = @"
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                message TEXT NOT NULL
            );
        ";

        using (var command = new SqliteCommand(createTableQuery, connection))
        {
            command.ExecuteNonQuery();
        }
    }

    // Insert a new message into the database
    public void InsertMessage(string message)
    {
        string insertQuery = "INSERT INTO messages (message) VALUES (@message)";
        using (var command = new SqliteCommand(insertQuery, connection))
        {
            command.Parameters.AddWithValue("@message", message);
            command.ExecuteNonQuery();
        }
    }

    // Get all messages from the database
    public class Message
    {
        public int Id { get; set; }
        public string Text { get; set; }
    }
    public List<Message> GetAllMessages()
    {
        List<Message> messages = new List<Message>();

        string selectQuery = "SELECT id, message FROM messages";
        using (var command = new SqliteCommand(selectQuery, connection))
        {
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);           // Get the id (first column)
                    string message = reader.GetString(1);  // Get the message (second column)
                    messages.Add(new Message
                    {
                        Id = id,
                        Text = message
                    });
                    //messages.Add(reader["message"].ToString());
                }
            }
        }

        return messages;
    }

    public bool MessageHasElements()
    {
        long rowCount = 0;

        string selectQuery = "SELECT COUNT(*) FROM messages";
        using (var command = new SqliteCommand(selectQuery, connection))
        {
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    rowCount = reader.GetInt32(0);
                }
            }
        }

        return rowCount > 0;

    }

    // Delete a message by its ID
    public void DeleteMessage(int id)
    {
        string deleteQuery = "DELETE FROM messages WHERE id = @id";
        using (var command = new SqliteCommand(deleteQuery, connection))
        {
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();
        }
    }

    // Close the database connection
    public void Close()
    {
        if (connection != null)
        {
            connection.Close();
            connection.Dispose();
        }
    }
}