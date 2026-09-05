using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MoonWorld
{
    public sealed class LordJob_EnemyWarParty : LordJob
    {
        private const string RetreatMemo = "MW_EnemyRetreat";
        public override bool AddFleeToil => false;
        public override bool CanAutoAddPawns => false;
        public bool Retreating => lord.CurLordToil is LordToil_ExitMap;

        public override StateGraph CreateGraph()
        {
            StateGraph graph = new StateGraph();
            LordToil assault = new LordToil_AssaultColony();
            LordToil exit = new LordToil_ExitMap(LocomotionUrgency.Jog, interruptCurrentJob: true);
            graph.AddToil(assault);
            graph.AddToil(exit);
            Transition retreat = new Transition(assault, exit);
            retreat.AddTrigger(new Trigger_Memo(RetreatMemo));
            retreat.AddPostAction(new TransitionAction_EndAllJobs());
            graph.AddTransition(retreat);
            return graph;
        }

        public override void LordJobTick()
        {
            if (Retreating) return;
            HolyGrailWarEntry entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry;
            if (entry == null || !entry.EnemyDeployed) return;
            if (entry.EnemyEliminated || entry.EnemyMaster.Downed || entry.EnemyMaster.IsPrisoner
                || entry.EnemyServant.IsPrisoner || ServantQuery.Instance.IsSpirit(entry.EnemyServant))
            {
                lord.ReceiveMemo(RetreatMemo);
                Messages.Message("敌方主从退出本次交战，开始撤退。", MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            if (Find.TickManager.TicksGame % 250 == 0) TryUseTestNoblePhantasm(entry.EnemyServant);
        }

        private static void TryUseTestNoblePhantasm(Pawn servant)
        {
            if (!servant.Spawned || servant.stances.FullBodyBusy) return;
            var defs = ServantIdentityUtility.GetIdentity(servant)?.noblePhantasms;
            if (defs == null) return;
            foreach (AbilityDef def in defs)
            {
                Ability_NoblePhantasm ability = servant.abilities?.GetAbility(def) as Ability_NoblePhantasm;
                if (ability == null || !ability.CanCast) continue;
                Pawn target = null;
                float distance = float.MaxValue;
                foreach (Pawn candidate in servant.Map.mapPawns.AllPawnsSpawned)
                {
                    if (candidate.Dead || candidate.Downed || !candidate.HostileTo(servant)
                        || ServantQuery.Instance.IsSpirit(candidate)) continue;
                    float current = (candidate.Position - servant.Position).LengthHorizontalSquared;
                    if (current >= distance || !ability.verb.ValidateTarget(new LocalTargetInfo(candidate.Position), false)) continue;
                    bool friendlyInBlast = false;
                    foreach (Pawn friendly in servant.Map.mapPawns.AllPawnsSpawned)
                        if (!friendly.HostileTo(servant) && friendly.Position.InHorDistOf(candidate.Position, def.EffectRadius + 1f))
                        { friendlyInBlast = true; break; }
                    if (friendlyInBlast) continue;
                    target = candidate;
                    distance = current;
                }
                if (target != null)
                {
                    ability.QueueCastingJob(new LocalTargetInfo(target.Position), LocalTargetInfo.Invalid);
                    return;
                }
            }
        }
    }

    public static class EnemyRetreatUtility
    {
        private sealed class ExitJobGiver : JobGiver_ExitMapBest
        {
            public Job For(Pawn pawn) => TryGiveJob(pawn);
        }

        private static readonly ExitJobGiver ExitGiver = new ExitJobGiver();

        public static bool ShouldExit(Pawn servant)
        {
            if (!EnemyContractUtility.HasEnemyContract(servant)
                || !(servant.GetLord()?.LordJob is LordJob_EnemyWarParty party) || !party.Retreating) return false;
            Pawn master = ServantQuery.Instance.GetMaster(servant);
            return master == null || !master.Spawned || master.Map != servant.Map;
        }

        public static Job ExitJob(Pawn servant) => ShouldExit(servant) ? ExitGiver.For(servant) : null;
    }
}
