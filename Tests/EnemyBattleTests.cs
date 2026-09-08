using System;
using MoonWorld;
using RimWorld;
using Verse;
using Verse.AI.Group;

internal static partial class SummoningTests
{
    private static EnemyBattleSession Battle()
    {
        SevenClasses(); PrepareEnemy();
        Check(EnemyBattleService.TryStart(State), "battle did not start");
        return State.enemyBattle;
    }
    private static void BattleRound(EnemyBattleSession battle)
    {
        Find.TickManager.TicksGame += Math.Max(0, battle.nextRoundTickAbs - GenTicks.TicksAbs);
        EnemyBattleService.Advance(State);
    }
    private static Map BattleMap(EnemyBattleSession battle)
    {
        var result = new Map { IsPlayerHome = false, Parent = battle.site };
        battle.site.Map = result;
        battle.site.PostMapGenerate();
        return result;
    }

    private static void EnemyBattleScenarios()
    {
        Test("battle waits one day after actual war start", () => {
            SevenClasses(); PrepareEnemy(); EnemyBattleService.Tick(State);
            Check(State.enemyBattle == null, "started early");
            Find.TickManager.TicksGame += 60000; EnemyBattleService.Tick(State);
            Check(State.enemyBattle != null, "did not start after wait");
        });
        Test("offmap battle creates no pawns or map and reserves both servants", () => {
            var battle = Battle(); int count = PawnGenerator.Created.Count;
            Check(!battle.site.HasMap && !battle.attacker.Spawned && !battle.defender.Spawned, "generated map");
            Check(!EnemyBattleService.TryStart(State) && PawnGenerator.Created.Count == count, "duplicate battle");
            Check(EnemyRestUtility.ReadinessRejection(battle.attacker) != null
                && EnemyRestUtility.ReadinessRejection(battle.defender) != null, "raid can steal participant");
        });
        Test("round debits real needs and sends damage through pawn health once", () => {
            var battle = Battle(); BattleRound(battle);
            Check(battle.rounds == 1 && battle.attacker.needs.Prana.CurLevel == 82
                && battle.defender.needs.Prana.CurLevel == 82, "incorrect cost");
            Check(battle.attacker.health.hediffSet.hediffs.Count == 1 && battle.defender.health.hediffSet.hediffs.Count == 1,
                "no actual injuries");
            EnemyBattleService.Advance(State);
            Check(battle.rounds == 1 && battle.attacker.DamageCalls == 1, "round replayed");
        });
        Test("round limit ends battle and preserves original servants with rest", () => {
            var battle = Battle();
            for (int i = 0; i < EnemyBattleService.MaximumRounds; i++)
            {
                battle.attacker.needs.Prana.CurLevel = battle.defender.needs.Prana.CurLevel = 100;
                BattleRound(battle);
            }
            Check(State.enemyBattle == null && battle.rounds == 6 && !battle.attacker.Dead && !battle.defender.Dead,
                "battle did not disengage");
            Check(State.CurrentWarEntry.FindEnemy(battle.attacker).EnemyRestStartTickAbs == GenTicks.TicksAbs,
                "rest did not start");
            EnemyBattleService.Tick(State); Check(State.enemyBattle == null, "cooldown bypassed");
        });
        Test("native defeat ends round without giving defeated servant another attack", () => {
            var battle = Battle(); battle.defender.OnDamage = p => p.State.PresenceState = ServantPresenceState.DefeatedSpirit;
            BattleRound(battle);
            Check(State.enemyBattle == null && battle.attacker.DamageCalls == 0 && !battle.defender.Dead
                && !battle.site.RetreatOrdered && !battle.site.BothEscaped, "defeat became death or workshop retreat");
        });
        Test("loss of qualification cancels battle without dealing damage or refreshing deadline", () => {
            var battle = Battle(); battle.attackerMaster.Spells.Charges = 0;
            battle.attacker.State.Master = null; battle.attacker.State.UnboundUntilTickAbs = GenTicks.TicksAbs + 30000;
            int deadline = battle.attacker.State.UnboundUntilTickAbs;
            BattleRound(battle);
            Check(State.enemyBattle == null && battle.defender.DamageCalls == 0
                && battle.attacker.State.UnboundUntilTickAbs == deadline, "invalid contract settled");
        });
        Test("player faction transfer cancels session and keeps player pawn on map", () => {
            var battle = Battle(); Map battleMap = BattleMap(battle);
            battle.attacker.Faction = Faction.OfPlayer;
            EnemyBattleService.Advance(State);
            Check(State.enemyBattle == null && battle.attacker.Spawned && battle.attacker.Map == battleMap,
                "player servant abducted");
        });
        Test("map intervention spawns original servants near center without masters", () => {
            var battle = Battle(); BattleRound(battle); int count = PawnGenerator.Created.Count;
            Map battleMap = BattleMap(battle);
            Check(battle.onMap && battle.attacker.Map == battleMap && battle.defender.Map == battleMap
                && !battle.attackerMaster.Spawned && !battle.defenderMaster.Spawned && PawnGenerator.Created.Count == count,
                "map deployment copied participants or spawned masters");
            Check(battle.attacker.needs.Prana.CurLevel == 82 && battle.attacker.DamageCalls == 1, "map reset resources");
            Find.TickManager.TicksGame += 10000; EnemyBattleService.Advance(State);
            Check(battle.rounds == 1 && battle.attacker.needs.Prana.CurLevel == 82, "offmap round ran on map");
        });
        Test("unloading map checkpoints battle and resumes next round once", () => {
            var battle = Battle(); BattleRound(battle); BattleMap(battle);
            battle.attacker.needs.Prana.CurLevel = 77;
            Check(battle.site.ShouldRemoveMapNow(out bool removeSite) && !removeSite, "battle site discarded");
            battle.site.Notify_MyMapAboutToBeRemoved(); battle.site.Map = null;
            Check(State.enemyBattle == battle && !battle.onMap && Find.WorldPawns.Contains(battle.attacker)
                && Find.WorldPawns.Contains(battle.defender) && battle.attacker.needs.Prana.CurLevel == 77,
                "unload lost battle");
            EnemyBattleService.Advance(State); Check(battle.rounds == 1, "immediate extra round");
            BattleRound(battle); Check(battle.rounds == 2 && battle.attacker.needs.Prana.CurLevel == 59, "continuation reset state");
        });
        Test("native map retreat ends session instead of continuing offmap", () => {
            var battle = Battle(); BattleMap(battle);
            ((LordJob_EnemyWarParty)battle.attacker.GetLord().LordJob).BeginRetreat();
            battle.site.Notify_MyMapAboutToBeRemoved(); battle.site.Map = null;
            Check(State.enemyBattle == null && battle.rounds == 0, "native retreat ignored");
        });
        Test("native map death ends session without restoring dead pawn", () => {
            var battle = Battle(); BattleMap(battle); battle.defender.Dead = true;
            EnemyBattleService.Advance(State);
            Check(State.enemyBattle == null && battle.defender.Dead && !Find.WorldPawns.Contains(battle.defender)
                && Find.WorldPawns.Contains(battle.attacker), "native death undone");
        });
        Test("deployment failure returns both originals and allows retry", () => {
            var battle = Battle(); LordMaker.Fail = true; BattleMap(battle);
            Check(!battle.onMap && !battle.attacker.Spawned && !battle.defender.Spawned
                && battle.attacker.GetLord() == null && State.enemyBattle == battle, "partial deployment leaked");
            LordMaker.Fail = false; Check(EnemyBattleService.TryMaterialize(battle.site) && battle.onMap, "retry failed");
        });
        Test("attacking absent servant workshop spawns only its master", () => {
            var battle = Battle(); var ownSite = WorkshopRebuildService.FindWorkshop(State.CurrentWarEntry.FindEnemy(battle.attacker));
            ownSite.Map = new Map { Parent = ownSite }; ownSite.PostMapGenerate();
            Check(battle.attackerMaster.Spawned && !battle.attacker.Spawned && State.enemyBattle == battle,
                "original workshop pulled servant from battle");
        });
        Test("master-only site and two friendly servants never start mutual combat", () => {
            SevenClasses(); PrepareEnemy();
            foreach (var enemy in State.CurrentWarEntry.Enemies) enemy.EnemyServant.Dead = true;
            Check(!EnemyBattleService.TryStart(State), "AI attacks master-only sites");
            var one = State.CurrentWarEntry.Enemies[0]; var two = State.CurrentWarEntry.Enemies[1];
            one.EnemyServant.Dead = two.EnemyServant.Dead = false;
            two.EnemyServant.Faction = two.EnemyMaster.Faction = one.EnemyServant.Faction;
            Check(!EnemyBattleService.TryStart(State), "friendly servants attacked");
        });
        Test("session round and exact pawn site references survive host save roundtrip", () => {
            var battle = Battle(); BattleRound(battle);
            Scribe.Data.Clear(); Scribe.Loading = false; battle.ExposeData();
            Scribe.Loading = true; var loaded = new EnemyBattleSession(); loaded.ExposeData(); Scribe.Loading = false;
            State.enemyBattle = loaded;
            Check(loaded.attacker == battle.attacker && loaded.defender == battle.defender && loaded.site == battle.site
                && loaded.rounds == 1 && loaded.nextRoundTickAbs == battle.nextRoundTickAbs, "session changed on load");
            EnemyBattleService.Advance(State); Check(loaded.rounds == 1, "load replayed completed round");
        });
        Test("current unbound attacker can fight without supply or deadline renewal", () => {
            SevenClasses(); PrepareEnemy();
            foreach (var enemy in State.CurrentWarEntry.Enemies) Orphan(enemy.EnemyServant);
            Check(EnemyBattleService.TryStart(State), "valid unbound servants rejected");
            var battle = State.enemyBattle; int deadline = battle.attacker.State.UnboundUntilTickAbs;
            BattleRound(battle);
            Check(!EnemyContractUtility.CanReceiveSupply(battle.attacker) && battle.attacker.State.UnboundUntilTickAbs == deadline,
                "battle renewed unbound life or supply");
        });
    }
}

namespace Verse
{
    public struct DamageInfo
    {
        public float Amount;
        public DamageInfo(object def, float amount, float armor, float angle, Pawn instigator) { Amount = amount; }
    }
    public partial class Pawn
    {
        public int DamageCalls;
        public Action<Pawn> OnDamage;
        public bool HostileTo(Pawn other) => Faction != null && Faction.HostileTo(other.Faction);
        public void TakeDamage(DamageInfo damage)
        {
            DamageCalls++;
            health.hediffSet.hediffs.Add(new Hediff_Injury { Severity = damage.Amount });
            OnDamage?.Invoke(this);
        }
    }
}
namespace RimWorld
{
    public static class DamageDefOf { public static object Blunt = new object(); }
}
