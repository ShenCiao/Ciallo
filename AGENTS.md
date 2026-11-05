## Trust Principles

- Follow the "Let It Crash" philosophy. Trust internal application state and upstream context.
- Limit null checks and try-catch blocks to unpredictable external boundaries such as user input, disk IO, and network requests.
- Encourage removing lagacy code aggressively.

## Technical Judgment

- Do not agree merely to be polite. Flag flawed logic, code, security, or architecture directly as user propose. Then reject and stop requested work.
- When evaluating a proposal, compare it with the current approach and explain which is better and why.

## Documentation

- Write current-state guidance covering supported behavior, required configuration, constraints, and operational workflows. Do not cover rejected alternatives or abandoned designs.
- Keep one authoritative document for each topic. Put implementation history, rejected alternatives, and abandoned designs only in ADRs or explicit history records.
- Do not record common sense in paint software unless we are violating it.
- Do not record things that can be easily infered from code.
- Treat ADRs as architectural context and `docs/helper` PRDs or notes as product intent; verify implementation before presenting them as supported behavior.

## Tests

- Permanent tests are those not ignored by git and will be committed.
- Reserve permanent tests only for complex, error-prone edge cases. Do not write permanent tests for trivial common cases.
- Do not add permanent tests for common entry points already covered by downstream calls or other suites.
- Do not write permanent tests for the correctness Godot APIs or their simple wrappers -- they have been tested.
- Encourage aggressively remove unnecessary tests.


## Project Facts

- Ciallo targets Godot with C# and Forward Plus.
- The main project uses explicit `Godot.NET.Sdk` imports; `Ciallo/global.json` pins the published version. Run `./engine.sh sync` to prepare the selected engine. `./engine.sh local on [editor]` and `local off` control direct local SDK imports without modifying tracked files. See `docs/engine-setup.md`.
- Product logic is C#. Repository GDScript belongs to addons; project-owned rendering code includes `.gdshader` files.
- Fennara support scripts and autoloads are addon infrastructure, not unused product code.
- For Godot translation resources, automatic UI translation, and explicit translation behavior, follow [docs/godot-localization.md](docs/godot-localization.md).

## Godot And Fennara

- Fennara is managed by `./engine.sh fennara sync` and `./engine.sh fennara update [version]`, using `tools/engine/dependencies.lock`. Do not use official installers, `fennara install/update`, or the editor's setup/update buttons. See [docs/engine-setup.md](docs/engine-setup.md#fennara).
- Implement product behavior in C#; do not introduce GDScript for application logic.
- When Fennara needs a live editor and none is connected, the project editor may be launched as needed. Resolve it from an explicit `GODOT_BIN` or from `RunSettings/RunConfiguration/EnvironmentVariables/GODOT_BIN` in `Ciallo/.runsettings.local`.
- The Ciallo editor plugin generates and refreshes this ignored local file after the project opens. If neither source exists, the project must first be opened once with a supported editor.
- Edit `.tscn`, `.tres`, and `.res` through Fennara's Godot object-model tools, not as serialized text.
- Use `project_settings` for saved Godot configuration, and scene-tree/node-property tools for authored nodes, attached scripts, instanced scenes, resources, and C# exports.
- Attached script paths from scene inspection can connect Godot configuration to the corresponding GitNexus C# symbols and flows.
- Managed runtime sessions and runtime scripts are useful when a claim depends on live state, user input, or workflow reachability. Scene screenshots and runtime captures provide visual evidence when needed.
- `validate_scene`, script diagnostics, and asset-import inspection provide focused checks after related changes; choose the check that matches the affected surface.

## Fennara Timeout Recovery

Fennara records calls under `user://.fennara/tool_logs/<session_id>/`:

- `calls.jsonl` records `received`, `started`, `completed`, and `failed`; final events include result and artifact paths.
- Simple results use `results/tool_<n>_<tool_name>.json`; artifact-producing results use `results/tool_<n>_<tool_name>/result.json` with related artifacts beside it.
- After a timeout, inspect these records to determine whether the call completed before retrying.

## QMD Documentation Search

- When the local `Ciallo` collection is available, use QMD to discover relevant Markdown and retrieve complete documents. Search snippets are leads, not sufficient context for documentation claims.
- For user-facing documentation, QMD is suited to prior documentation, GitNexus to C# structure and execution flows, and Fennara to Godot configuration and observed behavior.

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus. Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> Index stale? Run `node .gitnexus/run.cjs analyze` from the project root — it auto-selects an available runner. No `.gitnexus/run.cjs` yet? `npx gitnexus analyze` (npm 11 crash → `npm i -g gitnexus`; #1939).

## Always Do

- Must use `--skip-agents-md` and `--skip-skills` to update graph `gitnexus analyze`. Never update graph without `--skip-agents-md` and `--skip-skills`.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/Ciallo/context` | Codebase overview, check index freshness |
| `gitnexus://repo/Ciallo/clusters` | All functional areas |
| `gitnexus://repo/Ciallo/processes` | All execution flows |
| `gitnexus://repo/Ciallo/process/{name}` | Step-by-step execution trace |

<!-- gitnexus:end -->
