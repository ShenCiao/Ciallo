using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SourceGeneration;

[Generator]
public sealed class RegisterStateGenerator : IIncrementalGenerator
{
    private const string RegisterStateFqn = "Ciallo.Tool.RegisterStateAttribute";
    private const string SubstateFqn = "Ciallo.Tool.SubstateAttribute";
    private const string StateAccessFqn = "Ciallo.Tool.StateAccessAttribute";
    private const string ToolButtonFqn = "Ciallo.Tool.RequestedByToolButtonAttribute";
    private const string LayerDependentFqn = "Ciallo.Tool.ILayerDependent";
    private const string ScopeName = "InteractionScope";
    private const string ConfigureMethodName = "ConfigureStateMachine";
    private const string LayersTriggerName = "WorkingLayersChanged";

    private static readonly DiagnosticDescriptor ButtonToolNotRegistered = new(
        id: "CIALLO003",
        title: "Tool-button tool is not registered",
        messageFormat:
            "'{0}' has [RequestedByToolButton] but not [RegisterState], so it is never instantiated "
            + "and the button can never resolve to it",
        category: "InteractionState",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ButtonToolNotScope = new(
        id: "CIALLO004",
        title: "Tool-button tool is not an InteractionScope",
        messageFormat:
            "'{0}' has [RequestedByToolButton] but does not derive from InteractionScope; "
            + "a tool button can only resolve to a scope",
        category: "InteractionState",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateLayersTrigger = new(
        id: "CIALLO005",
        title: "Working-layer trigger configured twice",
        messageFormat:
            "'{0}' implements ILayerDependent, so {1} is generated for it; its {2} also configures "
            + "{1}. Stateless rejects two transitions for one trigger at the first fire, not at "
            + "build time. Remove the hand-written configuration.",
        category: "InteractionState",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnreachableButtonTool = new(
        id: "CIALLO006",
        title: "Tool-button candidate is unreachable",
        messageFormat:
            "'{0}' can never be resolved: '{1}' also answers button '{2}' and, not implementing "
            + "ILayerDependent, accepts every context. Implement ILayerDependent on one of them, or "
            + "give them distinct buttons.",
        category: "InteractionState",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<StateModel> states = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                RegisterStateFqn,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetState(ctx));

        // Tools carrying only [RequestedByToolButton] never reach the provider
        // above, so they would be invisible to diagnostics. Collect them separately.
        IncrementalValuesProvider<StrayModel> strays = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => GetStray(ctx))
            .Where(static stray => stray.Fqn is not null);

        IncrementalValueProvider<ImmutableArray<string>> buttons = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is EnumDeclarationSyntax,
                transform: static (ctx, _) => GetButtonNames(ctx))
            .Where(static names => !names.IsDefault)
            .Collect()
            .Select(static (collected, _) =>
                collected.Length > 0 ? collected[0] : ImmutableArray<string>.Empty);

        var combined = states.Collect().Combine(strays.Collect()).Combine(buttons);
        context.RegisterSourceOutput(combined, static (spc, data) =>
            Execute(spc, data.Left.Left, data.Left.Right, data.Right));
    }

    private static ImmutableArray<string> GetButtonNames(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is not EnumDeclarationSyntax syntax)
            return default;
        if (ctx.SemanticModel.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol)
            return default;
        if (symbol.Name != "Type" || symbol.ContainingType?.ToDisplayString() != "Ciallo.Tool.ToolButton")
            return default;

