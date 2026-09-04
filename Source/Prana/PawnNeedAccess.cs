using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MoonWorld
{
    // RimWorld exposes no public API for adding a Need to a living pawn.
    // Keeping this compatibility call here prevents that private API from leaking into gameplay modules.
    internal static class PawnNeedAccess
    {
        private static readonly MethodInfo AddNeed = AccessTools.Method(
            typeof(Pawn_NeedsTracker),
            "AddNeed",
            new[] { typeof(NeedDef) });

        internal static void EnsureNeed(Pawn pawn, NeedDef needDef)
        {
            if (pawn == null || needDef == null || pawn.needs.TryGetNeed(needDef) != null)
            {
                return;
            }
            if (AddNeed == null)
            {
                Log.ErrorOnce("MoonWorld could not locate Pawn_NeedsTracker.AddNeed.", 124824781);
                return;
            }
            AddNeed.Invoke(pawn.needs, new object[] { needDef });
        }
    }
}
