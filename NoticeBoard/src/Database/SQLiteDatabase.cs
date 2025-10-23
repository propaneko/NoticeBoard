﻿using Microsoft.Data.Sqlite;
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

        using (var command = new SqliteCommand(createTableQuery, connection))
        {
            command.ExecuteNonQuery();
        }
    }

    private void MigrateDatabase()
    {
        TryOpenConnection();

        // Step 1: Create schema_version table if it doesn't exist
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

        // Step 2: Check current schema version
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

        // Step 3: If version is already 1, migration is complete, so exit
        const int targetVersion = 1; // Increment this for future migrations
        if (currentVersion >= targetVersion)
        {
            NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] Database is already at version {0}, no migration needed.", currentVersion);
            return;
        }

        // Step 4: Perform migration
        NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] Starting database migration to version {0}.", targetVersion);

        // Create temporary tables with the new schema
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

        // Migrate data to temporary tables
        // Copy players
        string copyPlayersQuery = "INSERT INTO temp_players (playerId, playerName) SELECT playerId, playerName FROM players;";
        using (var command = new SqliteCommand(copyPlayersQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        // Insert a default 'unknown' player for noticeBoard migration
        string insertUnknownPlayer = "INSERT OR IGNORE INTO temp_players (playerId, playerName) VALUES ('unknown', 'Unknown Player');";
        using (var command = new SqliteCommand(insertUnknownPlayer, connection))
        {
            command.ExecuteNonQuery();
        }

        // Copy noticeBoard, assigning default playerId ('unknown') and isLocked (0)
        string copyNoticeBoardQuery = "INSERT INTO temp_noticeBoard (boardId, playerId, pos, isLocked) SELECT boardId, 'unknown', pos, 0 FROM noticeBoard;";
        using (var command = new SqliteCommand(copyNoticeBoardQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        // Copy messages, ensuring playerId and boardId exist
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

        // Drop old tables
        string dropOldTablesQuery = @"
        DROP TABLE IF EXISTS messages;
        DROP TABLE IF EXISTS noticeBoard;
        DROP TABLE IF EXISTS players;
    ";
        using (var command = new SqliteCommand(dropOldTablesQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        // Rename temporary tables
        string renameTablesQuery = @"
        ALTER TABLE temp_players RENAME TO players;
        ALTER TABLE temp_noticeBoard RENAME TO noticeBoard;
        ALTER TABLE temp_messages RENAME TO messages;
    ";
        using (var command = new SqliteCommand(renameTablesQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        // Step 5: Update schema version
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

        // Ensure foreign keys are enabled
        using (var command = new SqliteCommand("PRAGMA foreign_keys = ON;", connection))
        {
            command.ExecuteNonQuery();
        }

        NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] Database migration to version {0} completed successfully.", targetVersion);
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