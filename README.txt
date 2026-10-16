RJW Stack Compatibility 0.1.0 — RimWorld 1.7

What this patch does
1. Patches Real Faction Guest's own generation hook, not PawnGenerator itself.
   Requests restricted to non-adult developmental stages and explicit dynamic
   kind selectors retain their original pawn kind. Direct generation callers in
   the detected RJW stack receive nested, exception-safe generation scopes;
   RFG substitutions are skipped inside those scopes. Ordinary faction guests
   continue through RFG's existing selection logic.
2. Copies the faction options list before RFG appends trader/guard options.
   The original repeatedly changed shared definitions during pawn generation.
3. Indexes RJW's first-time race-group lookup, preserving external-kind,
   external-race, base-kind, base-race priority and first-match order. RJW's own
   subsequent per-kind cache stays intact. Hooks install before static startup
   caches; generation scopes install after definitions finish loading.

Requirements and placement
- RimWorld 1.7; Harmony; RimJobWorld.
- Real Faction Guest (Continued) is optional. Its module remains dormant if absent.
- Load after RJW, RFG, and your RJW add-ons. The About file includes ordering
  hints for the 53 detected stack packages. Keep existing race support packs.
- This build targets the installed 1.7 .NET runtime. It is not a 1.6 binary.

Diagnostics and limits
After startup, Config/RJWStackCompatibility-report.txt records installed modules,
protected caller counts, and loaded humanlike race-group matches. A skipped hook
or missing assembly is recorded, not silently treated as compatibility.

The file audit examined 1,547 active installed packages, including 53 direct or
indirect RJW stack packages, 15,729 definition XML files, 675 candidate race-group
definitions, and 74 candidate xenotypes. Conditional XML and inheritance require
runtime confirmation. No malformed definition XML or missing direct required
dependency was found within that stack snapshot. Possible overlapping definitions
are listed for review; some are deliberate overrides or conditionally excluded.
The snapshot does not establish that all these definitions load together.

This patch does NOT certify all races, animations, body models, equipment,
frameworks, or add-on behaviors. It does not add speculative body/race mappings,
remove dependencies, hide errors, alter activity/job intervals, or change gameplay
settings. It adds no ticker, periodic world scan, pawn component, or saved field.
The race index assumes definitions remain stable after startup, as RJW's existing
per-kind cache already does. Definition hot reload requires restarting the game.

Testing
See Audit/test-results.txt. Automated tests use the installed 1.7 game's own
compatibility runtime, Harmony 1.7 library, and installed RFG/RJW assemblies.
Lookup microbenchmarks measure isolated uncached lookups, not game TPS or total
stack performance. A colony-level performance gain has not been measured.

The upstream incompatibility declaration in RJW/About/About.xml is left intact;
RimSort may continue to display it even with this patch installed. This patch
works at runtime. Its presence alone does not establish that the warning is safe
to remove in upstream metadata.

Removal and source
Disable this mod and restart to remove its runtime hooks. No original mod file is
modified and no new saved-game data is added. Source is included. Rebuild with
.NET 10 and Source/RJWStackCompatibility.csproj; override GameDir and HarmonyPath
when building on another machine.
