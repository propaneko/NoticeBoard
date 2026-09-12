using System;
using System.Collections.Generic;
using NoticeBoard.Packets;
using NoticeBoard.Rendering;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace NoticeBoard.Utils;

public static class NoticeParchment
{
    public const int BookTitleMax = 80; // ModSystemEditableBook.MaxTitleLength

    public static ItemStack FromNotice(
        IWorldAccessor world,
        string text,
        string playerName,
        int isAnonymous,
        int holder,
        string paperTheme,
        string playerUid,
        string date
    )
    {
        Item parchmentItem = world.GetItem(new AssetLocation("game", "paper-parchment"));
        if (parchmentItem == null)
            return null;

        ItemStack stack = new ItemStack(parchmentItem, 1);
        WriteBookMeta(stack.Attributes, text, playerName, isAnonymous, playerUid, date);
        stack.Attributes.SetInt("noticeboardHolder", MessageHolder.Clamp(holder));
        string theme = ThemeManager.Sanitize(paperTheme);
        if (theme != "")
            stack.Attributes.SetString("noticeboardTheme", theme);
        else
            stack.Attributes.RemoveAttribute("noticeboardTheme");
        return stack;
    }

    public static void WriteBookMeta(
        ITreeAttribute attrs,
        string text,
        string playerName,
        int isAnonymous,
        string playerUid,
        string date
    )
    {
        attrs.SetString("text", text ?? "");
        bool anon = isAnonymous != 0;
        string author = anon ? "" : (playerName ?? "");
        attrs.SetString("signedby", author);
        if (!anon && !string.IsNullOrEmpty(playerUid))
            attrs.SetString("signedbyuid", playerUid);
        else
            attrs.RemoveAttribute("signedbyuid");
        if (anon)
            attrs.SetInt("noticeboardAnonymous", 1);
        else
            attrs.RemoveAttribute("noticeboardAnonymous");
        attrs.SetString("title", BookTitle(author, date));
    }

    public static string BookTitle(string author, string date)
    {
        string title;
        if (!string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(date))
            title = author + ", " + date;
        else if (!string.IsNullOrEmpty(author))
            title = author;
        else if (!string.IsNullOrEmpty(date))
            title = date;
        else
            title = "Notice";
        if (title.Length > BookTitleMax)
            title = title.Substring(0, BookTitleMax);
        return title;
    }

