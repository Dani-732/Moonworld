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
        private const int DefaultCharges = CommandSpellService.MaximumCharges;
        private bool healthMigrated = true;
        private int legacyCharges;

        public int Charges => CommandSpellService.Mark(parent as Pawn)?.Charges ?? 0;

        internal bool TryGrantForWar(out string rejection)
        {
            return CommandSpellService.TryGrant(parent as Pawn, DefaultCharges, out rejection);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref healthMigrated, "commandSpellHealthMigrated", false);
            if (!healthMigrated && (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.Saving))
                Scribe_Values.Look(ref legacyCharges, "commandSpellCharges", DefaultCharges);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (healthMigrated) CommandSpellService.RemoveLegacyTrait(parent as Pawn);
                else if (!CommandSpellService.MigrateLegacy(parent as Pawn, legacyCharges)) return;
                legacyCharges = 0;
                healthMigrated = true;
            }
        }

        public override string CompInspectStringExtra()
        {
            Pawn master = parent as Pawn;
            if (!MasterCircuitUtility.HasCircuit(master) && Charges == 0) return null;
            return "令咒：" + Charges + " / " + DefaultCharges
                + (CommandSpellService.HasQualification(master) ? "（右手印记有效）" : "（无御主资格）");
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
            if (!CommandSpellService.HasQualification(master))
                command.Disable("没有有效的右手令咒印记。");
            else if (!damaged)
                command.Disable("目标没有灵基受损。");
            return command;
        }

        public bool TryRecastMiracle(Pawn servant, out string rejection)
        {
            Pawn master = parent as Pawn;
            rejection = null;
            if (!CommandSpellService.HasQualification(master)) { rejection = "没有有效的右手令咒印记。"; return false; }
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
            return CommandSpellService.TrySpend(parent as Pawn);
        }

        private static bool IsValidTarget(Pawn master, Pawn servant)
        {
            CompServantState state = servant?.TryGetComp<CompServantState>();
            return master != null && servant != null && master.Faction == Faction.OfPlayer
                && !master.Dead && !master.Destroyed && !servant.Dead && !servant.Destroyed
                && MasterCircuitUtility.HasCircuit(master) && ServantQuery.Instance.GetMaster(servant) == master
                && state != null && state.PresenceState != ServantPresenceState.Annihilated;
        }

        private static bool HasSpiritDamage(Pawn servant)
        {
            return servant?.health?.hediffSet?.GetFirstHediffOfDef(MW_DefOf.MW_SpiritDamage) != null;
        }
    }
}
