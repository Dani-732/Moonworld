# Holy Grail War Module Boundaries

## Scope

This document is the implementation contract for the Holy Grail War MVP inside `MoonWorld`. It freezes dependency direction without prebuilding phase-two systems. `HolyGrailWarTest` is a separate visual prototype and is not a dependency.

## Dependency Rule

```text
Defs -> Core query/contract -> Lifecycle
                         -> Prana pipeline
                         -> Damage policy / autonomy / noble phantasm resolution
                         -> presentation and Harmony adapters
```

No lower layer may call a presentation class. No presentation, Gizmo, VFX, AI worker, or Harmony adapter may write a servant state, Need, or Hediff directly.

## Modules

| Module | Owns | Public boundary | Forbidden responsibility |
|---|---|---|---|
| `Core` | identity lookup, contract lookup, immutable snapshots | `IServantQuery`, `IContractLookup` | saving mutable state or running gameplay Tick logic |
| `Lifecycle` | contract bind, materialized/spirit/annihilated transition | `IServantLifecycle` | prana arithmetic, targeting, VFX |
| `Core` physiology policy | immortal servant age and disease classification | `ServantPhysiologyPolicy` | adding or removing Hediffs |
| `Prana` | Need changes, source pipeline, upkeep, shortage, healing | `IPranaSource` | changing presence state except by lifecycle request |
| `Combat` | damage permission and battle-defeat request | `IServantDamagePolicy` | creating effects or directly changing Need/Hediff |
| `Autonomy` | Quest Lodger guest setup, vanilla visitor Lord/Duty and target policy | `IServantAutonomyPolicy` | changing contract or damage rules |
| `Abilities` | ability cost, validity and resolved impact | `INoblePhantasmResolver` | rendering or direct lifecycle field writes |
| `Presentation` | Gizmos, sounds, VFX and rendering | result payloads only | gameplay authority |
| `Integration` | narrow Harmony patches into a service, including role-specific Need eligibility | no state API | business rules |

## Persistent Data

Only these custom fields are persisted:

```text
GameComponent_MoonWorld.warStartTick
CompServantState.master
CompServantState.presenceState
CompServantState.rematerializationReadyTick
CompMasterPranaControl.supplyThresholdOverride
CompMasterCommandSpells.commandSpellCharges (future MVP slice)
```

Prana values remain in `Need_MasterPrana` and `Need_Prana`; shortage duration uses the vanilla Hediff `ageTicks`, and spirit damage remains in its Hediff severity. Runtime recursion guards are not saved. Contract reverse indexes are rebuilt by query, never saved.

## Static Defs

`ServantIdentityDef` owns only character identity, PawnKind and references. Mutable gameplay values are separated:

```text
ServantIdentityDef
  -> ServantResourceProfileDef
  -> ServantAutonomyProfileDef
  -> List<NoblePhantasmDef>

TraitDef + MasterCircuitExtension
  -> MasterCircuitDef
```

Adding a servant should normally be XML-only. Adding a prana source, target policy or noble phantasm requires one implementation of its matching interface plus one Def reference, without editing lifecycle or damage code.

## Authoritative Call Paths

```text
GameComponent tick
  1. master natural regeneration
  2. materialized servant food conversion
  3. master surplus distribution
  4. upkeep and shortage
  5. healing above sustain threshold

Pawn.PreApplyDamage
  -> IServantDamagePolicy
  -> allow or absorb

Master death / map exit / spirit command / battle defeat
  -> IServantLifecycle
```

The prana pipeline is the only normal writer for prana Needs. Direct `Pawn.Kill` is not routed through battle defeat. A later combat slice may enter battle defeat only while a normal `PreApplyDamage` flow scope is active.

## First Development Slice

Implemented now: independent Mod shell, static profile Defs, identity and contract query, servant state component, prana Need pipeline, servant hunger-decay suppression, white-list damage gate, master death cleanup, timeless and disease-immune servant physiology, rest-need suppression, neutral guest test summon, persistent vanilla visitor Lord duties, debug master/prana controls, and the adjustable master-prana supply-threshold Gizmo.

Deferred: battle-defeat interception, spirit rendering and controls, LordJob target priority, command spells, and noble phantasm migration. Servant map exit is currently prevented by the autonomy LordJob boundary; explicit map-exit lifecycle handling remains deferred. These are deliberately deferred rather than stubbed inside unrelated modules.
