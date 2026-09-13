global using AppCommandLineOptions = Ciallo.CommandLine.AppCommandLineOptions;

using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Ciallo.CommandLine;

public static class AppCommandLineOptions
{
    public static string DocumentPath { get; private set; }
    public static string UserDataDirectory { get; private set; }
    public static bool FactoryStartup { get; private set; }
    public static bool DebugInfo { get; private set; }

    internal static void Initialize(IReadOnlyList<string> arguments)
    {
        string document = null;
        string userData = null;
        bool factory = false;
        bool debug = false;
        bool positionalOnly = false;

        for (int i = 0; i < arguments.Count; i++)
        {
            string argument = arguments[i];
            if (!positionalOnly && argument == "--")
            {
                positionalOnly = true;
                continue;
            }
            if (!positionalOnly && argument.StartsWith("--", StringComparison.Ordinal))
            {
                int equals = argument.IndexOf('=');
                string option = equals < 0 ? argument : argument[..equals];
                switch (option)
                {
                    case "--open":
                        SetDocument(ReadValue());
                        break;
                    case "--user-data-dir":
                        if (userData != null)
                            throw new ArgumentException("--user-data-dir may only be specified once.");
                        userData = ReadValue();
                        break;
                    case "--factory-startup" when equals < 0:
                        factory = true;
                        break;
                    case "--debug-info" when equals < 0:
                        debug = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{argument}'.");
                }

                string ReadValue()
                {
                    if (equals >= 0)
                        return RequireValue(argument[(equals + 1)..], option);
                    if (i + 1 == arguments.Count || arguments[i + 1].StartsWith("--", StringComparison.Ordinal))
                        throw new ArgumentException($"{option} requires a path.");
                    return RequireValue(arguments[++i], option);
                }
            }
            else
            {
                if (!positionalOnly && argument.StartsWith('-'))
                    throw new ArgumentException($"Unknown option '{argument}'. Use '--' before a filename beginning with '-'.");
                SetDocument(RequireValue(argument, "Document"));
            }
        }

        DocumentPath = document;
        UserDataDirectory = userData;
        FactoryStartup = factory;
        DebugInfo = debug;

        void SetDocument(string path)
        {
            if (document != null)
                throw new ArgumentException("Ciallo supports one startup document. Specify a filename or --open, once.");
            document = path;
        }
    }

    internal static void ResolveDocumentPath()
    {
        if (DocumentPath != null)
            DocumentPath = Path.GetFullPath(ProjectSettings.GlobalizePath(DocumentPath));
    }

    private static string RequireValue(string value, string option) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{option} requires a non-empty path.") : value;
}
