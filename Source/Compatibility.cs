using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RJWStackCompatibility;

public sealed class CompatibilityMod : Mod
{
    public CompatibilityMod(ModContentPack content) : base(content)
    {
        try { Installer.Install(); }
        catch (Exception e) { Log.Error("[RJW Stack Compatibility] Hook installation failed. " + e); }
    }
}

[StaticConstructorOnStartup]
public static class Startup
{
    static Startup()
    {
        try { Installer.Finish(); }
        catch (Exception e) { Log.Error("[RJW Stack Compatibility] Initialization failed; see exception. " + e); }
    }
}

public static class Installer
{
    public const string HarmonyId = "umrae.rjwstackcompatibility";
    public static readonly Harmony Harmony = new(HarmonyId);
    public static readonly List<string> Status = new();
    public static readonly HashSet<Assembly> StackAssemblies = new();
    static bool installed;
    static bool finished;
    static bool rfgInstalled;

    public static void Install()
    {
        if (installed) return;
        installed = true;
        foreach (var pack in LoadedModManager.RunningModsListForReading)
            if (InstalledStack.PackageIds.Contains(pack.PackageId.ToLowerInvariant()))
                foreach (var asm in pack.assemblies.loadedAssemblies)
                    if (asm.GetName().Name is not ("0Harmony" or "0MultiplayerAPI" or "0PrepatcherAPI"))
                        StackAssemblies.Add(asm);

        var rfg = AccessTools.TypeByName("raceQuestPawn.PawnGenerator_GeneratePawn");
        if (rfg != null)
        {
            try
            {
            var target = AccessTools.Method(rfg, "Prefix", new[] { typeof(PawnGenerationRequest).MakeByRefType() });
            if (target == null) throw new MissingMethodException("Real Faction Guest Prefix signature changed.");
            // Only intercept RFG's own prefix. Never skip the game's generation method or other mods' patches.
            var original = PatchProcessor.GetOriginalInstructions(target).ToList();
            RfgPatch.ValidateShape(original);
            Harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(RfgPatch), nameof(RfgPatch.Guard)),
                transpiler: new HarmonyMethod(typeof(RfgPatch), nameof(RfgPatch.CopyFactionLists)));
            rfgInstalled = true;
            Status.Add("Real Faction Guest: constrained-request guard and faction-list integrity patch installed.");
            }
            catch (Exception e) { Status.Add("WARNING: Real Faction Guest module unavailable: " + e.Message); }
        }
        else Status.Add("Real Faction Guest not active. Its compatibility module is dormant.");

        var helper = AccessTools.TypeByName("rjw.RaceGroupDef_Helper");
        var groupType = AccessTools.TypeByName("rjw.RaceGroupDef");
        if (helper != null && groupType != null)
        {
            try
            {
                RaceLookup.SetType(groupType);
                var method = AccessTools.Method(helper, "GetRaceGroupDefInternal", new[] { typeof(PawnKindDef) });
                if (method == null || method.ReturnType != groupType)
                    throw new MissingMethodException("Race lookup signature changed.");
                if (HarmonyLib.Harmony.GetPatchInfo(method)?.Prefixes.Count > 0 ||
                    HarmonyLib.Harmony.GetPatchInfo(method)?.Transpilers.Count > 0)
                {
                    Status.Add("Race lookup optimization skipped: another mod already changes this method.");
                }
                else
                {
                    RaceLookup.Replacement = AccessTools.Method(typeof(RaceLookup), nameof(RaceLookup.Get)).MakeGenericMethod(groupType);
                    Harmony.Patch(method, transpiler: new HarmonyMethod(typeof(RaceLookup), nameof(RaceLookup.ReplaceLookup)));
                    Status.Add("Race lookup indexing installed before startup caches; original match priority preserved.");
                }
            }
            catch (Exception e) { Status.Add("Race lookup optimization unavailable: " + e.Message); }
        }
        else Status.Add("RJW assembly not active or failed to load; race lookup module unavailable.");

    }

    public static void Finish()
    {
        Install();
        if (finished) return;
        finished = true;
        // Patching a caller may run its type initializer. Wait until definitions are loaded.
        if (rfgInstalled) InstallGenerationScopes();
        RaceLookup.EnsureIndex();
        var report = RuntimeAudit.Build();
        try { File.WriteAllText(Path.Combine(GenFilePaths.ConfigFolderPath, "RJWStackCompatibility-report.txt"), report); }
        catch (Exception e) { Log.Warning("[RJW Stack Compatibility] Could not save report: " + e.Message); }
        foreach (var status in Status) Log.Message("[RJW Stack Compatibility] " + status);
    }

    static void InstallGenerationScopes()
    {
        int patched = 0, failed = 0;
        foreach (var assembly in StackAssemblies)
        foreach (var type in SafeTypes(assembly))
        foreach (var method in SafeMethods(type))
        {
            try
            {
                if (method.ContainsGenericParameters || method.IsAbstract || method.GetMethodBody() == null) continue;
                var instructions = PatchProcessor.GetOriginalInstructions(method);
                if (!instructions.Any(i => i.operand is MethodInfo called &&
                    called.DeclaringType == typeof(PawnGenerator) && called.Name == "GeneratePawn")) continue;
                Harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(GenerationScope), nameof(GenerationScope.Enter)),
                    finalizer: new HarmonyMethod(typeof(GenerationScope), nameof(GenerationScope.Restore)));
                patched++;
            }
            catch { failed++; }
        }
        Status.Add($"Generation scopes: {patched} direct RJW-stack callers protected; {failed} methods could not be inspected/patched.");
        if (patched == 0) Status.Add("WARNING: no RJW generation scopes installed; only explicitly constrained generation is protected.");
    }

    public static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
    }
    static IEnumerable<MethodInfo> SafeMethods(Type type)
    {
        try { return type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
        catch (Exception e) { Status.Add("Could not inspect generation methods in " + type.FullName + ": " + e.GetType().Name); return Array.Empty<MethodInfo>(); }
    }
}

