using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class CompProperties_MasterCommandSpells : CompProperties
    {
        public CompProperties_MasterCommandSpells()
        {
            compClass = typeof(CompMasterCommandSpells);
        }
    }

    public sealed class CompMasterCommandSpells : ThingComp
    {
        private const int DefaultCharges = 3;
        private int charges = DefaultCharges;

        public int Charges => charges;

        internal bool TryGrantForWar(out string rejection)
        {
            Pawn master = parent as Pawn;
            rejection = null;
            Trait added = null;
            try
            {
                if (!master.story.traits.HasTrait(MW_DefOf.MW_CommandSpell))
                {
                    added = new Trait(MW_DefOf.MW_CommandSpell);
                    master.story.traits.GainTrait(added);
                }
                if (!master.story.traits.HasTrait(MW_DefOf.MW_CommandSpell))
                    throw new System.InvalidOperationException("令咒特质未能授予。");
                charges = DefaultCharges;
                return true;
            }
            catch (System.Exception ex)
            {
                if (added != null && master.story.traits.allTraits.Contains(added))
                    master.story.traits.RemoveTrait(added);
                Log.Error("[MoonWorld] 授予令咒失败: " + ex);
                rejection = "未能授予令咒，本届事件尚未接取。";
                return false;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref charges, "commandSpellCharges", DefaultCharges);
        }

        public override string CompInspectStringExtra()
        {
            Pawn master = parent as Pawn;
            if (!MasterCircuitUtility.HasCircuit(master)
                || (master.story?.traits?.HasTrait(MW_DefOf.MW_CommandSpell) != true && charges > 0))
                return null;
            return "令咒：" + charges + " / " + DefaultCharges;
        }

        public static Command_Action CreateMiracleCommand(Pawn master, Pawn servant)
        {
            CompMasterCommandSpells spells = master?.TryGetComp<CompMasterCommandSpells>();
            if (spells == null || !MasterCircuitUtility.HasCircuit(master) || !IsValidTarget(master, servant))
                return null;

            bool damaged = HasSpiritDamage(servant);
            Command_Action command = new Command_Action
            {
                defaultLabel = "奇迹重铸：" + servant.LabelShort,
                defaultDesc = "清除该契约从者全部伤势与灵基受损状态，消耗一枚令咒。",
                icon = TexButton.Reveal,
                action = delegate
                {
                    string rejection;
                    if (spells.TryRecastMiracle(servant, out rejection))
                        Messages.Message(servant.LabelShortCap + " 的灵基受损已被奇迹重铸清除。", servant, MessageTypeDefOf.PositiveEvent, false);
                    else
                        Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
                },
                Order = -96f
            };
            if (spells.charges <= 0)
                command.Disable("令咒已耗尽。");
            else if (!damaged)
                command.Disable("目标没有灵基受损。");
            return command;
        }

        public bool TryRecastMiracle(Pawn servant, out string rejection)
        {
            Pawn master = parent as Pawn;
            rejection = null;
            if (charges <= 0) { rejection = "令咒已耗尽。"; return false; }
            if (!IsValidTarget(master, servant)) { rejection = "目标不是有效的己方契约从者。"; return false; }
            List<Hediff> hediffs = servant.health.hediffSet.hediffs;
            bool removed = false;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                Hediff hediff = hediffs[i];
                if (hediff.def == MW_DefOf.MW_SpiritDamage || hediff is Hediff_Injury)
                {
                    servant.health.RemoveHediff(hediff);
                    removed = true;
                }
            }
            if (!removed) { rejection = "目标没有灵基受损。"; return false; }
            TrySpendCharge();
            ServantPresenceEffects.Reconcile(servant);
            return true;
        }

        internal bool TrySpendCharge()
        {
            if (charges <= 0) return false;
            Pawn master = parent as Pawn;
            if (charges == 1 && master.story?.traits?.HasTrait(MW_DefOf.MW_CommandSpell) == true)
            {
                Trait commandSpell = master.story.traits.allTraits.Find(t => t.def == MW_DefOf.MW_CommandSpell);
                if (commandSpell != null)
                    master.story.traits.RemoveTrait(commandSpell);
            }
            charges--;
            return true;
        }

        private static bool IsValidTarget(Pawn master, Pawn servant)
        {
            CompServantState state = servant?.TryGetComp<CompServantState>();
            return master != null && servant != null && master.Faction == Faction.OfPlayer
                && MasterCircuitUtility.HasCircuit(master) && ServantQuery.Instance.GetMaster(servant) == master
                && state != null && state.PresenceState != ServantPresenceState.Annihilated;
        }

        private static bool HasSpiritDamage(Pawn servant)
        {
            return servant?.health?.hediffSet?.GetFirstHediffOfDef(MW_DefOf.MW_SpiritDamage) != null;
        }
    }
}
