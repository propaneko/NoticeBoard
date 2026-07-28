using NoticeBoard.BlockType;
using NoticeBoard.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Extensions
{
    public static class ICoreClientAPIExtensions
    {
        public static NoticeBoardBlockEntity GetNoticeBoardEntity(this ICoreAPI api, BlockPos pos)
        {
            if (pos == null)
                return null;

            return api.World.BlockAccessor.GetBlockEntity(pos) as NoticeBoardBlockEntity;
        }
    }
}
