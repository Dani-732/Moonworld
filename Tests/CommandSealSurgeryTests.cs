using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using Verse;

internal static partial class CommandSpellTests
{
    private static void SurgeryScenarios()
    {
        foreach (int charges in new[] { 1, 2, 3 })
            Test("dedicated extraction and implantation conserve charges " + charges, () => {
                Pawn donor = Master(); donor.Downed = true; Grant(donor, charges);
                Thing item = CommandSealSurgery.Extract(donor, CommandSpellService.RightHand(donor), () => false);
                Check(CommandSealSurgery.Charges(item) == charges && donor.Spells.Charges == 0
                    && UnboundServantService.LastUnavailable == donor, "extraction did not consume mark");
                Pawn receiver = Master();
                Check(CommandSealSurgery.Implant(receiver, CommandSpellService.RightHand(receiver), item, () => false)
                    && receiver.Spells.Charges == charges && item.Destroyed, "implant mismatch");
                Check(!CommandSealSurgery.Implant(Master(), CommandSpellService.RightHand(receiver), item, () => false), "spent item reused");
            });
        Test("extraction failure destroys mark and never yields item", () => {
            Pawn p = Master(); p.Downed = true; Grant(p, 2);
            Check(CommandSealSurgery.Extract(p, CommandSpellService.RightHand(p), () => true) == null
                && p.Spells.Charges == 0 && UnboundServantService.LastUnavailable == p, "failed extraction retained mark");
        });
        Test("conscious recipient cannot be extracted merely because surgery was requested", () => {
            Pawn p = Master(); Grant(p);
            Check(CommandSealSurgery.Extract(p, CommandSpellService.RightHand(p), () => false) == null && p.Spells.Charges == 3,
                "conscious master extracted");
        });
        Test("direct arm amputation never yields transplantable seal", () => {
            Pawn p = Master(); p.Downed = true; Grant(p);
            p.health.AddHediff(new Hediff_MissingPart { Part = CommandSpellService.RightHand(p).parent });
            Check(CommandSealSurgery.Extract(p, CommandSpellService.RightHand(p), () => false) == null, "amputation harvested seals");
        });
        Test("extraction native outcome death or limb loss prevents output", () => {
            foreach (bool dies in new[] { false, true }) {
                Pawn p = Master(); p.Downed = true; Grant(p); BodyPartRecord hand = CommandSpellService.RightHand(p);
                Thing result = CommandSealSurgery.Extract(p, hand, () => {
                    if (dies) p.Dead = true; else p.health.AddHediff(new Hediff_MissingPart { Part = hand }); return false;
                });
                Check(result == null && p.Spells.Charges == 0, "invalid outcome generated seal");
            }
        });
        Test("recursive and repeated extraction cannot duplicate output", () => {
            Pawn p = Master(); p.Downed = true; Grant(p); BodyPartRecord hand = CommandSpellService.RightHand(p);
            Thing result = CommandSealSurgery.Extract(p, hand, () => {
                Check(CommandSealSurgery.Extract(p, hand, () => false) == null, "recursive extraction"); return false;
            });
            Check(result != null && CommandSealSurgery.Extract(p, hand, () => false) == null, "repeated extraction");
        });
        Test("implant failure consumes material without granting mark", () => {
            Pawn p = Master(); Thing item = ThingMaker.MakeThing(MW_DefOf.MW_CommandSealTwo);
            Check(!CommandSealSurgery.Implant(p, CommandSpellService.RightHand(p), item, () => true)
                && item.Destroyed && p.Spells.Charges == 0, "failed implant retained material");
        });
        Test("existing mark blocks implantation without stacking", () => {
            Pawn p = Master(); Grant(p, 1); Thing item = ThingMaker.MakeThing(MW_DefOf.MW_CommandSealThree);
            Check(!CommandSealSurgery.Implant(p, CommandSpellService.RightHand(p), item, () => false)
                && !item.Destroyed && p.Spells.Charges == 1, "stacked marks");
        });
        Test("implant rechecks death right hand and existing mark after native callbacks", () => {
            foreach (int mode in new[] { 0, 1, 2 }) {
                Pawn p = Master(); var hand = CommandSpellService.RightHand(p); var item = ThingMaker.MakeThing(MW_DefOf.MW_CommandSealThree);
                Check(!CommandSealSurgery.Implant(p, hand, item, () => {
                    if (mode == 0) p.Dead = true;
                    else if (mode == 1) p.health.AddHediff(new Hediff_MissingPart { Part = hand });
                    else Grant(p, 1);
                    return false;
                }) && item.Destroyed && p.Spells.Charges <= 1, "callback bypassed implant eligibility");
            }
        });
        Test("implant recursive callback cannot reuse a consumed item", () => {
            Pawn p = Master(); Pawn other = Master(); var item = ThingMaker.MakeThing(MW_DefOf.MW_CommandSealTwo);
            Check(CommandSealSurgery.Implant(p, CommandSpellService.RightHand(p), item, () => {
                Check(!CommandSealSurgery.Implant(other, CommandSpellService.RightHand(other), item, () => false), "recursive item reused"); return false;
            }) && p.Spells.Charges == 2 && other.Spells.Charges == 0, "recursive grant");
        });
        Test("native pre-consumption defers only seal and worker consumes it once", () => {
            Pawn p = Master(), doctor = Master(); doctor.Map = p.Map;
            var worker = new Recipe_ImplantCommandSeal(); var item = ThingMaker.MakeThing(MW_DefOf.MW_CommandSealTwo);
            var medicine = ThingMaker.MakeThing(new ThingDef()); var ingredients = new List<Thing> { item, medicine };
            worker.ConsumeIngredient(item, new RecipeDef(), p.Map); worker.ConsumeIngredient(medicine, new RecipeDef(), p.Map);
            Check(!item.Destroyed && medicine.Destroyed, "native consumption lost seal prematurely");
            worker.ApplyOnPawn(p, CommandSpellService.RightHand(p), doctor, ingredients, new Bill());
            worker.ApplyOnPawn(p, CommandSpellService.RightHand(p), doctor, ingredients, new Bill());
            Check(p.Spells.Charges == 2 && item.Destroyed, "native worker reused ingredient");
        });
        Test("no circuit servant and missing hand reject implant", () => {
            foreach (int mode in new[] { 0, 1, 2 }) {
                Pawn p = Master(); var hand = CommandSpellService.RightHand(p);
                if (mode == 0) p.Circuit = false;
                else if (mode == 1) p.Servant = true;
                else p.health.AddHediff(new Hediff_MissingPart { Part = hand });
                Check(!CommandSealSurgery.CanImplant(p, hand), "invalid implant recipient");
            }
        });
    }
}
