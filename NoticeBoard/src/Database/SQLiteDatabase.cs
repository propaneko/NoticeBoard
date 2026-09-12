using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NoticeBoard;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace NoticeBoard.Database;

public class SQLiteDatabase
{
    private string dbFilePath;
    private SqliteConnection connection;

    public SQLiteDatabase(string databaseName = "noticeboard.db")
    {
        var api =
            NoticeBoardModSystem.getSAPI()
            ?? throw new InvalidOperationException(
                "[NoticeBoard] Cannot create SQLiteDatabase before server API is available."
            );

        string worldId = api.World?.SavegameIdentifier;

        if (string.IsNullOrEmpty(worldId))
        {
            worldId = "global";
        }

        string modConfigDir = Path.Combine(GamePaths.DataPath, "ModData", worldId, "noticeboard");
        if (!Directory.Exists(modConfigDir))
        {
            Directory.CreateDirectory(modConfigDir);
        }
        this.dbFilePath = Path.Combine(modConfigDir, databaseName);
        api.Logger.Debug("[noticeboard] path db is " + this.dbFilePath);
        this.connection = new SqliteConnection("Data Source=" + this.dbFilePath + ";");
        this.TryOpenConnection();
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
        string createTableQuery =
            @"
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS players (
                playerId TEXT NOT NULL PRIMARY KEY,
                playerName TEXT NOT NULL,
                displayName TEXT,
                createdAt DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS noticeBoard (
                boardId TEXT NOT NULL PRIMARY KEY,
                boardName TEXT DEFAULT 'Notice Board',
                boardFont TEXT DEFAULT 'Ari-W9500',
                boardFontSize REAL DEFAULT '16.0',
                boardTheme TEXT DEFAULT 'Classic Aged',

                ownerPlayerId TEXT NOT NULL,
                pos TEXT,
                permissionMode INTEGER DEFAULT 0,
                enableParticles INTEGER DEFAULT 1,
                enableParchment INTEGER DEFAULT 1,
                enableProximity INTEGER DEFAULT 0,
                proximityChannel TEXT DEFAULT 'Proximity',
                proximityDistance INTEGER DEFAULT 100,
                maxPapersOnBoard INTEGER DEFAULT 20,
                enableLegacyBoard INTEGER DEFAULT 0,
                textSharpness INTEGER DEFAULT 2,
                enablePaperSway INTEGER DEFAULT 1,
                swayStrength INTEGER DEFAULT 50,
                enableManualPin INTEGER DEFAULT 1,
                enableDiscord INTEGER DEFAULT 0,
                discordWebhook TEXT DEFAULT '',
                corkAttachment TEXT,
                createdAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (ownerPlayerId) REFERENCES players(playerId)
            );

            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                boardId TEXT NOT NULL,
                senderPlayerId TEXT NOT NULL,
                message TEXT NOT NULL,
                totalHours REAL NOT NULL,
                isAnonymous INTEGER DEFAULT 0,
                holder INTEGER DEFAULT 0,
                paperTheme TEXT DEFAULT '',
                paperSeed INTEGER DEFAULT 0,
                createdAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                updatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,

                FOREIGN KEY (boardId) REFERENCES noticeBoard(boardId),
                FOREIGN KEY (senderPlayerId) REFERENCES players(playerId)
            );

            CREATE TABLE IF NOT EXISTS playerBoardReads (
                playerId TEXT NOT NULL,
                boardId TEXT NOT NULL,
                lastReadMessageId INTEGER,
                lastReadAt DATETIME DEFAULT CURRENT_TIMESTAMP,
            
                PRIMARY KEY (playerId, boardId),
                FOREIGN KEY (playerId) REFERENCES players(playerId),
                FOREIGN KEY (boardId) REFERENCES noticeBoard(boardId)
            );

            CREATE INDEX IF NOT EXISTS idx_messages_boardId ON messages(boardId);
            CREATE INDEX IF NOT EXISTS idx_messages_board_created ON messages(boardId, createdAt DESC);
            CREATE INDEX IF NOT EXISTS idx_messages_sender ON messages(senderPlayerId);

            CREATE INDEX IF NOT EXISTS idx_noticeBoard_owner ON noticeBoard(ownerPlayerId);

            CREATE INDEX IF NOT EXISTS idx_playerBoardReads_player ON playerBoardReads(playerId);
            CREATE INDEX IF NOT EXISTS idx_playerBoardReads_board ON playerBoardReads(boardId);
        ";

        using (var command = new SqliteCommand(createTableQuery, connection))
        {
            command.ExecuteNonQuery();
        }

        MigrateNoticeBoardSchema();
        MigrateMessagesSchema();
        MigratePlayersSchema();
    }

