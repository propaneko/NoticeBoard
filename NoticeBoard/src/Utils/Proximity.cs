using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace NoticeBoard.Utils
{
    public static class Proximity
    {
        public const int MinDistance = 1;
        public const int MaxDistance = 1000;
        public const int DefaultDistance = 100;

        public static int ClampDistance(int distance) =>
            GameMath.Clamp(distance, MinDistance, MaxDistance);

        public static void SendLocalChatByPlayer(
            IServerPlayer byPlayer,
            string message,
            string proximityGroupName,
            int distanceToBroadcast = DefaultDistance,
            EnumChatType chatType = EnumChatType.OthersMessage,
            string data = null
        )
        {
            PlayerGroup proximityGroup = NoticeBoardModSystem
                .getSAPI()
                .Groups.GetPlayerGroupByName(proximityGroupName);

            if (proximityGroup == null)
                return;

            foreach (
                var player in NoticeBoardModSystem
                    .getSAPI()
                    .World.AllOnlinePlayers.Where(x =>
                        x.Entity.Pos.AsBlockPos.ManhattanDistance(byPlayer.Entity.Pos.AsBlockPos)
                        < distanceToBroadcast
                    )
            )
            {
                var serverPlayer = player as IServerPlayer;
                if (serverPlayer == null)
                    continue;

                if (
                    serverPlayer.PlayerUID != byPlayer.PlayerUID
                    && serverPlayer.GetGroup(proximityGroup.Uid) == null
                )
                    continue;

                serverPlayer.SendMessage(proximityGroup.Uid, message, chatType, data);
            }
        }
    }
}
