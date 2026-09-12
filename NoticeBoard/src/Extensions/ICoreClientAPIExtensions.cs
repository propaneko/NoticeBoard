using NoticeBoard.BlockType;
using NoticeBoard.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace NoticeBoard.Extensions
{
    public static class ICoreClientAPIExtensions
    {
        public static NoticeBoardBlockEntity GetNoticeBoardEntity(this ICoreAPI api, BlockPos pos)
        {
            if (pos == null)
                return null;

            if (api.World.BlockAccessor.GetBlockEntity(pos) is NoticeBoardBlockEntity be)
                return be;

            Block block = api.World.BlockAccessor.GetBlock(pos);
            if (block is BlockMultiblock mb && mb.OffsetInv != null)
                return api.World.BlockAccessor.GetBlockEntity(pos.AddCopy(mb.OffsetInv)) as NoticeBoardBlockEntity;

            return null;
        }
    }
}
