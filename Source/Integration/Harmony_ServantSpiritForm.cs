using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn_JobTracker), "DetermineNextJob")]
    public static class Harmony_SpiritForm_DetermineNextJob
    {
        public static bool Prefix(Pawn ___pawn, ref ThinkTreeDef thinkTree, ref ThinkResult __result)
        {
            if (!ServantQuery.Instance.IsSpirit(___pawn))
            {
                return true;
            }

            thinkTree = null;
            __result = new ThinkResult(SpiritFollowJobPolicy.CreateJob(___pawn), null, null, false);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Harmony_SpiritForm_StartJob
    {
        public static bool Prefix(Pawn ___pawn, Job newJob)
        {
            return !ServantQuery.Instance.IsSpirit(___pawn)
                || SpiritFollowJobPolicy.IsAllowed(___pawn, newJob);
        }
    }

    [HarmonyPatch(typeof(InvisibilityUtility), nameof(InvisibilityUtility.GetAlpha))]
    public static class Harmony_SpiritForm_RenderAlpha
    {
        private const float SpiritOpacity = 0.3f;

        public static bool Prefix(Pawn __0, ref float __result)
        {
            if (__0 != null && __0.Dead && ServantQuery.Instance.IsServant(__0))
            {
                __result = 1f;
                return false;
            }
            return true;
        }

        public static void Postfix(Pawn __0, ref float __result)
        {
            if (__0 != null && !__0.Dead && ServantQuery.Instance.IsSpirit(__0))
            {
                __result = SpiritOpacity;
            }
        }
    }

    [HarmonyPatch(typeof(InvisibilityUtility), nameof(InvisibilityUtility.IsPsychologicallyInvisible))]
    public static class Harmony_SpiritForm_DeadPawnVisibility
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (pawn != null && pawn.Dead && ServantQuery.Instance.IsServant(pawn))
            {
                __result = false;
                return false;
            }
            return true;
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
