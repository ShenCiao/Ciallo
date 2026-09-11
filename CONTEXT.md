# Context

## Terminology Basis

The timeline model follows **Clip Studio Paint**: a cel folder holds cels, a cel is
specified at a frame, and that cel shows until the next specification.

CSP names the *actions* and the *objects* — Specify cel / Assign cels (セル指定),
Animation folder (アニメーションフォルダー), blank cel (空セル) — but leaves unnamed both
the stored *assignment* and the *interval* that assignment produces. This project takes
both names from the X-sheet tradition: **Exposure** for the assignment (one cel exposed
starting at one frame) and **Exposure Span** for the interval it covers.

The word "exposure" carries duration by origin — to expose a drawing for N frames — so
Harmony attaches it to the interval (`Hold Exposure` / `Extend Exposure`). Here the
duration lives in the span instead, because the assignment is what the document actually
stores and the interval is derived from it. Readers coming from Harmony should note this
is not an X-sheet: there is no column of per-frame cells, so there is nothing to "hold".
An exposure exists once, at its key frame, and its span is implied by the next key.

So the split is deliberate: CSP defines the behavior, X-sheet terms supply the nouns.
Where CSP does have a name and it does not collide, prefer CSP's — hence **Blank**
(CSP `blank cel`, Toei timesheet 空セル) rather than "Empty".

## Glossary

### Document

A document is complete editable animation content with a stable identity whose individual saved and recovered versions each use one `.ciallo` file.

### Working Document

The working document is the one document currently open for editing in a Ciallo process, and is absent when that process has no document open.

### Editing Session

An editing session is one device's continuous period of working on a document between opening and closing it.

### Interrupted Editing Session

An interrupted editing session is an editing session that did not close normally and has reported no activity for 15 minutes.

### Document Identity

A document identity distinguishes one document across changes to its name, local path, and editing device.

### Recovery Snapshot

A recovery snapshot is an automatically captured version of the working document reserved for restoring work after an unexpected interruption. It stores committed document content and excludes command history. It is never a manually saved document version.

### Recovery Retention

Recovery retention is the per-document recovery snapshot history maintained within the account-wide storage limit. The same policy applies to local snapshots and their Steam Cloud copies.

### Recovered Working State

A recovered working state is a recovery snapshot opened as unsaved working document content after an unexpected interruption.

### Cloud Protection Point

A cloud protection point is the newest recovery snapshot confirmed to be stored in Steam Cloud. Cloud protection stores the same content as the local recovery snapshot; it does not store a separate manually saved document version.

### Cloud Authorization

A cloud authorization is a Steam user's scoped permission for Ciallo to read and write that user's recovery snapshots and editing-session records in Steam Cloud.

### Cel

A cel is a direct child layer of a cel folder, whether or not any exposure currently exposes it.

### Cel Button

A cel button is the clickable timeline control for one exposure on a CelTrack, drawn at its exposure key. Cel exposures and Blank exposures are visually distinguishable from each other, and both support the same click, drag, replace, delete, and undo workflows.

### Frame Cell

A frame cell is the timeline display interval for one frame number `n`, from `n` inclusive to `n + 1` exclusive. Its width follows the timeline zoom (`PixelsPerFrame`). Right-clicking a CelTrack fills the clicked frame cell across the full track height with the track accent color while the menu is open. The same accent color marks valid drag destinations, dragged exposure arrows, and selected-track borders.

### Cel Folder

A cel folder is a folder layer whose children are cels. Cel folders do not nest inside other cel folders.

### Cel Child Archetype

A cel child archetype groups every same-named direct child layer across one cel folder's cels. Names starting with `_` or `!` are excluded. An archetype exists even when only one layer has its name. Its display reflects one representative member; visibility edits apply to every member of the targeted archetypes. Renaming applies to the clicked archetype's members and updates its selection-template name. Renaming to an existing name combines the groups and removes duplicate names from the template.

The timeline archetype selection button edits an ordered selection template within one cel folder. A checkmark means that name participates across cels; the brush marks the current cel's actual primary layer. A missing name remains checked with a dimmed button and an explanatory tooltip. Clicking a name selects only that archetype. Additional buttons toggle names; the template's first name cannot be deselected with its button. Clicking an archetype in another cel folder switches the editing context, and each folder retains its own template.

Right-clicking a selected archetype targets the selected archetype names in its folder. Right-clicking an unselected archetype targets only that name without changing selection when opening the menu. The menu works even on Blank exposures or cels with no matching layers, and labels its scope as All Cels. It expands names to all matching direct children, including duplicate names and unexposed cels, then builds one ordered operation unit per cel. Repeated exposures never repeat an operation on the same cel.

