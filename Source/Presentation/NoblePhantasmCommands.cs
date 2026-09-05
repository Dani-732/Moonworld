using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class NoblePhantasmCommands
    {
        public static IEnumerable<Gizmo> ForServant(Pawn master, Pawn servant)
        {
            var defs = ServantIdentityUtility.GetIdentity(servant)?.noblePhantasms;
            if (defs == null || defs.Count == 0 || servant.Dead || servant.Destroyed) yield break;
            foreach (AbilityDef def in defs)
            {
                Ability ability = servant.abilities?.GetAbility(def);
                if (ability != null)
                    foreach (Command command in ability.GetGizmos()) yield return command;
            }
            var overcharge = new Command_Action
            {
                defaultLabel = "宝具过载：" + servant.LabelShort,
                defaultDesc = "消耗一枚令咒，令目标下一次成功释放的宝具免除魔力消耗并增强伤害。",
                icon = TexButton.Reveal,
                action = delegate
                {
                    string reason;
                    if (!NoblePhantasmService.TryOvercharge(master, servant, out reason))
                        Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                    else Messages.Message(servant.LabelShortCap + " 已获得宝具过载。", servant, MessageTypeDefOf.PositiveEvent, false);
                }
            };
            string rejection = NoblePhantasmService.ValidateOvercharge(master, servant);
            if (rejection != null) overcharge.Disable(rejection);
            yield return overcharge;
        }
    }
}
