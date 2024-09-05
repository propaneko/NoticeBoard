using NoticeBoard.src;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace NoticeBoard
{
    public class NoticeBoardModSystem : ModSystem
    {
        ICoreClientAPI capi;
        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass("NoticeBoardBlock", typeof(NoticeBoardBlock));
            api.RegisterBlockEntityClass("NoticeBoardBlockEntity", typeof(NoticeBoardBlockEntity));

            api.Logger.Notification("Hello from template mod: " + api.Side);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Logger.Notification("Hello from template mod server side: " + Lang.Get("noticeboard:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Logger.Notification("Hello from template mod client side: " + Lang.Get("noticeboard:hello"));
        }
    }
}
