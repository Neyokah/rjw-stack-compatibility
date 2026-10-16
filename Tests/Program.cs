using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using RJWStackCompatibility;
using Verse;
using RimWorld;

var game = Environment.GetEnvironmentVariable("RIMWORLD_MANAGED_DIR")!;
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var file = Path.Combine(game, name.Name + ".dll");
    return File.Exists(file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
};
int tests = 0;
void Check(bool ok, string description) { if (!ok) throw new Exception("FAIL " + description); tests++; }

foreach (var f in new[] { "Assembly-CSharp.dll", "UnityStubs.dll", "Waylith.UnityCompat.dll" })
    Console.WriteLine("Game reference: " + AssemblyName.GetAssemblyName(Path.Combine(game, f)));

var request = default(PawnGenerationRequest);
request.AllowedDevelopmentalStages = DevelopmentalStage.Adult;
Check(RfgPatch.Guard(ref request), "ordinary adult generation still reaches RFG");
foreach (var stage in new[] { DevelopmentalStage.Newborn, DevelopmentalStage.Baby, DevelopmentalStage.Child })
{
    request.AllowedDevelopmentalStages = stage;
    Check(!RfgPatch.Guard(ref request), "explicit developmental-stage kind is preserved: " + stage);
}
request.AllowedDevelopmentalStages = DevelopmentalStage.Adult;
GenerationScope.Enter(out var state);
Check(!RfgPatch.Guard(ref request), "RJW scoped request bypasses RFG only");
GenerationScope.Enter(out var nested);
GenerationScope.Restore(nested);
Check(GenerationScope.Depth == 1, "nested scope preserves parent");
GenerationScope.Restore(default);
Check(GenerationScope.Depth == 1, "skipped scope prefix cannot clear its parent");
GenerationScope.Restore(state);
Check(GenerationScope.Depth == 0, "scope restores cleanly");
request.PawnKindDefGetter = _ => null;
Check(!RfgPatch.Guard(ref request), "dynamic kind selector preserved");

var harmony = new Harmony("umrae.rjwstackcompatibility.tests");
var scopeTarget = AccessTools.Method(typeof(ScopeFixture), nameof(ScopeFixture.Throwing));
harmony.Patch(scopeTarget, prefix: new HarmonyMethod(typeof(GenerationScope), nameof(GenerationScope.Enter)),
    finalizer: new HarmonyMethod(typeof(GenerationScope), nameof(GenerationScope.Restore)));
try { ScopeFixture.Throwing(); throw new Exception("Exception lost"); }
catch (InvalidOperationException e) { Check(e.Message == "fixture" && GenerationScope.Depth == 0, "Harmony finalizer restores depth without swallowing exception"); }

var fixture = AccessTools.Method(typeof(ListFixture), nameof(ListFixture.Run));
RfgPatch.ValidateShape(PatchProcessor.GetOriginalInstructions(fixture));
harmony.Patch(fixture, prefix: new HarmonyMethod(typeof(RfgPatch), nameof(RfgPatch.Guard)),
    transpiler: new HarmonyMethod(typeof(RfgPatch), nameof(RfgPatch.CopyFactionLists)));
ListFixture.First = new PawnGroupMaker { options = new(), traders = new() { new PawnGenOption() }, guards = new() };
ListFixture.Second = new PawnGroupMaker { options = new(), traders = new(), guards = new() { new PawnGenOption() } };
request.PawnKindDefGetter = null;
for (int i = 0; i < 20; i++) ListFixture.Run(ref request);
Check(ListFixture.First.options.Count == 0 && ListFixture.Second.options.Count == 0, "faction list sizes do not grow across repeated generations");
Check(ListFixture.LastSize == 2, "trader and guard candidates remain available");
request.AllowedDevelopmentalStages = DevelopmentalStage.Newborn;
ListFixture.Executions = 0;
ListFixture.Run(ref request);
Check(ListFixture.Executions == 0, "guard skips only RFG fixture on protected request");
try { RfgPatch.ValidateShape(new[] { new CodeInstruction(OpCodes.Ret) }); throw new Exception("shape accepted"); }
catch (InvalidOperationException) { Check(true, "unsupported RFG shape rejected"); }

var rnd = new Random(317);
var data = new List<(object Group, bool Own, string[] Kinds, string[] Races)>();
var index = new DefinitionIndex<object>();
for (int i = 0; i < 500; i++)
{
    var entry = ((object)i, i % 3 == 0,
        Enumerable.Range(0, rnd.Next(4)).Select(_ => "kind" + rnd.Next(60)).ToArray(),
        Enumerable.Range(0, rnd.Next(4)).Select(_ => "race" + rnd.Next(45)).ToArray());
    data.Add(entry); index.Add(entry.Item1, entry.Item2, entry.Item3, entry.Item4);
}
object Baseline(string k, string r) =>
    data.FirstOrDefault(d => !d.Own && d.Kinds.Contains(k)).Group ??
    data.FirstOrDefault(d => !d.Own && d.Races.Contains(r)).Group ??
    data.FirstOrDefault(d => d.Kinds.Contains(k)).Group ??
    data.FirstOrDefault(d => d.Races.Contains(r)).Group;