        var names = ImmutableArray.CreateBuilder<string>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field)
                names.Add(field.Name);
        }

        return names.ToImmutable();
    }

    // Diagnostics only; never emitted.
    private static StrayModel GetStray(GeneratorSyntaxContext ctx)
    {
        if (ctx.Node is not ClassDeclarationSyntax syntax)
            return default;
        if (ctx.SemanticModel.GetDeclaredSymbol(syntax) is not INamedTypeSymbol type)
            return default;
        if (GetAttribute(type, RegisterStateFqn) is not null)
            return default;

        if (GetAttribute(type, ToolButtonFqn) is null)
            return default;

        return new StrayModel(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.Name,
            type.Locations.Length > 0 ? type.Locations[0] : Location.None);
    }

    private static bool ImplementsLayerDependent(INamedTypeSymbol type)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString() == LayerDependentFqn)
                return true;
        }

        return false;
    }

    private static bool IsScope(INamedTypeSymbol type)
    {
        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType.Name == ScopeName)
                return true;
        }

        return false;
    }

    // Scans this type's own ConfigureStateMachine for any Permit*/Ignore whose trigger argument
    // mentions WorkingLayersChanged. Syntactic and deliberately broad: the realistic mistake is
    // pasting the old PermitReentryIf line into a tool that now gets one generated (CIALLO005).
    private static Location? FindLayersTriggerConfiguration(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers(ConfigureMethodName))
        {
            if (member is not IMethodSymbol method)
                continue;

            foreach (var reference in method.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not MethodDeclarationSyntax declaration)
                    continue;

                foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is not MemberAccessExpressionSyntax access)
                        continue;

                    string name = access.Name.Identifier.ValueText;
                    if (!name.StartsWith("Permit") && name != "Ignore" && name != "InternalTransition")
                        continue;
                    if (invocation.ArgumentList.Arguments.Count == 0)
                        continue;

                    var trigger = invocation.ArgumentList.Arguments[0].Expression;
                    if (trigger.ToString().Contains(LayersTriggerName))
                        return trigger.GetLocation();
                }
            }
        }

        return null;
    }

    private static StateModel GetState(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol type)
            return default;

        var substates = ImmutableArray.CreateBuilder<MemberRef>();
        var accesses = ImmutableArray.CreateBuilder<MemberRef>();
        CollectMembers(type, substates, accesses, inheritedAccessOnly: false);

        for (var baseType = type.BaseType; baseType is { SpecialType: not SpecialType.System_Object }; baseType = baseType.BaseType)
            CollectMembers(baseType, substates, accesses, inheritedAccessOnly: true);

        string? button = null;
        var buttonAttribute = GetAttribute(type, ToolButtonFqn);
        if (buttonAttribute is not null)
        {
            if (buttonAttribute.ConstructorArguments.Length == 1)
            {
                button = ResolveEnumMemberName(
                    buttonAttribute.ConstructorArguments[0].Type,
                    buttonAttribute.ConstructorArguments[0].Value);
            }

        }

        bool isLayerDependent = ImplementsLayerDependent(type);

        return new StateModel(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.Name,
            type.Name == "GlobalInteractiveScope",
            type.Name == "NoDocument",
            substates.ToImmutable(),
            accesses.ToImmutable(),
            button,
            isLayerDependent,
            IsScope(type),
            isLayerDependent && IsScope(type) ? FindLayersTriggerConfiguration(type) : null,
            type.Locations.Length > 0 ? type.Locations[0] : Location.None);
    }

    // The attribute stores ToolButton.Type as its underlying int; recover the member name so the
    // emitted switch reads ToolButton.Type.Select rather than a bare literal.
    private static string? ResolveEnumMemberName(ITypeSymbol? enumType, object? value)
    {
        if (enumType is not INamedTypeSymbol named || value is null)
            return null;

        foreach (var member in named.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } field &&
                Equals(field.ConstantValue, value))
            {
                return field.Name;
            }
        }

        return null;
    }
    private static void CollectMembers(
        INamedTypeSymbol declaringType,
        ImmutableArray<MemberRef>.Builder substates,
        ImmutableArray<MemberRef>.Builder accesses,
        bool inheritedAccessOnly)
    {
        foreach (var member in declaringType.GetMembers())
        {
            if (member.IsStatic)
                continue;

            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol field => field.Type,
                IPropertySymbol property => property.Type,
                _ => null,
            };
            if (memberType is not INamedTypeSymbol namedType)
                continue;

            int declOrder = member.Locations.Length > 0 ? member.Locations[0].SourceSpan.Start : 0;
            string memberFqn = namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (!inheritedAccessOnly)
            {
                var substate = GetAttribute(member, SubstateFqn);
                if (substate is not null)
                {
                    int order = 0;
                    if (substate.ConstructorArguments.Length == 1 &&
                        substate.ConstructorArguments[0].Value is int orderValue)
                        order = orderValue;
                    substates.Add(new MemberRef(member.Name, memberFqn, namedType.Name, order, declOrder));
                }
            }

            var access = GetAttribute(member, StateAccessFqn);
            if (access is not null)
                accesses.Add(new MemberRef(member.Name, memberFqn, namedType.Name, 0, declOrder));
        }
    }

    private static AttributeData? GetAttribute(ISymbol symbol, string fqn)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            string? name = attribute.AttributeClass?.ToDisplayString();
            if (name == fqn)
                return attribute;
        }

        return null;
    }

    // Type name only: candidates accept disjoint layers, so this order should never be observable.
    // It exists to make codegen deterministic; real overlap is caught by the emitted assertion.
    private static List<StateModel> OrderCandidates(IEnumerable<StateModel> candidates) =>
        candidates.OrderBy(static c => c.Name, StringComparer.Ordinal).ToList();

    private static void ReportDiagnostics(
        SourceProductionContext context,
        StateModel[] valid,
        ImmutableArray<StrayModel> strays)
    {
        foreach (var stray in strays)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ButtonToolNotRegistered, stray.Location, stray.Name));
        }

        foreach (var state in valid)
        {
            if (state.Button is not null && !state.IsScope)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ButtonToolNotScope, state.Location, state.Name));
            }

            // Stateless rejects two transitions for one trigger only at the first fire (verified),
            // so this is the difference between a build error and a crash on the first layer change.
            if (state.LayersTriggerLocation is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateLayersTrigger,
                    state.LayersTriggerLocation,
                    state.Name,
                    LayersTriggerName,
                    ConfigureMethodName));
            }
        }

        var byButton = valid
            .Where(static s => s.Button is not null && s.IsScope)
            .GroupBy(static s => s.Button!);

        foreach (var group in byButton)
        {
            var ordered = OrderCandidates(group);

            // A candidate that is not ILayerDependent matches unconditionally, so anything after it
            // in resolution order is dead code.
            for (int i = 0; i < ordered.Count - 1; i++)
            {
                if (ordered[i].IsLayerDependent)
                    continue;

                for (int j = i + 1; j < ordered.Count; j++)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnreachableButtonTool,
                        ordered[j].Location,
                        ordered[j].Name,
                        ordered[i].Name,
                        group.Key));
                }

                break;
            }
        }
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<StateModel> states,
        ImmutableArray<StrayModel> strays,
        ImmutableArray<string> buttonOrder)
    {
        var valid = states.Where(static s => s.Fqn is not null).ToArray();
        if (valid.Length == 0)
            return;

        ReportDiagnostics(context, valid, strays);

        var byFqn = valid.ToDictionary(static s => s.Fqn);
        var byName = new Dictionary<string, StateModel>();
        foreach (var state in valid)
            byName[state.Name] = state;

        var global = valid.FirstOrDefault(static s => s.IsGlobal);
        var buttonTools = valid.Where(static s => s.Button is not null && s.IsScope).ToArray();

        var generatedGlobalSubstates = OrderCandidates(buttonTools);

        var builder = new StringBuilder();
        builder.AppendLine(
            """
            // <auto-generated/>
            // Deliberately no #nullable enable: this project does not use nullable reference types,
            // and opting in here would force every hand-written partial half to opt in as well.

            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Diagnostics;
            using System.Linq;
            using Ciallo.Misc;
            using Frent;
            using Stateless;

            namespace Ciallo.Tool;

            using StateMachine = Stateless.StateMachine<InteractionState, Trigger>;

            internal static class InteractionStateGraph
            {
            """);

        foreach (var state in valid)
        {
            builder.AppendLine($"    internal static {state.Fqn} {LocalName(state)} = null!;");
        }

        builder.AppendLine("    internal static InteractionState[] States = null!;");
        builder.AppendLine();
        builder.AppendLine("    internal static void Create(out GlobalInteractiveScope outGlobal, out NoDocument outNoDocument)");
        builder.AppendLine("    {");

        foreach (var state in valid)
        {
            builder.AppendLine($"        {LocalName(state)} = new {state.Fqn}();");
        }

        builder.AppendLine();
        foreach (var parent in valid)
        {
            string parentLocal = LocalName(parent);
            foreach (var child in parent.Substates.OrderBy(static s => s.Order).ThenBy(static s => s.DeclOrder))
            {
                if (!byFqn.TryGetValue(child.TypeFqn, out var childState) &&
                    !byName.TryGetValue(child.TypeName, out childState))
                    continue;
                builder.AppendLine($"        {parentLocal}.{child.MemberName} = {LocalName(childState)};");
            }

            foreach (var access in parent.Accesses)
            {
                if (!byFqn.TryGetValue(access.TypeFqn, out var target) &&
                    !byName.TryGetValue(access.TypeName, out target))
                    continue;
                builder.AppendLine($"        {parentLocal}.{access.MemberName} = {LocalName(target)};");
            }
        }

        builder.AppendLine();
        builder.AppendLine("        States =");
        builder.AppendLine("        [");
        foreach (var state in OrderStates(valid, byFqn, byName, generatedGlobalSubstates, global))
            builder.AppendLine($"            {LocalName(state)},");
        builder.AppendLine("        ];");
        builder.AppendLine("        outGlobal = global;");
        builder.AppendLine("        outNoDocument = noDocument;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    internal static void Configure(StateMachine stateMachine)");
        builder.AppendLine("    {");
        builder.AppendLine(
            """
                    foreach (var state in States)
                    {
                        stateMachine.Configure(state)
                            .OnEntry(state.OnEntry)
                            .OnExit(state.OnExit);
                    }
            """);

        foreach (var parent in valid)
        {
            string parentLocal = LocalName(parent);
            foreach (var child in parent.Substates.OrderBy(static s => s.Order).ThenBy(static s => s.DeclOrder))
            {
                if (!byFqn.TryGetValue(child.TypeFqn, out var childState) &&
                    !byName.TryGetValue(child.TypeName, out childState))
                    continue;
                builder.AppendLine($"        stateMachine.Configure({LocalName(childState)}).SubstateOf({parentLocal});");
            }
        }

        if (global.Fqn is not null)
        {
            foreach (var tool in generatedGlobalSubstates)
            {
                builder.AppendLine(
                    $"        stateMachine.Configure({LocalName(tool)}).SubstateOf({LocalName(global)});");
            }
        }

        builder.AppendLine(
            """
                    foreach (var scope in States.OfType<InteractionScope>())
                    {
                        scope.ConfigureStateMachine(stateMachine);
                    }
            """);

        // Working-layer reentry. Without it Stateless keeps the scope active as common ancestor when
        // the layer context changes, so only the leaf session re-enters and the scope's layer-bound
        // state stays bound to the previous layer.
        //
        // The guard takes the layers from the trigger parameter: InteractionManager.WorkingLayers is
        // still the pre-transition snapshot while a guard runs. Guard failure falls through to
        // GlobalInteractiveScope's PermitDynamic, which performs the tool switch.
        // Layer dependency applies equally to manually registered and button-registered scopes.
        var reentryTools = valid.Where(static t => t.IsScope && t.IsLayerDependent).ToArray();
        if (reentryTools.Length > 0 && global.Fqn is not null)
        {
            builder.AppendLine();
            foreach (var tool in reentryTools)
            {
                builder.AppendLine(
                    $"        stateMachine.Configure({LocalName(tool)})");
                builder.AppendLine(
                    "            .PermitReentryIf(");
                builder.AppendLine(
                    "                InteractionManager.WorkingLayersChanged,");
                builder.AppendLine(
                    $"                layers => InteractionManager.HasUsableLayers(layers) && "
                    + $"{tool.Fqn}.CanHandleLayers(layers));");
            }
        }

        builder.AppendLine();
        builder.AppendLine("        stateMachine.BuildSubstateMap();");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        EmitResolver(builder, generatedGlobalSubstates, buttonOrder);

        context.AddSource("InteractionStateGraph.g.cs", SourceText.From(builder.ToString(), Encoding.UTF8));
    }

    // Emitted as a partial-method implementation so the hand-written policy (arity, liveness,
    // fallback) stays in GlobalInteractiveScope and only the dispatch is generated.
    private static void EmitResolver(
        StringBuilder builder,
        List<StateModel> tools,
        ImmutableArray<string> buttonOrder)
    {
        var groups = tools
            .Where(static t => t.Button is not null)
            .GroupBy(static t => t.Button!)
            .OrderBy(g => buttonOrder.IsDefaultOrEmpty ? 0 : buttonOrder.IndexOf(g.Key))
            .ThenBy(static g => g.Key, StringComparer.Ordinal)
            .ToArray();

        builder.AppendLine();
        builder.AppendLine(
            """
            public partial class GlobalInteractiveScope
            {
                private partial InteractionState ResolveToolButton(
                    ToolButton.Type toolButton,
                    ImmutableArray<Entity> layers)
                {
                    AssertSingleCandidate(toolButton, layers);

                    return toolButton switch
                    {
            """);

        foreach (var group in groups)
        {
            foreach (var tool in OrderCandidates(group))
            {
                // A tool that is not ILayerDependent accepts any context this button reaches.
                string guard = tool.IsLayerDependent
                    ? $" when {tool.Fqn}.CanHandleLayers(layers)"
                    : string.Empty;
                builder.AppendLine(
                    $"            ToolButton.Type.{group.Key}{guard} =>");
                builder.AppendLine(
                    $"                InteractionStateGraph.{LocalName(tool)},");
            }
        }

        builder.AppendLine(
            """
                        _ => null,
                    };
                }
            """);

        EmitCandidateAssertion(builder, groups);
        builder.AppendLine("}");
    }

    // Asserts the disjointness the switch above assumes, at the one moment it can be checked: with
    // real layers in hand. Cheaper than making every tool declare a priority to prove it does not care.
    private static void EmitCandidateAssertion(
        StringBuilder builder,
        IEnumerable<IGrouping<string, StateModel>> groups)
    {
        var contested = groups
            .Select(static g => (Button: g.Key, Tools: OrderCandidates(g)))
            .Where(static g => g.Tools.Count > 1)
            .ToArray();

        builder.AppendLine();
        builder.AppendLine("    [Conditional(\"DEBUG\")]");
        builder.AppendLine(
            "    private static void AssertSingleCandidate(ToolButton.Type toolButton, ImmutableArray<Entity> layers)");
        builder.AppendLine("    {");

        if (contested.Length == 0)
        {
            builder.AppendLine("        // Every tool button has a single candidate; nothing can overlap.");
            builder.AppendLine("    }");
            return;
        }

        builder.AppendLine("        var matched = new List<string>();");
        foreach (var (button, candidateTools) in contested)
        {
            builder.AppendLine();
            builder.AppendLine($"        if (toolButton == ToolButton.Type.{button})");
            builder.AppendLine("        {");
            foreach (var tool in candidateTools)
            {
                if (tool.IsLayerDependent)
                {
                    builder.AppendLine($"            if ({tool.Fqn}.CanHandleLayers(layers))");
                    builder.AppendLine($"                matched.Add(\"{tool.Name}\");");
                }
                else
                {
                    // Unconditional candidate: CIALLO006 already rejects this at build time.
                    builder.AppendLine($"            matched.Add(\"{tool.Name}\");");
                }
            }

            builder.AppendLine("        }");
        }

        builder.AppendLine();
        builder.AppendLine(
            """
                    if (matched.Count > 1)
                    {
                        string names = string.Join(", ", matched);
                        Debug.Assert(
                            false,
                            $"Tool button {toolButton} has {matched.Count} candidates accepting the same "
                            + $"layers ({names}), so which one wins is decided by type-name order rather "
                            + "than by intent. Narrow one tool's CanHandleLayers.");
                    }
                }
            """);
    }


    private static string LocalName(StateModel state)
    {
        if (state.IsGlobal)
            return "global";
        if (state.IsNoDocument)
            return "noDocument";
        string name = state.Name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static List<StateModel> OrderStates(
        StateModel[] states,
        Dictionary<string, StateModel> byFqn,
        Dictionary<string, StateModel> byName,
        List<StateModel> generatedGlobalSubstates,
        StateModel global)
    {
        var result = new List<StateModel>(states.Length);
        var seen = new HashSet<string>();
        if (global.Fqn is not null)
            Visit(global);

        // Keep attribute-attached tools adjacent to Global so States still reads as a hierarchy walk.
        foreach (var tool in generatedGlobalSubstates)
            Visit(tool);

        foreach (var state in states)
            Visit(state);

        return result;

        void Visit(StateModel state)
        {
            if (state.Fqn is null || !seen.Add(state.Fqn))
                return;
            result.Add(state);
            foreach (var child in state.Substates.OrderBy(static s => s.Order).ThenBy(static s => s.DeclOrder))
            {
                if (byFqn.TryGetValue(child.TypeFqn, out var childState) ||
                    byName.TryGetValue(child.TypeName, out childState))
                    Visit(childState);
            }
        }
    }

    private readonly struct MemberRef
    {
        public MemberRef(string memberName, string typeFqn, string typeName, int order, int declOrder)
        {
            MemberName = memberName;
            TypeFqn = typeFqn;
            TypeName = typeName;
            Order = order;
            DeclOrder = declOrder;
        }

        public string MemberName { get; }
        public string TypeFqn { get; }
        public string TypeName { get; }
        public int Order { get; }
        public int DeclOrder { get; }
    }

    private readonly struct StrayModel
    {
        public StrayModel(
            string fqn,
            string name,
            Location location)
        {
            Fqn = fqn;
            Name = name;
            Location = location;
        }

        public string Fqn { get; }
        public string Name { get; }
        public Location Location { get; }
    }

    private readonly struct StateModel
    {
        public StateModel(
            string fqn,
            string name,
            bool isGlobal,
            bool isNoDocument,
            ImmutableArray<MemberRef> substates,
            ImmutableArray<MemberRef> accesses,
            string? button,
            bool isLayerDependent,
            bool isScope,
            Location? layersTriggerLocation,
            Location location)
        {
            Fqn = fqn;
            Name = name;
            IsGlobal = isGlobal;
            IsNoDocument = isNoDocument;
            Substates = substates;
            Accesses = accesses;
            Button = button;
            IsLayerDependent = isLayerDependent;
            IsScope = isScope;
            LayersTriggerLocation = layersTriggerLocation;
            Location = location;
        }

        public string Fqn { get; }
        public string Name { get; }
        public bool IsGlobal { get; }
        public bool IsNoDocument { get; }
        public ImmutableArray<MemberRef> Substates { get; }
        public ImmutableArray<MemberRef> Accesses { get; }

        public string? Button { get; }
        public bool IsLayerDependent { get; }
        public bool IsScope { get; }

        // Where this type hand-configures WorkingLayersChanged, when it also gets one generated.
        public Location? LayersTriggerLocation { get; }
        public Location Location { get; }
    }
}
