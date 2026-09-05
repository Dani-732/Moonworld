using System;
using MoonWorld;
using RimWorld;
using Verse;

internal static class SpiritMeleeTests
{
    private static void Check(bool result, string message) { if (!result) throw new Exception(message); }
    public static void Main()
    {
        int passed = 0;
        foreach (Presence state in new[] { Presence.Ordinary, Presence.Materialized, Presence.VoluntarySpirit, Presence.DefeatedSpirit })
        {
            Pawn_MeleeVerbs tracker = new Pawn_MeleeVerbs { Pawn = new Pawn { State = state } };
            bool spirit = state == Presence.VoluntarySpirit || state == Presence.DefeatedSpirit;
            Verb cached = new Verb(); Verb result = cached;
            bool runOriginal = Harmony_SpiritForm_MeleeVerb.Prefix(tracker, ref result);
            Check(runOriginal == !spirit, "wrong verb query routing: " + state);
            Check(spirit ? result == null : result == cached, "cached verb handling: " + state);
            Console.WriteLine("PASS " + state + " melee query routing and cached verb"); passed++;
            bool attackResult = true;
            runOriginal = Harmony_SpiritForm_MeleeAttack.Prefix(tracker, ref attackResult);
            Check(runOriginal == !spirit && attackResult == !spirit, "direct attack bypass: " + state);
            Console.WriteLine("PASS " + state + " direct melee attack routing"); passed++;
        }
        Console.WriteLine(passed + " production melee guard scenarios passed; actual Harmony dispatch requires runtime verification.");
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
}
namespace Verse
{
    public enum Presence { Ordinary, Materialized, VoluntarySpirit, DefeatedSpirit }
    public class Pawn { public Presence State; }
    public class Verb { }
}
namespace RimWorld
{
    public class Pawn_MeleeVerbs
    {
        public Pawn Pawn;
        public Verb TryGetMeleeVerb(object target) => throw new Exception("Original spirit query must not run");
        public bool TryMeleeAttack(object target) => throw new Exception("Original spirit attack must not run");
    }
}
namespace MoonWorld
{
    public class ServantQuery
    {
        public static readonly ServantQuery Instance = new ServantQuery();
        public bool IsSpirit(Pawn pawn) => pawn.State == Presence.VoluntarySpirit || pawn.State == Presence.DefeatedSpirit;
    }
}
