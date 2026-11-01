# Translating Ciallo

This guide covers Ciallo's translation workflow and Godot's automatic and explicit translation behavior.

## Translation tables and keys

Maintain application translations, including the embedded Keychain UI, in `Ciallo/Localization/TranslatableStrings.csv` and `Ciallo/Localization/LongStrings.csv`. Both use `key,en,zh_CN,zh_TW,ja,ko,de,fr`; fill every language column, including English.

- **Short text** belongs in `TranslatableStrings.csv`: button/menu names, field labels, mode names, and brief standalone status messages. Use the displayed English wording as the key, preserving case, spaces, and punctuation. A short tooltip that only names an action also belongs here.
- **Long text** belongs in `LongStrings.csv`: explanatory sentences, interaction instructions, detailed warnings, and multi-line help. Use a concise English summary in square brackets, such as `[Canvas Reference Size Hint]` or `[Stroke Radius Multiplier Tooltip]`. Put the full wording in each language column; the bracketed key itself is not display text. Classify by purpose, not a fixed character count or the property receiving it.
- Keep each key in exactly one table. When moving or renaming a key, update its C# and authored scene/resource references in the same change. Do not keep aliases for replaced keys.
- Preserve format placeholders such as `{0}` (C#) and `%s` (Keychain) in every translation. Translate the format before substituting values; do not build translation keys from runtime values. Keep user names, paths, device names, and other external data out of the tables. Generated layer names translate their format once when created and then remain editable document data.
- Store line breaks as literal `\n` sequences. The CSV importers unescape translations, but not keys. Quote fields containing commas or quotes using CSV escaping. Save as UTF-8.

## Updating translations

1. Review the release diff and the current UI call sites: authored controls, dynamically built menus/tool panels, enum labels, shortcut names, and composed dialogs. A nonempty CSV does not establish that every visible string has a matching key.
2. Classify the wording and add all seven translations. Reuse an existing key only when the wording and meaning agree. Keep explanatory text behind a bracketed key even if it fits on one line. Shortcut action and group labels use humanized Title Case; translate their display keys without renaming persisted input actions.
3. Assign keys directly to auto-translated properties. Translate explicitly only at composition or custom-drawing boundaries, as described below. Do not translate user-authored names.
4. Edit authored `.tscn`, `.tres`, and `.res` properties through Fennara's Godot object model. Let Godot reimport the CSV files to regenerate `.translation` resources; do not edit those generated resources by hand. Both tables' seven generated resources are registered in Project Settings.
5. Check for duplicate keys, missing language cells, changed placeholders, stale references, and untranslated new UI strings. Build changed C# code and verify the imported translations in Godot, including English and a non-English locale. Check language switching and representative long text in the affected UI.

## Translation resources and locale

Godot loads `Translation` resources registered in Project Settings at `internationalization/locale/translations`. The current locale is managed by `TranslationServer`; changing it causes the scene tree to receive translation-changed notifications. Godot looks up source strings in the registered translations for that locale and any configured fallback. A supported UI property does not create a translation for a missing key.

Translation keys are source strings. Keep the text assigned to a control identical to the key in the translation resource, including punctuation and whitespace. Engine-owned labels can also use translations shipped with Godot.

## Automatic translation in the scene tree

Nodes have an `auto_translate_mode` with `Inherit`, `Always`, and `Disabled` values. `Inherit` follows the nearest ancestor with an explicit mode. A scene-tree root cannot remain `Inherit`; Godot changes it to `Always` when it enters the tree. A subtree can opt out with `Disabled` or opt back in with `Always`.

Automatic translation is implemented by individual properties, not by every string in an application. Common examples include `Button.Text`, `Label.Text`, `Window.Title`, popup/list item text, and `Control.TooltipText`. Tooltips have their own `tooltip_auto_translate_mode`, which defaults to `Inherit`; some item-based controls also expose a mode per item. Check the specific class property when behavior is unclear.

Built-in controls that support automatic translation retain their assigned source text. When the locale changes, they translate that source again in response to the translation-changed notification. For example, assign source keys directly:

```csharp
button.Text = "Save";
dialog.DialogText = "Save changes?";
```

Calling `TranslationServer.Translate()` or `Node.Tr()` first returns a plain string for the current locale; it does not preserve a separate source key on the control. Assigning that result to an auto-translated property can therefore make later locale changes fail: the control will look up the translated result as though it were the original key. It may appear to work when the current result happens to equal the source key.

## Dialogs and other compound controls

`ConfirmationDialog` is not a special translation exception. In Godot's implementation, `AcceptDialog` forwards its message to an internal `Label`, and its buttons are `Button` controls. Their text follows the same automatic translation rules as other labels and buttons. If a dialog shows mixed languages, check each displayed string, its registered key, and the effective auto-translation mode of the dialog and its ancestors.

The same principle applies to composite controls: a built-in child `Label` or `Button` can translate its own source text, but arbitrary strings assembled by application code are not automatically translated as a whole.

## Explicit translation

Use `TranslationServer.Translate(source)` or `Node.Tr(source)` when a string is not assigned to an auto-translated property, such as custom-drawn text or a composed message. These APIs translate once using the current locale and return a normal string; they do not bind that result to future locale changes.

If an explicitly translated string remains visible across locale changes, keep its source key or format data and rebuild the display string when the locale changes. For composite messages, translate the application-owned wording and preserve external values such as paths or system error details. Text saved as data is also just data: decide whether to save a source key, the current translated value, or user-authored text according to the intended behavior.

## Engine source references

The behavior described here is implemented in `scene/main/node.cpp`, `scene/gui/control.cpp`, `scene/gui/button.cpp`, `scene/gui/label.cpp`, `scene/gui/popup_menu.cpp`, `scene/main/window.cpp`, and `scene/gui/dialogs.cpp`. Check the corresponding files for the Godot version being used when APIs or translation behavior change.
