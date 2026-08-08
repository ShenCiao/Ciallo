# Context

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

A recovery snapshot is an automatically captured version of the working document reserved for restoring work after an unexpected interruption and is never a manually saved document version.

### Cloud Saved Version

A cloud saved version is a manually saved version of a document made available for normal opening on another device.

### Document Version Conflict

A document version conflict exists when multiple devices independently save different versions of the same document without observing each other's save.

### Recovery Retention

Recovery retention is the per-document recovery snapshot history maintained within the account-wide storage limit.

### Recovered Working State

A recovered working state is a recovery snapshot opened as unsaved working document content after an unexpected interruption.

### Cloud Protection Point

A cloud protection point is the newest document state confirmed to be stored in Steam Cloud.

### Cloud Authorization

A cloud authorization is a Steam user's scoped permission for Ciallo to read and write that user's document files in Steam Cloud.

### Managed Cloud Copy

A managed cloud copy is a device-local `.ciallo` file maintained by Ciallo for editing a document obtained from Steam Cloud.

### Cel

A cel is a direct child layer of a cel folder, whether or not it is currently assigned to an exposure.

### Cel Button

A cel button is the clickable timeline control for an exposure key on a CelTrack. A cel exposure uses a labeled bar with an outgoing hold arrow. An Empty exposure uses an unlabeled X with no outgoing arrow. Both forms support the same click, drag, replace, delete, and undo workflows.

### Cel Folder

A cel folder is a folder layer whose children are cels. Cel folders do not nest inside other cel folders.

### Cel Child Archetype

A cel child archetype is a shared editable setting grouped by layer name across the cel children of one cel folder. Cel children are the layers nested inside the cels (the grandchildren of the cel folder), grouped by name. Editing an archetype applies its values one-way to every cel child layer that currently shares that name, overwriting their prior values. Renaming an archetype renames all those layers; if the new name already names another group, the groups merge. An archetype exists for every distinct name, including names used by only one layer.

### Preferred Cel Child Name

A preferred cel child name is a cel folder's runtime memory of which cel child layer, by name, the working layer should follow when navigating between cels. Navigating to a cel (clicking a cel button or scrubbing the timeline) resolves the working layer to the same-named cel child under the newly exposed cel. When no cel child under that cel matches the name, no layer is selected. Navigating to Empty selects the cel folder itself without changing the preferred name. It is set only when the working layer becomes a direct cel child, and is empty by default.

### Folder layer
Any layer's parent must be a folder layer. The document entity is a folder layer entity.

### Exposure

An exposure is a timeline assignment that says which cel, or Empty, is shown from a frame until the next exposure on the same cel folder.

### Empty Exposure

An Empty exposure is an explicit blank interval on a cel folder. In `FolderLayerSetting.Exposures`, any value whose entity is a CelFolder represents Empty; authored Empty values are self-references to the owning CelFolder. Empty displays no current layer, contributes a blank onion-skin exposure offset, and preserves the cel folder as the working layer so later cel navigation can recover the preferred cel child.

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

A paint stroke snap hint is the user-visible dot that shows an available paint stroke snap target during hover or drawing.

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
- A **Document** has at most one current **Cloud Saved Version**.
- An unresolved **Document Version Conflict** has at least two candidate **Cloud Saved Versions** and no current version.
- Resolving a **Document Version Conflict** selects one candidate as current while leaving every other candidate available to become a new **Document**.
- A **Recovery Snapshot** belongs to exactly one **Document**.
- A **Recovery Snapshot** contains only completed **Undoable Actions** and excludes an interaction still in progress.
- A **Recovery Snapshot** excludes **Command History**.
- A **Recovery Snapshot** from an active **Editing Session** becomes a recovery candidate only after that session becomes an **Interrupted Editing Session**.
- **Recovery Retention** may remove the oldest **Recovery Snapshots** but never a **Cloud Saved Version** or an unresolved **Document Version Conflict** candidate.
- A **Recovered Working State** becomes a **Cloud Saved Version** only after the user actively saves it.
- A **Cloud Protection Point** may lag behind the newest local **Recovery Snapshot** while cloud storage is unavailable or an upload is pending.
- Cloud operations require a **Cloud Authorization** belonging to the Steam user currently running Ciallo.
- A **Cloud Authorization** expires 30 days after issuance and must then be renewed by the user.
- Opening a **Cloud Saved Version** on a device without a linked local file creates a **Managed Cloud Copy**.
- A **Vector Fill Layer** can have zero or more **Reference Layers**.
- A **Reference Layer** can provide boundary artwork for zero or more **Vector Fill Layers**.
- When editing reference artwork from a **Vector Fill Layer**, the edited **Shape** remains owned by its original **Reference Layer**.

## Flagged Ambiguities

- "Project" was used to mean a **Document** stored in one `.ciallo` file; **Document** is the canonical term and does not contain multiple documents.
- "One `.ciallo` file" describes the format of one **Document** version, not a single physical copy across all devices.
- "Autosave" was used to mean both recovery and normal document saving; **Recovery Snapshot** is non-authoritative, and normal reopening after choosing not to save uses the last manually saved **Document** version.
