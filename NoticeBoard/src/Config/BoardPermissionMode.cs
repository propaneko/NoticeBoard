using Vintagestory.API.MathTools;

namespace NoticeBoard.Configs
{
    public enum BoardPermissionMode
    {
        Default = 0,
        All = 1,
        Locked = 2,
    }

    public static class BoardPermission
    {
        public const int Default = (int)BoardPermissionMode.Default;
        public const int All = (int)BoardPermissionMode.All;
        public const int Locked = (int)BoardPermissionMode.Locked;

        public static int ClampMode(int mode) => GameMath.Clamp(mode, Default, Locked);
    }
}
