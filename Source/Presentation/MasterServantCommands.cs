using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class CompProperties_MasterServantCommands : CompProperties
    {
        public CompProperties_MasterServantCommands()
        {
            compClass = typeof(CompMasterServantCommands);
        }
    }

    public sealed class CompMasterServantCommands : ThingComp
    {
        public override string CompInspectStringExtra()
        {
            HolyGrailWarEntry entry = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry;
            if (entry == null) return null;
            if (parent == entry.DesignatedMaster && entry.PlayerIdentity != null)
                return "圣杯战争阵营：" + HolyGrailWarClassDef.For(entry.PlayerIdentity)?.label
                    + "\n剩余敌对阵营：" + entry.Enemies.FindAll(e => !e.EnemyEliminated).Count + "/" + entry.Enemies.Count;
            var enemy = entry.FindEnemy(parent as Pawn);
            if (enemy != null) return "圣杯战争阵营：" + enemy.Seat?.label + "（敌对）";
            return null;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Pawn master = parent as Pawn;
            if (master?.Faction != Faction.OfPlayer || !MasterCircuitUtility.HasCircuit(master))
            {
                yield break;
            }

            Map map = master.Map;
            Command_Target summon = new Command_Target
            {
                defaultLabel = "召唤从者",
                defaultDesc = "选择落点，随机召唤一名英灵并订立契约。成功后用尽本届圣杯战争授予的常规召唤资格，不消耗令咒划数。",
                icon = TexButton.Add,
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    validator = target => target.Map == map && Find.CurrentMap == map
                        && ServantSummoningService.Instance.Validate(master, map, target.Cell) == null
                },
                action = target =>
                {
                    string rejection;
                    Pawn servant;
                    if (Find.CurrentMap != map) return;
                    if (ServantSummoningService.Instance.TrySummon(master, map, target.Cell, out servant, out rejection))
                        Messages.Message(servant.LabelShortCap + " 已完成召唤。", servant, MessageTypeDefOf.PositiveEvent, false);
                    else
                        Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
                },
                Order = -100f
            };
            string summonRejection = ServantSummoningService.Instance.CommandRejection(master, map);
            if (summonRejection != null) summon.Disable(summonRejection);
            yield return summon;

            List<Pawn> servants = new List<Pawn>();
            ServantQuery.Instance.GetBoundServants(master, servants);
            servants.Sort((left, right) => left.thingIDNumber.CompareTo(right.thingIDNumber));
            foreach (Pawn servant in servants)
            {
                Command_Action command = CreatePresenceCommand(master, servant);
                if (command != null)
                {
                    yield return command;
                }

                Command_Action miracle = CompMasterCommandSpells.CreateMiracleCommand(master, servant);
                if (miracle != null)
                    yield return miracle;
                foreach (Gizmo gizmo in NoblePhantasmCommands.ForServant(master, servant))
                    yield return gizmo;
            }
        }

        public static Command_Action CreatePresenceCommand(Pawn master, Pawn servant)
        {
            CompServantState state = servant?.TryGetComp<CompServantState>();
            if (state == null || state.PresenceState == ServantPresenceState.Annihilated)
            {
                return null;
            }

            bool materialized = state.PresenceState == ServantPresenceState.Materialized;
            Command_Action command = new Command_Action
            {
                defaultLabel = (materialized ? "灵体化：" : "实体化：") + servant.LabelShort,
                defaultDesc = materialized
                    ? "命令契约从者进入半透明灵体状态。同图时跟随御主，分离时可独立旅行，不可攻击、工作或进食。"
                    : "命令契约从者解除灵体状态，恢复工作与战斗控制。",
                icon = materialized ? TexButton.Suspend : TexButton.Reveal,
                action = delegate
                {
                    string rejection;
                    bool succeeded = materialized
                        ? ServantLifecycleService.Instance.TryEnterVoluntarySpirit(master, servant, out rejection)
                        : ServantLifecycleService.Instance.TryRematerialize(master, servant, out rejection);
                    if (succeeded)
                    {
                        string message = servant.LabelShortCap + (materialized ? " 已进入灵体状态。" : " 已重新实体化。");
                        Messages.Message(message, servant, MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        Messages.Message(rejection ?? "当前无法转换从者状态。", MessageTypeDefOf.RejectInput, false);
                    }
                },
                Order = -97f
            };

            if (!ServantLifecycleService.CanChangePresence(master, servant))
            {
                command.Disable("需要有效的存活主从契约，且从者位于地图上。");
            }
            return command;
        }
    }
}
