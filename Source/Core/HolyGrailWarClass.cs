namespace MoonWorld
{
    public enum HolyGrailWarClass
    {
        None,
        Saber,
        Archer,
        Lancer,
        Assassin,
        Caster,
        Rider,
        Berserker
    }

    public static class HolyGrailWarClassUtility
    {
        public static bool IsWarClass(HolyGrailWarClass warClass)
        {
            return warClass >= HolyGrailWarClass.Saber && warClass <= HolyGrailWarClass.Berserker;
        }

        public static ServantIdentityDef PickOpponent(ServantIdentityDef player)
        {
            if (player == null || !IsWarClass(player.warClass)) return null;
            return ServantSummonPoolDef.Pick(player.warClass);
        }
    }
}
