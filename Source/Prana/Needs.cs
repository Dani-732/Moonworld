using RimWorld;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    public class Need_Prana : Need
    {
        public Need_Prana(Pawn pawn) : base(pawn)
        {
        }

        public override float MaxLevel => Mathf.Max(1f, ServantIdentityUtility.GetProfile(pawn)?.maxPrana ?? 100f);

        public override void NeedInterval()
        {
            // Prana is written only by PranaCycleService and explicit ability costs.
        }
    }

    public sealed class Need_MasterPrana : Need_Prana
    {
        public Need_MasterPrana(Pawn pawn) : base(pawn)
        {
        }

        public override float MaxLevel => Mathf.Max(1f, MasterCircuitUtility.GetCircuit(pawn)?.maxPrana ?? 100f);
    }
}
