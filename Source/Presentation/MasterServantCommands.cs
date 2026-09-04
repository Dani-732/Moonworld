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

            List<Pawn> servants = new List<Pawn>();
            ServantQuery.Instance.GetBoundServants(master, servants);
            servants.Sort((left, right) => left.thingIDNumber.CompareTo(right.thingIDNumber));
            foreach (Pawn servant in servants)
            {
                Command_Action command = CreateCommand(master, servant);
                if (command != null)
                {
                    yield return command;
                }
            }
        }

        private static Command_Action CreateCommand(Pawn master, Pawn servant)
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
                    ? "命令契约从者进入灵体状态。灵体状态不可攻击、工作、进食或被正常选定。"
                    : "命令契约从者解除灵体状态并恢复自主行动。",
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
            else if (state.PresenceState == ServantPresenceState.DefeatedSpirit)
            {
                int remaining = state.RematerializationReadyTick - Find.TickManager.TicksGame;
                if (remaining > 0)
                {
                    command.Disable("战败后的灵基仍在凝聚，还需 " + remaining.ToStringTicksToPeriod() + "。");
                }
            }
            return command;
        }
    }
}
