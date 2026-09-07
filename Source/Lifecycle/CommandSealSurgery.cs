using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    internal static class CommandSealSurgery
    {
        private static readonly HashSet<Pawn> operating = new HashSet<Pawn>();
        internal static ThingDef ItemDef(int charges) => charges == 1 ? MW_DefOf.MW_CommandSealOne
            : charges == 2 ? MW_DefOf.MW_CommandSealTwo : charges == 3 ? MW_DefOf.MW_CommandSealThree : null;
        internal static int Charges(Thing item) => item?.def == MW_DefOf.MW_CommandSealOne ? 1
            : item?.def == MW_DefOf.MW_CommandSealTwo ? 2 : item?.def == MW_DefOf.MW_CommandSealThree ? 3 : 0;
        internal static bool CanExtract(Pawn pawn, BodyPartRecord part) => pawn != null && !pawn.Dead && !pawn.Destroyed
            && pawn.Downed && CommandSpellService.Mark(pawn) is Hediff_CommandSpell mark && mark.Part == part;
        internal static bool CanImplant(Pawn pawn, BodyPartRecord part) => pawn != null && !pawn.Dead && !pawn.Destroyed
            && !ServantQuery.Instance.IsServant(pawn) && MasterCircuitUtility.HasCircuit(pawn)
            && CommandSpellService.IsValidCarrier(pawn, part)
            && pawn.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_CommandSpellMark) == null;

        internal static Thing Extract(Pawn pawn, BodyPartRecord part, Func<bool> surgeryFailed)
        {
            if (!CanExtract(pawn, part) || !operating.Add(pawn)) return null;
            try
            {
                Hediff_CommandSpell mark = CommandSpellService.Mark(pawn);
                int charges = mark.Charges;
                // Both success and failure consume the source before native injury callbacks can reenter.
                pawn.health.RemoveHediff(mark);
                if (surgeryFailed() || pawn.Dead || pawn.Destroyed || !CommandSpellService.IsValidCarrier(pawn, part)) return null;
                return ThingMaker.MakeThing(ItemDef(charges));
            }
            finally { operating.Remove(pawn); }
        }

        internal static bool Implant(Pawn pawn, BodyPartRecord part, Thing item, Func<bool> surgeryFailed)
        {
            int charges = Charges(item);
            if (charges == 0 || item.Destroyed || item.stackCount != 1 || !CanImplant(pawn, part) || !operating.Add(pawn)) return false;
            try
            {
                item.Destroy();
                if (surgeryFailed() || !CanImplant(pawn, part)) return false;
                return CommandSpellService.TryGrant(pawn, charges, out _);
            }
            finally { operating.Remove(pawn); }
        }
    }

    public sealed class Recipe_ExtractCommandSeal : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null) => base.AvailableOnNow(thing, part)
            && thing is Pawn pawn && CommandSealSurgery.CanExtract(pawn, part ?? CommandSpellService.RightHand(pawn));
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            BodyPartRecord hand = CommandSpellService.RightHand(pawn);
            if (CommandSealSurgery.CanExtract(pawn, hand)) yield return hand;
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer == null || !billDoer.Spawned || pawn?.Map != billDoer.Map) return;
            Thing extracted = CommandSealSurgery.Extract(pawn, part,
                () => CheckSurgeryFail(billDoer, pawn, ingredients, part, bill));
            if (extracted != null && !GenPlace.TryPlaceThing(extracted, billDoer.Position, billDoer.Map, ThingPlaceMode.Near))
            {
                extracted.Destroy();
                Log.Warning("[MoonWorld] 令咒摘取完成但无法放置物品，印记已损毁。");
            }
        }
    }

    public sealed class Recipe_ImplantCommandSeal : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null) => base.AvailableOnNow(thing, part)
            && thing is Pawn pawn && CommandSealSurgery.CanImplant(pawn, part ?? CommandSpellService.RightHand(pawn));
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            BodyPartRecord hand = CommandSpellService.RightHand(pawn);
            if (CommandSealSurgery.CanImplant(pawn, hand)) yield return hand;
        }
        public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map)
        {
            // Bill_Medical.ApplyOnPawn runs after native ingredient consumption.
            if (CommandSealSurgery.Charges(ingredient) == 0) base.ConsumeIngredient(ingredient, recipe, map);
        }
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer == null || !billDoer.Spawned || pawn?.Map != billDoer.Map || ingredients == null) return;
            Thing seal = null;
            foreach (Thing ingredient in ingredients)
                if (!ingredient.Destroyed && CommandSealSurgery.Charges(ingredient) > 0)
                {
                    if (seal != null) return;
                    seal = ingredient;
                }
            if (seal != null) CommandSealSurgery.Implant(pawn, part, seal,
                () => CheckSurgeryFail(billDoer, pawn, ingredients, part, bill));
        }
    }
}
