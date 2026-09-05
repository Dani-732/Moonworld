using HarmonyLib;
using RimWorld;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryGetMeleeVerb))]
    public static class Harmony_SpiritForm_MeleeVerb
    {
        public static bool Prefix(Pawn_MeleeVerbs __instance, ref Verb __result)
        {
            if (!ServantQuery.Instance.IsSpirit(__instance.Pawn)) return true;
            // An empty verb list is intentional for spirits, not a broken pawn definition.
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
    public static class Harmony_SpiritForm_MeleeAttack
    {
        public static bool Prefix(Pawn_MeleeVerbs __instance, ref bool __result)
        {
            if (!ServantQuery.Instance.IsSpirit(__instance.Pawn)) return true;
            __result = false;
            return false;
        }
    }
}