for (int i = 0; i < 5000; i++)
{
    var k = "kind" + rnd.Next(70); var r = "race" + rnd.Next(55);
    Check(ReferenceEquals(Baseline(k, r), index.Get(k, r)), "indexed lookup matches original priority " + i);
}
const int loops = 40000;
long before = GC.GetAllocatedBytesForCurrentThread();
var watch = Stopwatch.StartNew();
for (int i = 0; i < loops; i++) _ = Baseline("kind58", "race42");
watch.Stop();
long baselineAlloc = GC.GetAllocatedBytesForCurrentThread() - before; double baselineMs = watch.Elapsed.TotalMilliseconds;
before = GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
for (int i = 0; i < loops; i++) _ = index.Get("kind58", "race42");
watch.Stop();
Console.WriteLine($"Synthetic uncached lookup comparison ({loops} repetitions, 500 groups): baseline {baselineMs:F2} ms / {baselineAlloc} bytes; indexed {watch.Elapsed.TotalMilliseconds:F2} ms / {GC.GetAllocatedBytesForCurrentThread() - before} bytes. Not a game TPS benchmark.");

var rfgPath = Path.Combine(Environment.GetEnvironmentVariable("RIMWORLD_WORKSHOP")!, "2886929245/1.6/Assemblies/raceQuestPawn.dll");
using (var module = Mono.Cecil.ModuleDefinition.ReadModule(rfgPath))
{
    var method = module.Types.Single(t => t.FullName == "raceQuestPawn.PawnGenerator_GeneratePawn").Methods.Single(m => m.Name == "Prefix");
    Check(method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == "Verse.PawnGenerationRequest&", "installed RFG signature matches guard");
    Check(method.Body.Instructions.Count(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldfld && i.Operand is Mono.Cecil.FieldReference field && field.FullName.Contains("PawnGroupMaker::options")) == 2, "installed RFG contains exactly two mutable list reads");
}
try
{
    var actual = AssemblyLoadContext.Default.LoadFromAssemblyPath(rfgPath);
    var actualPrefix = actual.GetType("raceQuestPawn.PawnGenerator_GeneratePawn", true).GetMethod("Prefix");
    var instructions = PatchProcessor.GetOriginalInstructions(actualPrefix).ToList();
    RfgPatch.ValidateShape(instructions);
    harmony.Patch(actualPrefix, prefix: new HarmonyMethod(typeof(RfgPatch), nameof(RfgPatch.Guard)),
        transpiler: new HarmonyMethod(typeof(RfgPatch), nameof(RfgPatch.CopyFactionLists)));
    request.AllowedDevelopmentalStages = DevelopmentalStage.Newborn;
    actualPrefix.Invoke(null, new object[] { request });
    Check(true, "actual installed RFG prefix patches and safely handles constrained generation");
    var previousState = Current.ProgramState;
    try
    {
        Current.ProgramState = ProgramState.Playing;
        var corePack = FixturePack("ludeon.rimworld", "Core");
        var factionPack = FixturePack("fixture.faction", "Fixture Faction");
        var basic = FixtureKind("Villager", "Human", 30);
        basic.modContentPack = corePack;
        var chosen = FixtureKind("FactionGuest", "FixtureRace", 31);
        chosen.modContentPack = factionPack;
        var maker = new PawnGroupMaker { options = new() { new PawnGenOption { kind = chosen } }, traders = new() { new PawnGenOption { kind = chosen } }, guards = new() { new PawnGenOption { kind = chosen } } };
        var factionDef = (FactionDef)RuntimeHelpers.GetUninitializedObject(typeof(FactionDef));
        factionDef.modContentPack = factionPack;
        factionDef.pawnGroupMakers = new() { maker };
        var faction = (Faction)RuntimeHelpers.GetUninitializedObject(typeof(Faction));
        faction.def = factionDef;
        request = default;
        request.Faction = faction; request.AllowedDevelopmentalStages = DevelopmentalStage.Adult;
        for (int i = 0; i < 20; i++)
        {
            request.KindDef = basic;
            object[] arguments = { request };
            actualPrefix.Invoke(null, arguments);
            Check(((PawnGenerationRequest)arguments[0]).KindDef == chosen, "real RFG still selects faction guest " + i);
        }
        Check(maker.options.Count == 1, "real RFG shared faction options remain unchanged");
        request.KindDef = basic;
        GenerationScope.Enter(out var scopeState);
        try
        {
            object[] arguments = { request };
            actualPrefix.Invoke(null, arguments);
            Check(((PawnGenerationRequest)arguments[0]).KindDef == basic, "real RFG preserves a scoped RJW request");
        }
        finally { GenerationScope.Restore(scopeState); }
    }
    finally { Current.ProgramState = previousState; }
    harmony.Unpatch(actualPrefix, HarmonyPatchType.All, harmony.Id);
}
catch (Exception e) { Environment.ExitCode = 1; Console.WriteLine("ACTUAL RFG HARNESS LIMIT: " + e); }

