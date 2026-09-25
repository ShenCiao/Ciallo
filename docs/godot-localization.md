# Godot localization behavior

This guide describes Godot's built-in localization behavior: how translations are registered, how scene-tree nodes translate supported text properties, and when code must translate a string explicitly.

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
