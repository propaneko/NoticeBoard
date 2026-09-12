using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Packets
{
    [ProtoContract]
    public class RequestAllMessages
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string PlayerId { get; set; }
    }

    [ProtoContract]
    public class PlayerCreateNoticeBoard
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string PlayerId { get; set; }

        [ProtoMember(3)]
        public string Pos { get; set; }
    }

    [ProtoContract]
    public class PlayerRemoveMessage
    {
        [ProtoMember(1)]
        public int MessageId { get; set; }

        [ProtoMember(2)]
        public string BoardId { get; set; }
    }

    [ProtoContract]
    public class PlayerBumpMessage
    {
        [ProtoMember(1)]
        public int MessageId { get; set; }

        [ProtoMember(2)]
        public string BoardId { get; set; }
    }

    [ProtoContract]
    public class PlayerSendMessage
    {
        [ProtoMember(1)]
        public string Message { get; set; }

        [ProtoMember(2)]
        public string BoardId { get; set; }

        [ProtoMember(3)]
        public string PlayerId { get; set; }

        [ProtoMember(4)]
        public double TotalHours { get; set; }

        [ProtoMember(5)]
        public int IsAnonymous { get; set; }

        [ProtoMember(6)]
        public int Holder { get; set; }

        [ProtoMember(7)]
        public string PaperTheme { get; set; }

        [ProtoMember(8)]
        public float PinX { get; set; }

        [ProtoMember(9)]
        public float PinY { get; set; }

        [ProtoMember(10)]
        public bool HasPin { get; set; }

        [ProtoMember(11)]
        public float PinRotZ { get; set; }

        [ProtoMember(12)]
        public int PinLayer { get; set; }

        [ProtoMember(13)]
        public bool HasWaypoint { get; set; }

        [ProtoMember(14)]
        public float WaypointX { get; set; }

        [ProtoMember(15)]
        public float WaypointZ { get; set; }

        [ProtoMember(16)]
        public int PaperSeed { get; set; }

        [ProtoMember(17)]
        public string WaypointTitle { get; set; }

        [ProtoMember(18)]
        public string WaypointIcon { get; set; }

        [ProtoMember(19)]
        public string WaypointColor { get; set; }
    }

    [ProtoContract]
    public class PlayerSendDocument
    {
        [ProtoMember(1)]
        public string Document { get; set; }

        [ProtoMember(2)]
        public string BoardId { get; set; }

        [ProtoMember(3)]
        public string PlayerId { get; set; }

        [ProtoMember(4)]
        public double TotalHours { get; set; }

        [ProtoMember(5)]
        public int Holder { get; set; }

        [ProtoMember(6)]
        public string PaperTheme { get; set; }

        [ProtoMember(7)]
        public float PinX { get; set; }

        [ProtoMember(8)]
        public float PinY { get; set; }

        [ProtoMember(9)]
        public bool HasPin { get; set; }

        [ProtoMember(10)]
        public float PinRotZ { get; set; }

        [ProtoMember(11)]
        public int PinLayer { get; set; }

        [ProtoMember(12)]
        public int PaperSeed { get; set; }

        [ProtoMember(13)]
        public int IsAnonymous { get; set; }
    }

    [ProtoContract]
    public class PlayerEditMessage
    {
        [ProtoMember(1)]
        public int Id { get; set; }

        [ProtoMember(2)]
        public string Message { get; set; }

        [ProtoMember(3)]
        public string BoardId { get; set; }

        [ProtoMember(4)]
        public double TotalHours { get; set; }
        [ProtoMember(5)]
        public int IsAnonymous { get; set; }

        [ProtoMember(6)]
        public int Holder { get; set; }

        [ProtoMember(7)]
        public string PaperTheme { get; set; }

        [ProtoMember(8)]
        public bool HasWaypoint { get; set; }

        [ProtoMember(9)]
        public float WaypointX { get; set; }

        [ProtoMember(10)]
        public float WaypointZ { get; set; }

        [ProtoMember(11)]
        public string WaypointTitle { get; set; }

        [ProtoMember(12)]
        public string WaypointIcon { get; set; }

        [ProtoMember(13)]
        public string WaypointColor { get; set; }
    }

    [ProtoContract]
    public class PlayerRepositionMessage
    {
        [ProtoMember(1)]
        public int Id { get; set; }

        [ProtoMember(2)]
        public string BoardId { get; set; }

        [ProtoMember(3)]
        public float PinX { get; set; }

        [ProtoMember(4)]
        public float PinY { get; set; }

        [ProtoMember(5)]
        public float PinRotZ { get; set; }

        [ProtoMember(6)]
        public int PinLayer { get; set; }
    }

    [ProtoContract]
    public class ResponseAllMessages
    {
        [ProtoMember(1)]
        public List<Message> Messages { get; internal set; }

        [ProtoMember(2)]
        public NoticeBoardObject BoardProperties { get; set; }
    }

    [ProtoContract]
    public class EditPermissionMode
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public int PermissionMode { get; set; }
    }

    [ProtoContract]
    public class EditEnableParticles
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public bool EnableParticles { get; set; }
    }

    [ProtoContract]
    public class EditEnableNoticeAging
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public bool EnableNoticeAging { get; set; }
    }

    [ProtoContract]
    public class EditNoticeAgingDays
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public int NoticeAgingDays { get; set; }
    }

    [ProtoContract]
    public class EditEnableParchment
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public bool EnableParchment { get; set; }
    }

    [ProtoContract]
    public class EditEnableManualPin
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public bool EnableManualPin { get; set; }
    }

    [ProtoContract]
    public class PersistPaperPins
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public int[] Ids { get; set; }

        [ProtoMember(3)]
        public float[] Xs { get; set; }

        [ProtoMember(4)]
        public float[] Ys { get; set; }

        [ProtoMember(5)]
        public float[] Rots { get; set; }
    }

    [ProtoContract]
    public class EditBoardName
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string BoardName { get; set; }
    }

    [ProtoContract]
    public class EditBoardOwner
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string NewOwnerUid { get; set; }
    }

    [ProtoContract]
    public class EditBoardFont
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string BoardFont { get; set; }
    }

    [ProtoContract]
    public class EditBoardTheme
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string BoardTheme { get; set; }
    }

    [ProtoContract]
    public class EditEnableProximity
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public bool EnableProximity { get; set; }
    }

    [ProtoContract]
    public class EditProximityChannel
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string ChannelName { get; set; }
    }

    [ProtoContract]
    public class EditProximityDistance
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public int Distance { get; set; }
    }

    [ProtoContract]
    public class EditEnableDiscord
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public bool EnableDiscord { get; set; }
    }

    [ProtoContract]
    public class EditDiscordWebhook
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string WebhookUrl { get; set; }
    }

    [ProtoContract]
    public class EditBoardFontSize
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public float BoardFontSize { get; set; }
    }

    [ProtoContract]
    public class EditMaxPapersOnBoard
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public int MaxPapersOnBoard { get; set; }
    }

    [ProtoContract]
    public class EditEnableLegacyBoard
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public bool EnableLegacyBoard { get; set; }
    }

    [ProtoContract]
    public class EditBoardTextSharpness
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public int TextSharpness { get; set; }
    }

    [ProtoContract]
    public class EditBoardSwayStrength
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public int SwayStrength { get; set; }
    }

    [ProtoContract]
    public class Message
    {
        [ProtoMember(1)]
        public int Id { get; set; }

        [ProtoMember(2)]
        public string Text { get; set; }

        [ProtoMember(3)]
        public string PlayerId { get; set; }

        [ProtoMember(4)]
        public string PlayerName { get; set; }

        [ProtoMember(5)]
        public double TotalHours { get; set; }

        [ProtoMember(6)]
        public int IsAnonymous { get; set; }

        [ProtoMember(7)]
        public DateTime CreatedAt { get; set; }

        [ProtoMember(8)]
        public int Holder { get; set; }

        [ProtoMember(9)]
        public string PaperTheme { get; set; }

        [ProtoMember(10)]
        public string BoardId { get; set; }

        [ProtoMember(11)]
        public bool HasWaypoint { get; set; }

        [ProtoMember(12)]
        public float WaypointX { get; set; }

        [ProtoMember(13)]
        public float WaypointZ { get; set; }

        [ProtoMember(14)]
        public int PaperSeed { get; set; }

        [ProtoMember(15)]
        public string WaypointTitle { get; set; }

        [ProtoMember(16)]
        public string WaypointIcon { get; set; }

        [ProtoMember(17)]
        public string WaypointColor { get; set; }
    }

    [ProtoContract]
    public class NoticeBoardObject
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }

        [ProtoMember(2)]
        public string BoardName { get; set; }

        [ProtoMember(3)]
        public string BoardFont { get; set; }

        [ProtoMember(4)]
        public float BoardFontSize { get; set; }

        [ProtoMember(5)]
        public string BoardTheme { get; set; }

        [ProtoMember(6)]
        public string PlayerId { get; set; }

        [ProtoMember(7)]
        public string PlayerName { get; set; }

        [ProtoMember(8)]
        public string Pos { get; set; }

        [ProtoMember(9)]
        public int PermissionMode { get; set; }

        [ProtoMember(10)]
        public int EnableParticles { get; set; }

        [ProtoMember(11)]
        public int EnableParchment { get; set; }

        [ProtoMember(12)]
        public int EnableProximity { get; set; }

        [ProtoMember(13)]
        public string ProximityChannel { get; set; }

        [ProtoMember(14)]
        public int ProximityDistance { get; set; }

        [ProtoMember(15)]
        public int MaxPapersOnBoard { get; set; }

        [ProtoMember(16)]
        public int EnableLegacyBoard { get; set; }

        [ProtoMember(17)]
        public int TextSharpness { get; set; }

        [ProtoMember(18)]
        public int EnableManualPin { get; set; }

        [ProtoMember(19)]
        public int SwayStrength { get; set; }

        [ProtoMember(20)]
        public int EnableNoticeAging { get; set; }

        [ProtoMember(21)]
        public int NoticeAgingDays { get; set; }

        [ProtoMember(22)]
        public int EnableDiscord { get; set; }

        [ProtoMember(23)]
        public int HasDiscordWebhook { get; set; }
    }

    [ProtoContract]
    public class RequestAllPlayers { }

    [ProtoContract]
    public class ResponseAllPlayers
    {
        [ProtoMember(1)]
        public List<PlayerEntry> Players { get; set; }
    }

    [ProtoContract]
    public class PlayerEntry
    {
        [ProtoMember(1)]
        public string PlayerUID { get; set; }

        [ProtoMember(2)]
        public string PlayerName { get; set; }
    }

    [ProtoContract]
    public class UnreadParticlesPacket
    {
        [ProtoMember(1)]
        public BlockPos Pos { get; set; }
    }

    [ProtoContract]
    public class ExpiredNoticesFall
    {
        [ProtoMember(1)] public string BoardId { get; set; }
        [ProtoMember(2)] public List<ExpiredNoticeFall> Notices { get; set; }
        [ProtoMember(3)] public BlockPos Pos { get; set; }
    }

    [ProtoContract]
    public class ExpiredNoticeFall
    {
        [ProtoMember(1)] public int Id { get; set; }
        [ProtoMember(2)] public string Text { get; set; }
        [ProtoMember(3)] public string Author { get; set; }
        [ProtoMember(4)] public double TotalHours { get; set; }
        [ProtoMember(5)] public int Holder { get; set; }
        [ProtoMember(6)] public string PaperTheme { get; set; }
        [ProtoMember(7)] public float PinX { get; set; }
        [ProtoMember(8)] public float PinY { get; set; }
        [ProtoMember(9)] public float PinRotZ { get; set; }
        [ProtoMember(10)] public int PinLayer { get; set; }
        [ProtoMember(11)] public int PaperSeed { get; set; }
        [ProtoMember(12)] public bool HasPin { get; set; }
    }
}
