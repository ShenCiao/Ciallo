# Translation Scanner

This script scans the Godot project for translatable text in both C# code files and Godot scene files (.tscn).

## Usage

```bash
python3 translation_scanner.py <project_path>
```

For example:
```bash
python3 translation_scanner.py .
```

## Output

The script generates a CSV file named `translatable_strings.csv` with one column containing all the translatable text found in the project.

## What it scans

### C# files (.cs)
- Menu items and UI text from dictionaries and arrays
- Error messages and user-facing text assignments
- String interpolation with user-facing content
- Warning/error messages in GD.Print calls
- Exception messages

### Godot scene files (.tscn)
- text properties
- tooltip_text
- title properties
- placeholder_text
- ok_button_text
- item_*/text (menu items)

## Filtering

The script automatically filters out:
- Technical strings (file paths, UIDs, node references)
- Code fragments and variable names
- Very short strings (< 3 characters)
- Strings that are mostly symbols or numbers
- BBCode formatting tags (cleaned from .tscn files)

## Results

The script found **74 unique translatable strings** in this project, including:
- User interface text
- Error messages and warnings
- Tooltips and help text
- Menu items and button labels
- Dialog titles and prompts