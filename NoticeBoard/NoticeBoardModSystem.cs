using System;
using Microsoft.Data.Sqlite;
using NoticeBoard.BlockType;
using NoticeBoard.Database;
using NoticeBoard.Events;
using NoticeBoard.Packets;
using NoticeBoard.Rendering;
using NoticeBoard.Utils;
using NoticeBoard.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace NoticeBoard
{
    public class NoticeBoardModSystem : ModSystem
    {
        public static NoticeBoardModSystem modInstance;
        private static ICoreServerAPI sapi;
        private static ICoreClientAPI capi;
        private static SQLiteDatabase databaseHandler;
        private SQLiteHandler db;

        public NoticeBoardModSystem()
        {
            NoticeBoardModSystem.modInstance = this;
        }

        public static ICoreServerAPI getSAPI() => NoticeBoardModSystem.sapi;
        public static ICoreClientAPI getCAPI() => NoticeBoardModSystem.capi;
        public SQLiteDatabase getDatabaseHandler() => NoticeBoardModSystem.databaseHandler;
        public static NoticeBoardModSystem getModInstance() => NoticeBoardModSystem.modInstance;

        public static void LoadDatabase()
        {
            try
            {
                NoticeBoardModSystem.databaseHandler = new SQLiteDatabase("noticeboard.db");
            }
            catch (Exception ex)
            {
                NoticeBoardModSystem.sapi?.Logger.Error("[NoticeBoard] LoadDatabase failed: " + ex.Message);
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("NoticeBoardBlock", typeof(NoticeBoardBlock));
            api.RegisterBlockEntityClass("NoticeBoardBlockEntity", typeof(NoticeBoardBlockEntity));
            api.Network.RegisterChannel("noticeboard")
                .RegisterMessageType<RequestAllMessages>()
                .RegisterMessageType<ResponseAllMessages>()
                .RegisterMessageType<PlayerSendMessage>()
                .RegisterMessageType<PlayerSendDocument>()
                .RegisterMessageType<PlayerEditMessage>()
                .RegisterMessageType<PlayerRepositionMessage>()
                .RegisterMessageType<PlayerBumpMessage>()
                .RegisterMessageType<EditPermissionMode>()
                .RegisterMessageType<EditEnableParticles>()
                .RegisterMessageType<EditEnableNoticeAging>()
                .RegisterMessageType<EditNoticeAgingDays>()
                .RegisterMessageType<EditEnableParchment>()
                .RegisterMessageType<EditEnableManualPin>()
                .RegisterMessageType<PersistPaperPins>()
                .RegisterMessageType<EditBoardName>()
                .RegisterMessageType<EditBoardFont>()
                .RegisterMessageType<EditBoardFontSize>()
                .RegisterMessageType<EditMaxPapersOnBoard>()
                .RegisterMessageType<EditEnableLegacyBoard>()
                .RegisterMessageType<EditBoardTextSharpness>()
                .RegisterMessageType<EditBoardSwayStrength>()
                .RegisterMessageType<EditBoardTheme>()
                .RegisterMessageType<EditBoardOwner>()
                .RegisterMessageType<EditEnableProximity>()
                .RegisterMessageType<EditProximityChannel>()
                .RegisterMessageType<EditProximityDistance>()
                .RegisterMessageType<EditEnableDiscord>()
                .RegisterMessageType<EditDiscordWebhook>()
                .RegisterMessageType<RequestAllPlayers>()
                .RegisterMessageType<ResponseAllPlayers>()
                .RegisterMessageType<PlayerRemoveMessage>()
                .RegisterMessageType<UnreadParticlesPacket>()
                .RegisterMessageType<ExpiredNoticesFall>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            NoticeBoardModSystem.capi = api;
            new ClientMessageHandler().SetMessageHandlers();
            new PaperPinController(api);
            api.Input.RegisterHotKey(
                NoticeBoardPreviewOverlay.HotkeyCode,
                Lang.Get("noticeboard:hotkey-preview"),
                GlKeys.R,
                HotkeyType.HelpAndOverlays
            );
            new NoticeBoardPreviewOverlay(api);
            api.ChatCommands
                .Create("nbpinhud")
                .WithDescription("Toggle noticeboard pin-ghost debug HUD")
                .HandleWith(_ =>
                {
                    bool on = PaperPinController.Instance.ToggleDebugHud();
                    return TextCommandResult.Success(
                        Lang.Get(on ? "noticeboard:pin-debug-hud-on" : "noticeboard:pin-debug-hud-off")
                    );
                });

            FontManager.InitializeFonts(api);

#if DEBUG
            try
            {
                NoticeBoardPaperLayout.SelfCheckPermitStability();
                NoticeBoardPaperLayout.SelfCheckPinnedStability();
                NoticeBoardPaperLayout.SelfCheckAutoIgnoresPins();
                NoticeBoardPaperLayout.SelfCheckWorldHitToTack();
                NoticeBoardPaperLayout.SelfCheckLookRayToTack();
                NoticeBoardPaperLayout.SelfCheckAttachmentPinY();
                NoticeBoardPaperLayout.SelfCheckPinLayer();
                NoticeBoardPaperLayout.SelfCheckPickSheetAt();
                PositionHelper.SelfCheckHudCoords();
                GameDateFormatter.SelfCheckWear();
                PaperFall.SelfCheckFallMs();
                NoticeParchment.SelfCheckBookMeta();
                PaperSize.SelfCheckLanternWindSmooth();
                api.Logger.Notification("[NoticeBoard] SelfCheckPermitStability passed.");
            }
            catch (Exception ex)
            {
                api.Logger.Error("[NoticeBoard] SelfCheckPermitStability failed: " + ex.Message);
            }
#endif
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            NoticeBoardModSystem.sapi = api;

            NoticeBoardModSystem.sapi.Event.ServerRunPhase(
                EnumServerRunPhase.ModsAndConfigReady,
                delegate ()
                {
                    NoticeBoardModSystem.LoadDatabase();
                    new ServerMessageHandler().SetMessageHandlers();
                }
            );

            NoticeBoardModSystem.sapi.Event.PlayerJoin += OnPlayerJoin;

            sapi.ChatCommands
                .Create("nbdiscord")
                .WithDescription(Lang.Get("noticeboard:discord-cmd-desc"))
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("webhook")
                    .WithDescription(Lang.Get("noticeboard:discord-cmd-webhook-desc"))
                    .WithArgs(
                        sapi.ChatCommands.Parsers.Word("boardId"),
                        sapi.ChatCommands.Parsers.OptionalAll("url")
                    )
                    .HandleWith(OnDiscordWebhookCommand)
                .EndSubCommand()
                .BeginSubCommand("status")
                    .WithDescription(Lang.Get("noticeboard:discord-cmd-status-desc"))
                    .WithArgs(sapi.ChatCommands.Parsers.Word("boardId"))
                    .HandleWith(OnDiscordStatusCommand)
                .EndSubCommand();

#if DEBUG
            try
            {
                DiscordNoticeBridge.SelfCheckWebhookUrl();
                api.Logger.Notification("[NoticeBoard] SelfCheckWebhookUrl passed.");
                TheBasicsNick.SelfCheckNametagStrip();
                api.Logger.Notification("[NoticeBoard] SelfCheckNametagStrip passed.");
            }
            catch (Exception ex)
            {
                api.Logger.Error("[NoticeBoard] SelfCheckWebhookUrl failed: " + ex.Message);
            }
#endif
        }

        private TextCommandResult OnDiscordWebhookCommand(TextCommandCallingArgs args)
        {
            if (databaseHandler == null)
                return TextCommandResult.Error(Lang.Get("noticeboard:discord-cmd-unknown-board"));

            var handler = new SQLiteHandler();
            string boardId = args[0] as string;
            if (string.IsNullOrEmpty(boardId) || handler.GetBoardData(boardId) == null)
                return TextCommandResult.Error(Lang.Get("noticeboard:discord-cmd-unknown-board"));

            bool urlMissing = args.Parsers.Count < 2 || args.Parsers[1].IsMissing;
            string url = urlMissing ? null : args[1] as string;
            if (string.IsNullOrWhiteSpace(url))
            {
                handler.EditDiscordWebhook(new EditDiscordWebhook { BoardId = boardId, WebhookUrl = "" });
                RefreshBoardAfterDiscordEdit(handler, boardId);
                return TextCommandResult.Success(Lang.Get("noticeboard:discord-cmd-webhook-cleared"));
            }

            url = url.Trim();
            if (!DiscordNoticeBridge.IsWebhookUrl(url))
                return TextCommandResult.Error(Lang.Get("noticeboard:discord-cmd-webhook-invalid"));

            handler.EditDiscordWebhook(new EditDiscordWebhook { BoardId = boardId, WebhookUrl = url });
            RefreshBoardAfterDiscordEdit(handler, boardId);
            return TextCommandResult.Success(Lang.Get("noticeboard:discord-cmd-webhook-set"));
        }

        private TextCommandResult OnDiscordStatusCommand(TextCommandCallingArgs args)
        {
            if (databaseHandler == null)
                return TextCommandResult.Error(Lang.Get("noticeboard:discord-cmd-unknown-board"));

            var handler = new SQLiteHandler();
            string boardId = args[0] as string;
            NoticeBoardObject board = string.IsNullOrEmpty(boardId) ? null : handler.GetBoardData(boardId);
            if (board == null)
                return TextCommandResult.Error(Lang.Get("noticeboard:discord-cmd-unknown-board"));

            return board.HasDiscordWebhook != 0
                ? TextCommandResult.Success(Lang.Get("noticeboard:discord-cmd-status-set"))
                : TextCommandResult.Success(Lang.Get("noticeboard:discord-cmd-status-missing"));
        }

        private void RefreshBoardAfterDiscordEdit(SQLiteHandler handler, string boardId)
        {
            NoticeBoardObject data = handler.GetBoardData(boardId);
            if (data == null || string.IsNullOrEmpty(data.Pos))
                return;
            sapi.GetNoticeBoardEntity(PositionHelper.FromString(data.Pos))?.RefreshPaperVisuals();
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            if (NoticeBoardModSystem.databaseHandler == null)
            {
                sapi?.Logger.Warning("[NoticeBoard] Player joined before database was ready; skipping player registration.");
                return;
            }

            db = new SQLiteHandler();
            db.AddPlayerToDatabase(byPlayer.PlayerUID, byPlayer.PlayerName);
            db.SetPlayerDisplayName(
                byPlayer.PlayerUID,
                TheBasicsNick.Resolve(sapi, byPlayer, byPlayer.PlayerName));
        }
    }
}