public static class GenerationScope
{
    [ThreadStatic] public static int Depth;
    public struct State { public int Previous; public bool Entered; }
    public static void Enter(out State __state) { __state = new State { Previous = Depth, Entered = true }; Depth++; }
    // A finalizer runs on success and exception; a void finalizer preserves exceptions.
    // A different prefix may skip Enter; the default state must not clear a parent scope.
    public static void Restore(State __state) { if (__state.Entered) Depth = __state.Previous; }
}

public static class RfgPatch
{
    static readonly FieldInfo Options = AccessTools.Field(typeof(PawnGroupMaker), "options");
    static readonly ConstructorInfo Copy = typeof(List<PawnGenOption>).GetConstructor(new[] { typeof(IEnumerable<PawnGenOption>) });

    public static bool Guard(ref PawnGenerationRequest __0)
    {
        if (GenerationScope.Depth > 0) return false;
        // A birth/child-only request must keep its explicitly chosen kind and inherited constraints.
        if ((__0.AllowedDevelopmentalStages & DevelopmentalStage.Adult) == 0) return false;
        // Dynamic kind selectors are explicit generation contracts, not generic faction guests.
        if (__0.PawnKindDefGetter != null) return false;
        return true;
    }

    public static void ValidateShape(IEnumerable<CodeInstruction> instructions)
    {
        int reads = instructions.Count(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, Options));
        if (reads != 2) throw new InvalidOperationException($"RFG faction-list pattern changed ({reads} reads; expected 2). Refusing partial patch.");
    }

    public static IEnumerable<CodeInstruction> CopyFactionLists(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, Options))
                yield return new CodeInstruction(OpCodes.Newobj, Copy);
        }
    }
}

// A compact index replaces repeated full-definition scans on first use of each pawn kind.
// RJW's existing per-kind cache remains in charge of subsequent calls.
public sealed class DefinitionIndex<T> where T : class
{
    readonly Dictionary<string, T> externalKinds = new(StringComparer.Ordinal);
    readonly Dictionary<string, T> externalRaces = new(StringComparer.Ordinal);
    readonly Dictionary<string, T> baseKinds = new(StringComparer.Ordinal);
    readonly Dictionary<string, T> baseRaces = new(StringComparer.Ordinal);
    public void Add(T group, bool own, IEnumerable<string> kinds, IEnumerable<string> races)
    {
        AddFirst(own ? baseKinds : externalKinds, kinds, group);
        AddFirst(own ? baseRaces : externalRaces, races, group);
    }
    static void AddFirst(Dictionary<string, T> index, IEnumerable<string> names, T value)
    {
        if (names == null) return;
        foreach (var name in names) if (name != null) index.TryAdd(name, value);
    }
    public T Get(string kind, string race)
    {
        if (kind != null && externalKinds.TryGetValue(kind, out var a)) return a;
        if (race != null && externalRaces.TryGetValue(race, out var b)) return b;
        if (kind != null && baseKinds.TryGetValue(kind, out var c)) return c;
        return race != null && baseRaces.TryGetValue(race, out var d) ? d : null;
    }
}

