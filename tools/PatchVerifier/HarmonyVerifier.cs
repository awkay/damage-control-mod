using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace PatchVerifier;

/// <summary>
/// Verifies the compiled mod's Harmony patches against the game assemblies without running
/// anything: everything is inspected through MetadataLoadContext.
///
/// Resolution mirrors Harmony 2.2.2 (PatchTools.GetOriginalMethod): targets are looked up with
/// AccessTools.Declared* — i.e. only members DECLARED on the named type, all visibilities; a
/// name-only lookup that hits several overloads throws AmbiguousMatchException.
///
/// The mod's "patch registry" is its attribute-based patch classes. Manual Harmony.Patch calls,
/// TargetMethod(s) and reverse patches cannot be verified offline and are rejected.
/// </summary>
public sealed class HarmonyVerifier : IDisposable
{
    private const int InlineRiskIlBytes = 20; // Mono's default inlining limit (MONO_INLINELIMIT)

    private static readonly string[] PatchKinds = { "Prefix", "Postfix", "Transpiler", "Finalizer" };
    private static readonly HashSet<string> AuxMethods = new() { "Prepare", "Cleanup", "TargetMethod", "TargetMethods" };

    private readonly MetadataLoadContext mlc;
    private readonly Assembly mod;
    private readonly Report report;
    private readonly string dllPath;
    private string[] methodTypeNames = { "Normal", "Getter", "Setter", "Constructor", "StaticConstructor", "Enumerator" };

