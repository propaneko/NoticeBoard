using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace NoticeBoard.Utils;

public static class TheBasicsNick
{
    // Reflection so TheBasics stays optional. If they rename RPProximityChatSystem / GetNickname, fall back to BASIC_NICKNAME then the account name.
    public static string Resolve(ICoreAPI api, IPlayer player, string fallback)
    {
        if (player is IServerPlayer server)
        {
            if (api?.ModLoader?.IsModEnabled("thebasics") == true)
            {
                string fromApi = TryGetNickname(api, server);
                if (!string.IsNullOrWhiteSpace(fromApi))
                    return fromApi.Trim();
            }

            string legacy = server.GetModData<string>("BASIC_NICKNAME", null);
            if (!string.IsNullOrWhiteSpace(legacy))
                return legacy.Trim();
        }
        else
        {
            string fromTag = FromNametag(player);
            if (!string.IsNullOrWhiteSpace(fromTag))
                return fromTag;
        }

        return fallback;
    }

    public static string StripNametagAccount(string nametagName, string account)
    {
        if (string.IsNullOrWhiteSpace(nametagName))
            return null;
        nametagName = nametagName.Trim();
        if (string.IsNullOrEmpty(account))
            return nametagName;
        string suffix = " (" + account + ")";
        if (nametagName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            nametagName = nametagName.Substring(0, nametagName.Length - suffix.Length).Trim();
        return string.IsNullOrWhiteSpace(nametagName) ? null : nametagName;
    }

    private static string FromNametag(IPlayer player)
    {
        ITreeAttribute nametag = player?.Entity?.WatchedAttributes?.GetTreeAttribute("nametag");
        return StripNametagAccount(nametag?.GetString("name"), player?.PlayerName);
    }

    private static string TryGetNickname(ICoreAPI api, IServerPlayer player)
    {
        try
        {
            ModSystem sys = api.ModLoader.GetModSystem(
                "thebasics.ModSystems.ProximityChat.RPProximityChatSystem");
            if (sys == null)
                return null;

            object config = sys.GetType().GetProperty("Config")?.GetValue(sys);
            Type ext = sys.GetType().Assembly.GetType(
                "thebasics.Extensions.IServerPlayerExtensions");
            if (ext == null)
                return null;

            MethodInfo method = config == null
                ? ext.GetMethod("GetNickname", new[] { typeof(IServerPlayer) })
                : ext.GetMethod("GetNickname", new[] { typeof(IServerPlayer), config.GetType() });
            if (method == null)
                return null;

            object[] args = config == null
                ? new object[] { player }
                : new object[] { player, config };
            return method.Invoke(null, args) as string;
        }
        catch
        {
            return null;
        }
    }

#if DEBUG
    public static void SelfCheckNametagStrip()
    {
        if (StripNametagAccount("Caden (Propaneko)", "Propaneko") != "Caden")
            throw new InvalidOperationException("strip nick+account");
        if (StripNametagAccount("Propaneko", "Propaneko") != "Propaneko")
            throw new InvalidOperationException("strip account-only");
    }
#endif
}
