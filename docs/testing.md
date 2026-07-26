# Testing

Ciallo's C# global regression suites live in `Ciallo/Tests`. The main project is also the
GdUnit4 test project, and `Ciallo/.runsettings` is loaded automatically by `dotnet test`.

## Test Scope

Global suites cover complex, error-prone boundaries such as persistence graph integrity,
durability conflict handling, native DuckDB composite vectors, resource retention, and external
authorization state. Common entry points belong in a global suite only when they exercise one of
these failure-prone contracts and are not already covered by a downstream scenario.

Use `[RequireGodotRuntime]` only for tests that access Godot runtime objects. Logic-only tests run
in the lightweight GdUnit4 host.

## Local Workflow

Opening the project in a managed Godot editor generates `Ciallo/.runsettings.local` with that
editor's executable path. The file is machine-local and ignored by Git. Reopening the project
with another editor build refreshes it.

For command-line or CI runs that intentionally use another managed editor, set `GODOT_BIN`.
An explicit environment variable takes precedence over `.runsettings.local`:

```powershell
$env:GODOT_BIN = "C:\path\to\godot.mono.exe"
```

Restore, build, and run all global suites from the repository root:

```powershell
dotnet restore Ciallo/Ciallo.csproj
dotnet build Ciallo/Ciallo.csproj --no-restore
dotnet test Ciallo/Ciallo.csproj --no-build
```

Use VSTest filtering for one suite:

```powershell
dotnet test Ciallo/Ciallo.csproj --no-build --filter FullyQualifiedName~DurabilityTests
```

File-based VSTest artifacts, when produced, are written under `Ciallo/TestResults` and are ignored by Git.
