using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

internal static class RuntimeContractChecks
{
    private static string[] directories;
    private static int Main(string[] args)
    {
        directories = args;
        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) => {
            string name = new AssemblyName(eventArgs.Name).Name + ".dll";
            foreach (string directory in directories)
            {
                string path = Path.Combine(directory, name);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try { Check(); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Check()
    {
        Assembly mod = Assembly.LoadFrom(Path.Combine(directories[0], "MoonWorld.dll"));
        int patches = 0;
        foreach (Type type in mod.GetTypes())
        {
            foreach (HarmonyPatch attribute in type.GetCustomAttributes(typeof(HarmonyPatch), false))
            {
                HarmonyMethod info = attribute.info;
                MethodInfo original = AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
                if (original == null) throw new Exception("Missing patch target: " + type.FullName);
                foreach (MethodInfo patch in type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Prefix" || m.Name == "Postfix"))
                {
                    foreach (ParameterInfo parameter in patch.GetParameters())
                    {
                        string name = parameter.Name;
                        Type supplied;
                        if (name == "__instance") supplied = original.DeclaringType;
                        else if (name == "__result") supplied = original.ReturnType;
                        else if (name.StartsWith("___"))
                        {
                            FieldInfo field = AccessTools.Field(original.DeclaringType, name.Substring(3));
                            if (field == null) throw new Exception("Missing injected field: " + type.Name + "." + name);
                            supplied = field.FieldType;
                        }
                        else if (name == "__state" || name == "__originalMethod" || name == "__runOriginal") continue;
                        else
                        {
                            ParameterInfo[] args = original.GetParameters();
                            int index;
                            ParameterInfo source = name.StartsWith("__") && int.TryParse(name.Substring(2), out index)
                                ? args[index] : args.SingleOrDefault(p => p.Name == name);
                            if (source == null) throw new Exception("Missing injected argument: " + type.Name + "." + name);
                            supplied = source.ParameterType;
                        }
                        Type wanted = parameter.ParameterType;
                        if (wanted.IsByRef) wanted = wanted.GetElementType();
                        if (supplied.IsByRef) supplied = supplied.GetElementType();
                        if (!wanted.IsAssignableFrom(supplied)) throw new Exception("Patch type mismatch: " + type.Name + "." + name);
                    }
                }
                patches++;
            }
        }
        if (mod.GetType("MoonWorld.LordJob_ServantGuest") == null) throw new Exception("Legacy lord type removed");
        if (mod.GetType("MoonWorld.GameComponent_MoonWorld").GetField("warStartTick") == null)
            throw new Exception("War start field removed");
        Assembly game = Assembly.LoadFrom(Path.Combine(directories[1], "Assembly-CSharp.dll"));
        foreach (string[] pair in new[] {
            new[] { "MoonWorld.ScenPart_HolyGrailWar", "RimWorld.ScenPart" },
            new[] { "MoonWorld.IncidentWorker_HolyGrailWarInvitation", "RimWorld.IncidentWorker" },
            new[] { "MoonWorld.IncidentWorker_EnemyServantRaid", "RimWorld.IncidentWorker" },
            new[] { "MoonWorld.ChoiceLetter_HolyGrailWar", "Verse.ChoiceLetter" },
            new[] { "MoonWorld.HolyGrailWarEntry", "Verse.IExposable" },
            new[] { "MoonWorld.QuestPart_HolyGrailWar", "RimWorld.QuestPart" },
            new[] { "MoonWorld.Site_WarWorkshop", "RimWorld.Planet.Site" },
            new[] { "MoonWorld.LordJob_EnemyWarParty", "Verse.AI.Group.LordJob" },
            new[] { "MoonWorld.LordToil_EnemyServantAssault", "Verse.AI.Group.LordToil" },
            new[] { "MoonWorld.JobGiver_EnemyServantAssault", "RimWorld.JobGiver_AIFightEnemies" } })
        {
            Type implementation = mod.GetType(pair[0]);
            Type contract = game.GetType(pair[1]);
            if (implementation == null || contract == null || !contract.IsAssignableFrom(implementation)
                || implementation.GetConstructor(Type.EmptyTypes) == null)
                throw new Exception("Invalid XML/Scribe entry type: " + pair[0]);
        }
        Type stateType = mod.GetType("MoonWorld.GameComponent_MoonWorld", true);
        FieldInfo warQuest = stateType.GetField("warQuest", BindingFlags.Instance | BindingFlags.NonPublic);
        if (warQuest == null || warQuest.FieldType != game.GetType("RimWorld.Quest"))
            throw new Exception("MoonWorld war state must retain a native Quest reference");
        Type questService = mod.GetType("MoonWorld.HolyGrailWarQuestService", true);
        if (questService.GetMethod("Ensure", BindingFlags.Static | BindingFlags.NonPublic) == null)
            throw new Exception("Holy Grail War quest service entry point missing");
        foreach (string removed in new[] { "Harmony_ServantDeparture_Selectable", "Harmony_ServantDeparture_NoCapture",
            "Harmony_ServantTravelAutonomy", "Harmony_ServantTravelBoardingDuty", "Harmony_ServantTravelSection",
            "Harmony_ServantTravel_NoHaulingStandingGuest", "Command_NoblePhantasm" })
            if (mod.GetType("MoonWorld." + removed) != null) throw new Exception("Obsolete adapter remains: " + removed);
        Assembly content = Assembly.LoadFrom(Path.Combine(directories[3], "HolyGrailWarTest.dll"));
        Type identityUtility = content.GetType("HolyGrailWar.ServantIdentityUtility", true);
        Type pawnType = game.GetType("Verse.Pawn", true);
        MethodInfo getIdentity = identityUtility.GetMethod("GetIdentity", new[] { pawnType });
        if (getIdentity == null || identityUtility.GetMethod("Enforce",
            new[] { pawnType, getIdentity.ReturnType, typeof(bool) }) == null)
            throw new Exception("Installed Holy Grail War content initialization API incompatible");
        Console.WriteLine(patches + " Harmony targets and injected parameter types resolved against installed RimWorld 1.6.");
        Console.WriteLine("Legacy types, XML/Scribe entry types, Site and installed content initialization API checked. This does not execute patches or start Unity.");
    }
}
