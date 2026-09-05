using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public static class ServantTravelSection
    {
        public static void Add(TransferableOneWayWidget widget, List<TransferableOneWay> transferables)
        {
            List<TransferableOneWay> servants = new List<TransferableOneWay>();
            foreach (TransferableOneWay transferable in transferables)
            {
                Pawn pawn = transferable.AnyThing as Pawn;
                if (ServantDepartureService.IsContractGuest(pawn) && !pawn.IsFreeNonSlaveColonist)
                    servants.Add(transferable);
            }
            if (servants.Count > 0) widget.AddSection("契约从者", servants);
        }
    }
}