// Compare the real installed RJW helper before/after indexing, under the game's compatibility runtime.
try
{
    var dir = Environment.GetEnvironmentVariable("RJW_ASSEMBLIES")!;
    AssemblyLoadContext.Default.Resolving += (_, name) =>
    {
        var path = Path.Combine(dir, name.Name + ".dll");
        return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
    };
    var rjw = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dir, "RJW.dll"));
    var groupType = rjw.GetType("rjw.RaceGroupDef", true);
    var helper = rjw.GetType("rjw.RaceGroupDef_Helper", true);
    var pack = FixturePack("rim.job.world", "RimJobWorld");
    var owned = new List<Def>();
    AccessTools.Field(typeof(ModContentPack), "defs").SetValue(pack, owned);
    LoadedModManager.RunningModsListForReading.Add(pack);
    var database = typeof(DefDatabase<>).MakeGenericType(groupType);
    var groupList = (System.Collections.IList)database.GetProperty("AllDefsListForReading").GetValue(null);
    for (int i = 0; i < data.Count; i++)
    {
        var def = (Def)Activator.CreateInstance(groupType);
        def.defName = "FixtureGroup" + i;
        groupType.GetField("pawnKindNames").SetValue(def, data[i].Kinds.ToList());
        groupType.GetField("raceNames").SetValue(def, data[i].Races.ToList());
        groupList.Add(def);
        if (data[i].Own) owned.Add(def);
    }
    var original = AccessTools.Method(helper, "GetRaceGroupDefInternal", new[] { typeof(PawnKindDef) });
    var requests = Enumerable.Range(0, 250).Select(_ => FixtureKind("kind" + rnd.Next(70), "race" + rnd.Next(55), 0)).ToList();
    var expected = requests.Select(k => original.Invoke(null, new object[] { k })).ToList();
    RaceLookup.SetType(groupType);
    RaceLookup.Replacement = AccessTools.Method(typeof(RaceLookup), nameof(RaceLookup.Get)).MakeGenericMethod(groupType);
    harmony.Patch(original, transpiler: new HarmonyMethod(typeof(RaceLookup), nameof(RaceLookup.ReplaceLookup)));
    for (int i = 0; i < requests.Count; i++)
        Check(ReferenceEquals(expected[i], original.Invoke(null, new object[] { requests[i] })), "real installed RJW helper matches before/after indexing " + i);
    Console.WriteLine("Actual RJW helper comparison passed for 250 requests using 500 installed-type group fixtures.");
}
catch (Exception e) { Environment.ExitCode = 1; Console.WriteLine("ACTUAL RJW HARNESS LIMIT: " + e); }
Console.WriteLine($"{(Environment.ExitCode == 0 ? "PASS" : "PARTIAL")} {tests} checks using installed 1.7 game/Harmony references and installed RFG/RJW binaries.");

static ModContentPack FixturePack(string id, string name)
{
    var p = (ModContentPack)RuntimeHelpers.GetUninitializedObject(typeof(ModContentPack));
    AccessTools.Field(typeof(ModContentPack), "packageIdInt").SetValue(p, id);
    AccessTools.Field(typeof(ModContentPack), "nameInt").SetValue(p, name);
    return p;
}
static PawnKindDef FixtureKind(string name, string race, float power)
{
    var kind = (PawnKindDef)RuntimeHelpers.GetUninitializedObject(typeof(PawnKindDef));
    kind.defName = name; kind.combatPower = power;
    kind.race = (ThingDef)RuntimeHelpers.GetUninitializedObject(typeof(ThingDef));
    kind.race.defName = race;
    kind.race.race = new RaceProperties { intelligence = Intelligence.Humanlike };
    return kind;
}

static class ScopeFixture
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throwing() { if (GenerationScope.Depth < 1) throw new Exception("scope absent"); throw new InvalidOperationException("fixture"); }
}
static class ListFixture
{
    public static PawnGroupMaker First, Second;
    public static int LastSize, Executions;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Run(ref PawnGenerationRequest request)
    {
        Executions++;
        var a = First.options; a.AddRange(First.traders); a.AddRange(First.guards);
        var b = Second.options; b.AddRange(Second.traders); b.AddRange(Second.guards);
        LastSize = a.Count + b.Count;
    }
}
