using RimWorld;
using Verse;
using Verse.AI;

namespace MoonWorld
{
    public static class EnemyTargetingPolicy
    {
        public static bool IsServantTarget(Pawn attacker, Pawn target)
        {
            return IsCombatTarget(attacker, target) && ServantQuery.Instance.IsMaterialized(target);
        }

        public static bool IsMasterTarget(Pawn attacker, Pawn target)
        {
            if (!IsCombatTarget(attacker, target) || ServantQuery.Instance.IsServant(target)) return false;
            GameComponent_MoonWorld war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            return war?.CurrentWarEntry?.IsCurrentMaster(target) == true;
        }

        public static bool IsPriorityTarget(Pawn attacker, Pawn target)
        {
            return IsServantTarget(attacker, target) || IsMasterTarget(attacker, target);
        }

        public static Pawn FindPreferredTarget(Pawn attacker)
        {
            Pawn servant = FindClosestReachableTarget(attacker, IsServantTarget);
            return servant ?? FindClosestReachableTarget(attacker, IsMasterTarget);
        }

        private static bool IsCombatTarget(Pawn attacker, Pawn target)
        {
            return attacker != null && target != null && target.Spawned && !target.Dead && !target.Destroyed && !target.Downed
                && target.Map == attacker.Map && target.HostileTo(attacker)
                && !target.IsPsychologicallyInvisible() && !target.ThreatDisabled(attacker)
                && AttackTargetFinder.IsAutoTargetable(target);
        }

        private static Pawn FindClosestReachableTarget(Pawn attacker, System.Func<Pawn, Pawn, bool> eligible)
        {
            Pawn result = null;
            float distance = float.MaxValue;
            foreach (Pawn candidate in attacker.Map.mapPawns.AllPawnsSpawned)
            {
                if (!eligible(attacker, candidate)) continue;
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
