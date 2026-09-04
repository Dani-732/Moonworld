using HarmonyLib;
using Verse;

namespace MoonWorld
{
    public enum ServantDamageDecision
    {
        Allow,
        Absorb
    }

    public interface IServantDamagePolicy
    {
        ServantDamageDecision Evaluate(Pawn target, DamageInfo damage);
    }

    public sealed class ServantDamagePolicy : IServantDamagePolicy
    {
        public static readonly ServantDamagePolicy Instance = new ServantDamagePolicy();

        private ServantDamagePolicy()
        {
        }

        public ServantDamageDecision Evaluate(Pawn target, DamageInfo damage)
        {
            if (!ServantQuery.Instance.IsServant(target))
            {
                return ServantDamageDecision.Allow;
            }
            if (!ServantQuery.Instance.IsMaterialized(target))
            {
                return ServantDamageDecision.Absorb;
            }

            Thing instigator = damage.Instigator;
            if (instigator == null)
            {
                // Environmental damage remains effective by the agreed MVP rule.
                return ServantDamageDecision.Allow;
            }
            Pawn attacker = GetResponsiblePawn(instigator);
            return attacker != null && ServantQuery.Instance.IsMaterialized(attacker)
                ? ServantDamageDecision.Allow
                : ServantDamageDecision.Absorb;
        }

        private static Pawn GetResponsiblePawn(Thing instigator)
        {
            Pawn pawn = instigator as Pawn;
            if (pawn != null)
            {
                return pawn;
            }
            Projectile projectile = instigator as Projectile;
            return projectile?.Launcher as Pawn;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Harmony_Pawn_PreApplyDamage
    {
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (ServantDamagePolicy.Instance.Evaluate(__instance, dinfo) == ServantDamageDecision.Allow)
            {
                return true;
            }

            absorbed = true;
            return false;
        }
    }
}
