using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
        [DllImport("gdi32.dll", EntryPoint = "AddFontResourceEx", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport(
            "libfontconfig.so.1",
            EntryPoint = "FcConfigAppFontAddFile",
            CharSet = CharSet.Ansi
        )]
        private static extern bool FcConfigAppFontAddFile(
            IntPtr config,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string file
        );

        [DllImport("libfontconfig.so.1", EntryPoint = "FcConfigGetCurrent")]
        private static extern IntPtr FcConfigGetCurrent();

        [DllImport("libfontconfig.so.1", EntryPoint = "FcConfigBuildFonts")]
        private static extern bool FcConfigBuildFonts(IntPtr config);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGDataProviderCreateWithFilename(string filename);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGFontCreateWithDataProvider(IntPtr provider);

        [DllImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
        private static extern bool CTFontManagerRegisterGraphicsFont(IntPtr font, out IntPtr error);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr obj);

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
                NoticeBoardModSystem.config = NoticeBoardModSystem.sapi.LoadModConfig<ModConfig>(
                    "noticeboard.json"
                );
            }
            catch (Exception)
            {
                NoticeBoardModSystem.sapi.Server.LogError(
                    "NoticeBoard: Failed to load mod config!",
                    Array.Empty<object>()
                );
                return;
            }
            if (NoticeBoardModSystem.config == null)
            {
                NoticeBoardModSystem.sapi.Server.LogNotification(
                    "NoticeBoards: non-existant modconfig at 'ModConfig/noticeboardnoticeboard.json', creating default...",
                    Array.Empty<object>()
                );
                NoticeBoardModSystem.config = new ModConfig();
                NoticeBoardModSystem.sapi.StoreModConfig<ModConfig>(
                    NoticeBoardModSystem.config,
                    "noticeboard.json"
                );
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

        public static List<string> ParseFontNames(string[] inputs)
        {
            var result = new List<string>();

            foreach (var input in inputs)
            {
                string dir = Path.GetDirectoryName(input) ?? "";
                string ext = Path.GetExtension(input);
                string withoutExt = Path.GetFileNameWithoutExtension(input);

                int dashIndex = withoutExt.LastIndexOf(" - ");
                string name = dashIndex >= 0 ? withoutExt[..dashIndex] : withoutExt;

                string fullPath = string.IsNullOrEmpty(dir)
                    ? name + ext
                    : Path.Combine(dir, name + ext);

                result.Add(fullPath);
            }

            return result;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            NoticeBoardModSystem.capi = api;
            new ClientMessageHandler().SetMessageHandlers();

            string fontsDir = Path.Combine(
                capi.ModLoader.GetMod("noticeboard").SourcePath,
                "assets",
                "noticeboard",
                "fonts"
            );

            if (!Directory.Exists(fontsDir))
            {
                api.Logger.Warning("[NoticeBoard] Fonts directory missing at: " + fontsDir);
                return;
            }

            string[] fontFiles =
            [
                .. Directory.GetFiles(fontsDir, "*.ttf"),
                .. Directory.GetFiles(fontsDir, "*.otf"),
            ];

            if (fontFiles.Length == 0)
            {
                api.Logger.Warning("[NoticeBoard] No font files found in: " + fontsDir);
                return;
            }

            foreach (string fontPath in fontFiles)
            {
                LoadFont(api, fontPath);
            }
        }

        private void LoadFont(ICoreClientAPI api, string fontPath)
        {
            if (!File.Exists(fontPath))
            {
                api.Logger.Warning("[NoticeBoard] Font file missing at: " + fontPath);
                return;
            }

            string fontName = Path.GetFileName(fontPath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AddFontResourceEx(fontPath, 0x10, IntPtr.Zero);
                api.Logger.Notification($"[NoticeBoard] Loaded font '{fontName}' on Windows.");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    IntPtr currentConfig = FcConfigGetCurrent();

                    if (currentConfig == IntPtr.Zero)
                    {
                        api.Logger.Warning(
                            $"[NoticeBoard] Could not retrieve current fontconfig configuration for '{fontName}'."
                        );
                        return;
                    }

                    if (FcConfigAppFontAddFile(currentConfig, fontPath))
                    {
                        FcConfigBuildFonts(currentConfig);

                        api.Logger.Notification(
                            $"[NoticeBoard] Loaded font '{fontName}' on Linux."
                        );
                    }
                    else
                    {
                        api.Logger.Warning(
                            $"[NoticeBoard] Linux fontconfig rejected font '{fontName}'."
                        );
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Error(
                        $"[NoticeBoard] Linux font loading failed for '{fontName}': " + ex.Message
                    );
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    IntPtr provider = CGDataProviderCreateWithFilename(fontPath);
                    if (provider != IntPtr.Zero)
                    {
                        IntPtr cgFont = CGFontCreateWithDataProvider(provider);
                        if (cgFont != IntPtr.Zero)
                        {
                            bool success = CTFontManagerRegisterGraphicsFont(
                                cgFont,
                                out IntPtr error
                            );

                            if (success)
                                api.Logger.Notification(
                                    $"[NoticeBoard] Loaded font '{fontName}' on macOS."
                                );
                            else
                                api.Logger.Warning(
                                    $"[NoticeBoard] macOS CoreText rejected font '{fontName}'."
                                );

                            CFRelease(cgFont);
                        }
                        CFRelease(provider);
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Error(
                        $"[NoticeBoard] macOS font loading failed for '{fontName}': " + ex.Message
                    );
                }
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            NoticeBoardModSystem.sapi = api;

            NoticeBoardModSystem.sapi.Event.ServerRunPhase(
                EnumServerRunPhase.ModsAndConfigReady,
                delegate()
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
