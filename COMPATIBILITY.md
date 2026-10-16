# Compatibility

## Supported target

RimWorld **1.7**, targeting its .NET 10 runtime. The source/binaries were prepared against a local macOS compatibility-runtime installation. Other operating systems, future game builds, and every possible mod combination are not separately certified. Obtain the required dependencies independently; they are not bundled.

## Integration scope

Real Faction Guest hooks target its known generation-prefix signature and verify the expected instruction pattern before patching. If that shape changes, the module reports a warning instead of applying a partial rewrite. Race lookup optimization is skipped if another prefix/transpiler already changes its target. The stack-ID list scopes direct generation-call discovery; it is not a supported-features matrix for every listed package. Keep ordinary race/anatomy support packs. No global dependency warnings are suppressed.

## Load-order hints from the shipped metadata

The following are placement hints, **not a list of required dependencies or a blanket compatibility guarantee**. Required dependencies are separately listed in README.md and About/About.xml.

- `brrainz.harmony`
- `mlie.realfactionguest`
- `com.yayo.racequestpawn`
- `akiya82.spawn.rjw.genes`
- `akt.lewdclothes`
- `alpenglow.canines.animations`
- `asmr.rjw.racesupport`
- `c0ffee.rimworld.animations`
- `c0ffee.rjw.events`
- `c0ffee.rjw.ideologyaddons`
- `calamabanana.rjw.brothelcolony`
- `cc.anims`
- `ed86.rjwinsects`
- `eltoro.anims`
- `eltoro.baddon`
- `eltoro.maddon`
- `eltoro.rjw.menstruation.fluids`
- `eltoro.rjw.menstruation.resources`
- `euclidean.rjw.patches`
- `euclidean.s16.core`
- `hawkeye32.hawks.anims`
- `jikulopo.rjwcocoon`
- `kcd.piercings`
- `nalzurin.knotphile`
- `otyoty.sizedapparel`
- `otyoty.sizedapparel.extended`
- `qa.lewdclothes`
- `readyforjeff.rjwoddyspecific`
- `readyforjeff.rjwpeculiarinstitution`
- `rim.job.world`
- `rimworld.ekss.rjwex`
- `rjw.fb`
- `rjw.fh`
- `rjw.lifeanddeath.animations`
- `rjw.menstruation`
- `rjw.quirks`
- `rjw.sexpanded.core`
- `rjw.sexpanded.corruption`
- `rjw.sexperience`
- `rjw.sexperience.ideology`
- `rjw.unleashed.framework`
- `rjw.unleashed.gastronomy`
- `robtrg.milkdrugs`
- `robtrg.rjwaditionalracepatches`
- `robtrg.rjwaditionalxenopatchs`
- `shauaputa.lewdtrader`
- `teacher.uap`
- `tehdav.lewdclothesretextured`
- `tory187.rjw.gasses`
- `tory187.rjwanimaddons.sextra2`
- `vegapnk.cumpilation`
- `vegapnk.rjw.genes`
- `videnoir.chillrjw`
- `worldie.luxandralust`
- `worldie.unlimitedcumworks`
- `zoc2k.nicehealthtab.rjwgenitalia`

## Reporting an integration problem

Report the exact game and dependency versions, whether the problem reproduces with only required dependencies, and a minimal reproduction. Preserve unrelated load-order rules from upstream mods. See the included verification records for what has actually been tested.