- Delete and Split Stroke and Fill affect every matching member. Split retains the source entities for strokes and follows their new stroke names in the selection template.
- Merge and Group Layers run independently within each cel. Missing members are omitted; merge skips units with fewer than two layers. Incompatible member types disable the entire operation. Each unit's first available layer supplies the result name and settings for merge; grouping uses that layer's name. The template retains the result names in their original relative order, so a cel missing A may produce B while other cels produce A. Visual stacking follows the source layer order.
- Ungroup Folder and Wrap Children in Folders affect all targeted folder members. Ungroup selects the promoted children by name, with each folder's topmost child first.
- New Shape Layer and New Folder Layer create one same-named layer per matching cel, directly above that cel's primary member. Cels without matching members receive no new layer. The new archetype becomes the selection template. The cel folder header's Add Shape Layer to All Cels adds at the top of all folder cels.

Each batch action is one undoable action. Undo/redo restores structure, content, references, the selection template, and the current selection. Layer selection after a batch resolves only within the currently exposed cel; batches do not change exposures or the playhead. The layer panel continues to operate on its concrete selected layers.

### Cel Layer Selection Template

`FolderLayerSetting.PreferredNamesForCelSelection` stores the ordered names to follow when navigating between cels. Explicit selection of direct cel children records the names of selected siblings in primary-first selection order. Archetype selection can include names missing from the current cel. Scrubbing, cel-button navigation, playback completion, and new-cel creation resolve the template without overwriting it with a partial result.

Names resolve in template order. Every same-named direct child is included in layer order, and the first resolved layer becomes the primary layer. If A is missing from `[A, B, C]`, the actual selection becomes `[B, C]`; returning to a complete cel restores `[A, B, C]`. Blank exposures, frames before the first exposure, and cels with no matches select the cel folder as a navigation context without selecting a drawable layer. A non-folder cel selects the cel itself. Navigation outside a cel folder leaves the existing selection untouched.

Templates are saved with each cel folder and restored independently of the current concrete selection. New documents begin with their initial shape layer selected. Older saved selection preferences may be discarded when the selection schema changes.

### Folder layer
Any layer's parent must be a folder layer. The document entity is a folder layer entity.

### Selected Layers

Selected layers are an ordered selection without duplicates, stored in `SelectionManager.SelectedLayers`. Clicking a layer name selects only that layer. Its selection button toggles additional membership, while the primary layer's button cannot deselect it. Navigating between cels resolves the cel folder's ordered selection template. Saved selection state may be discarded when its schema changes.

Right-clicking a selected layer targets the whole selection. Right-clicking an unselected layer targets only that layer and leaves the selection unchanged. Delete, group, split, and drag operate on these targets as one undoable action. Structural operations treat a selected ancestor as covering its selected descendants and preserve visual stacking order. Visibility and the layer panel's opacity, mark color, blend mode, and clipping controls edit the selected layers together.

### Primary Layer

The primary layer is the first selected layer, exposed as the read-only `PrimaryLayer` property. It shows a brush icon; additional selected layers show checkmarks. Current drawing tools judge and edit only the primary layer. An empty selection has no primary layer (`Entity.Null`).

### Layer Merge

Merge accepts two or more Shape or Vector Fill layers with the same parent, excluding direct cels. All Vector Fill markers are materialized as filled polygons from their current bounded faces before any source layer changes. The result is one Shape layer at the primary layer's position, with shapes ordered by the source layers' visual stacking. The primary layer supplies the name and layer settings, even when this changes the appearance.

A Shape primary retains its entity and existing incoming Vector Fill references. References to other merged layers are removed rather than redirected. A Vector Fill primary becomes a new Shape layer with its common settings. Undo restores layers, selection, and removed references. Merge is unavailable while a source Vector Fill arrangement is rebuilding. Layers in different cels cannot be merged or grouped into one folder; they can still be selected, deleted, and moved together.

### Exposure

An exposure is one authored assignment on a cel folder, pairing an exposure key with the cel, or Blank, exposed from that key onward. It is the unit stored in a cel folder's exposure list and the unit a user adds, moves, replaces, and deletes. An exposure does not store its own length; the length is its exposure span.

### Exposure Key

An exposure key is the frame at which an exposure begins, and is the key half of that exposure. No two exposures on the same cel folder share a key.

### Exposed Cel

The exposed cel is the value half of an exposure: the cel shown throughout that exposure's span. It is Blank when the value is the owning cel folder itself.

### Exposure Span

An exposure span is the interval an exposure covers. It begins at that exposure's key and ends where the next exposure's key on the same cel folder begins, or at the playback end when no exposure follows. A span is always at least one frame long, and is never stored: it is implied by the distance to the next key, so adding, moving, or deleting an exposure changes the spans around it.

Drag an exposure arrowhead to adjust its duration.

### Blank Exposure

A Blank exposure is an exposure whose exposed cel is Blank, giving the cel folder an explicit empty span. In `FolderLayerSetting.Exposures`, any value whose entity is a CelFolder represents Blank; authored Blank values are self-references to the owning CelFolder. Blank displays no current layer, contributes a blank onion-skin exposure offset, and preserves the cel folder as the primary layer so later cel navigation can recover the preferred cel child.

