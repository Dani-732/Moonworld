using Verse;

namespace MoonWorld
{
    public static class ServantPhysiologyPolicy
    {
        public static bool IsTimeless(Pawn pawn)
        {
            return ServantQuery.Instance.IsServant(pawn);
        }

        public static bool IsDisease(HediffDef def)
        {
            if (def?.comps == null)
            {
                return false;
            }
            foreach (HediffCompProperties properties in def.comps)
            {
                if (properties is HediffCompProperties_Immunizable)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
