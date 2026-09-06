using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class WarOutcomeService
    {
        public static void Tick(GameComponent_MoonWorld state)
        {
            if (state == null || state.CurrentWarOutcome != WarOutcome.Ongoing) return;
            HolyGrailWarEntry entry = state.CurrentWarEntry;
            if (entry == null || state.warStartTick < 0 || !entry.RegularSummonUsed) return;

            Pawn playerMaster = entry.DesignatedMaster;
            if (playerMaster == null || playerMaster.Dead || playerMaster.Destroyed)
            {
                if (state.TrySetWarOutcome(WarOutcome.PlayerDefeat))
                    Messages.Message("圣杯战争失败：玩家御主已失去参战资格。", playerMaster, MessageTypeDefOf.NegativeEvent, false);
                return;
            }

            if (!entry.HasEnemyParticipants) return;
            foreach (var enemy in entry.Enemies)
                if (!enemy.EnemyEliminated) return;
            if (state.TrySetWarOutcome(WarOutcome.PlayerVictory))
                Messages.Message("圣杯战争胜利：全部敌方阵营已失去御主资格。", entry.EnemyMaster, MessageTypeDefOf.PositiveEvent, false);
        }

        public static bool IsWarOngoing()
        {
            return Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarOutcome == WarOutcome.Ongoing;
        }

    }
}
