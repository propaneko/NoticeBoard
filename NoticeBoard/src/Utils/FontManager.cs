using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Vintagestory.API.Client;

namespace NoticeBoard
{
    public static class FontManager
    {
        public static string[] FontDisplayNames { get; private set; } = ["Default"];
        public static string[] FontFileNames { get; private set; } = ["Default"];

        [DllImport("gdi32.dll", EntryPoint = "AddFontResourceEx", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport("libfontconfig.so.1", EntryPoint = "FcConfigAppFontAddFile", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FcConfigAppFontAddFile(IntPtr config, [MarshalAs(UnmanagedType.LPUTF8Str)] string file);

        [DllImport("libfontconfig.so.1", EntryPoint = "FcConfigGetCurrent", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FcConfigGetCurrent();

        [DllImport("libfontconfig.so.1", EntryPoint = "FcConfigBuildFonts", CallingConvention = CallingConvention.Cdecl)]
        private static extern int FcConfigBuildFonts(IntPtr config);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGDataProviderCreateWithFilename([MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern IntPtr CGFontCreateWithDataProvider(IntPtr provider);

        [DllImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
        private static extern bool CTFontManagerRegisterGraphicsFont(IntPtr font, out IntPtr error);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr obj);

        public static void InitializeFonts(ICoreClientAPI api)
        {
            try
            {
                InitializeFontsInternal(api);
            }
            catch (DllNotFoundException ex)
            {
                api.Logger.Error("[NoticeBoard] Font loading library not found: " + ex.Message);
            }
            catch (Exception ex)
            {
                api.Logger.Error("[NoticeBoard] Font initialization failed: " + ex.Message);
            }
        }

        private static void InitializeFontsInternal(ICoreClientAPI api)
        {
            string sourcePath = api.ModLoader.GetMod("noticeboard").SourcePath;
            string fontsDir = "";

            if (sourcePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                string zipFileName = Path.GetFileName(sourcePath);
                string unpackBaseDir = Path.Combine(api.GetOrCreateDataPath("Cache"), "unpack");

                if (Directory.Exists(unpackBaseDir))
                {
                    string[] matchingDirs = Directory.GetDirectories(unpackBaseDir, zipFileName + "_*");
                    if (matchingDirs.Length > 0)
                    {
                        fontsDir = Path.Combine(matchingDirs[0], "assets", "noticeboard", "fonts");
                    }
                }
            }
            else
            {
                fontsDir = Path.Combine(sourcePath, "assets", "noticeboard", "fonts");
            }

            if (string.IsNullOrEmpty(fontsDir) || !Directory.Exists(fontsDir))
            {
                api.Logger.Warning($"[NoticeBoard] Could not locate fonts directory. Searched for: {fontsDir}");
                return;
            }

            string[] fontFiles = Directory
                .GetFiles(fontsDir)
                .Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (fontFiles.Length == 0)
            {
                api.Logger.Warning("[NoticeBoard] No font files found in: " + fontsDir);
                return;
            }

            string[] rawNames = fontFiles.Select(Path.GetFileNameWithoutExtension).ToArray();

            FontFileNames = ["Default", .. rawNames];
            FontDisplayNames = ["Default", .. ParseFontNames(rawNames)];

            foreach (string file in fontFiles)
            {
                LoadFont(api, file);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    IntPtr currentConfig = FcConfigGetCurrent();
                    if (currentConfig != IntPtr.Zero && FcConfigBuildFonts(currentConfig) != 0)
                    {
                        api.Logger.Notification("[NoticeBoard] Successfully built Linux fontconfig cache.");
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Error("[NoticeBoard] Error building Linux font cache: " + ex.Message);
                }
            }
        }

        private static void LoadFont(ICoreClientAPI api, string fontPath)
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
                        api.Logger.Warning($"[NoticeBoard] Could not retrieve current fontconfig configuration for '{fontName}'.");
                        return;
                    }

                    if (FcConfigAppFontAddFile(currentConfig, fontPath) != 0)
                    {
                        api.Logger.Notification($"[NoticeBoard] Loaded font '{fontName}' on Linux.");
                    }
                    else
                    {
                        api.Logger.Warning($"[NoticeBoard] Linux fontconfig rejected font '{fontName}'.");
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Error($"[NoticeBoard] Linux font loading failed for '{fontName}': " + ex.Message);
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
                            bool success = CTFontManagerRegisterGraphicsFont(cgFont, out IntPtr error);

                            if (success)
                            {
                                api.Logger.Notification($"[NoticeBoard] Loaded font '{fontName}' on macOS.");
                            }
                            else
                            {
                                api.Logger.Warning($"[NoticeBoard] macOS CoreText rejected font '{fontName}'.");
                                if (error != IntPtr.Zero)
                                {
                                    CFRelease(error);
                                }
                            }

                            CFRelease(cgFont);
                        }
                        CFRelease(provider);
                    }
                }
                catch (Exception ex)
                {
                    api.Logger.Error($"[NoticeBoard] macOS font loading failed for '{fontName}': " + ex.Message);
                }
            }
        }

        private static string[] ParseFontNames(string[] inputs)
        {
            var result = new string[inputs.Length];

            for (int i = 0; i < inputs.Length; i++)
            {
                string input = inputs[i];
                string dir = Path.GetDirectoryName(input) ?? "";
                string ext = Path.GetExtension(input);
                string withoutExt = Path.GetFileNameWithoutExtension(input);

                int dashIndex = withoutExt.LastIndexOf(" - ");
                string name = dashIndex >= 0 ? withoutExt[..dashIndex] : withoutExt;

                result[i] = string.IsNullOrEmpty(dir) ? name + ext : Path.Combine(dir, name + ext);
            }

            return result;
        }
    }
}
