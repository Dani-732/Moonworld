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
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Pawn master = parent as Pawn;
            if (master?.Faction != Faction.OfPlayer || !MasterCircuitUtility.HasCircuit(master))
            {
                yield break;
            }

            Command_Action summon = new Command_Action
            {
                defaultLabel = "召唤从者",
                defaultDesc = "在鼠标处随机召唤阿尔托莉雅或卫宫。",
                icon = TexButton.Add,
                action = delegate
                {
                    string rejection;
                    Pawn servant;
                    if (ServantSummoningService.Instance.TrySummon(master, master.Map, UI.MouseCell(), out servant, out rejection))
                        Messages.Message(servant.LabelShortCap + " 已完成召唤。", servant, MessageTypeDefOf.PositiveEvent, false);
                    else
                        Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
                },
                Order = -100f
            };
            if (ServantQueryHasActiveServant(master)) summon.Disable("已有未湮灭的契约从者。");
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

        private static bool ServantQueryHasActiveServant(Pawn master)
        {
            List<Pawn> servants = new List<Pawn>();
            ServantQuery.Instance.GetBoundServants(master, servants);
            foreach (Pawn servant in servants)
                if (servant?.TryGetComp<CompServantState>()?.PresenceState != ServantPresenceState.Annihilated)
                    return true;
            return false;
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
                    ? "命令契约从者进入半透明灵体状态。灵体只跟随御主，不可攻击、工作或进食，仍可查看状态。"
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

            if (!master.Spawned || !servant.Spawned || master.Map != servant.Map)
            {
                command.Disable("御主与从者必须处于同一张地图。");
            }
            return command;
        }
    }
}
