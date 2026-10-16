# Building and testing

The compiled mod is already in `1.7/Assemblies/`. Players do not need the SDK.

## Build prerequisites

- .NET **10** SDK.
- Your own RimWorld 1.7 assemblies and installed dependency mods.
- For headless fixture projects that reference it, your own Mono.Cecil DLL.

`Directory.Build.props` reads environment variables; you can instead pass matching MSBuild properties with `-p:Name=value`. Paths with spaces must be quoted.

| Environment variable | MSBuild property | Points to |
| --- | --- | --- |
| `RIMWORLD_ROOT` | `GameRoot` | Installation directory containing Data and Mods. |
| `RIMWORLD_MANAGED_DIR` | `GameDir` / `GameManagedDir` | Directory containing Assembly-CSharp.dll and UnityStubs.dll. |
| `RIMWORLD_WORKSHOP` | `Workshop` | Steam Workshop content/294100 directory, when required. |
| `RJW_ASSEMBLIES` | `RjwDir` | Directory containing the installed RJW.dll. |
| `HARMONY_DLL` | `HarmonyPath` | The installed 1.7 0Harmony.dll. |
| `MONO_CECIL_DLL` | `CecilPath` | Mono.Cecil.dll, for the fixture projects that use it. |

Example after setting the required paths:

```sh
dotnet build Source/RJWStackCompatibility.csproj -c Release
```

The project writes its own DLL to `1.7/Assemblies/`; game and dependency references are marked non-copying for the production build. Some dependencies retain a `1.6/Assemblies` folder in the installed 1.7 runtime; use the actual assembly version/layout you have rather than renaming folders.

Fluids and Solids additionally references Dubs Bad Hygiene, its Multiplayer API, S16, and Lactation Expansion from the corresponding installed GameRoot/Workshop paths shown in its project. The test suite also references PipeSystem and RainsS16Addons. GameDir can be overridden separately for another platform's assembly layout.

## Test sources

Tests/Tests.csproj requires an installed Real Faction Guest assembly at its documented Workshop layout, an installed RJW assembly, and Mono.Cecil via MONO_CECIL_DLL. The historical result is in Audit/test-results.txt.

The RJW suites were originally run under the game's bundled compatibility runtime. When using that runtime, copy the test output to a temporary directory and use the installed RimWorld runtime configuration/dependency mapping, as illustrated by Fluids and Solids' test runner. Stock .NET may not provide the game's legacy assembly redirects. Do not copy proprietary game/dependency assemblies into this repository.

The packaged verification files describe historical integration results, including fixture substitutions and remaining interactive checks. Preparing these repositories does not turn those fixtures into a full live-colony test. If dependencies or gameplay code change, rebuild, rerun the relevant suite and perform a disposable-colony smoke test before publishing a release.
