using System;
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
        var api = NoticeBoardModSystem.getSAPI();
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
        NoticeBoardModSystem.getSAPI().Logger.Debug("[noticeboard] path db is " + this.dbFilePath);
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
                isLocked INTEGER DEFAULT 0,
                enableParticles INTEGER DEFAULT 1,
                enableParchment INTEGER DEFAULT 1,
                enableProximity INTEGER DEFAULT 0,
                proximityChannel TEXT DEFAULT 'Proximity',
                proximityDistance INTEGER DEFAULT 100,
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
