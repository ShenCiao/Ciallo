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

### Cel Folder

A cel folder is a folder layer whose children are cels. Cel folders do not nest inside other cel folders.

### Cel Child Archetype

A cel child archetype is a shared editable setting grouped by layer name across the cel children of one cel folder. Cel children are the layers nested inside the cels (the grandchildren of the cel folder), grouped by name. Editing an archetype applies its values one-way to every cel child layer that currently shares that name, overwriting their prior values. Renaming an archetype renames all those layers; if the new name already names another group, the groups merge. An archetype exists for every distinct name, including names used by only one layer.

### Preferred Cel Child Name

A preferred cel child name is a cel folder's runtime memory of which cel child layer, by name, the working layer should follow when navigating between cels. Navigating to a cel (clicking a cel button or scrubbing the timeline) resolves the working layer to the same-named cel child under the newly exposed cel. When no cel child under that cel matches the name, no layer is selected. Navigating to Blank selects the cel folder itself without changing the preferred name. It is set only when the working layer becomes a direct cel child, and is empty by default.

### Folder layer
Any layer's parent must be a folder layer. The document entity is a folder layer entity.

### Exposure

An exposure is one authored assignment on a cel folder, pairing an exposure key with the cel, or Blank, exposed from that key onward. It is the unit stored in a cel folder's exposure list and the unit a user adds, moves, replaces, and deletes. An exposure does not store its own length; the length is its exposure span.

### Exposure Key

An exposure key is the frame at which an exposure begins, and is the key half of that exposure. No two exposures on the same cel folder share a key.

### Exposed Cel

The exposed cel is the value half of an exposure: the cel shown throughout that exposure's span. It is Blank when the value is the owning cel folder itself.

### Exposure Span

An exposure span is the interval an exposure covers. It begins at that exposure's key and ends where the next exposure's key on the same cel folder begins, or at the playback end when no exposure follows. A span is always at least one frame long, and is never stored: it is implied by the distance to the next key, so adding, moving, or deleting an exposure changes the spans around it.

### Blank Exposure

A Blank exposure is an exposure whose exposed cel is Blank, giving the cel folder an explicit empty span. In `FolderLayerSetting.Exposures`, any value whose entity is a CelFolder represents Blank; authored Blank values are self-references to the owning CelFolder. Blank displays no current layer, contributes a blank onion-skin exposure offset, and preserves the cel folder as the working layer so later cel navigation can recover the preferred cel child.

Blank is an authored exposure, not the absence of one: a frame that no exposure's span covers is not Blank. This is why Blank occupies an exposure of its own.

### Frame Sequence

A frame sequence is an animation export made of one still image file per timeline frame.

### Onion Skin

Onion skin is a timeline viewing mode that shows nearby cel frames around the current frame as drawing references.

### Vector Fill Layer

A vector fill layer is a fill layer whose filled regions are bounded by reference layers.

### Reference Layer

A reference layer is a shape layer that provides boundary artwork for a vector fill layer.

### Shape

A shape is a selectable drawable object inside a shape-editable layer. The Select tool can select shapes independently of layer selection.

### Shape Clipboard

A shape clipboard is temporary copied or cut shape content that can later be pasted into a compatible working layer. It is distinct from layer-level copy and paste.

### Shape Paste

A shape paste creates new shapes from shape clipboard content in the current working layer. It is distinct from layer-level paste.

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
