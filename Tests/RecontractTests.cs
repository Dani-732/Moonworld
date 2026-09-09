using System;
using MoonWorld;
using RimWorld;
using Verse;

internal static partial class SummoningTests
{
    private static Pawn Orphan(Pawn pawn)
    {
        pawn.State.Bind(null); pawn.State.UnboundUntilTickAbs = GenTicks.TicksAbs + 60000;
        return pawn;
    }
    private static void RecontractScenarios()
    {
        Test("master enumeration survives nested refresh of native shared pawn buffer", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry;
            Pawn owner = entry.EnemyMaster; Orphan(entry.EnemyServant);
            var candidates = ServantRecontractService.Masters();
            Check(candidates.Contains(owner) && candidates.Count == 1, "nested pawn query lost or duplicated master");
        });
        Test("recontract keeps pawn resources class history and summon use", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; var enemy = entry.Enemies[0];
            Pawn originalMaster = enemy.OriginalMaster, target = Orphan(enemy.EnemyServant);
            entry.PlayerServant.Dead = true; target.Spawned = true; target.Map = map; target.needs.Prana.CurLevel = 17;
            Check(!ServantRecontractService.TryRecontract(master, target, false, out _), "player consent skipped");
            Check(ServantRecontractService.TryRecontract(master, target, true, out string reason), reason);
            Check(target.State.Master == master && target.Faction == Faction.OfPlayer && target.needs.Prana.CurLevel == 17
                && target.State.UnboundUntilTickAbs == -1 && enemy.OriginalMaster == originalMaster
                && entry.FindEnemy(master) == enemy && entry.RegularSummonUsed && PawnGenerator.Created.Count == 3,
                "recontract copied pawn, mana, history or summon");
            Check(!EnemyContractUtility.CanReceiveSupply(target), "player received fixed enemy supply");
        });
        Test("enemy successor joins servant class and inherits its workshop", () => {
            SevenClasses(); PrepareEnemy(); var entry = State.CurrentWarEntry;
            var former = entry.Enemies[0]; var next = entry.Enemies[1];
            former.EnemyServant.Dead = true; Pawn owner = former.OriginalMaster;
            Pawn orphan = Orphan(next.EnemyServant); Faction expected = orphan.Faction;
            var workshop = WorkshopRebuildService.FindWorkshop(next);
            var oldWorkshop = WorkshopRebuildService.FindWorkshop(former);
            Check(ServantRecontractService.TryRecontract(owner, orphan, false, out string reason), reason);
            Check(owner.Faction == expected && next.CurrentMaster == owner && next.OriginalMaster != owner
                && workshop.OwnerMaster == owner && workshop.Participant == next
                && oldWorkshop.OwnerMaster == null
                && entry.FindEnemy(owner) == next && EnemyContractUtility.IsResting(orphan), "enemy successor desynchronized");
        });
        Test("failed binding preserves deadline faction and original lord", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; entry.PlayerServant.Dead = true;
            Pawn target = Orphan(entry.EnemyServant); target.Spawned = true; target.Map = map;
            var faction = target.Faction; int deadline = target.State.UnboundUntilTickAbs;
            var lord = Verse.AI.Group.LordMaker.MakeNewLord(faction, new LordJob_EnemyWarParty(), map, new[] { target });
            ServantLifecycleService.Fail = true;
            Check(!ServantRecontractService.TryRecontract(master, target, true, out _), "failure accepted");
            Check(target.State.Master == null && target.State.UnboundUntilTickAbs == deadline && target.Faction == faction
                && target.Lord == lord && master.Spells.Charges == 3, "failed transfer changed deadline or faction");
        });
        Test("workshop rebuilt after recontract belongs to servant class not master's opening class", () => {
            SevenClasses(); PrepareEnemy(); var entry = State.CurrentWarEntry;
            var former = entry.Enemies[0]; var next = entry.Enemies[1];
            former.EnemyServant.Dead = true; Pawn owner = former.OriginalMaster;
            Pawn orphan = Orphan(next.EnemyServant);
            Check(ServantRecontractService.TryRecontract(owner, orphan, false, out string reason), reason);
            var old = WorkshopRebuildService.FindWorkshop(next); var tile = old.Tile; old.Destroy();
            next.ScheduleWorkshopRebuild(tile);
            Check(WorkshopRebuildService.TryRebuild(State, next, out reason, ignoreTime: true), reason);
            var rebuilt = WorkshopRebuildService.FindWorkshop(next);
            Check(rebuilt != null && rebuilt.OwnerMaster == owner && rebuilt.Participant == next
                && rebuilt.Participant != former && !next.WorkshopRebuildPending, "rebuilt workshop reverted class");
        });
        Test("expired orphan and already bound master cannot recontract", () => {
            PrepareEnemy(); Pawn target = Orphan(State.CurrentWarEntry.EnemyServant);
            target.Spawned = true; target.Map = map;
            Check(!ServantRecontractService.TryRecontract(master, target, true, out _), "bound master took second match");
            State.CurrentWarEntry.PlayerServant.Dead = true; target.State.UnboundUntilTickAbs = GenTicks.TicksAbs;
            Check(!ServantRecontractService.TryRecontract(master, target, true, out _), "expired servant rescued");
        });
        Test("automatic matching offers player a choice without transferring", () => {
            PrepareEnemy(); State.CurrentWarEntry.PlayerServant.Dead = true;
            Pawn target = Orphan(State.CurrentWarEntry.EnemyServant); target.Spawned = true; target.Map = map;
            int deadline = target.State.UnboundUntilTickAbs;
            ServantRecontractService.Tick(); ServantRecontractService.Tick();
            Check(target.State.Master == null && target.State.UnboundUntilTickAbs == deadline
                && ChoiceLetter_Recontract.HasPending(target), "automatic player transfer");
        });
        Test("automatic enemy world match does not generate or refill pawns", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry;
            Pawn owner = entry.EnemyMaster, target = Orphan(entry.EnemyServant); target.needs.Prana.CurLevel = 11;
            ServantRecontractService.Tick();
            Check(target.State.Master == owner && target.needs.Prana.CurLevel == 11 && PawnGenerator.Created.Count == 3,
                "world matching failed or refilled");
        });
        Test("transporter is protected while different maps permit event binding without teleport", () => {
            PrepareEnemy(); State.CurrentWarEntry.PlayerServant.Dead = true;
            Pawn target = Orphan(State.CurrentWarEntry.EnemyServant); target.ParentHolder = new object();
            Check(!ServantRecontractService.TryRecontract(master, target, true, out _), "container pulled out");
            target.ParentHolder = null; target.Spawned = true; target.Map = new Map();
            Map before = target.Map;
            Check(ServantRecontractService.TryRecontract(master, target, true, out string reason) && target.Map == before, reason ?? "cross-map teleport");
        });
        Test("enemy can take original player servant only after confirmation and joins its class", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; Pawn owner = entry.EnemyMaster;
            entry.EnemyServant.Dead = true; Pawn target = Orphan(entry.PlayerServant);
            owner.Spawned = true; owner.Map = map;
            Check(!ServantRecontractService.TryRecontract(owner, target, false, out _), "player servant stolen automatically");
            Check(ServantRecontractService.TryRecontract(owner, target, true, out string reason), reason);
            Check(target.Faction == owner.Faction && target.Faction.def == HolyGrailWarClassDef.For(entry.PlayerIdentity).oppositionFaction
                && entry.FindEnemy(target).CurrentMaster == owner && WarOutcomeService.IsHostileServant(target), "original player slot not hostile");
        });
        Test("post-bind loss of qualification rolls back and preserves remaining lifetime", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; entry.PlayerServant.Dead = true;
            Pawn target = Orphan(entry.EnemyServant); target.Spawned = true; target.Map = map;
            Faction before = target.Faction; int deadline = target.State.UnboundUntilTickAbs;
            ServantLifecycleService.Callback = p => master.Spells.Charges = 0;
            Check(!ServantRecontractService.TryRecontract(master, target, true, out _)
                && target.Faction == before && target.State.Master == null && target.State.UnboundUntilTickAbs == deadline,
                "callback loss accepted or refreshed lifetime");
        });
        Test("recontracted seat retains original and latest master after servant dies and field save", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; var seat = entry.Enemies[0];
            Pawn original = seat.OriginalMaster; entry.PlayerServant.Dead = true;
            Pawn target = Orphan(seat.EnemyServant); target.Spawned = true; target.Map = map;
            Check(ServantRecontractService.TryRecontract(master, target, true, out string reason), reason);
            target.Dead = true; seat.ExposeData(); Scribe.Loading = true;
            var restored = new EnemyWarParticipant(); restored.ExposeData(); Scribe.Loading = false;
            Check(restored.OriginalMaster == original && restored.CurrentMaster == null && restored.EnemyMaster == master,
                "historical and current master conflated on load");
        });
        Test("same caravan player recontract keeps membership without teleport", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; entry.PlayerServant.Dead = true;
            Pawn target = Orphan(entry.EnemyServant); target.Faction = Faction.OfPlayer;
            master.Spawned = target.Spawned = false;
            master.Caravan = target.Caravan = new RimWorld.Planet.Caravan();
            Check(ServantRecontractService.TryRecontract(master, target, true, out string reason), reason);
            Check(!target.Spawned && target.Caravan == master.Caravan, "caravan position changed");
        });
        Test("world orphan accepted by player arrives as original pawn without resource refill", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; entry.PlayerServant.Dead = true;
            Pawn target = Orphan(entry.EnemyServant); target.needs.Prana.CurLevel = 13;
            Check(ServantRecontractService.TryRecontract(master, target, true, out string reason), reason);
            Check(target.Spawned && target.Map == map && target.Position.Id != 0
                && target.needs.Prana.CurLevel == 13 && PawnGenerator.Created.Count == 3,
                "arrival changed pawn, position or mana");
        });
        Test("arrival failure returns world pawn and leaves old deadline", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; entry.PlayerServant.Dead = true;
            Pawn target = Orphan(entry.EnemyServant); int deadline = target.State.UnboundUntilTickAbs;
            target.becameWorldPawnTickAbs = 123; GenSpawn.Fail = true;
            Check(!ServantRecontractService.TryRecontract(master, target, true, out _)
                && !target.Spawned && Find.WorldPawns.Contains(target) && target.State.Master == null
                && target.State.UnboundUntilTickAbs == deadline && target.becameWorldPawnTickAbs == 123, "failed arrival lost original pawn");
        });
        Test("arrival callback qualification loss rolls back before workshop transfer", () => {
            PrepareEnemy(); var entry = State.CurrentWarEntry; entry.PlayerServant.Dead = true;
            Pawn target = Orphan(entry.EnemyServant); int deadline = target.State.UnboundUntilTickAbs;
            var seat = entry.FindEnemy(target); var site = WorkshopRebuildService.FindWorkshop(seat);
            Pawn owner = site.OwnerMaster; Faction faction = target.Faction;
            GenSpawn.Callback = () => master.Spells.Charges = 0;
            Check(!ServantRecontractService.TryRecontract(master, target, true, out _)
                && !target.Spawned && Find.WorldPawns.Contains(target) && target.State.Master == null
                && target.State.UnboundUntilTickAbs == deadline && target.Faction == faction
                && site.OwnerMaster == owner && seat.EnemyMaster == owner, "late callback committed invalid contract");
        });
        Test("chance miss retries later and rejecting one offer leaves other opportunities", () => {
            SevenClasses(); PrepareEnemy(); var entry = State.CurrentWarEntry; entry.PlayerServant.Dead = true;
            Pawn first = Orphan(entry.Enemies[0].EnemyServant), second = Orphan(entry.Enemies[1].EnemyServant);
            first.Spawned = second.Spawned = true; first.Map = second.Map = map;
            Rand.Pass = false; ServantRecontractService.Tick();
            Check(!ChoiceLetter_Recontract.HasPending(first) && !first.State.RecontractOfferSent, "chance miss consumed offer");
            Rand.Pass = true; ServantRecontractService.Tick();
            var firstOffer = (ChoiceLetter_Recontract)Find.LetterStack.LettersListForReading[0];
            new System.Collections.Generic.List<DiaOption>(firstOffer.Choices).Find(o => o.Label == "reject").action();
            int deadline = first.State.UnboundUntilTickAbs;
            ServantRecontractService.Tick();
            Check(!ChoiceLetter_Recontract.HasPending(first) && ChoiceLetter_Recontract.HasPending(second)
                && ServantRecontractService.IsWaitingMaster(master) && master.Spells.Charges == 3
                && first.State.UnboundUntilTickAbs == deadline, "rejection revoked master or refreshed lifetime");
        });
        Test("real offer acceptance binds once and stale saved choice cannot accept again", () => {
            PrepareEnemy(); State.CurrentWarEntry.PlayerServant.Dead = true;
            Pawn target = Orphan(State.CurrentWarEntry.EnemyServant);
            ServantRecontractService.Tick();
            var offer = (ChoiceLetter_Recontract)Find.LetterStack.LettersListForReading[0];
            var accept = new System.Collections.Generic.List<DiaOption>(offer.Choices)[0];
            accept.action();
            Check(target.State.Master == master && !ChoiceLetter_Recontract.HasPending(target), "letter acceptance did not bind");
            Orphan(target); int deadline = target.State.UnboundUntilTickAbs; accept.action();
            Check(target.State.Master == null && target.State.UnboundUntilTickAbs == deadline, "archived callback rebound servant");
        });
        Test("real saved offer from earlier unbound episode cannot block or consent to later episode", () => {
            PrepareEnemy(); State.CurrentWarEntry.PlayerServant.Dead = true;
            Pawn target = Orphan(State.CurrentWarEntry.EnemyServant);
            ChoiceLetter_Recontract.Offer(target);
            var offer = (ChoiceLetter_Recontract)Find.LetterStack.LettersListForReading[0];
            var accept = new System.Collections.Generic.List<DiaOption>(offer.Choices)[0];
            offer.ExposeData(); Scribe.Loading = true;
            var restored = new ChoiceLetter_Recontract(); restored.ExposeData(); Scribe.Loading = false;
            Find.LetterStack.LettersListForReading.Clear(); Find.LetterStack.ReceiveLetter(restored);
            Check(ChoiceLetter_Recontract.HasPending(target), "field roundtrip lost current event");
            target.State.UnboundUntilTickAbs += 100;
            accept.action();
            Check(target.State.Master == null && !ChoiceLetter_Recontract.HasPending(target)
                && new System.Collections.Generic.List<DiaOption>(restored.Choices)[0].Label == "close", "old episode stayed actionable");
        });
        Test("real expired offer closes without extending orphan life", () => {
            PrepareEnemy(); State.CurrentWarEntry.PlayerServant.Dead = true;
            Pawn target = Orphan(State.CurrentWarEntry.EnemyServant); ChoiceLetter_Recontract.Offer(target);
            var offer = (ChoiceLetter_Recontract)Find.LetterStack.LettersListForReading[0];
            var accept = new System.Collections.Generic.List<DiaOption>(offer.Choices)[0];
            Find.TickManager.TicksGame += target.State.UnboundUntilTickAbs - GenTicks.TicksAbs; accept.action();
            Check(target.State.Master == null && !ChoiceLetter_Recontract.HasPending(target)
                && new System.Collections.Generic.List<DiaOption>(offer.Choices)[0].Label == "close", "expired offer still bound");
        });
    }
}
