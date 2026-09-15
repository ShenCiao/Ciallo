using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SourceGeneration;

[Generator]
public sealed class ShortcutMetadataGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidActionName = new(
        id: "CIALLO001",
        title: "Invalid shortcut action name",
        messageFormat: "Input action '{0}' must use '<Scope>.<Group>.<Action>' with three non-empty identifier segments",
        category: "ShortcutMetadata",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MissingProjectFile = new(
        id: "CIALLO002",
        title: "Missing project.godot",
        messageFormat: "Shortcut metadata generation requires project.godot as an AdditionalFile",
        category: "ShortcutMetadata",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var projectFiles = context.AdditionalTextsProvider
            .Where(static file => string.Equals(
                System.IO.Path.GetFileName(file.Path),
                "project.godot",
                StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => Parse(file.GetText(cancellationToken)))
            .Collect();

        context.RegisterSourceOutput(projectFiles, static (productionContext, projectActions) =>
        {
            if (projectActions.IsDefaultOrEmpty)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(MissingProjectFile, Location.None));
                productionContext.AddSource(
                    "ShortcutMetadata.g.cs",
                    SourceText.From(BuildSource(new ShortcutMetadata([], ImmutableArray<ShortcutAction>.Empty)), Encoding.UTF8));
                return;
            }

            Execute(productionContext, projectActions[0]);
        });
    }

    private static ImmutableArray<ShortcutAction> Parse(SourceText? source)
    {
        if (source is null)
            return ImmutableArray<ShortcutAction>.Empty;

        var actions = ImmutableArray.CreateBuilder<ShortcutAction>();
        bool inInputSection = false;

        foreach (TextLine line in source.Lines)
        {
            string text = line.ToString().Trim();
            if (text.Length == 0 || text[0] == ';')
                continue;

            if (text[0] == '[')
            {
                inInputSection = string.Equals(text, "[input]", StringComparison.Ordinal);
                continue;
            }

            if (!inInputSection)
                continue;

            int equalsIndex = text.IndexOf('=');
            if (equalsIndex <= 0)
                continue;

            string actionName = text.Substring(0, equalsIndex).Trim().Trim('"');
            if (actionName.StartsWith("ui_", StringComparison.Ordinal))
                continue;

            string[] segments = actionName.Split('.');
            if (segments.Length != 3 || !AreIdentifiers(segments))
            {
                actions.Add(ShortcutAction.Invalid(actionName));
                continue;
            }

            string scope = segments[0];
            string group = segments[1];
            string action = segments[2];
            actions.Add(new ShortcutAction(
                actionName,
                $"{scope}.{group}",
                action,
                string.Equals(scope, "Global", StringComparison.Ordinal)));
        }

        return actions.ToImmutable();
    }

    private static bool AreIdentifiers(string[] segments)
    {
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || !IsIdentifierStart(segment[0]))
                return false;

            for (int i = 1; i < segment.Length; i++)
            {
                if (!IsIdentifierPart(segment[i]))
                    return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value == '_';

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<ShortcutAction> actions)
    {
        var validActions = ImmutableArray.CreateBuilder<ShortcutAction>();
        var groups = new List<string>();
        var seenGroups = new HashSet<string>(StringComparer.Ordinal);

        foreach (ShortcutAction action in actions)
        {
            if (!action.IsValid)
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidActionName, Location.None, action.ActionName));
                continue;
            }

            validActions.Add(action);
            if (seenGroups.Add(action.GroupName))
                groups.Add(action.GroupName);
        }

        context.AddSource(
            "ShortcutMetadata.g.cs",
            SourceText.From(BuildSource(new ShortcutMetadata(groups, validActions.ToImmutable())), Encoding.UTF8));
    }

    private static string BuildSource(ShortcutMetadata metadata)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("// Generated by Ciallo.SourceGeneration.ShortcutMetadataGenerator");
        builder.AppendLine();
        builder.AppendLine("namespace Ciallo.GuiControl;");
        builder.AppendLine();
        builder.AppendLine("internal static class GeneratedShortcutMetadata");
        builder.AppendLine("{");
        builder.AppendLine("    public static readonly string[] Groups =");
        builder.AppendLine("    [");

        foreach (string group in metadata.Groups)
            builder.AppendLine($"        \"{Escape(group)}\",");

        builder.AppendLine("    ];");
        builder.AppendLine();
        builder.AppendLine("    public static readonly (string ActionName, string ActionSegment, string GroupName, bool Global)[] Actions =");
        builder.AppendLine("    [");

        foreach (ShortcutAction action in metadata.Actions)
        {
            builder.AppendLine(
                $"        (\"{Escape(action.ActionName)}\", \"{Escape(action.ActionSegment)}\", \"{Escape(action.GroupName)}\", {action.Global.ToString().ToLowerInvariant()}),");
        }

        builder.AppendLine("    ];");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private readonly struct ShortcutMetadata
    {
        public ShortcutMetadata(List<string> groups, ImmutableArray<ShortcutAction> actions)
        {
            Groups = groups;
            Actions = actions;
        }

        public List<string> Groups { get; }
        public ImmutableArray<ShortcutAction> Actions { get; }
    }

    private readonly struct ShortcutAction
    {
        public ShortcutAction(string actionName, string groupName, string actionSegment, bool global)
        {
            ActionName = actionName;
            GroupName = groupName;
            ActionSegment = actionSegment;
            Global = global;
            IsValid = true;
        }

        private ShortcutAction(string actionName)
        {
            ActionName = actionName;
            GroupName = string.Empty;
            ActionSegment = string.Empty;
            Global = false;
            IsValid = false;
        }

        public string ActionName { get; }
        public string GroupName { get; }
        public string ActionSegment { get; }
        public bool Global { get; }
        public bool IsValid { get; }

        public static ShortcutAction Invalid(string actionName) => new(actionName);
    }
}
