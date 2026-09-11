using System;

namespace MoonWorld
{
    internal static class HolyGrailEndingPolicy
    {
        internal static int DeadlineFor(int nowTickAbs, int delayTicks)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(0L, (long)nowTickAbs + Math.Max(0, delayTicks)));
        }

        internal static bool DeadlineDue(bool materializationGranted, int deadlineTickAbs, int nowTickAbs)
        {
            return !materializationGranted && deadlineTickAbs >= 0 && nowTickAbs >= deadlineTickAbs;
        }

        internal static bool ShouldDismiss(bool exists, bool playerOwned, bool permanentlyMaterialized, bool annihilated)
        {
            return exists && playerOwned && !permanentlyMaterialized && !annihilated;
        }
    }
}
