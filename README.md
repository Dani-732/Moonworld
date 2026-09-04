# MoonWorld

`MoonWorld` is a new RimWorld 1.6 Mod root. It does not replace or load code from `HolyGrailWarTest`.

The first included module is the Holy Grail War MVP foundation:

* servant identity, contract and lifecycle state;
* master and servant prana needs with one low-frequency pipeline;
* servant-only damage permission with environmental damage retained;
* temporary guest initialization for autonomous servants;
* developer actions to grant a master circuit and summon a neutral test servant.

Build with:

```powershell
./Source/build.ps1
```

The implementation boundary is documented in `docs/HolyGrailWar_Module_Boundaries.md`.

Use `docs/MVP_Smoke_Test.md` for the first in-game verification pass. The current output remains in this workspace and has not changed the game's enabled-mod configuration.

Run `./Source/build.ps1 -Deploy` to compile and synchronize this Mod to `G:\steam\steamapps\common\RimWorld\Mods\MoonWorld`. This copies only the MoonWorld folder and does not alter the enabled-mod configuration.
