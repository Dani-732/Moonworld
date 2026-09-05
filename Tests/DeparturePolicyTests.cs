// Minimal host doubles exercise the production departure policy without starting Unity.
using System;
using System.Collections.Generic;
using MoonWorld;
using RimWorld;
using RimWorld.Planet;
using Verse;

internal static class DeparturePolicyTests
{
    private static int passed;

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
        passed++;
    }

    private static void Main()
    {
        Pawn master = new Pawn { LabelShortCap = "Master", Faction = Faction.OfPlayer };
        Pawn guest = new Pawn { LabelShortCap = "Guest", HostFaction = Faction.OfPlayer,
            state = new CompServantState { Master = master } };
        Pawn ordinary = new Pawn { LabelShortCap = "Ordinary", Faction = Faction.OfPlayer };
        ServantQuery.Pawns.Add(guest);
        string reason;
        Check(ServantDepartureService.CanDepartTogether(new[] { ordinary }, out reason), "ordinary pawn unaffected");
        Check(!ServantDepartureService.CanDepartTogether(new[] { master }, out reason)
            && reason.Contains("Guest"), "master cannot leave remote or unspawned servant behind");
        Check(!ServantDepartureService.CanDepartTogether(new[] { guest }, out reason), "servant cannot leave master behind");
        Check(ServantDepartureService.CanDepartTogether(new[] { guest, master }, out reason), "same party accepted independent of order");
        CompTransporter first = new CompTransporter();
        CompTransporter second = new CompTransporter();
        first.innerContainer.Add(master);
        second.innerContainer.Add(guest);
        Check(!ServantDepartureService.CanLaunchTogether(new[] { first }, out reason), "different launch group rejected");
        Check(ServantDepartureService.CanLaunchTogether(new[] { first, second }, out reason), "two pods in one launch group accepted");
        guest.state.PresenceState = ServantPresenceState.VoluntarySpirit;
        Check(!ServantDepartureService.CanDepartTogether(new[] { master }, out reason), "voluntary spirit still required");
        guest.state.PresenceState = ServantPresenceState.DefeatedSpirit;
        Check(!ServantDepartureService.CanDepartTogether(new[] { master }, out reason), "defeated spirit still required");
        guest.state.PresenceState = ServantPresenceState.Annihilated;
        Check(ServantDepartureService.CanDepartTogether(new[] { master }, out reason), "annihilated servant no longer blocks");
        guest.state.PresenceState = ServantPresenceState.Materialized;
        guest.Dead = true;
        Check(ServantDepartureService.CanDepartTogether(new[] { master }, out reason), "dead servant no longer blocks");
        guest.Dead = false;
        Pawn other = new Pawn { LabelShortCap = "Second", HostFaction = Faction.OfPlayer,
            state = new CompServantState { Master = master } };
        ServantQuery.Pawns.Add(other);
        Check(!ServantDepartureService.CanDepartTogether(new[] { master, guest }, out reason), "all bound servants required");
        ServantQuery.Pawns.Remove(other);
        master.caravan = new Caravan();
        master.caravan.PawnsListForReading.Add(guest);
        Check(ServantDepartureService.CanExitIndividually(master, false, out reason), "existing caravan membership accepted");
        master.caravan = null;
        master.carryTracker.CarriedThing = guest;
        Check(ServantDepartureService.CanExitIndividually(master, false, out reason), "carried companion belongs to departure party");
        Check(ServantDepartureService.IsContractGuest(guest), "guest identity retained");
        guest.IsPrisoner = true;
        Check(!ServantDepartureService.IsContractGuest(guest), "prisoners do not gain guest travel privileges");
        Console.WriteLine(passed + " departure scenarios passed; Unity AI and save/load require in-game testing.");
    }
}

namespace Verse
{
    public class Thing { }
    public sealed class Pawn : Thing
    {
        public bool Dead, Destroyed, IsPrisoner, IsSlave, Spawned;
        public string LabelShortCap;
        public Faction Faction, HostFaction;
        public CompServantState state;
        public Caravan caravan;
        public CarryTracker carryTracker = new CarryTracker();
        public T TryGetComp<T>() where T : class { return state as T; }
    }
    public sealed class CarryTracker { public Thing CarriedThing; }
}
namespace RimWorld
{
    public sealed class Faction { public static readonly Faction OfPlayer = new Faction(); }
    public sealed class CompTransporter { public readonly List<Thing> innerContainer = new List<Thing>(); }
}
namespace RimWorld.Planet
{
    public sealed class Caravan { public readonly List<Pawn> PawnsListForReading = new List<Pawn>(); }
    public static class CaravanUtility { public static Caravan GetCaravan(this Pawn pawn) { return pawn.caravan; } }
    public static class CaravanExitMapUtility
    {
        public static Caravan FindCaravanToJoinFor(Pawn pawn) { return null; }
    }
}
namespace MoonWorld
{
    public enum ServantPresenceState { Materialized, VoluntarySpirit, DefeatedSpirit, Annihilated }
    public sealed class CompServantState { public Pawn Master; public ServantPresenceState PresenceState; }
    public struct ServantSnapshot { public Pawn master; public ServantPresenceState presenceState; }
    public sealed class ServantQuery
    {
        public static readonly ServantQuery Instance = new ServantQuery();
        public static readonly List<Pawn> Pawns = new List<Pawn>();
        public bool TryGetSnapshot(Pawn pawn, out ServantSnapshot snapshot)
        {
            snapshot = new ServantSnapshot();
            if (pawn.state == null) return false;
            snapshot.master = pawn.state.Master;
            snapshot.presenceState = pawn.state.PresenceState;
            return true;
        }
        public Pawn GetMaster(Pawn pawn) { return pawn.state?.Master; }
        public void GetBoundServants(Pawn master, List<Pawn> buffer)
        {
            foreach (Pawn pawn in Pawns) if (pawn.state?.Master == master) buffer.Add(pawn);
        }
    }
}
