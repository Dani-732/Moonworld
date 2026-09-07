using System;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class Hediff_CommandSpell : Hediff_Implant
    {
        public int Charges => Math.Max(0, Math.Min(CommandSpellService.MaximumCharges, (int)Severity));
        public override string LabelInBrackets => Charges + " / " + CommandSpellService.MaximumCharges;
        public override bool ShouldRemove => Charges == 0 || !CommandSpellService.IsValidCarrier(pawn, Part);
        public override bool TryMergeWith(Hediff other) => other.def == def;
    }

    public static class CommandSpellService
    {
        public const int MaximumCharges = 3;

        public static BodyPartRecord RightHand(Pawn pawn)
        {
            BodyPartRecord found = null;
            if (pawn?.RaceProps?.body == null) return null;
            foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
            {
                // Vanilla labels are translated. The right fingers' group identifies the hand structurally.
                if (part.def != BodyPartDefOf.Hand || !HasRightHandGroup(part)) continue;
                if (found != null) return null;
                found = part;
            }
            return found;
        }

        private static bool HasRightHandGroup(BodyPartRecord part)
        {
            if (part.groups != null && part.groups.Exists(group => group.defName == "RightHand")) return true;
            return part.parts != null && part.parts.Exists(HasRightHandGroup);
        }

        internal static bool IsValidCarrier(Pawn pawn, BodyPartRecord part)
        {
            if (part == null || part != RightHand(pawn) || pawn.health?.hediffSet == null) return false;
            // Native PartIsMissing/HasBodyPart only test the exact record, not its ancestors.
            for (BodyPartRecord current = part; current != null; current = current.parent)
                if (pawn.health.hediffSet.PartIsMissing(current)
                    || (current != part && pawn.health.hediffSet.HasDirectlyAddedPartFor(current))) return false;
            return true;
        }

        internal static Hediff_CommandSpell Mark(Pawn pawn)
        {
            var hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            Hediff_CommandSpell found = null;
            foreach (Hediff hediff in hediffs)
            {
                if (hediff.def != MW_DefOf.MW_CommandSpellMark) continue;
                var mark = hediff as Hediff_CommandSpell;
                if (mark == null || mark.ShouldRemove || found != null) return null;
                found = mark;
            }
            return found;
        }

        public static bool HasQualification(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && Mark(pawn) != null;
        }

        internal static bool TryGrant(Pawn pawn, int charges, out string rejection)
        {
            rejection = null;
            BodyPartRecord hand = RightHand(pawn);
            if (pawn == null || pawn.Dead || pawn.Destroyed || !IsValidCarrier(pawn, hand))
            { rejection = "需要存活且拥有可承载令咒的右手。"; return false; }
            if (charges < 1 || charges > MaximumCharges)
            { rejection = "令咒剩余划数无效。"; return false; }
            if (pawn.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_CommandSpellMark) != null)
            { rejection = "右手已有令咒印记，不能重复授予或叠加。"; return false; }

            Hediff_CommandSpell mark = null;
            try
            {
                mark = (Hediff_CommandSpell)HediffMaker.MakeHediff(MW_DefOf.MW_CommandSpellMark, pawn, hand);
                mark.Severity = charges;
                pawn.health.AddHediff(mark, hand);
                if (Mark(pawn) != mark || mark.Charges != charges)
                    throw new InvalidOperationException("右手令咒印记未能完整授予。");
                return true;
            }
            catch (Exception ex)
            {
                if (mark != null && pawn.health.hediffSet.hediffs.Contains(mark)) pawn.health.RemoveHediff(mark);
                Log.Error("[MoonWorld] 授予令咒失败: " + ex);
                rejection = "未能授予令咒。";
                return false;
            }
        }

        internal static bool TrySpend(Pawn pawn)
        {
            if (!HasQualification(pawn)) return false;
            Hediff_CommandSpell mark = Mark(pawn);
            if (mark.Charges == 1) pawn.health.RemoveHediff(mark);
            else mark.Severity = mark.Charges - 1;
            return true;
        }

        internal static bool MigrateLegacy(Pawn pawn, int charges)
        {
            Trait legacy = pawn?.story?.traits?.allTraits.Find(trait => trait.def == MW_DefOf.MW_CommandSpell);
            // Legacy generated enemy masters used the component's charges without receiving its trait.
            bool legacyEnemy = EnemyContractUtility.IsWarPawn(pawn) && MasterCircuitUtility.HasCircuit(pawn)
                && !ServantQuery.Instance.IsServant(pawn);
            if ((legacy != null || legacyEnemy) && charges > 0
                && pawn.health?.hediffSet?.GetFirstHediffOfDef(MW_DefOf.MW_CommandSpellMark) == null
                && !pawn.Dead && !pawn.Destroyed && IsValidCarrier(pawn, RightHand(pawn)))
            {
                string rejection;
                if (!TryGrant(pawn, Math.Min(charges, MaximumCharges), out rejection))
                {
                    Log.Warning("[MoonWorld] 令咒迁移未完成，保留旧划数供下次读档重试：" + pawn.LabelShort);
                    return false;
                }
            }
            RemoveLegacyTrait(pawn);
            ReconcileHealth(pawn);
            return true;
        }

        internal static void RemoveLegacyTrait(Pawn pawn)
        {
            var traits = pawn?.story?.traits;
            if (traits == null) return;
            for (int i = traits.allTraits.Count - 1; i >= 0; i--)
                if (traits.allTraits[i].def == MW_DefOf.MW_CommandSpell) traits.RemoveTrait(traits.allTraits[i]);
        }

        internal static void ReconcileHealth(Pawn pawn)
        {
            var hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i] is Hediff_CommandSpell mark && mark.ShouldRemove)
                    pawn.health.RemoveHediff(mark);
            }
        }
    }
}
