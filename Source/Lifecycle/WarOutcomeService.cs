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

            if (!entry.HasEnemyParticipants) return;
            bool hostileAlive = IsHostileServant(entry.PlayerServant);
            bool playerAlive = IsPlayerServant(entry.PlayerServant);
            bool playerQualified = IsQualifiedPlayer(entry.DesignatedMaster);
            foreach (var enemy in entry.Participants)
            {
                hostileAlive |= IsHostileServant(enemy.EnemyServant);
                playerAlive |= IsPlayerServant(enemy.EnemyServant);
                playerQualified |= IsQualifiedPlayer(enemy.EnemyMaster);
                playerQualified |= IsQualifiedPlayer(enemy.OriginalMaster);
            }
            foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive) playerQualified |= IsQualifiedPlayer(pawn);
            if (!hostileAlive)
            {
                if (state.TrySetWarOutcome(WarOutcome.PlayerVictory))
                    Messages.Message("圣杯战争胜利：本届敌对从者已全部退场。", entry.DesignatedMaster, MessageTypeDefOf.PositiveEvent, false);
            }
            else if (entry.PlayerServant != null && !playerAlive && !playerQualified)
            {
                if (state.TrySetWarOutcome(WarOutcome.PlayerDefeat))
                    Messages.Message("圣杯战争失败：己方已无存续从者或合格御主。", entry.DesignatedMaster, MessageTypeDefOf.NegativeEvent, false);
            }
        }

        internal static bool IsHostileServant(Pawn pawn) => UnboundServantService.Exists(pawn)
            && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer);
        private static bool IsPlayerServant(Pawn pawn) => UnboundServantService.Exists(pawn) && pawn.Faction == Faction.OfPlayer;
        private static bool IsQualifiedPlayer(Pawn pawn) => CommandSpellService.HasQualification(pawn) && pawn.Faction == Faction.OfPlayer;

        public static bool IsWarOngoing()
        {
            return Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarOutcome == WarOutcome.Ongoing;
        }

    }
}
