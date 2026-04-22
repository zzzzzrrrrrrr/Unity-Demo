using GameMain.GameLogic.Data;

namespace GameMain.GameLogic.World
{
    /// <summary>
    /// Runtime session state for selected role in current run.
    /// </summary>
    public static class RoleSelectionRuntimeState
    {
        public static bool IsConfirmed { get; private set; }

        public static RoleSelectionProfileData ConfirmedProfile { get; private set; }

        public static void SetConfirmedProfile(RoleSelectionProfileData profile)
        {
            ConfirmedProfile = profile;
            IsConfirmed = profile != null;
        }

        public static void Clear()
        {
            IsConfirmed = false;
            ConfirmedProfile = null;
        }
    }
}
