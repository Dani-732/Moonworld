using System;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    public static class EnemyRestUtility
    {
        public static int TicksRemaining(Pawn servant)
        {
            int duration = Math.Max(0, MW_DefOf.MW_HolyGrailWarSettings.enemyRestDurationTicks);
            if (servant == null || servant.becameWorldPawnTickAbs < 0) return duration;
            long elapsed = Math.Max(0L, (long)GenTicks.TicksAbs - servant.becameWorldPawnTickAbs);
            return (int)Math.Max(0L, duration - elapsed);
        }

        public static string ReadinessRejection(Pawn servant)
        {
            if (!EnemyContractUtility.IsResting(servant))
                return "敌方主从已退场、被俘、仍在地图或运输途中，不能再次出战。";
            if (servant.becameWorldPawnTickAbs < 0 || TicksRemaining(servant) > 0)
                return "敌方从者仍在场外休整，尚未达到最短休整时间。";
            if (ServantQuery.Instance.GetMaster(servant).Downed || servant.InMentalState
                || servant.health.ShouldBeDead() || servant.health.ShouldBeDowned())
                return "敌方主从当前健康或精神状态无法出战。";
            foreach (Hediff hediff in servant.health.hediffSet.hediffs)
                if (hediff is Hediff_Injury && hediff.Severity > 0f)
                    return "敌方从者的伤势尚未恢复。";
            if (ServantHealingPolicy.FindWorstCurableCondition(servant) != null)
                return "敌方从者仍有需要魔力治疗的状态。";
            Need_Prana prana = servant.needs?.TryGetNeed<Need_Prana>();
            ServantResourceProfileDef profile = ServantIdentityUtility.GetProfile(servant);
            float fraction = Mathf.Clamp01(MW_DefOf.MW_HolyGrailWarSettings.enemyRaidPranaFraction);
            if (prana == null || profile == null || prana.MaxLevel <= 0f
                || prana.CurLevel < Mathf.Max(prana.MaxLevel * fraction, profile.materializedSustainThreshold))
                return "敌方从者尚未恢复出战所需的魔力。";
            return null;
        }
    }
}
