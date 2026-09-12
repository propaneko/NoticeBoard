using System;

namespace NoticeBoard;

public class MessageVisualData
{
    public string Text { get; set; }
    public string Author { get; set; }
    public string Date { get; set; }
    public int Holder { get; set; }
    public string PaperTheme { get; set; }
    public bool HasPin { get; set; }
    public float PinX { get; set; }
    public float PinY { get; set; }
    public bool HasPinRotZ { get; set; }
    public float PinRotZ { get; set; }
    public int PinLayer { get; set; }
    public double TotalHours { get; set; }
    public int PaperSeed { get; set; }

    public MessageVisualData() { }

    public MessageVisualData(string text, string author, string date, int holder = 0, string paperTheme = "")
    {
        Text = text;
        Author = author;
        Date = date;
        Holder = holder;
        PaperTheme = paperTheme ?? "";
    }

    public int ResolveParchmentSeed(int messageId) => PaperSeed != 0 ? PaperSeed : messageId;

    public static int NewPaperSeed()
    {
        int seed = unchecked((int)((uint)Environment.TickCount * 0x9E3779B1u ^ (uint)Guid.NewGuid().GetHashCode()));
        return seed == 0 ? 1 : seed;
    }
}

public class ExpiredNoticeRow
{
    public int Id { get; set; }
    public string Text { get; set; }
    public string PlayerName { get; set; }
    public int IsAnonymous { get; set; }
    public int Holder { get; set; }
    public string PaperTheme { get; set; }
    public float PinX { get; set; }
    public float PinY { get; set; }
    public float PinRotZ { get; set; }
    public bool HasPin { get; set; }
    public int PinLayer { get; set; }
    public int PaperSeed { get; set; }
    public double TotalHours { get; set; }
    public string PlayerId { get; set; }
}
