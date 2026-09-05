using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    public static class NoblePhantasmService
    {
        public static void EnsureAbilities(Pawn servant)
        {
            var defs = ServantIdentityUtility.GetIdentity(servant)?.noblePhantasms;
            if (defs == null || defs.Count == 0) return;
            if (servant.abilities == null) servant.abilities = new Pawn_AbilityTracker(servant);
            foreach (AbilityDef def in defs)
                if (servant.abilities.GetAbility(def) == null) servant.abilities.GainAbility(def);
        }

        public static bool IsOvercharged(Pawn servant)
        {
            return servant?.health?.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_NoblePhantasmOvercharge) != null;
        }

        private static string ValidateContract(Pawn master, Pawn servant)
        {
            if (master == null || master.Dead || master.Destroyed || master.Faction != Faction.OfPlayer
                || !MasterCircuitUtility.HasCircuit(master)) return "需要存活的玩家御主。";
            if (servant == null || servant.Dead || servant.Destroyed
                || ServantQuery.Instance.GetMaster(servant) != master
                || servant.TryGetComp<CompServantState>()?.PresenceState == ServantPresenceState.Annihilated)
                return "目标不是有效的契约从者。";
            if (!master.Spawned || !servant.Spawned || master.Map != servant.Map)
                return "御主与从者必须处于同一张地图。";
            return null;
        }

        public static string ValidateCaster(Ability ability)
        {
            Pawn servant = ability.pawn;
            string rejection = ValidateContract(ServantQuery.Instance.GetMaster(servant), servant);
            if (rejection != null) return rejection;
            if (!ServantQuery.Instance.IsMaterialized(servant)) return "灵体状态不能释放宝具。";
            if (servant.Downed || servant.InMentalState || servant.WorkTagIsDisabled(WorkTags.Violent))
                return "从者当前不能施放攻击能力。";
            var identity = ServantIdentityUtility.GetIdentity(servant);
            if (identity == null || !identity.noblePhantasms.Contains(ability.def)
                || servant.abilities?.GetAbility(ability.def) != ability) return "从者未拥有该宝具。";
            var settings = ability.def.GetModExtension<NoblePhantasmExtension>();
            if (settings == null || ability.def.EffectRadius <= 0f || float.IsNaN(ability.def.EffectRadius)
                || float.IsInfinity(ability.def.EffectRadius))
                return "宝具配置无效。";
            foreach (string error in settings.ConfigErrors()) return "宝具配置无效：" + error;
            Need_Prana prana = servant.needs?.TryGetNeed<Need_Prana>();
            if (prana == null) return "从者缺少魔力需求。";
            if (!IsOvercharged(servant) && prana.CurLevel < settings.pranaCost)
                return "从者魔力不足，需要 " + settings.pranaCost + " 点。";
            return null;
        }

        public static string ValidateOvercharge(Pawn master, Pawn servant)
        {
            string rejection = ValidateContract(master, servant);
            if (rejection != null) return rejection;
            var defs = ServantIdentityUtility.GetIdentity(servant)?.noblePhantasms;
            if (defs == null || defs.Count == 0)
                return "该从者没有可用宝具。";
            if (IsOvercharged(servant)) return "目标已有待消耗的宝具过载。";
            if ((master.TryGetComp<CompMasterCommandSpells>()?.Charges ?? 0) <= 0)
                return "令咒已耗尽。";
            return null;
        }

        public static bool TryOvercharge(Pawn master, Pawn servant, out string rejection)
        {
            rejection = ValidateOvercharge(master, servant);
            if (rejection != null) return false;
            Hediff pending = HediffMaker.MakeHediff(MW_DefOf.MW_NoblePhantasmOvercharge, servant);
            try
            {
                servant.health.AddHediff(pending);
                if (!servant.health.hediffSet.hediffs.Contains(pending)
                    || !master.TryGetComp<CompMasterCommandSpells>().TrySpendCharge())
                    throw new InvalidOperationException("Overcharge could not be applied or paid for");
                return true;
            }
            catch (Exception ex)
            {
                if (servant.health.hediffSet.hediffs.Contains(pending)) servant.health.RemoveHediff(pending);
                Log.Error("[MoonWorld] 宝具过载失败: " + ex);
                rejection = "宝具过载未能生效。";
                return false;
            }
        }

        internal static bool TryCast(Ability_NoblePhantasm ability, LocalTargetInfo target, out string rejection)
        {
            rejection = ValidateCaster(ability);
            if (rejection != null) return false;
            if (!ability.CanCast) { rejection = "宝具当前不可施放或仍在冷却。"; return false; }
            Pawn servant = ability.pawn;
            Map map = servant.Map;
            if (!target.IsValid || !target.Cell.InBounds(map)
                || (target.HasThing && (!target.Thing.Spawned || target.Thing.Map != map))
                || !ability.verb.ValidateTarget(target, false))
            { rejection = "目标无效、超出射程或不可命中。"; return false; }

            var settings = ability.def.GetModExtension<NoblePhantasmExtension>();
            Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
            float previousPrana = prana.CurLevel;
            Hediff pending = servant.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_NoblePhantasmOvercharge);
            Explosion explosion = null;
            try
            {
                // Keep the owned vanilla explosion so initialization failure can remove it before its first damage tick.
                explosion = (Explosion)ThingMaker.MakeThing(ThingDefOf.Explosion);
                explosion.radius = ability.def.EffectRadius;
                explosion.damType = DamageDefOf.Bomb;
                explosion.instigator = servant;
                explosion.damAmount = Mathf.RoundToInt(settings.damage * (pending == null ? 1f : settings.overchargeDamageMultiplier));
                explosion.armorPenetration = settings.armorPenetration;
                explosion.doVisualEffects = true;
                explosion.doSoundEffects = true;
                if (pending == null) prana.CurLevel -= settings.pranaCost;
                else servant.health.RemoveHediff(pending);
                GenSpawn.Spawn(explosion, target.Cell, map);
                explosion.StartExplosion(DamageDefOf.Bomb.soundExplosion, null);
                ability.CompleteCast(target);
                return true;
            }
            catch (Exception ex)
            {
                if (explosion != null && !explosion.Destroyed) explosion.Destroy(DestroyMode.Vanish);
                prana.CurLevel = previousPrana;
                ability.ResetCooldown();
                if (pending != null && !servant.health.hediffSet.hediffs.Contains(pending)) servant.health.AddHediff(pending);
                Log.Error("[MoonWorld] 宝具释放失败并回滚: " + ex);
                rejection = "宝具释放失败，魔力与过载状态已恢复。";
                return false;
            }
        }
    }
}
