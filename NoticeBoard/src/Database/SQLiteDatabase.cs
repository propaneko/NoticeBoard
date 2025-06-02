﻿using System.IO;
using Microsoft.Data.Sqlite;
using NoticeBoard;
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