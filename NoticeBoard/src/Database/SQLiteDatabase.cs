using System.IO;
using Microsoft.Data.Sqlite;
using NoticeBoard.src;
using Vintagestory.API.Config;

public class SQLiteDatabase
{
    private string dbFilePath;
    private SqliteConnection connection;

    public SQLiteDatabase(string databaseName = "noticeboard.db"): base()
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

        NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] path db is " + dbFilePath);
        connection = new SqliteConnection($"Data Source={dbFilePath};");
        TryOpenConnection();

        // Initialize the SQLite database
        InitializeDatabase();
    }
    public SqliteConnection getSQLiteConnection()
    {
        return connection;
    }
    public string getSQLiteDBPath()
    {
        return dbFilePath;
    }

    private void InitializeDatabase()
    {
        TryOpenConnection();
        string createTableQuery = @"
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                message TEXT NOT NULL,
                playerId TEXT NOT NULL,
                boardId TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS players (
                playerId TEXT NOT NULL PRIMARY KEY,
                playerName TEXT,
                UNIQUE(playerId)
            );

            CREATE TABLE IF NOT EXISTS noticeBoard (
                boardId TEXT NOT NULL PRIMARY KEY,
                pos TEXT,
                UNIQUE(boardId)
            );
        ";

        using (var command = new SqliteCommand(createTableQuery, connection))
        {
            command.ExecuteNonQuery();
        }
    }

    public void TryOpenConnection()
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }
    }

    public void Close()
    {
        if (connection != null)
        {
            connection.Close();
            connection.Dispose();
        }
    }
}