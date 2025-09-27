# Ciallo Coding Styles

## Guidelines
- No defensive programming. Follow the "Let It Crash" philosophy.
  - About ["Let It Crash"](https://stackoverflow.com/questions/4393197/erlangs-let-it-crash-philosophy-applicable-elsewhere).
  - Copilot, please stop adding redundant boundary checks. C# lists/arrays will throw on their own, unlike in C++.
  - I'm still learning how to make Ciallo robust while following this philosophy.
- Be mindful of Rider's suggestions, and follow most of them.
- Zero memory leaks.
  - After closing Ciallo's window in Rider debug, make sure zero Godot warning messages in the Rider console.
  - Running project in Godot editor seems lacking of necessary check. Must in Rider debug.
  - Sometimes the warnings are not your fault. Ask for review.

## Details
- Global variables or static classes (that need to work with) are named as "App*"
- No plural forms for class, folder, or namespace names.
  - Optionally, use plural names for collection-type variables, fields, and properties.
  - Ignore English countable/uncountable rules.
    - e.g., `Datas` is an acceptable name for a `List<Data>` object.
    - Counting rules in English are legacy system and not worth the effort.
- Stack (Last-In, First-Out) ordered operations for undo/redo, create/delete
