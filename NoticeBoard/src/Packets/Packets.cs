using ProtoBuf;
using System.Collections.Generic;

namespace NoticeBoard.src.Packets
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
        public string Pos { get; set; }
    }

    [ProtoContract]
    public class PlayerDestroyNoticeBoard
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }
    }

    [ProtoContract]
    public class ResponseIsActive
    {
        [ProtoMember(1)]
        public bool IsActive { get; set; }
    }

    [ProtoContract]
    public class PlayerRemoveMessage
    {
        [ProtoMember(1)]
        public int MessageId { get; set; }
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
    }

    [ProtoContract]
    public class ResponseAllMessages
    {
        [ProtoMember(1)]
        public string BoardId { get; internal set; }
        [ProtoMember(2)]
        public string PlayerId { get; set; }
        [ProtoMember(3)]
        public List<Message> Messages { get; internal set; }
    }

    [ProtoContract]
    public class Message
    {
        [ProtoMember(1)]
        public int Id { get; set; }
        [ProtoMember(2)]
        public string Text { get; set; }
    }

    [ProtoContract]
    public class NoticeBoardObject
    {
        [ProtoMember(1)]
        public string BoardId { get; set; }
        [ProtoMember(2)]
        public string Pos { get; set; }
    }
}
