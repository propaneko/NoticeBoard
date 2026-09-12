using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NoticeBoard.Database;
using NoticeBoard.Packets;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace NoticeBoard.Utils;

public static class DiscordNoticeBridge
{
    private static readonly HttpClient Client = new HttpClient(
        new HttpClientHandler { AllowAutoRedirect = false }
    )
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public static bool IsWebhookUrl(string url)
    {
        if (
            !Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
        )
            return false;

        if (IPAddress.TryParse(uri.Host, out _))
            return false;

        string host = uri.Host;
        bool discordHost =
            host.Equals("discord.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("discordapp.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".discord.com", StringComparison.OrdinalIgnoreCase);
        if (!discordHost)
            return false;

        string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 4
            && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && parts[1].Equals("webhooks", StringComparison.OrdinalIgnoreCase)
            && parts[2].Length > 0
            && parts[3].Length > 0;
    }

    public static void TryNotify(
        ICoreServerAPI sapi,
        SQLiteHandler db,
        NoticeBoardObject board,
        string displayNameOrNull
    )
    {
        if (sapi == null || db == null || board == null || board.EnableDiscord != 1)
            return;

        string url = db.GetDiscordWebhook(board.BoardId);
        if (string.IsNullOrWhiteSpace(url) || !IsWebhookUrl(url))
        {
            if (board.HasDiscordWebhook != 0)
                sapi.Logger.Warning("[NoticeBoard] Discord webhook for a board is set but invalid.");
            return;
        }

        string name = displayNameOrNull ?? Lang.Get("noticeboard:discord-someone");
        string boardName = string.IsNullOrEmpty(board.BoardName) ? "Notice Board" : board.BoardName;
        var cal = sapi.World.Calendar;
        if (cal == null)
            return;
        string date = GameDateFormatter.FormatImmersiveDate(
            cal.HoursPerDay, cal.DaysPerMonth, cal.TotalHours, includeTime: false);
        string text = Lang.Get("noticeboard:discord-new-notice", name, boardName, date);
        if (text.Length > 2000)
            text = text.Substring(0, 2000);

        var payload = new Dictionary<string, object>
        {
            ["content"] = text,
            ["allowed_mentions"] = new Dictionary<string, object>
            {
                ["parse"] = Array.Empty<string>(),
            },
        };
        string json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Fire-and-forget; HTTP 429s are dropped.
        _ = Client
            .PostAsync(url, content)
            .ContinueWith(task =>
            {
                if (!task.IsCompletedSuccessfully)
                {
                    sapi.Logger.Warning("[NoticeBoard] Discord webhook post failed.");
                    return;
                }

                using HttpResponseMessage response = task.Result;
                if (!response.IsSuccessStatusCode)
                    sapi.Logger.Warning(
                        $"[NoticeBoard] Discord webhook post failed: {(int)response.StatusCode}"
                    );
            });
    }

#if DEBUG
    public static void SelfCheckWebhookUrl()
    {
        if (!IsWebhookUrl("https://discord.com/api/webhooks/1/abc"))
            throw new InvalidOperationException("discord.com webhook must pass.");
        if (!IsWebhookUrl("https://discordapp.com/api/webhooks/1/abc"))
            throw new InvalidOperationException("discordapp.com webhook must pass.");
        if (!IsWebhookUrl("https://canary.discord.com/api/webhooks/1/abc"))
            throw new InvalidOperationException("canary.discord.com webhook must pass.");
        if (IsWebhookUrl("http://discord.com/api/webhooks/1/abc"))
            throw new InvalidOperationException("http webhook must fail.");
        if (IsWebhookUrl("https://example.com/api/webhooks/1/abc"))
            throw new InvalidOperationException("example.com webhook must fail.");
        if (IsWebhookUrl("https://discord.com.evil.com/api/webhooks/1/abc"))
            throw new InvalidOperationException("discord.com.evil.com webhook must fail.");
        if (IsWebhookUrl("https://127.0.0.1/api/webhooks/1/abc"))
            throw new InvalidOperationException("loopback webhook must fail.");
    }
#endif
}
