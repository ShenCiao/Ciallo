# Layer selection API cleanup

Work remaining after multi-layer selection (`86e16723`) and the terminology rename (`a2c0737`). Product rules live in CONTEXT (`Selected Layers`, `Primary Layer`, `Layer Merge`). This note is the implementation debt those commits left: command and GUI APIs still speak single-entity while the store is an ordered selection.

Do not relitigate the rename. Do not change CONTEXT behavior. Collapse duplication rather than adding helpers that restate the same boolean.

## Required command shape

Selection writes only through `ImmutableArray<Entity>`:

```csharp
SelectLayers(ImmutableArray<Entity> layers, bool recordCelPreference = false)
```

- One layer: `SelectLayers([layer])`.
- Empty: `SelectLayers([])`.
- `recordCelPreference` is a named argument, never a leading positional `true`.

`SelectLayersCmd` still takes a `CommandBuilder` target plus constructor `layers` plus a leading `bool`. `default(ImmutableArray)` and `[]` mean different things. `Do`/`Undo` ignore `newLayerE`; the target is only `CommandBase.Document` and a fallback payload. Call sites currently mix `SelectLayers()`, `SelectLayers(true)`, `SelectLayers(layers: survivors)`, and `SelectLayers(true, layers)` (see `LayerContextActions.SplitStrokeAndFill`). Document entity as “clear selection” is not part of the store; stop using it as a command payload.

## Resolve vs commit

`ComputePrimaryLayerForCelButtonSelection` encodes the no-op (`already primary && Length == 1` → `Entity.Null`). Timeline resolve does not. The same apply predicate is copied at:

- `TimelineAction` (navigate, playback)
- `TimelineRuler` (three frame-commit paths; `ResolvePrimaryLayerAfterFrameChange` is a pure forwarder)
- `TimelineRulerRightClickMenu`

The rule is: navigating cels resets the selection to the resolved primary, including when the primary entity is unchanged but extras are selected. Put “already `[resolved]` → do not write” on `SelectionManager` or in the command. Resolve functions only answer which layer. Callers must not each re-encode `Null` / document entity / empty array.

## Tree operations

`LayerContextActions.OperationRoots` lives under `GuiControl` and walks the whole document preorder on every delete, move, group, `CanGroup`, and merge compose. Ancestor covering is `O(selection × depth)`. Document order is a separate sort. Split those; put both on `LayerTreeNode`.

`LayerTreeNode.GetNextFocusPathAfterDeletion` is unused. `DeleteLayers` finds the next focus with `LayerOrder` + `IndexOf(layers[0])`. That is not “next sibling > previous sibling > parent”: deleting at the end of the tree can land on the previous preorder descendant instead of the previous sibling. Use the existing path helper or replace it; do not leave both.

`Merge` uses `OperationRoots` only for visual stacking order, not covering. `CanMerge` checks the raw `layers` (same parent, shape/vector-fill, not cels, arrangement ready). Do not pretend merge needs roots.

## Group

`CanGroupLayers` relies on `&&` binding tighter than `||` (`Length == 1` always allowed). Multi-select merge requires the same parent; group only requires the same containing cel, then computes LCA and child index with two while-walks inside `GroupLayers`. That belongs on `LayerTreeNode` (common ancestor + index under that ancestor).

The group folder is named from `layers[0]` (selection primary, which may be covered and absent from `roots`) while the structure follows `roots`. Single-select goes through `WrapSelfInFolder`; multi-select through LCA. The menu item is still `WrapSelfInFolder` for both labels.

`MoveLayers` migrates cel exposures through `AppendMoveLayer`. Multi `GroupLayers` calls `MoveLayer` directly and only stays safe because `CanGroup` rejects cels. Exposure migration belongs on the move primitive.

## Duplicated insert position

`LayerAction.GetNewLayerInsertPosition` and `LayerContextActions.GetNewLayerInsertPosition` are the same function. `OnNewShapeLayer` already calls `LayerContextActions.NewShapeLayer`; `OnRemoveLayer` already calls `DeleteLayers`. `OnNewFolderLayer` still inlines `CommandBuilder` instead of `LayerContextActions.NewFolderLayer`.

## Two PrimaryLayer contracts

`SelectionManager.PrimaryLayer` is `Entity.Null` when the selection is empty. `InteractionState.PrimaryLayer` is `SelectedLayers.First()` and throws on empty. Tools only judge `layers[0]`; empty-selection semantics must be one definition. `HasUsableLayers` currently keeps tools off empty snapshots, which is why this has not thrown.

## Smaller leftovers (fix if touching the file)

- `SelectOnly` always `SetProperty` on `CurrentFrame` even when the frame is unchanged. The previous working-layer path skipped that.
- `UngroupFolders` builds the new selection as `roots.Reverse().SelectMany(children.Reverse())` with no obvious primary.
- Single-layer delete still commits as `"Delete Layers"`.
- `Merge` assigns compose-time child indices onto a live primary. It happens to match `MoveChild` post-removal coordinates; prefer an explicit final child list.
- Do not churn leftover `Working*` UI names (`WorkingButton`, `WorkingCelFolder`) unless the same edit already owns that surface.
