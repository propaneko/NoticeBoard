using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace NoticeBoard.Utilities
{
    public static class Proximity
    {
        public static void SendLocalChatByPlayer(IServerPlayer byPlayer, string message, int distanceToBroadcast, EnumChatType chatType = EnumChatType.OthersMessage, string data = null)
        {
            var proximityGroup = NoticeBoardModSystem.getSAPI().Groups.GetPlayerGroupByName("Proximity");

            foreach (var player in NoticeBoardModSystem.getSAPI().World.AllOnlinePlayers.Where(x =>
                         x.Entity.Pos.AsBlockPos.ManhattenDistance(byPlayer.Entity.Pos.AsBlockPos) < distanceToBroadcast))
            {
                var serverPlayer = player as IServerPlayer;

                serverPlayer.SendMessage(proximityGroup.Uid, message, chatType, data);
            }
        }
    }

    public static class GUI
    {
        public static double CalculateTextHeight(string text, double width)
        {
            CairoFont font = CairoFont.WhiteDetailText();
            return NoticeBoardModSystem.getCAPI().Gui.Text.GetMultilineTextHeight(font, text, width);
        }
    }
}