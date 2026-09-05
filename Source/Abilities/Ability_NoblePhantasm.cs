using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class NoblePhantasmExtension : DefModExtension
    {
        public float pranaCost = 40f;
        public int damage = 40;
        public float armorPenetration = 0.6f;
        public float overchargeDamageMultiplier = 2f;

        public override IEnumerable<string> ConfigErrors()
        {
            if (float.IsNaN(pranaCost) || float.IsInfinity(pranaCost) || pranaCost <= 0f)
                yield return "pranaCost must be finite and positive";
            if (damage <= 0 || armorPenetration < 0f || float.IsNaN(armorPenetration) || float.IsInfinity(armorPenetration))
                yield return "Explosion damage must be positive and armor penetration nonnegative";
            if (float.IsNaN(overchargeDamageMultiplier) || float.IsInfinity(overchargeDamageMultiplier)
                || overchargeDamageMultiplier < 1f)
                yield return "overchargeDamageMultiplier must be finite and at least one";
        }
    }

    public sealed class Ability_NoblePhantasm : Ability
    {
        private bool resolving;
        public Ability_NoblePhantasm() { }
        public Ability_NoblePhantasm(Pawn pawn) : base(pawn) { }
        public Ability_NoblePhantasm(Pawn pawn, AbilityDef def) : base(pawn, def) { }

        public override AcceptanceReport CanCast
        {
            get
            {
                string reason = NoblePhantasmService.ValidateCaster(this);
                return reason != null ? new AcceptanceReport(reason) : base.CanCast;
            }
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (resolving) return false;
            resolving = true;
            try
            {
                string rejection;
                if (NoblePhantasmService.TryCast(this, target, out rejection)) return true;
                Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            finally { resolving = false; }
        }

        internal void CompleteCast(LocalTargetInfo target)
        {
            // No effect comps: the service owns the explosion, vanilla owns cooldown and cast bookkeeping.
            base.Activate(target, LocalTargetInfo.Invalid);
        }

        public override string Tooltip => base.Tooltip + "\n魔力消耗："
            + def.GetModExtension<NoblePhantasmExtension>().pranaCost
            + (NoblePhantasmService.IsOvercharged(pawn) ? "（宝具过载：本次免消耗）" : "");
    }
}
