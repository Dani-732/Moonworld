using UnityEngine;
using Verse;

namespace MoonWorld
{
    // Current consumers share these weights. A later event-phase policy can change them here.
    internal static class WarEncounterPolicy
    {
        internal static float FieldBattleChance => Mathf.Clamp01(MW_DefOf.MW_HolyGrailWarSettings.enemyFieldBattleChance);
        internal static float ChallengeChance => Mathf.Clamp01(MW_DefOf.MW_HolyGrailWarSettings.enemyChallengeChance);
        internal static bool CanAttackWorkshop(Pawn servant)
        {
            Need_Prana prana = servant?.needs?.TryGetNeed<Need_Prana>();
            return prana != null && prana.CurLevel >= prana.MaxLevel
                * Mathf.Clamp01(MW_DefOf.MW_HolyGrailWarSettings.enemyWorkshopAttackPranaFraction);
        }
    }
}