    private void MigrateNoticeBoardSchema()
    {
        HashSet<string> columns = GetTableColumns("noticeBoard");
        if (columns.Count == 0)
            return;

        EnsureColumn(columns, "noticeBoard", "boardName", "TEXT DEFAULT 'Notice Board'");
        EnsureColumn(columns, "noticeBoard", "boardFont", "TEXT DEFAULT 'Ari-W9500'");
        EnsureColumn(columns, "noticeBoard", "boardFontSize", "REAL DEFAULT 16.0");
        EnsureColumn(columns, "noticeBoard", "boardTheme", "TEXT DEFAULT 'Classic Aged'");
        EnsureColumn(columns, "noticeBoard", "pos", "TEXT");
        EnsureColumn(columns, "noticeBoard", "enableParticles", "INTEGER DEFAULT 1");
        EnsureColumn(columns, "noticeBoard", "enableParchment", "INTEGER DEFAULT 1");
        EnsureColumn(columns, "noticeBoard", "enableProximity", "INTEGER DEFAULT 0");
        EnsureColumn(columns, "noticeBoard", "proximityChannel", "TEXT DEFAULT 'Proximity'");
        EnsureColumn(columns, "noticeBoard", "proximityDistance", "INTEGER DEFAULT 100");
        EnsureColumn(columns, "noticeBoard", "maxPapersOnBoard", "INTEGER DEFAULT 20");

        if (columns.Contains("enableCustomPaperTextures") && !columns.Contains("enableLegacyBoard"))
        {
            ExecuteNonQuery(
                "ALTER TABLE noticeBoard RENAME COLUMN enableCustomPaperTextures TO enableLegacyBoard"
            );
            columns.Remove("enableCustomPaperTextures");
            columns.Add("enableLegacyBoard");
        }

        EnsureColumn(columns, "noticeBoard", "enableLegacyBoard", "INTEGER DEFAULT 0");
        EnsureColumn(columns, "noticeBoard", "textSharpness", "INTEGER DEFAULT 2");
        EnsureColumn(columns, "noticeBoard", "enablePaperSway", "INTEGER DEFAULT 1");
        EnsureColumn(columns, "noticeBoard", "swayStrength", "INTEGER DEFAULT 50");
        EnsureColumn(columns, "noticeBoard", "enableManualPin", "INTEGER DEFAULT 1");
        EnsureColumn(columns, "noticeBoard", "enableNoticeAging", "INTEGER DEFAULT 0");
        EnsureColumn(columns, "noticeBoard", "noticeAgingDays", "INTEGER DEFAULT 9");
        EnsureColumn(columns, "noticeBoard", "corkAttachment", "TEXT");
        EnsureColumn(columns, "noticeBoard", "enableDiscord", "INTEGER DEFAULT 0");
        EnsureColumn(columns, "noticeBoard", "discordWebhook", "TEXT DEFAULT ''");
        EnsureColumn(columns, "noticeBoard", "createdAt", "DATETIME DEFAULT CURRENT_TIMESTAMP");

        if (!columns.Contains("permissionMode"))
        {
            ExecuteNonQuery(
                "ALTER TABLE noticeBoard ADD COLUMN permissionMode INTEGER DEFAULT 0"
            );
            columns.Add("permissionMode");

            if (columns.Contains("isLocked"))
            {
                // Legacy isLocked (0/1) → BoardPermissionMode.Default (0) / Locked (2)
                ExecuteNonQuery(
                    @"UPDATE noticeBoard
                      SET permissionMode = CASE WHEN isLocked = 1 THEN 2 ELSE 0 END"
                );
            }

            NoticeBoardModSystem
                .getSAPI()
                ?.Logger.Notification(
                    "[NoticeBoard] Migrated noticeBoard schema: added permissionMode column."
                );
        }
    }

    private void MigratePlayersSchema()
    {
        HashSet<string> columns = GetTableColumns("players");
        if (columns.Count == 0)
            return;
        EnsureColumn(columns, "players", "displayName", "TEXT");
    }

    private void MigrateMessagesSchema()
    {
        HashSet<string> columns = GetTableColumns("messages");
        if (columns.Count == 0)
            return;

        EnsureColumn(columns, "messages", "holder", "INTEGER DEFAULT 0");
        EnsureColumn(columns, "messages", "paperTheme", "TEXT DEFAULT ''");
        EnsureColumn(columns, "messages", "paperOverlay", "INTEGER DEFAULT 0");
        EnsureColumn(columns, "messages", "pinX", "REAL");
        EnsureColumn(columns, "messages", "pinY", "REAL");
        EnsureColumn(columns, "messages", "pinRotZ", "REAL");
        EnsureColumn(columns, "messages", "pinLayer", "INTEGER DEFAULT 0");
        EnsureColumn(columns, "messages", "waypointX", "REAL");
        EnsureColumn(columns, "messages", "waypointZ", "REAL");
        EnsureColumn(columns, "messages", "paperSeed", "INTEGER DEFAULT 0");
        ExecuteNonQuery("UPDATE messages SET paperSeed = id WHERE paperSeed = 0");
        EnsureColumn(columns, "messages", "waypointTitle", "TEXT DEFAULT ''");
        EnsureColumn(columns, "messages", "waypointIcon", "TEXT DEFAULT 'circle'");
        EnsureColumn(columns, "messages", "waypointColor", "TEXT DEFAULT 'steelblue'");
    }

    private HashSet<string> GetTableColumns(string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = new SqliteCommand($"PRAGMA table_info({tableName})", connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }
        return columns;
    }

    private void EnsureColumn(
        HashSet<string> columns,
        string tableName,
        string columnName,
        string columnDefinition
    )
    {
        if (columns.Contains(columnName))
            return;

        ExecuteNonQuery($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition}");
        columns.Add(columnName);
    }

    private void ExecuteNonQuery(string sql)
    {
        using var command = new SqliteCommand(sql, connection);
        command.ExecuteNonQuery();
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
            connection = null;
        }
    }
}
