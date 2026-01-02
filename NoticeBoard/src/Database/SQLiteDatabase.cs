using Microsoft.Data.Sqlite;
using NoticeBoard;
using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace NoticeBoard.Database;
public class SQLiteDatabase
{
    private string dbFilePath;
    private SqliteConnection connection;

    public SQLiteDatabase(string databaseName = "noticeboard.db")
    {
        string modConfigDir = Path.Combine(GamePaths.DataPath, "ModData/noticeboard");
        if (!Directory.Exists(modConfigDir))
        {
            Directory.CreateDirectory(modConfigDir);
        }
        this.dbFilePath = Path.Combine(modConfigDir, databaseName);
        NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] path db is " + this.dbFilePath);
        this.connection = new SqliteConnection("Data Source=" + this.dbFilePath + ";");
        this.TryOpenConnection();
        this.MigrateDatabase();
        this.InitializeDatabase();
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
            CREATE TABLE IF NOT EXISTS players (
                playerId TEXT NOT NULL PRIMARY KEY,
                playerName TEXT,
                UNIQUE(playerId)
            );

            CREATE TABLE IF NOT EXISTS noticeBoard (
                boardId TEXT NOT NULL PRIMARY KEY,
                playerId TEXT NOT NULL,
                pos TEXT,
                isLocked INTEGER,
                UNIQUE(boardId),
                FOREIGN KEY (playerId) REFERENCES players(playerId)
            );

            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                message TEXT NOT NULL,
                playerId TEXT NOT NULL,
                boardId TEXT NOT NULL,
                FOREIGN KEY (playerId) REFERENCES players(playerId),
                FOREIGN KEY (boardId) REFERENCES noticeBoard(boardId)
            );
        ";

        try
        {
            using (var command = new SqliteCommand(createTableQuery, connection))
            {
                command.ExecuteNonQuery();
            }
        }
        catch (Exception e)
        {
            throw;
        }
      
    }

    private void MigrateDatabase()
    {
        TryOpenConnection();

        string createSchemaVersionTable = @"
        CREATE TABLE IF NOT EXISTS schema_version (
            id INTEGER PRIMARY KEY,
            version INTEGER NOT NULL,
            migration_date TEXT NOT NULL
        );
    ";
        using (var command = new SqliteCommand(createSchemaVersionTable, connection))
        {
            command.ExecuteNonQuery();
        }

        bool dbWasInitialised = TableExists("players") && TableExists("noticeBoard") && TableExists("messages");
        if (!dbWasInitialised)
        {
            NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] Database file exists but core tables are missing – skipping migration.");
            using (var cmd = new SqliteCommand(
                       "INSERT OR IGNORE INTO schema_version (id, version, migration_date) VALUES (1, 1, @date);", connection))
            {
                cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
            return;
        }

        int currentVersion = 0;
        string checkVersionQuery = "SELECT version FROM schema_version WHERE id = 1;";
        using (var command = new SqliteCommand(checkVersionQuery, connection))
        {
            var result = command.ExecuteScalar();
            if (result != null && result != DBNull.Value)
            {
                currentVersion = Convert.ToInt32(result);
            }
        }

        const int targetVersion = 1; 
        if (currentVersion >= targetVersion)
        {
            NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] Database is already at version {0}, no migration needed.", currentVersion);
            return;
        }

        NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] Starting database migration to version {0}.", targetVersion);

        string createTempTablesQuery = @"
        CREATE TABLE temp_players (
            playerId TEXT NOT NULL PRIMARY KEY,
            playerName TEXT,
            UNIQUE(playerId)
        );
        CREATE TABLE temp_noticeBoard (
            boardId TEXT NOT NULL PRIMARY KEY,
            playerId TEXT NOT NULL,
            pos TEXT,
            isLocked INTEGER,
            UNIQUE(boardId),
            FOREIGN KEY (playerId) REFERENCES temp_players(playerId)
        );
        CREATE TABLE temp_messages (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            message TEXT NOT NULL,
            playerId TEXT NOT NULL,
            boardId TEXT NOT NULL,
            FOREIGN KEY (playerId) REFERENCES temp_players(playerId),
            FOREIGN KEY (boardId) REFERENCES temp_noticeBoard(boardId)
        );
    ";
        using (var command = new SqliteCommand(createTempTablesQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        string copyPlayersQuery = "INSERT INTO temp_players (playerId, playerName) SELECT playerId, playerName FROM players;";
        using (var command = new SqliteCommand(copyPlayersQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        string insertUnknownPlayer = "INSERT OR IGNORE INTO temp_players (playerId, playerName) VALUES ('unknown', 'Unknown Player');";
        using (var command = new SqliteCommand(insertUnknownPlayer, connection))
        {
            command.ExecuteNonQuery();
        }

        string copyNoticeBoardQuery = "INSERT INTO temp_noticeBoard (boardId, playerId, pos, isLocked) SELECT boardId, 'unknown', pos, 0 FROM noticeBoard;";
        using (var command = new SqliteCommand(copyNoticeBoardQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        string copyMessagesQuery = @"
        INSERT INTO temp_messages (id, message, playerId, boardId)
        SELECT m.id, m.message, m.playerId, m.boardId
        FROM messages m
        WHERE EXISTS (SELECT 1 FROM temp_players p WHERE p.playerId = m.playerId)
        AND EXISTS (SELECT 1 FROM temp_noticeBoard nb WHERE nb.boardId = m.boardId);
    ";
        using (var command = new SqliteCommand(copyMessagesQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        string dropOldTablesQuery = @"
        DROP TABLE IF EXISTS messages;
        DROP TABLE IF EXISTS noticeBoard;
        DROP TABLE IF EXISTS players;
    ";
        using (var command = new SqliteCommand(dropOldTablesQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        string renameTablesQuery = @"
        ALTER TABLE temp_players RENAME TO players;
        ALTER TABLE temp_noticeBoard RENAME TO noticeBoard;
        ALTER TABLE temp_messages RENAME TO messages;
    ";
        using (var command = new SqliteCommand(renameTablesQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        string updateVersionQuery = @"
        INSERT OR REPLACE INTO schema_version (id, version, migration_date)
        VALUES (1, @version, @date);
    ";
        using (var command = new SqliteCommand(updateVersionQuery, connection))
        {
            command.Parameters.AddWithValue("@version", targetVersion);
            command.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            command.ExecuteNonQuery();
        }

        using (var command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection))
        {
            command.ExecuteNonQuery();
        }

        NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] Database migration to version {0} completed successfully.", targetVersion);
    }

    private bool TableExists(string tableName)
    {
        const string sql = "SELECT name FROM sqlite_master WHERE type='table' AND name=@name;";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@name", tableName);
        return cmd.ExecuteScalar() != null;
    }

    public void TryOpenConnection()
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
            using (var command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection))
            {
                command.ExecuteNonQuery();
            }
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