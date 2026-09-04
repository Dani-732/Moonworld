using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn_AgeTracker), "AgeTickInterval")]
    public static class Harmony_ServantAge
    {
        public static bool Prefix(Pawn ___pawn)
        {
            return !ServantPhysiologyPolicy.IsTimeless(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_AgeTracker), "AgeTickMothballed")]
    public static class Harmony_ServantAgeMothballed
    {
        public static bool Prefix(Pawn ___pawn)
        {
            return !ServantPhysiologyPolicy.IsTimeless(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff", new[]
    {
        typeof(HediffDef),
        typeof(BodyPartRecord),
        typeof(DamageInfo?),
        typeof(DamageWorker.DamageResult)
    })]
    public static class Harmony_ServantDisease
    {
        public static bool Prefix(Pawn ___pawn, HediffDef def)
        {
            return !ServantQuery.Instance.IsServant(___pawn) || !ServantPhysiologyPolicy.IsDisease(def);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff", new[]
    {
        typeof(Hediff),
        typeof(BodyPartRecord),
        typeof(DamageInfo?),
        typeof(DamageWorker.DamageResult)
    })]
    public static class Harmony_ServantDiseaseInstance
    {
        public static bool Prefix(Pawn ___pawn, Hediff hediff)
        {
            return !ServantQuery.Instance.IsServant(___pawn) || !ServantPhysiologyPolicy.IsDisease(hediff?.def);
        }
    }
}