public static class RaceLookup
{
    static DefinitionIndex<Def> index;
    public static MethodInfo Replacement;
    public static int GroupCount;
    public static List<Def> Groups = new();
    static Type groupType;
    public static void SetType(Type type)
    {
        if (type.GetField("raceNames")?.FieldType != typeof(List<string>) ||
            type.GetField("pawnKindNames")?.FieldType != typeof(List<string>))
            throw new InvalidOperationException("Unsupported race group schema.");
        groupType = type;
    }
    public static void EnsureIndex()
    {
        if (index == null && groupType != null) Configure(groupType);
    }

    public static void Configure(Type groupType)
    {
        var names = groupType.GetField("raceNames");
        var kinds = groupType.GetField("pawnKindNames");
        if (names?.FieldType != typeof(List<string>) || kinds?.FieldType != typeof(List<string>))
            throw new InvalidOperationException("Unsupported race group schema.");
        var groups = (IEnumerable)typeof(DefDatabase<>).MakeGenericType(groupType).GetProperty("AllDefsListForReading").GetValue(null);
        var rjw = LoadedModManager.RunningModsListForReading.Single(p => p.PackageId.Equals("rim.job.world", StringComparison.OrdinalIgnoreCase));
        var originals = new HashSet<Def>(rjw.AllDefs);
        var next = new DefinitionIndex<Def>();
        Groups = groups.Cast<Def>().ToList();
        foreach (var group in Groups)
            next.Add(group, originals.Contains(group), (IEnumerable<string>)kinds.GetValue(group), (IEnumerable<string>)names.GetValue(group));
        GroupCount = Groups.Count;
        index = next;
    }

    public static T Get<T>(PawnKindDef kind) where T : Def
    {
        EnsureIndex();
        return (T)index.Get(kind?.defName, kind?.race?.defName);
    }
    public static IEnumerable<CodeInstruction> ReplaceLookup(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        yield return new CodeInstruction(OpCodes.Call, Replacement);
        yield return new CodeInstruction(OpCodes.Ret);
    }
}

public static class RuntimeAudit
{
    public static string Build()
    {
        var lines = new List<string> { "RJW Stack Compatibility 0.1.0", "Generated: " + DateTimeOffset.Now.ToString("O"),
            "Scope: generation guards, faction list integrity, race-definition lookup indexing.",
            "This report does not certify all gameplay, animations, equipment, or add-on features.", "", "Modules:" };
        lines.AddRange(Installer.Status);
        lines.Add("\nLoaded stack packages:");
        foreach (var pack in LoadedModManager.RunningModsListForReading.Where(p => InstalledStack.PackageIds.Contains(p.PackageId.ToLowerInvariant())))
            lines.Add(pack.PackageId + " | " + pack.Name + " | " + pack.assemblies.loadedAssemblies.Count + " assemblies");
        lines.Add("\nHumanlike races and loaded race-group matches (a missing explicit match can use RJW's fallback):");
        var type = AccessTools.TypeByName("rjw.RaceGroupDef");
        if (type != null && RaceLookup.GroupCount > 0)
        {
            var lookup = AccessTools.Method(typeof(RaceLookup), nameof(RaceLookup.Get)).MakeGenericMethod(type);
            foreach (var race in DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.race?.Humanlike == true))
            {
                var sample = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race == race);
                var match = sample == null ? null : (Def)lookup.Invoke(null, new object[] { sample });
                lines.Add(race.defName + " | " + (race.modContentPack?.PackageId ?? "unknown") + " | " + (match?.defName ?? "fallback/unverified"));
            }
        }
        else lines.Add("Race coverage unavailable: lookup index could not be built.");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
