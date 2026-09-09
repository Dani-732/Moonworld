using UnityEngine;

namespace MoonWorld
{
    public enum WarPhase
    {
        Probing,
        Fierce,
        Endgame
    }

    internal static class WarRhythmPolicy
    {
        internal const int DayTicks = 60000;
        internal static WarPhase Phase(GameComponent_MoonWorld war)
        {
            if (war == null || war.warStartTick < 0) return WarPhase.Probing;
            long elapsed = System.Math.Max(0L, (long)Verse.Find.TickManager.TicksGame - war.warStartTick);
            if (elapsed < DayTicks * 3L) return WarPhase.Probing;
            if (elapsed < DayTicks * 7L) return WarPhase.Fierce;
            return WarPhase.Endgame;
        }

        internal static string PhaseLabel(GameComponent_MoonWorld war)
        {
            switch (Phase(war))
            {
                case WarPhase.Fierce: return "激烈阶段";
                case WarPhase.Endgame: return "终局阶段";
                default: return "试探阶段";
            }
        }

        internal static int BattleCooldown(GameComponent_MoonWorld war)
        {
            switch (Phase(war))
            {
                case WarPhase.Fierce: return 30000;
                case WarPhase.Endgame: return 0;
                default: return 90000;
            }
        }

        internal static float NaturalActionMultiplier(GameComponent_MoonWorld war)
        {
            switch (Phase(war))
            {
                case WarPhase.Fierce: return 2f;
                case WarPhase.Endgame: return 3f;
                default: return 0.75f;
            }
        }

        internal static float FieldBattleChance(GameComponent_MoonWorld war)
        {
            float configured = Mathf.Clamp01(MW_DefOf.MW_HolyGrailWarSettings.enemyFieldBattleChance);
            switch (Phase(war))
            {
                case WarPhase.Fierce: return Mathf.Clamp01(configured * 0.7f);
                case WarPhase.Endgame: return Mathf.Clamp01(configured * 0.45f);
                default: return Mathf.Clamp01(configured * 1.15f);
            }
        }

        internal static float ChallengeChance(GameComponent_MoonWorld war)
        {
            float configured = Mathf.Clamp01(MW_DefOf.MW_HolyGrailWarSettings.enemyChallengeChance);
            switch (Phase(war))
            {
                case WarPhase.Fierce: return Mathf.Clamp01(configured * 1.1f);
                case WarPhase.Endgame: return Mathf.Clamp01(configured * 0.35f);
                default: return Mathf.Clamp01(configured * 1.15f);
            }
        }

        internal static bool FinalBattleDue(GameComponent_MoonWorld war)
        { return war != null && war.CurrentWarOutcome == WarOutcome.Ongoing && war.warStartTick >= 0 && Phase(war) == WarPhase.Endgame
                && (long)Verse.Find.TickManager.TicksGame - war.warStartTick >= DayTicks * 10L; }
    }
}