    public HarmonyVerifier(GameEnvironment env, string modDll, Report report)
    {
        this.report = report;
        dllPath = Path.GetFullPath(modDll);
        var paths = Directory.GetFiles(env.ManagedDir, "*.dll").Append(dllPath);
        mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), "mscorlib");
        mod = mlc.LoadFromAssemblyPath(dllPath);
        var mt = mlc.LoadFromAssemblyPath(env.Dll("0Harmony")).GetType("HarmonyLib.MethodType");
        if (mt != null) methodTypeNames = Enum.GetNames(mt);
    }

    public void Dispose() => mlc.Dispose();

    private sealed class PatchSpec
    {
        public Type DeclaringType;
        public string TypeName;
        public string MethodName;
        public int? MethodType;
        public Type[] ArgumentTypes;
        public int[] ArgumentVariations; // HarmonyLib.ArgumentType: Normal, Ref, Out, Pointer

        public PatchSpec MergeWith(PatchSpec o) => new()
        {
            DeclaringType = o.DeclaringType ?? DeclaringType,
            TypeName = o.TypeName ?? TypeName,
            MethodName = o.MethodName ?? MethodName,
            MethodType = o.MethodType ?? MethodType,
            ArgumentTypes = o.ArgumentTypes ?? ArgumentTypes,
            ArgumentVariations = o.ArgumentVariations ?? ArgumentVariations,
        };
    }

    public void Run()
    {
        CheckEntryPoint();
        CheckTypeReferences();
        CheckManualPatching();

        report.Section("Harmony patches");
        Type[] types;
        try
        {
            types = mod.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            report.Error("mod types failed to load: " + string.Join("; ", e.LoaderExceptions.Select(x => x?.Message)));
            return;
        }

        int patchCount = 0;
        foreach (var type in types)
        {
            var classSpecs = PatchAttributes(type.GetCustomAttributesData()).ToList();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            bool methodLevelPatches = methods.Any(m => PatchAttributes(m.GetCustomAttributesData()).Any());
            if (classSpecs.Count == 0)
            {
                if (methodLevelPatches)
                    report.Error($"{type.FullName}: [HarmonyPatch] on methods but not on the class; Harmony.PatchAll skips such classes");
                continue;
            }

            foreach (var aux in methods.Where(m => AuxMethods.Contains(m.Name) && m.Name.StartsWith("Target")))
                report.Error($"{type.FullName}.{aux.Name}: dynamic targets cannot be verified offline; use [HarmonyPatch(typeof(T), \"Method\", ...)]");
            foreach (var m in methods.Where(m => HasAttr(m, "HarmonyLib.HarmonyTargetMethod") || HasAttr(m, "HarmonyLib.HarmonyTargetMethods")))
                report.Error($"{type.FullName}.{m.Name}: dynamic targets cannot be verified offline");
            foreach (var m in methods.Where(m => HasAttr(m, "HarmonyLib.HarmonyReversePatch")))
                report.Error($"{type.FullName}.{m.Name}: reverse patches are not supported by this verifier");

            var classSpec = classSpecs.Aggregate(new PatchSpec(), (a, b) => a.MergeWith(b));
            var patchMethods = methods.Where(m => PatchKindOf(m) != null).ToList();
            if (patchMethods.Count == 0)
            {
                report.Error($"{type.FullName}: has [HarmonyPatch] but no Prefix/Postfix/Transpiler/Finalizer method");
                continue;
            }
            // Apply-time fail-safe (docs/PATCHES.md): without a Cleanup that swallows the exception, one
            // unresolvable target aborts PatchAll for every later class.
            if (!methods.Any(m => m.IsStatic && m.Name == "Cleanup" && m.ReturnType.FullName == "System.Exception"
                && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.FullName == "System.Exception"))
                report.Error($"{type.FullName}: needs 'static Exception Cleanup(Exception ex) => PatchGuard.Cleanup(id, ex)' so a failed apply falls back to vanilla");
            foreach (var pm in patchMethods)
            {
                patchCount++;
                var spec = PatchAttributes(pm.GetCustomAttributesData()).Aggregate(classSpec, (a, b) => a.MergeWith(b));
                VerifyPatch(type, pm, PatchKindOf(pm), spec);
            }
        }
        if (patchCount == 0) report.Info("no Harmony patches found");
    }

    // ---------------------------------------------------------------- attributes

    private static bool HasAttr(MemberInfo m, string fullName) => m.GetCustomAttributesData().Any(a => a.AttributeType.FullName == fullName);

    private static string PatchKindOf(MethodInfo m)
    {
        foreach (var a in m.GetCustomAttributesData())
        {
            switch (a.AttributeType.FullName)
            {
                case "HarmonyLib.HarmonyPrefix": return "Prefix";
                case "HarmonyLib.HarmonyPostfix": return "Postfix";
                case "HarmonyLib.HarmonyTranspiler": return "Transpiler";
                case "HarmonyLib.HarmonyFinalizer": return "Finalizer";
            }
        }
        return PatchKinds.Contains(m.Name) ? m.Name : null;
    }

    private IEnumerable<PatchSpec> PatchAttributes(IList<CustomAttributeData> attrs)
    {
        foreach (var a in attrs)
        {
            if (a.AttributeType.FullName != "HarmonyLib.HarmonyPatch") continue;
            var spec = new PatchSpec();
            var strings = new List<string>();
            foreach (var arg in a.ConstructorArguments)
            {
                Type t = arg.ArgumentType;
                if (t.FullName == "System.Type") spec.DeclaringType = (Type)arg.Value;
                else if (t.FullName == "System.String") strings.Add((string)arg.Value);
                else if (t.FullName == "HarmonyLib.MethodType") spec.MethodType = Convert.ToInt32(arg.Value);
                else if (t.IsArray && t.GetElementType().FullName == "System.Type")
                    spec.ArgumentTypes = ((IEnumerable<CustomAttributeTypedArgument>)arg.Value).Select(x => (Type)x.Value).ToArray();
                else if (t.IsArray && t.GetElementType().FullName == "HarmonyLib.ArgumentType")
                    spec.ArgumentVariations = ((IEnumerable<CustomAttributeTypedArgument>)arg.Value).Select(x => Convert.ToInt32(x.Value)).ToArray();
                else report.Warn($"unrecognized HarmonyPatch argument of type {t.FullName}");
            }
            if (strings.Count == 1) spec.MethodName = strings[0];
            else if (strings.Count == 2)
            {
                spec.TypeName = strings[0];
                spec.MethodName = strings[1];
            }
            yield return spec;
        }
    }

    // ---------------------------------------------------------------- target resolution

    private const BindingFlags AllDeclared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private string MethodTypeName(int? mt) => mt is int i && i >= 0 && i < methodTypeNames.Length ? methodTypeNames[i] : "Normal";

    private void VerifyPatch(Type patchClass, MethodInfo patch, string kind, PatchSpec spec)
    {
        string where = $"{patchClass.FullName}.{patch.Name}";
        Type target = spec.DeclaringType;
        if (target == null && spec.TypeName != null)
        {
            target = FindTypeByName(spec.TypeName);
            if (target == null)
            {
                report.Error($"{where}: target type \"{spec.TypeName}\" not found");
                return;
            }
        }
        if (target == null)
        {
            report.Error($"{where}: no target type in [HarmonyPatch]");
            return;
        }

        Type[] args = spec.ArgumentTypes;
        if (args != null && spec.ArgumentVariations != null)
        {
            if (spec.ArgumentVariations.Length != args.Length)
            {
                report.Error($"{where}: argumentVariations length differs from argumentTypes length");
                return;
            }
            args = args.Select((t, i) => spec.ArgumentVariations[i] switch
            {
                1 or 2 => t.MakeByRefType(),
                3 => t.MakePointerType(),
                _ => t,
            }).ToArray();
        }

        string mtName = MethodTypeName(spec.MethodType);
        string desc = $"{target.FullName}::{spec.MethodName ?? "(ctor)"} [{mtName}{(args != null ? " (" + string.Join(", ", args.Select(TypeName)) + ")" : "")}]";
        MethodBase original = null;
        switch (mtName)
        {
            case "Normal":
            case "Enumerator":
            {
                if (spec.MethodName == null)
                {
                    report.Error($"{where}: no method name for {target.FullName}");
                    return;
                }
                var candidates = target.GetMethods(AllDeclared).Where(m => m.Name == spec.MethodName).ToList();
                if (candidates.Count == 0)
                {
                    var inherited = target.GetMethods(AllDeclared & ~BindingFlags.DeclaredOnly).Where(m => m.Name == spec.MethodName).Select(m => m.DeclaringType.FullName).Distinct().ToList();
                    report.Error($"{where}: {desc} not found{(inherited.Count > 0 ? $" (declared on {string.Join(", ", inherited)}; Harmony only looks at the named type)" : "")}");
                    return;
                }
                if (args == null)
                {
                    if (candidates.Count > 1)
                    {
                        report.Error($"{where}: {desc} is ambiguous ({candidates.Count} overloads); add argument types");
                        return;
                    }
                    original = candidates[0];
                }
                else
                {
                    original = candidates.FirstOrDefault(m => SameParams(m.GetParameters(), args));
                    if (original == null)
                    {
                        report.Error($"{where}: {desc} no overload with these exact parameter types; available: "
                                     + string.Join(" | ", candidates.Select(Signature)));
                        return;
                    }
                }
                if (mtName == "Enumerator")
                {
                    report.Warn($"{where}: MethodType.Enumerator patches the compiler-generated MoveNext; verified only that {Signature(original)} exists");
                    return;
                }
                break;
            }
            case "Getter":
            case "Setter":
            {
                var prop = target.GetProperties(AllDeclared).Where(p => p.Name == spec.MethodName).ToList();
                if (prop.Count != 1)
                {
                    report.Error($"{where}: {desc} {(prop.Count == 0 ? "property not declared on this type" : "ambiguous property")}");
                    return;
                }
                original = mtName == "Getter" ? prop[0].GetGetMethod(true) : prop[0].GetSetMethod(true);
                if (original == null)
                {
                    report.Error($"{where}: {desc} property has no {mtName.ToLowerInvariant()}");
                    return;
                }
                break;
            }
            case "Constructor":
            {
                var ctorArgs = args ?? Type.EmptyTypes;
                original = target.GetConstructors(AllDeclared & ~BindingFlags.Static).FirstOrDefault(c => SameParams(c.GetParameters(), ctorArgs));
                if (original == null)
                {
                    report.Error($"{where}: {desc} constructor not found; available: "
                                 + string.Join(" | ", target.GetConstructors(AllDeclared & ~BindingFlags.Static).Select(Signature)));
                    return;
                }
                break;
            }
            case "StaticConstructor":
                original = target.GetConstructors(AllDeclared).FirstOrDefault(c => c.IsStatic);
                if (original == null)
                {
                    report.Error($"{where}: {target.FullName} has no static constructor");
                    return;
                }
                break;
            default:
                report.Error($"{where}: unsupported MethodType {mtName}");
                return;
        }

        int before = report.Errors;
        CheckTargetPatchability(where, original);
        if (kind != "Transpiler") CheckPatchSignature(where, kind, patch, original);
        else CheckTranspilerSignature(where, patch);
        if (report.Errors == before) report.Ok($"{where} ({kind}) -> {Signature(original)}");
    }

    private Type FindTypeByName(string name)
    {
        foreach (var asm in mlc.GetAssemblies().ToList())
        {
            var t = asm.GetType(name.Split(',')[0].Trim(), false);
            if (t != null) return t;
        }
        return null;
    }

    private static bool SameParams(ParameterInfo[] ps, Type[] types) =>
        ps.Length == types.Length && ps.Select((p, i) => SameType(p.ParameterType, types[i])).All(x => x);

    private static bool SameType(Type a, Type b) => a.AssemblyQualifiedName == b.AssemblyQualifiedName || (a.FullName != null && a.FullName == b.FullName);

    private static string TypeName(Type t) => t.IsByRef ? "ref " + TypeName(t.GetElementType()) : (t.FullName ?? t.Name);

    private static string Signature(MethodBase m)
    {
        string ret = m is MethodInfo mi ? TypeName(mi.ReturnType) + " " : "";
        string stat = m.IsStatic ? "static " : "";
        return $"{stat}{ret}{m.DeclaringType.FullName}::{m.Name}({string.Join(", ", m.GetParameters().Select(p => TypeName(p.ParameterType) + " " + p.Name))})";
    }

    // ---------------------------------------------------------------- patchability

    private void CheckTargetPatchability(string where, MethodBase m)
    {
        string sig = Signature(m);
        if (m.IsAbstract) report.Error($"{where}: target {sig} is abstract (no body to patch); patch the overriding implementations");
        if (m.DeclaringType.IsInterface) report.Error($"{where}: target {sig} is an interface member");
        var impl = m.MethodImplementationFlags;
        if ((impl & MethodImplAttributes.InternalCall) != 0 || (m.Attributes & MethodAttributes.PinvokeImpl) != 0)
            report.Error($"{where}: target {sig} is extern/InternalCall (native); Harmony cannot patch it");
        // Burst compiles (a) [BurstCompile] static methods (direct call / function pointers) and
        // (b) all code of [BurstCompile] job structs. Other members of a [BurstCompile] class stay managed.
        if ((IsBurst(m) && m.IsStatic) || (IsJob(m.DeclaringType) && IsBurst(m.DeclaringType)))
            report.Error($"{where}: target {sig} is Burst-compiled; the managed body never runs, so the patch has no effect");
        if (m.IsGenericMethodDefinition || m.DeclaringType.IsGenericTypeDefinition)
            report.Warn($"{where}: target {sig} is generic; patches apply to shared instantiations only and are unreliable");
        if (IsJob(m.DeclaringType))
            report.Warn($"{where}: target {sig} is on a Unity job struct; if it is ever scheduled with Burst the patch is bypassed");

        MethodBody body = null;
        try
        {
            body = m.GetMethodBody();
        }
        catch (Exception)
        {
            // no body available through metadata
        }
        if (body == null)
        {
            if (!m.IsAbstract) report.Error($"{where}: target {sig} has no IL body");
            return;
        }
        int size = body.GetILAsByteArray()?.Length ?? 0;
        bool noInline = (impl & MethodImplAttributes.NoInlining) != 0;
        bool aggressive = (impl & MethodImplAttributes.AggressiveInlining) != 0;
        bool virtualCall = m.IsVirtual && !m.IsFinal;
        if (aggressive)
            report.Warn($"{where}: target {sig} is marked AggressiveInlining; callers may not see the patch");
        else if (!noInline && !virtualCall && size <= InlineRiskIlBytes && body.ExceptionHandlingClauses.Count == 0)
            report.Warn($"{where}: target {sig} has only {size} bytes of IL; Mono may inline it into callers so the patch is skipped there");
    }

    private static bool IsBurst(MemberInfo m) => m.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "Unity.Burst.BurstCompileAttribute");

    private static bool IsJob(Type t) =>
        t.IsValueType && t.GetInterfaces().Any(i => i.Namespace == "Unity.Jobs" && i.Name.StartsWith("IJob", StringComparison.Ordinal));

    // ---------------------------------------------------------------- patch method signatures

    private void CheckTranspilerSignature(string where, MethodInfo patch)
    {
        if (!patch.IsStatic) report.Error($"{where}: transpiler must be static");
        if (!patch.ReturnType.FullName?.StartsWith("System.Collections.Generic.IEnumerable`1[[HarmonyLib.CodeInstruction", StringComparison.Ordinal) ?? true)
            report.Error($"{where}: transpiler must return IEnumerable<CodeInstruction>");
    }

    private void CheckPatchSignature(string where, string kind, MethodInfo patch, MethodBase original)
    {
        if (!patch.IsStatic) report.Error($"{where}: patch methods must be static");
        Type originalReturn = original is MethodInfo omi ? omi.ReturnType : null;
        bool returnsVoid = originalReturn == null || originalReturn.FullName == "System.Void";
        var pars = patch.GetParameters();
        var originalParams = original.GetParameters();

        switch (kind)
        {
            case "Prefix" when patch.ReturnType.FullName is not ("System.Void" or "System.Boolean"):
                report.Error($"{where}: prefix must return void or bool");
                break;
            case "Postfix" when patch.ReturnType.FullName != "System.Void":
                if (returnsVoid || !SameType(patch.ReturnType, originalReturn) || pars.Length == 0 || !SameType(pars[0].ParameterType, originalReturn))
                    report.Error($"{where}: postfix must return void (or be a pass-through returning the original's type with it as first parameter)");
                break;
            case "Finalizer" when patch.ReturnType.FullName is not ("System.Void" or "System.Exception"):
                report.Error($"{where}: finalizer must return void or Exception");
                break;
        }

        foreach (var p in pars)
        {
            string n = p.Name;
            Type pt = p.ParameterType;
            Type bare = pt.IsByRef ? pt.GetElementType() : pt;
            switch (n)
            {
                case "__instance":
                    if (original.IsStatic) report.Error($"{where}: __instance on static target");
                    else if (!IsAssignable(bare, original.DeclaringType)) report.Error($"{where}: __instance type {TypeName(pt)} is not compatible with {original.DeclaringType.FullName}");
                    else if (original.DeclaringType.IsValueType && !pt.IsByRef) report.Warn($"{where}: __instance of struct target should be ref");
                    continue;
                case "__result":
                case "__resultRef":
                    if (returnsVoid) report.Error($"{where}: {n} on a target that returns void");
                    else if (!SameType(bare, originalReturn) && bare.FullName != "System.Object")
                        report.Error($"{where}: {n} type {TypeName(pt)} differs from target return type {TypeName(originalReturn)}");
                    else if (kind == "Prefix" && !pt.IsByRef) report.Warn($"{where}: prefix __result should be 'ref' to have an effect");
                    continue;
                case "__state":
                case "__runOriginal":
                case "__originalMethod":
                case "__args":
                    continue;
                case "__exception":
                    if (kind == "Prefix") report.Error($"{where}: __exception is only available to postfixes and finalizers");
                    continue;
            }
            if (n.StartsWith("___", StringComparison.Ordinal))
            {
                string fieldName = n.Substring(3);
                var field = FindField(original.DeclaringType, fieldName);
                if (field == null) report.Error($"{where}: {n}: no field \"{fieldName}\" on {original.DeclaringType.FullName} or its base types");
                else if (!field.IsStatic && original.IsStatic) report.Error($"{where}: {n}: instance field injected into patch of a static method");
                else if (!SameType(bare, field.FieldType) && bare.FullName != "System.Object")
                    report.Error($"{where}: {n} type {TypeName(pt)} differs from field type {TypeName(field.FieldType)}");
                continue;
            }
            if (n.StartsWith("__", StringComparison.Ordinal) && int.TryParse(n.Substring(2), out int idx))
            {
                if (idx < 0 || idx >= originalParams.Length) report.Error($"{where}: {n} out of range (target has {originalParams.Length} parameters)");
                else CheckArgType(where, n, pt, originalParams[idx]);
                continue;
            }
            var op = originalParams.FirstOrDefault(x => x.Name == n);
            if (op == null)
            {
                report.Error($"{where}: parameter \"{n}\" does not match any target parameter ({string.Join(", ", originalParams.Select(x => x.Name))}) or Harmony injection");
                continue;
            }
            CheckArgType(where, n, pt, op);
        }
    }

    private void CheckArgType(string where, string name, Type patchType, ParameterInfo op)
    {
        Type bare = patchType.IsByRef ? patchType.GetElementType() : patchType;
        Type obare = op.ParameterType.IsByRef ? op.ParameterType.GetElementType() : op.ParameterType;
        if (!SameType(bare, obare) && bare.FullName != "System.Object")
            report.Error($"{where}: parameter {name} type {TypeName(patchType)} differs from target parameter type {TypeName(op.ParameterType)}");
        else if (op.ParameterType.IsByRef && !patchType.IsByRef)
            report.Error($"{where}: parameter {name} must be 'ref' because the target parameter is by-ref");
    }

    private static FieldInfo FindField(Type t, string name)
    {
        for (; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, AllDeclared);
            if (f != null) return f;
        }
        return null;
    }

    private static bool IsAssignable(Type to, Type from)
    {
        if (to.FullName == "System.Object") return true;
        for (Type t = from; t != null; t = t.BaseType)
            if (SameType(t, to)) return true;
        return from.GetInterfaces().Any(i => SameType(i, to));
    }

    // ---------------------------------------------------------------- assembly-level checks

    private void CheckEntryPoint()
    {
        report.Section("entry point");
        var impls = mod.GetTypes().Where(t => t.GetInterfaces().Any(i => i.FullName == "UBOAT.Game.IUserMod")).ToList();
        if (impls.Count == 0)
        {
            report.Warn("no UBOAT.Game.IUserMod implementation: OnLoaded() will never run (fine only for data-only code)");
            return;
        }
        if (impls.Count > 1)
            report.Error("several IUserMod implementations; the loader instantiates only the first one it finds: " + string.Join(", ", impls.Select(t => t.FullName)));
        foreach (var t in impls)
        {
            if (t.IsAbstract || t.IsInterface) report.Error($"{t.FullName}: IUserMod implementation must be a concrete class (Activator.CreateInstance)");
            else if (t.GetConstructor(Type.EmptyTypes) == null) report.Error($"{t.FullName}: needs a public parameterless constructor (Activator.CreateInstance)");
            else report.Ok($"{t.FullName} implements IUserMod");
        }
    }

    /// <summary>Every type the mod references resolves to a type definition (or forwarder) in its scope assembly.</summary>
    private void CheckTypeReferences()
    {
        report.Section("external type references");
        using var pe = new PEReader(File.OpenRead(dllPath));
        var md = pe.GetMetadataReader();
        int count = 0, bad = 0;
        foreach (var h in md.TypeReferences)
        {
            var tr = md.GetTypeReference(h);
            if (tr.ResolutionScope.Kind != HandleKind.AssemblyReference) continue; // nested refs resolve via their parent
            var asmRef = md.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope);
            string asmName = md.GetString(asmRef.Name);
            string ns = md.GetString(tr.Namespace);
            string full = ns.Length == 0 ? md.GetString(tr.Name) : ns + "." + md.GetString(tr.Name);
            count++;
            Assembly a;
            try
            {
                a = mlc.LoadFromAssemblyName(asmName);
            }
            catch (FileNotFoundException)
            {
                report.Error($"referenced assembly {asmName} is not in the game's Managed folder");
                bad++;
                continue;
            }
            if (a.GetType(full, false) == null)
            {
                report.Error($"type {full} not found in {asmName}");
                bad++;
            }
        }
        if (bad == 0) report.Ok($"{count} external type references resolve against the game assemblies");
    }

    private void CheckManualPatching()
    {
        report.Section("manual patching");
        using var pe = new PEReader(File.OpenRead(dllPath));
        var md = pe.GetMetadataReader();
        var forbidden = new HashSet<string> { "Patch", "Unpatch", "CreateReversePatcher", "CreateProcessor", "CreateClassProcessor", "ReversePatch" };
        int found = 0;
        foreach (var h in md.MemberReferences)
        {
            var mr = md.GetMemberReference(h);
            if (mr.Parent.Kind != HandleKind.TypeReference) continue;
            var tr = md.GetTypeReference((TypeReferenceHandle)mr.Parent);
            string type = md.GetString(tr.Namespace) + "." + md.GetString(tr.Name);
            string name = md.GetString(mr.Name);
            if ((type == "HarmonyLib.Harmony" && forbidden.Contains(name)) || type == "HarmonyLib.PatchProcessor" || type == "HarmonyLib.ReversePatcher")
            {
                report.Error($"manual Harmony call {type}.{name}: targets are invisible to the verifier; use [HarmonyPatch] classes + PatchAll()");
                found++;
            }
        }
        if (found == 0) report.Ok("no manual Harmony.Patch calls (all patches are attribute-declared and verified below)");
    }
}
