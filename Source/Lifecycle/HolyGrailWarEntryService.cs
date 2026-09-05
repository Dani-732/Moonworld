using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class HolyGrailWarEntryService
    {
        private static bool accepting;

        public static bool IsEligibleMaster(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Faction == Faction.OfPlayer
                && pawn.IsColonistPlayerControlled && !pawn.IsQuestLodger() && !pawn.IsPrisoner && !pawn.IsSlave
                && !ServantQuery.Instance.IsServant(pawn) && MasterCircuitUtility.HasCircuit(pawn)
                && pawn.TryGetComp<CompMasterCommandSpells>() != null;
        }

        public static bool CanDesignate(Pawn pawn)
        {
            return IsEligibleMaster(pawn) && pawn.Spawned && pawn.Map.IsPlayerHome;
        }

        public static bool TryAccept(Pawn master, out string rejection)
        {
            rejection = null;
            GameComponent_MoonWorld state = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (accepting || state == null || !state.CanAcceptInvitation)
            {
                rejection = "本届圣杯战争已接取，不能另行指定御主或重复领取资格。";
                return false;
            }
            if (!CanDesignate(master))
            {
                rejection = "请指定基地中拥有魔力回路的自由殖民者。";
                return false;
            }

            accepting = true;
            try
            {
                MasterCircuitUtility.EnsureMasterPranaNeed(master);
                if (!master.TryGetComp<CompMasterCommandSpells>().TryGrantForWar(out rejection))
                    return false;
                state.AcceptInvitation(master);
                return true;
            }
            finally
            {
                accepting = false;
            }
        }

        public static string RegularSummonRejection(Pawn master)
        {
            if (!IsEligibleMaster(master)) return "需要存活且可控制的己方魔力回路持有者。";
            GameComponent_MoonWorld state = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            HolyGrailWarEntry entry = state?.CurrentWarEntry;
            if (entry == null) return state?.warStartTick >= 0
                ? "本届圣杯战争的常规召唤已使用。" : "请先在降灵之兆事件中指定御主并接受。";
            if (entry.RegularSummonUsed) return "本届圣杯战争的常规召唤已使用。";
            if (entry.DesignatedMaster != master) return "只有本届事件指定的御主拥有常规召唤资格。";
            if (master.story?.traits?.HasTrait(MW_DefOf.MW_CommandSpell) != true
                || master.TryGetComp<CompMasterCommandSpells>().Charges <= 0)
                return "御主已没有令咒。";
            return null;
        }

        internal static void PrepareStartingCircuit(Pawn pawn)
        {
            if (!MasterCircuitUtility.HasCircuit(pawn))
                pawn.story.traits.GainTrait(new Trait(MW_DefOf.MW_MagusCircuit_Basic));
            if (!pawn.story.traits.HasTrait(MW_DefOf.MW_MageRank_Apprentice))
                pawn.story.traits.GainTrait(new Trait(MW_DefOf.MW_MageRank_Apprentice));
            MasterCircuitUtility.EnsureMasterPranaNeed(pawn);
        }
    }
}
