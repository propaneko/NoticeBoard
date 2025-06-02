using System;
using HarmonyLib;
using Microsoft.Data.Sqlite;
using NoticeBoard.BlockType;
using NoticeBoard.Configs;
using NoticeBoard.Database;
using NoticeBoard.Events;
using NoticeBoard.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace NoticeBoard
{
    public class NoticeBoardModSystem : ModSystem
    {
        public Harmony harmony;
        public static NoticeBoardModSystem modInstance;
        private static ICoreServerAPI sapi;
        private static ICoreClientAPI capi;
        private static SQLiteDatabase databaseHandler;
        public static ModConfig config;
        private const string ConfigName = "noticeboard.json";

        public NoticeBoardModSystem()
        {
            NoticeBoardModSystem.modInstance = this;
        }

        public static ICoreServerAPI getSAPI()
        {
            return NoticeBoardModSystem.sapi;
        }

        public static ICoreClientAPI getCAPI()
        {
            return NoticeBoardModSystem.capi;
        }

        public SQLiteDatabase getDatabaseHandler()
        {
            return NoticeBoardModSystem.databaseHandler;
        }

        public static NoticeBoardModSystem getModInstance()
        {
            return NoticeBoardModSystem.modInstance;
        }

        public static ModConfig getConfig()
        {
            return NoticeBoardModSystem.config;
        }

        public static void LoadDatabase()
        {
            try
            {
                NoticeBoardModSystem.databaseHandler = new SQLiteDatabase("noticeboard.db");
            }
            catch (SqliteException ex)
            {
                NoticeBoardModSystem.sapi.Logger.Error("loadDatabase:" + ex.Message);
            }
        }

        private void LoadConfig()
        {
            try
            {
                NoticeBoardModSystem.config = NoticeBoardModSystem.sapi.LoadModConfig<ModConfig>("noticeboard.json");
            }
            catch (Exception)
            {
                NoticeBoardModSystem.sapi.Server.LogError("NoticeBoard: Failed to load mod config!", Array.Empty<object>());
                return;
            }
            if (NoticeBoardModSystem.config == null)
            {
                NoticeBoardModSystem.sapi.Server.LogNotification("NoticeBoards: non-existant modconfig at 'ModConfig/noticeboardnoticeboard.json', creating default...", Array.Empty<object>());
                NoticeBoardModSystem.config = new ModConfig();
                NoticeBoardModSystem.sapi.StoreModConfig<ModConfig>(NoticeBoardModSystem.config, "noticeboard.json");
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("NoticeBoardBlock", typeof(NoticeBoardBlock));
            api.RegisterBlockEntityClass("NoticeBoardBlockEntity", typeof(NoticeBoardBlockEntity));
            api.Network.RegisterChannel("noticeboard").RegisterMessageType(typeof(RequestAllMessages)).RegisterMessageType(typeof(ResponseIsActive)).RegisterMessageType(typeof(ResponseAllMessages)).RegisterMessageType(typeof(RefreshNoticeBoard)).RegisterMessageType(typeof(PlayerSendMessage)).RegisterMessageType(typeof(PlayerEditMessage)).RegisterMessageType(typeof(PlayerDestroyNoticeBoard)).RegisterMessageType(typeof(PlayerCreateNoticeBoard)).RegisterMessageType(typeof(PlayerRemoveMessage));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            NoticeBoardModSystem.capi = api;
            new ClientMessageHandler().SetMessageHandlers();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            NoticeBoardModSystem.sapi = api;
            NoticeBoardModSystem.sapi.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, delegate ()
            {
                this.LoadConfig();
                NoticeBoardModSystem.LoadDatabase();
                new ServerMessageHandler().SetMessageHandlers();
            });


        }
    }
}