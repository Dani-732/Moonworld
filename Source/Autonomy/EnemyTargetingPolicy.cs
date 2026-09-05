using RimWorld;
using Verse;
using Verse.AI;

namespace MoonWorld
{
    public static class EnemyTargetingPolicy
    {
        public static bool IsServantTarget(Pawn attacker, Pawn target)
        {
            return target != null && target.Spawned && !target.Dead && !target.Destroyed && !target.Downed
                && target.Map == attacker.Map && target.HostileTo(attacker)
                && ServantQuery.Instance.IsMaterialized(target)
                && !target.IsPsychologicallyInvisible() && !target.ThreatDisabled(attacker)
                && AttackTargetFinder.IsAutoTargetable(target);
        }

        public static Pawn FindPreferredTarget(Pawn attacker)
        {
            Pawn result = null;
            float distance = float.MaxValue;
            foreach (Pawn candidate in attacker.Map.mapPawns.AllPawnsSpawned)
            {
                if (!IsServantTarget(attacker, candidate)) continue;
                float current = (candidate.Position - attacker.Position).LengthHorizontalSquared;
                if (current >= distance) continue;
                if (!attacker.CanReach(candidate, PathEndMode.Touch, Danger.Deadly)
                    && !(attacker.TryGetAttackVerb(candidate)?.CanHitTarget(candidate) ?? false)) continue;
                result = candidate;
                distance = current;
            }
            return result;
        }
    }
}
