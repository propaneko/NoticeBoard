using System;
using Microsoft.Data.Sqlite;
using HarmonyLib;
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
        private SQLiteHandler db;

        public NoticeBoardModSystem()
        {
            NoticeBoardModSystem.modInstance = this;
        }

        public static ICoreServerAPI getSAPI() => NoticeBoardModSystem.sapi;
        public static ICoreClientAPI getCAPI() => NoticeBoardModSystem.capi;
        public SQLiteDatabase getDatabaseHandler() => NoticeBoardModSystem.databaseHandler;
        public static NoticeBoardModSystem getModInstance() => NoticeBoardModSystem.modInstance;
        public static ModConfig getConfig() => NoticeBoardModSystem.config;

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
                NoticeBoardModSystem.sapi.Server.LogNotification(
                    "NoticeBoards: non-existant modconfig at 'ModConfig/noticeboard.json', creating default...",
                    Array.Empty<object>()
                );
                NoticeBoardModSystem.config = new ModConfig();
                NoticeBoardModSystem.sapi.StoreModConfig<ModConfig>(NoticeBoardModSystem.config, "noticeboard.json");
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("NoticeBoardBlock", typeof(NoticeBoardBlock));
            api.RegisterBlockEntityClass("NoticeBoardBlockEntity", typeof(NoticeBoardBlockEntity));
            api.Network.RegisterChannel("noticeboard")
                .RegisterMessageType<RequestAllMessages>()
                .RegisterMessageType<ResponseIsActive>()
                .RegisterMessageType<ResponseAllMessages>()
                .RegisterMessageType<RefreshNoticeBoard>()
                .RegisterMessageType<PlayerSendMessage>()
                .RegisterMessageType<PlayerSendDocument>()
                .RegisterMessageType<PlayerEditMessage>()
                .RegisterMessageType<PlayerTakeMessage>()
                .RegisterMessageType<PlayerBumpMessage>()
                .RegisterMessageType<PlayerDestroyNoticeBoard>()
                .RegisterMessageType<PlayerCreateNoticeBoard>()
                .RegisterMessageType<EditIsLocked>()
                .RegisterMessageType<EditEnableParticles>()
                .RegisterMessageType<EditEnableParchment>()
                .RegisterMessageType<EditBoardName>()
                .RegisterMessageType<EditBoardFont>()
                .RegisterMessageType<EditBoardFontSize>()
                .RegisterMessageType<EditBoardTheme>()
                .RegisterMessageType<EditBoardOwner>()
                .RegisterMessageType<EditEnableProximity>()
                .RegisterMessageType<EditProximityChannel>()
                .RegisterMessageType<EditProximityDistance>()
                .RegisterMessageType<RequestAllPlayers>()
                .RegisterMessageType<ResponseAllPlayers>()
                .RegisterMessageType<PlayerRemoveMessage>()
                .RegisterMessageType<UnreadParticlesPacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            NoticeBoardModSystem.capi = api;
            new ClientMessageHandler().SetMessageHandlers();

            // Load OS fonts exactly once on client startup
            FontManager.InitializeFonts(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            NoticeBoardModSystem.sapi = api;

            NoticeBoardModSystem.sapi.Event.ServerRunPhase(
                EnumServerRunPhase.ModsAndConfigReady,
                delegate ()
                {
                    this.LoadConfig();
                    NoticeBoardModSystem.LoadDatabase();
                    new ServerMessageHandler().SetMessageHandlers();
                }
            );

            NoticeBoardModSystem.sapi.Event.PlayerJoin += OnPlayerJoin;
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            db = new SQLiteHandler();
            db.AddPlayerToDatabase(byPlayer.PlayerUID, byPlayer.PlayerName);
        }
    }
}