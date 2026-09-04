using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.JobTrackerTick))]
    public static class Harmony_SpiritForm_JobTrackerTick
    {
        public static bool Prefix(Pawn ___pawn)
        {
            return !ServantQuery.Instance.IsSpirit(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Harmony_SpiritForm_StartJob
    {
        public static bool Prefix(Pawn ___pawn)
        {
            return !ServantQuery.Instance.IsSpirit(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.TryStartAttack))]
    public static class Harmony_SpiritForm_TryStartAttack
    {
        public static bool Prefix(Pawn __instance, ref bool __result)
        {
            if (!ServantQuery.Instance.IsSpirit(__instance))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.Available))]
    public static class Harmony_SpiritForm_VerbAvailable
    {
        public static void Postfix(Verb __instance, ref bool __result)
        {
            if (__result && ServantQuery.Instance.IsSpirit(__instance.CasterPawn))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Ability), "get_CanCast")]
    public static class Harmony_SpiritForm_AbilityCanCast
    {
        public static void Postfix(Ability __instance, ref AcceptanceReport __result)
        {
            if (__result.Accepted && ServantQuery.Instance.IsSpirit(__instance.pawn))
            {
                __result = new AcceptanceReport("灵体化期间无法施放能力。");
            }
        }
    }
}
