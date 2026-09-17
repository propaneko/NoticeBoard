using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NoticeBoard.Packets;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace NoticeBoard.Utils;

public static class ScribeBridge
{
    private const int TitleCharLimit = 1000;

    private static readonly Regex BrTag = new Regex(
        @"<br\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );
    // Paired form (the composer's Item button): <itemstack ... code="x" ...>display text</itemstack>
    // Self-closing form: <itemstack code="x" />
    // Group 1 is the item code, group 2 the inner display text when the paired form matched.
    private static readonly Regex ItemstackTag = new Regex(
        @"<itemstack\b[^>]*?code\s*=\s*[""']?([A-Za-z0-9:_\-\./]+)[""']?[^>]*?(?:/>|>(.*?)</itemstack>)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled
    );
    private static readonly Regex AnyTag = new Regex(@"<[^>]+>", RegexOptions.Compiled);

    public static bool IsLoaded(ICoreAPI api)
    {
        if (api?.ModLoader == null)
            return false;
        if (api.ModLoader.IsModEnabled("scribe"))
            return true;
        foreach (var mod in api.ModLoader.Mods)
        {
            if (string.Equals(mod.Info?.ModID, "scribe", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static bool TryAddNotice(
        ICoreServerAPI sapi,
        IServerPlayer player,
        NoticeBoardObject board,
        Message message
    )
    {
        if (sapi == null || player == null || message == null)
            return false;
        if (!IsLoaded(sapi))
            return false;

        SplitNotice(ToPlainText(message.Text), out string title, out string body);
        string boardName = string.IsNullOrEmpty(board?.BoardName) ? "Notice Board" : board.BoardName;
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
            title = boardName;

        string author =
            message.IsAnonymous != 0
                ? Lang.Get("noticeboard:discord-someone")
                : message.PlayerName;
        string date = "";
        var cal = sapi.World?.Calendar;
        if (cal != null)
            date = GameDateFormatter.FormatImmersiveDate(
                cal.HoursPerDay,
                cal.DaysPerMonth,
                message.TotalHours,
                includeTime: false
            );
        string extraInfo = Lang.Get("noticeboard:scribe-extra-info", boardName, author, date);
        extraInfo = string.IsNullOrWhiteSpace(extraInfo) ? null : extraInfo.Trim();

        ModSystem sys = sapi.ModLoader.GetModSystem("Scribe.ScribeModSystem");
        if (sys == null)
            return false;
        MethodInfo method = sys.GetType()
            .GetMethod(
                "TryCreateExternalTask",
                new[] { typeof(IServerPlayer), typeof(string), typeof(string), typeof(string) }
            );
        if (method == null)
        {
            player.SendIngameError(
                "scribe-api-missing",
                Lang.Get("noticeboard:scribe-api-missing")
            );
            return false;
        }
        try
        {
            object result = method.Invoke(sys, new object[] { player, title, body, extraInfo });
            return result is bool ok && ok;
        }
        catch (Exception ex)
        {
            sapi.Logger.Warning("[NoticeBoard] Scribe TryCreateExternalTask failed: " + ex.Message);
            return false;
        }
    }

    internal static string ToPlainText(string vtml)
    {
        if (string.IsNullOrEmpty(vtml))
            return "";
        string s = vtml.Replace("\r\n", "\n").Replace('\r', '\n');
        s = BrTag.Replace(s, "\n");
        s = ItemstackTag.Replace(s, ItemstackText);
        s = AnyTag.Replace(s, "");
        s = s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
        return s.Trim();
    }

    // The paper draws an item as an icon plus its display text. Plain text keeps the display text,
    // and falls back to the code's last path segment when the tag has no inner text.
    private static string ItemstackText(Match m)
    {
        string inner = m.Groups[2].Success ? m.Groups[2].Value.Trim() : "";
        if (inner.Length > 0)
            return inner;
        string code = m.Groups[1].Value;
        int colon = code.LastIndexOf(':');
        return colon >= 0 ? code.Substring(colon + 1) : code;
    }

    internal static void SplitNotice(string plain, out string title, out string body)
    {
        title = null;
        body = null;
        if (string.IsNullOrEmpty(plain))
            return;

        int nl = plain.IndexOf('\n');
        string head;
        string tail;
        if (nl < 0)
        {
            head = plain;
            tail = "";
        }
        else
        {
            head = plain.Substring(0, nl);
            tail = plain.Substring(nl + 1);
        }

        if (string.IsNullOrWhiteSpace(head))
        {
            head = tail;
            tail = "";
        }

        if (head.Length > TitleCharLimit)
        {
            title = head.Substring(0, TitleCharLimit);
            string remainder = head.Substring(TitleCharLimit);
            body = string.IsNullOrEmpty(tail) ? remainder : remainder + "\n" + tail;
        }
        else
        {
            title = head;
            body = string.IsNullOrEmpty(tail) ? null : tail;
        }

        if (string.IsNullOrEmpty(title))
            title = null;
        if (string.IsNullOrEmpty(body))
            body = null;
    }

#if DEBUG
    public static void SelfCheckPlainText()
    {
        SplitNotice(ToPlainText("hello"), out string title, out string body);
        if (title != "hello" || body != null)
            throw new InvalidOperationException("hello");

        SplitNotice(ToPlainText("title\nbody"), out title, out body);
        if (title != "title" || body != "body")
            throw new InvalidOperationException("title-body");

        SplitNotice(ToPlainText("<b>hi</b>"), out title, out body);
        if (title != "hi" || body != null)
            throw new InvalidOperationException("bold");

        SplitNotice(ToPlainText("a<br>b"), out title, out body);
        if (title != "a" || body != "b")
            throw new InvalidOperationException("br");

        SplitNotice(ToPlainText("<itemstack code=\"game:ingot-copper\" />"), out title, out body);
        if (title != "ingot-copper" || body != null)
            throw new InvalidOperationException("itemstack");

        SplitNotice(
            ToPlainText(
                "<itemstack floattype=\"left\" type=\"block\" code=\"game:packeddirt\" rsize=\"1\" offx=\"0\" offy=\"0\">dirt</itemstack> is needed"
            ),
            out title,
            out body
        );
        if (title != "dirt is needed" || body != null)
            throw new InvalidOperationException("itemstack-pair");

        SplitNotice(ToPlainText("<icon name=dice> Dice night"), out title, out body);
        if (title != "Dice night" || body != null)
            throw new InvalidOperationException("icon");

        SplitNotice(ToPlainText("<a href='handbook://item-flint'>flint</a> needed"), out title, out body);
        if (title != "flint needed" || body != null)
            throw new InvalidOperationException("link");

        SplitNotice(ToPlainText(new string('x', 1001)), out title, out body);
        if (title == null || title.Length != 1000 || body == null || body.Length != 1)
            throw new InvalidOperationException("title-overflow");
    }
#endif
}
