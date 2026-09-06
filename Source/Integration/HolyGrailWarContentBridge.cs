using System;
using HarmonyLib;
using Verse;

namespace MoonWorld
{
    internal static class HolyGrailWarContentBridge
    {
        // The dependency owns identity and equipment, including for pawns created off-map.
        internal static void InitializeWorldServant(Pawn pawn)
        {
            Type utility = AccessTools.TypeByName("HolyGrailWar.ServantIdentityUtility");
            var getIdentity = utility == null ? null : AccessTools.Method(utility, "GetIdentity", new[] { typeof(Pawn) });
            object identity = getIdentity?.Invoke(null, new object[] { pawn });
            if (identity == null)
                throw new InvalidOperationException("内容依赖未提供敌方从者的初始化身份。");
            var enforce = AccessTools.Method(utility, "Enforce", new[] { typeof(Pawn), identity.GetType(), typeof(bool) });
            if (enforce == null)
                throw new InvalidOperationException("内容依赖的从者初始化接口不兼容。");
            enforce.Invoke(null, new[] { (object)pawn, identity, true });
        }
    }
}