Blank is an authored exposure, not the absence of one: a frame that no exposure's span covers is not Blank. This is why Blank occupies an exposure of its own.

### Frame Sequence

A frame sequence is an animation export made of one still image file per timeline frame.

### Onion Skin

Onion skin is a timeline viewing mode that shows nearby cel frames around the current frame as drawing references.

### Vector Fill Layer

A vector fill layer is a fill layer whose filled regions are bounded by reference layers.

### Reference Layer

A reference layer is a shape layer whose strokes provide boundary artwork for a vector fill layer. Only sampled polylines with a `StrokeSetting` component contribute to its arrangement; filled polygons do not form boundaries. Splitting stroke and fill retains the original layer entity for strokes, so its incoming references remain valid.

### Shape

A shape is a selectable drawable object inside a shape-editable layer. The Select tool can select shapes independently of layer selection.

### Shape Clipboard

A shape clipboard is temporary copied or cut shape content that can later be pasted into a compatible primary layer. It is distinct from layer-level copy and paste.

### Shape Paste

A shape paste creates new shapes from shape clipboard content in the current primary layer. It is distinct from layer-level paste.

### Stroke Prediction

A stroke prediction is a transient visual extension of an in-progress stroke. It may be replaced by later input and is not saved or treated as committed drawing intent.

### Stroke Preview

A stroke preview is the user-visible form of an in-progress stroke. It may include stable stroke samples and transient stroke predictions.

### Paint Stroke Snap Target

A paint stroke snap target is the reference curve and curve-local hit position that a paint stroke endpoint may snap through when committed.

### Paint Stroke Snap Hint

A paint stroke snap hint is the user-visible indicator that shows an available paint stroke snap target during hover or drawing.

### Gap Bridge

Gap Bridge repairs a visual gap by deforming a dangling endpoint of the source shape toward a target.

### Command History

Command history is a document-level record of undoable user changes.

### Undoable Action

An undoable action is one user-facing history entry that can be undone or redone as a unit. One undoable action may stay open long enough to gather multiple related command segments before the next action begins.

### Command Segment

A command segment is one ordered part of an undoable action. Related gestures may contribute multiple ordered command segments to the same undoable action.

## Relationships

- Every **Document** has exactly one **Document Identity**.
- Saving a **Document** as a new file creates a new **Document** with a new **Document Identity**.
- A **Recovery Snapshot** belongs to exactly one **Document**.
- A **Recovery Snapshot** contains only completed **Undoable Actions** and excludes an interaction still in progress.
- A **Recovery Snapshot** excludes **Command History**.
- A **Recovery Snapshot** from an active **Editing Session** becomes a recovery candidate only after that session becomes an **Interrupted Editing Session**.
- **Recovery Retention** may remove the oldest **Recovery Snapshots**, locally and in Steam Cloud, using the same limits.
- A **Recovered Working State** does not become a manually saved **Document** until the user saves it.
- Steam Cloud work protection copies **Recovery Snapshots** and **Editing Session** records. It does not copy manually saved document files.
- A **Cloud Protection Point** may lag behind the newest local **Recovery Snapshot** while cloud storage is unavailable or an upload is pending.
- Cloud operations require a **Cloud Authorization** belonging to the Steam user currently running Ciallo.
- A **Cloud Authorization** expires 30 days after issuance and must then be renewed by the user.
- A **Cel Folder** holds zero or more **Exposures**, each at a distinct **Exposure Key**.
- Every **Exposure** has exactly one **Exposure Span**, which is derived rather than stored.
- An **Exposure** exposes exactly one **Cel**, or is a **Blank Exposure**.
- A **Cel** may be exposed by zero or more **Exposures**; reusing one cel across several exposures is how a drawing repeats.
- Adding, moving, or deleting an **Exposure** changes the **Exposure Spans** of its neighbours without changing their exposed cels.
- A **Vector Fill Layer** can have zero or more **Reference Layers**.
- A **Reference Layer** can provide boundary artwork for zero or more **Vector Fill Layers**.
- When editing reference artwork from a **Vector Fill Layer**, the edited **Shape** remains owned by its original **Reference Layer**.

## Flagged Ambiguities

- "Project" was used to mean a **Document** stored in one `.ciallo` file; **Document** is the canonical term and does not contain multiple documents.
- "One `.ciallo` file" describes the format of one **Document** version, not a single physical copy across all devices.
- "Autosave" was used to mean both recovery and normal document saving; **Recovery Snapshot** is non-authoritative, and normal reopening after choosing not to save uses the last manually saved **Document** version.
- "Cloud saved version", "document version conflict", and "managed cloud copy" described a Steam-netdisk model that work protection does not use. Steam Cloud work protection mirrors **Recovery Snapshots**, not manually saved files.
