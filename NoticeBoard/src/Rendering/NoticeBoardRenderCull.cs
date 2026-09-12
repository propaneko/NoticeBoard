using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace NoticeBoard.Rendering;

internal static class NoticeBoardRenderCull
{
    // Origin cell centre to far lantern / hanging sheet. After yaw the 2-block board is still inside.
    public const float SphereRadius = 4f;

    public static bool ShouldDraw(ICoreClientAPI api, BlockPos pos)
    {
        Vec3d cam = api.World.Player.Entity.CameraPos;
        double dx = pos.X + 0.5 - cam.X;
        double dy = pos.Y + 0.5 - cam.Y;
        double dz = pos.Z + 0.5 - cam.Z;
        if (dx * dx + dy * dy + dz * dz > api.Render.DefaultFrustumCuller.ViewDistanceSq)
            return false;

        return api.Render.DefaultFrustumCuller.SphereInFrustum(
            pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, SphereRadius);
    }
}
