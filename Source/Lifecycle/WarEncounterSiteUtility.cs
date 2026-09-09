using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MoonWorld
{
    internal static class WarEncounterSiteUtility
    {
        internal static Site_WarEncounter TryCreate(PlanetTile origin, Faction faction, bool challenge)
        {
            Site_WarEncounter site = null;
            try
            {
                // Flood-fill only traversable tiles; never use the new-site fallback across impassable terrain.
                if (!TileFinder.TryFindPassableTileWithTraversalDistance(origin, 1, challenge ? 3 : 6,
                    out PlanetTile tile, t => !Find.WorldObjects.AnyWorldObjectAt(t)
                        && TileFinder.IsValidTileForNewSettlement(t))) return null;
                site = (Site_WarEncounter)WorldObjectMaker.MakeWorldObject(MW_DefOf.MW_WarEncounter);
                site.Tile = tile;
                site.SetFaction(faction);
                site.AddPart(new SitePart(site, challenge ? MW_DefOf.MW_WarChallengePart : MW_DefOf.MW_WarFieldBattlePart,
                    new SitePartParams()));
                Find.WorldObjects.Add(site);
                if (!site.Spawned || site.Destroyed) throw new InvalidOperationException("Encounter site was not retained.");
                return site;
            }
            catch (Exception ex)
            {
                if (site != null && !site.Destroyed && !site.HasMap) site.Destroy();
                Log.Warning("[MoonWorld] 无法建立野外交战地点：" + ex.Message);
                return null;
            }
        }

        internal static void Cleanup(Site site)
        {
            if (site is Site_WarEncounter && !site.Destroyed && !site.HasMap
                && !EnemyBattleService.AtSite(site) && !EnemyChallengeService.AtSite(site)) site.Destroy();
        }
    }
}
