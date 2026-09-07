using System;
using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(BackCompatibility), nameof(BackCompatibility.BackCompatibleDefName))]
    public static class Harmony_CommandSealDefCompatibility
    {
        public static void Prefix(Type defType, ref string defName)
        {
            if (defType != typeof(ThingDef)) return;
            // Preserve extracted items saved by the first surgery test deployment.
            switch (defName)
            {
                case "MW_CommandSeal1": defName = "MW_CommandSealOne"; break;
                case "MW_CommandSeal2": defName = "MW_CommandSealTwo"; break;
                case "MW_CommandSeal3": defName = "MW_CommandSealThree"; break;
            }
        }
    }
}
