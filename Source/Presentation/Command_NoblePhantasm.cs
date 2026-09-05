using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MoonWorld
{
    public sealed class Command_NoblePhantasm : Command_Ability
    {
        public Command_NoblePhantasm(Ability ability, Pawn pawn) : base(ability, pawn) { }

        public override void ProcessInput(Event ev)
        {
            if (ability.GizmoDisabled(out string reason))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
                return;
            }
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            Find.DesignatorManager.Deselect();
            // The master can expose this command while the servant remains the actual caster.
            Find.Targeter.BeginTargeting(ability.verb, allowNonSelectedTargetingSource: true);
        }
    }
}