    public static void ExpireAndDrop(
        ICoreServerAPI sapi,
        BlockPos pos,
        string boardId,
        List<ExpiredNoticeRow> expired,
        float rotateYDeg
    )
    {
        if (sapi == null || pos == null || expired == null || expired.Count == 0)
            return;

        var cal = sapi.World.Calendar;
        var stacks = new List<ItemStack>(expired.Count);
        var falls = new List<ExpiredNoticeFall>(expired.Count);
        foreach (ExpiredNoticeRow row in expired)
        {
            string date = GameDateFormatter.FormatImmersiveDate(
                cal.HoursPerDay, cal.DaysPerMonth, row.TotalHours, includeTime: false);
            ItemStack stack = FromNotice(
                sapi.World, row.Text, row.PlayerName, row.IsAnonymous,
                row.Holder, row.PaperTheme, row.PlayerId, date);
            stacks.Add(stack);

            falls.Add(
                new ExpiredNoticeFall
                {
                    Id = row.Id,
                    Text = row.Text,
                    Author = row.IsAnonymous == 1 ? "" : row.PlayerName,
                    TotalHours = row.TotalHours,
                    Holder = row.Holder,
                    PaperTheme = row.PaperTheme,
                    PinX = row.PinX,
                    PinY = row.PinY,
                    PinRotZ = row.PinRotZ,
                    PinLayer = row.PinLayer,
                    PaperSeed = row.PaperSeed,
                    HasPin = row.HasPin,
                }
            );
        }

        var packet = new ExpiredNoticesFall
        {
            BoardId = boardId,
            Notices = falls,
            Pos = pos,
        };

        Vec3d boardCenter = pos.ToVec3d();
        foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
        {
            if (player.ConnectionState != EnumClientState.Playing)
                continue;
            if (player.Entity.Pos.SquareDistanceTo(boardCenter) > 48 * 48)
                continue;
            sapi.Network.GetChannel("noticeboard").SendPacket(packet, player);
        }

        sapi.World.PlaySoundAt(
            new AssetLocation("noticeboard:sounds/effect/delete.ogg"),
            pos.X,
            pos.Y,
            pos.Z,
            null,
            true,
            32.0f,
            0.1f + (float)sapi.World.Rand.NextDouble() * 0.2f
        );

        // Delayed spawn uses IEventAPI.RegisterCallback with PaperFall.DurationMs.
        // If the server stops before land, those stacks never appear.
        bool isWall = sapi.World.BlockAccessor.GetBlock(pos)?.Variant?["attachment"] == "wall";
        float sheetZ = NoticeBoardPaperLayout.SheetZ(isWall);
        for (int i = 0; i < expired.Count; i++)
        {
            ItemStack stack = stacks[i];
            if (stack == null)
                continue;

            ExpiredNoticeRow row = expired[i];
            float tackX = row.HasPin ? row.PinX : 0.5f;
            float tackY = row.HasPin ? row.PinY : 1.5f;
            float tackZ = sheetZ + NoticeBoardPaperLayout.ClampPinLayer(row.PinLayer) * NoticeBoardPaperLayout.PinLayerStep;
            Vec3d start = PaperFall.LocalToWorld(pos, tackX, tackY, tackZ, rotateYDeg);
            double floorY = PaperFall.FindFloorY(sapi.World, start, pos.dimension);
            int delayMs = PaperFall.DurationMs(pos.Y + tackY, floorY);
            ItemStack captured = stack.Clone();
            float capX = tackX;
            float capY = tackY;
            float capZ = tackZ;
            double capFloor = floorY;
            sapi.Event.RegisterCallback(
                _ =>
                {
                    Vec3d land = PaperFall.LocalToWorld(
                        pos,
                        capX,
                        capY + (float)(capFloor - (pos.Y + capY)),
                        capZ + GameDateFormatter.FallDropOut,
                        rotateYDeg);
                    land.Y = capFloor + 0.05;
                    sapi.World.SpawnItemEntity(captured, land, new Vec3d(0, 0, 0));
                },
                delayMs);
        }
    }

#if DEBUG
    public static void SelfCheckBookMeta()
    {
        var t = new TreeAttribute();
        WriteBookMeta(t, "hello", "Ada", 0, "uid-1", "Day 1 of May, Year 1");
        if (t.GetString("text") != "hello") throw new InvalidOperationException("text");
        if (t.GetString("signedby") != "Ada") throw new InvalidOperationException("signedby");
        if (t.GetString("signedbyuid") != "uid-1") throw new InvalidOperationException("uid");
        if (t.GetString("title") != "Ada, Day 1 of May, Year 1") throw new InvalidOperationException("title");
        if (t.HasAttribute("noticeboardAnonymous")) throw new InvalidOperationException("anon flag");
        t = new TreeAttribute();
        WriteBookMeta(t, "hello", "Ada", 1, "uid-1", "Day 1 of May, Year 1");
        if (t.GetString("signedby") != "") throw new InvalidOperationException("anon signedby");
        if (!t.HasAttribute("signedby")) throw new InvalidOperationException("anon key");
        if (t.HasAttribute("signedbyuid")) throw new InvalidOperationException("anon uid");
        if (t.GetInt("noticeboardAnonymous") != 1) throw new InvalidOperationException("anon int");
        if (BookTitle("", "") != "Notice") throw new InvalidOperationException("fallback title");
        string longTitle = BookTitle(new string('A', 100), null);
        if (longTitle.Length != BookTitleMax) throw new InvalidOperationException("title cap");
    }
#endif
}
