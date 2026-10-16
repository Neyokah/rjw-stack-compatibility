# RJW Stack Compatibility

**Version 0.1.0 · RimWorld 1.7 · Package ID `umrae.rjwstackcompatibility`**

A focused compatibility patch for an RJW mod stack. It protects explicitly constrained pawn-generation requests from Real Faction Guest substitutions, keeps faction spawn lists from being modified during selection, and indexes RJW's initial race-group lookup.

## What it changes

- **Constrained pawn generation:** Real Faction Guest keeps the requested pawn kind for non-adult-only generation requests and explicit dynamic kind selectors. Direct pawn-generation callers discovered in the supported RJW stack receive nested, exception-safe generation scopes. Ordinary faction guests continue to use Real Faction Guest's selection logic.
- **Faction-list integrity:** the Real Faction Guest hook works on a copy before adding trader and guard candidates. Repeated generation no longer expands the shared faction definitions.
- **Initial race-group lookup:** an index preserves external-kind, external-race, base-kind, and base-race priority and first-match ordering. RJW's own per-kind cache remains in use afterward.
- **Startup report:** writes `Config/RJWStackCompatibility-report.txt` in RimWorld's save-data folder, listing installed/skipped hooks and runtime race-group results.

There is no periodic world scan or new tick component. This does not add anatomy mappings, fix every animation or race, or certify every mod named in the load-order hints.

## Installation

1. Install the required DLC and dependency mods listed below.
2. Download this repository using **Code → Download ZIP**, then extract it. The compiled mod is included; building is optional.
3. Put the extracted folder in `RimWorld/Mods/`. You may rename it to `RJWStackCompatibility`. `About/About.xml` must sit directly inside that mod folder, rather than an extra nested folder.
4. Keep only one enabled copy of this mod. Enable it in RimWorld's Mods menu after its dependencies and the supported mods you use.
5. Restart RimWorld after installation or updating.

For Steam, **Manage → Browse local files** opens the game installation. On macOS the usual Mods location is `~/Library/Application Support/Steam/steamapps/common/RimWorld/Mods/`.

## Dependencies and supported integrations

| Mod | Status | Purpose |
| --- | --- | --- |
| Harmony | Required | Runtime hooks. |
| RimJobWorld | Required | Race-group definitions and existing lookup behavior. |
| Real Faction Guest / Real Faction Guest (Continued) | Optional, targeted integration | Generation guards and faction-list copying. This module is dormant when absent. |
| RJW add-ons listed in `Source/InstalledStack.cs` | Scope discovery and load-order hints | Their directly detected generation callers can be guarded. This is not blanket certification of their other features. |

Load after Harmony, RJW, Real Faction Guest when present, and your RJW add-ons. Keep your race-support packs installed. See [COMPATIBILITY.md](COMPATIBILITY.md) for the exact package list and hook limitations.

## Configuration and saves

No Mod Settings sliders are added. The hooks initialize automatically. Inspect the startup report for skipped or unavailable modules. Disable this mod and restart to remove its hooks; it adds no custom saved fields and does not edit upstream mod files.

Restart after changing definitions: the lookup index assumes stable definitions, like RJW's own cache. Upstream incompatibility warnings are intentionally not removed from other mods' metadata.

## Verification status

The original integration run recorded **5,289 checks** against installed game, Harmony, RFG, and RJW assemblies. Isolated lookup benchmarks are included in `Audit/test-results.txt`; they do not measure colony TPS or promise an overall speed multiplier. A full-colony validation remains necessary.

## Source, testing, and support

- [Build instructions](BUILDING.md)
- [Compatibility details](COMPATIBILITY.md)
- [Changelog](CHANGELOG.md)
- [Contribution guidelines](CONTRIBUTING.md)
- [Report a bug](https://github.com/Neyokah/rjw-stack-compatibility/issues/new?template=bug_report.yml)

These builds target **RimWorld 1.7's .NET 10 runtime**. They are not RimWorld 1.6 binaries. Older dependency assemblies used by the installed 1.7 compatibility runtime do not establish that the mod itself supports 1.6. The original automated test records are included where available; full graphical gameplay with a large mod collection is not claimed to have been verified.

## Credits and license

Original code in this project is available under the [MIT license](LICENSE). Third-party dependencies retain their own authorship and terms and are not bundled. See [third-party notices](THIRD_PARTY_NOTICES.md) for any exceptions or adapted material.
