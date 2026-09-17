using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Utils
{
    public static class ClaimAccess
    {
        public static bool MayTraverse(IWorldAccessor world, IPlayer player, BlockPos pos)
        {
            if (player == null || pos == null)
                return false;

            LandClaim[] claims = world?.Claims?.Get(pos);
            if (claims == null || claims.Length == 0)
                return true;

            foreach (LandClaim claim in claims)
            {
                if (claim == null)
                    continue;

                if (claim.AllowTraverseEveryone || claim.AllowUseEveryone)
                    return true;

                if (claim.TestPlayerAccess(player, EnumBlockAccessFlags.Traverse) != EnumPlayerAccessResult.Denied)
                    return true;
            }

            return false;
        }
    }
}
