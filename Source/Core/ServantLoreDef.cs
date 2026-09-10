using Verse;

namespace MoonWorld
{
    // Static setting summary for the catalogue. It is never used as combat state.
    public sealed class ServantLoreDef : Def
    {
        public ServantIdentityDef identity;
        public string displayName;
        public string epithet;
        public string origin;
        public string alignment;
        public string gender;
        public string heightWeight;
        public string parameters;
        public string classSkills;
        public string noblePhantasm;
        public string summary;

        public ServantIdentityDef Identity => identity;
        public string DisplayName => string.IsNullOrEmpty(displayName)
            ? identity?.fixedName ?? identity?.label ?? identity?.defName ?? "未知英灵" : displayName;
    }
}
