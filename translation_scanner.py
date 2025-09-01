#!/usr/bin/env python3
"""
Script to scan Godot project for translatable text in both C# and .tscn files
"""

import os
import re
import csv
import sys
from pathlib import Path
from typing import Set, List

def extract_cs_strings(file_path: str) -> Set[str]:
    """Extract translatable strings from C# files"""
    translatable_strings = set()
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
    except UnicodeDecodeError:
        print(f"Warning: Could not read {file_path} with UTF-8 encoding")
        return translatable_strings
    
    # Remove comments to avoid extracting text from comments
    content = re.sub(r'//.*$', '', content, flags=re.MULTILINE)
    content = re.sub(r'/\*.*?\*/', '', content, flags=re.DOTALL)
    
    # Menu items and UI text (strings in dictionaries, arrays, or assignments)
    menu_pattern = r'{\s*"([^"]+)"\s*,'
    matches = re.findall(menu_pattern, content)
    for match in matches:
        if is_translatable_text(match):
            translatable_strings.add(match)
    
    # Error messages and user-facing strings
    error_pattern = r'(?:errorMessage\.Text|Text|ActionName)\s*=\s*"([^"]+)"'
    matches = re.findall(error_pattern, content)
    for match in matches:
        if is_translatable_text(match):
            translatable_strings.add(match)
    
    # String interpolation with user-facing text
    interpolation_pattern = r'\$"([^"]*[a-zA-Z][^"]*)"'
    matches = re.findall(interpolation_pattern, content)
    for match in matches:
        # Clean interpolation expressions but keep the text part
        cleaned = re.sub(r'{[^}]*}', '', match).strip()
        if is_translatable_text(cleaned):
            translatable_strings.add(cleaned)
    
    # Warning/Error messages in GD.Print calls
    gd_print_pattern = r'GD\.Print(?:Err|Warning)\s*\(\s*\$?"([^"]+)"'
    matches = re.findall(gd_print_pattern, content)
    for match in matches:
        if is_translatable_text(match):
            translatable_strings.add(match)
    
    # Exception messages
    exception_pattern = r'throw new \w+Exception\s*\(\s*"([^"]+)"'
    matches = re.findall(exception_pattern, content)
    for match in matches:
        if is_translatable_text(match):
            translatable_strings.add(match)
    
    return translatable_strings

def extract_tscn_strings(file_path: str) -> Set[str]:
    """Extract translatable strings from .tscn files"""
    translatable_strings = set()
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
    except UnicodeDecodeError:
        print(f"Warning: Could not read {file_path} with UTF-8 encoding")
        return translatable_strings
    
    # Patterns for various text properties in .tscn files
    patterns = [
        r'text = "([^"]+)"',
        r'tooltip_text = "([^"]*)"',  # Allow empty tooltips
        r'title = "([^"]+)"',
        r'placeholder_text = "([^"]+)"',
        r'ok_button_text = "([^"]+)"',
        r'item_\d+/text = "([^"]+)"',
    ]
    
    for pattern in patterns:
        matches = re.findall(pattern, content, re.MULTILINE | re.DOTALL)
        for match in matches:
            # Clean up multiline text and BBCode
            cleaned = clean_tscn_text(match)
            if cleaned and is_translatable_text(cleaned) and not is_technical_string(cleaned):
                translatable_strings.add(cleaned)
    
    return translatable_strings

def clean_tscn_text(text: str) -> str:
    """Clean text from .tscn files (remove BBCode, normalize whitespace)"""
    # Remove BBCode tags
    text = re.sub(r'\[/?[^\]]*\]', '', text)
    
    # Normalize whitespace and newlines
    text = ' '.join(text.split())
    
    return text.strip()

def is_translatable_text(text: str) -> bool:
    """Check if text is likely translatable (user-facing)"""
    if not text or len(text) < 2:
        return False
    
    # Skip very short strings
    if len(text) < 3:
        return False
    
    # Skip strings that are just numbers or symbols
    if text.isdigit() or not re.search(r'[a-zA-Z]', text):
        return False
    
    # Skip single characters or very technical strings
    if len(text) == 1 or text.startswith('%') or text.startswith('uid://'):
        return False
    
    # Skip strings with code patterns
    if re.search(r'[{}()\[\]<>]', text) and not text.endswith('?)'):
        return False
    
    # Skip strings that are mostly variable references or code
    if re.search(r'\{[^}]*\}', text) and not text.replace(re.findall(r'\{[^}]*\}', text)[0], '').strip():
        return False
    
    return True

def is_technical_string(text: str) -> bool:
    """Check if string is technical/non-translatable"""
    technical_patterns = [
        r'^res://',  # Resource paths
        r'^uid://',  # UIDs
        r'^\d+$',    # Pure numbers
        r'^[A-Z_]+$', # Constants
        r'\.cs$',    # File extensions
        r'\.tscn$',  # File extensions
        r'\.png$',   # File extensions
        r'\.svg$',   # File extensions
        r'^%\w+',    # Node references
        r'^\w+\(\)', # Method calls
        r'^[a-z_]+$', # Simple lowercase identifiers
        r'SignalName\.',  # Signal names
        r'TreeExited',    # Signal names
        r'ReactiveProperty', # Type names
        r'Value$',        # Property names
        r'^Name$',        # Property names
        r'^Path$',        # Property names
        r'Exception$',    # Exception types
        r'^\w+:\w+$',     # Key-value pairs
        r'^\d+\.\d+$',    # Version numbers
        r'F\d+',          # Format specifiers
        r'\{[^}]*\}.*\?', # Code fragments with conditionals
        r'FileMode\s*==', # Code comparisons
    ]
    
    for pattern in technical_patterns:
        if re.search(pattern, text):
            return True
    
    # Skip very short technical words
    if len(text) <= 4 and text.lower() in ['true', 'false', 'null', 'void', 'var', 'new', 'get', 'set']:
        return True
    
    # Skip strings that contain mostly code patterns
    if len([c for c in text if c in '{}()[]<>=']) > len(text) // 3:
        return True
    
    return False

def scan_project(project_path: str) -> Set[str]:
    """Scan entire project for translatable strings"""
    all_strings = set()
    
    project_root = Path(project_path)
    
    # Scan C# files
    cs_files = list(project_root.rglob("*.cs"))
    print(f"Found {len(cs_files)} C# files")
    
    for cs_file in cs_files:
        strings = extract_cs_strings(str(cs_file))
        all_strings.update(strings)
        print(f"Scanned {cs_file.name}: {len(strings)} strings")
    
    # Scan .tscn files
    tscn_files = list(project_root.rglob("*.tscn"))
    print(f"Found {len(tscn_files)} .tscn files")
    
    for tscn_file in tscn_files:
        strings = extract_tscn_strings(str(tscn_file))
        all_strings.update(strings)
        print(f"Scanned {tscn_file.name}: {len(strings)} strings")
    
    return all_strings

def write_csv(strings: Set[str], output_path: str):
    """Write strings to CSV file"""
    sorted_strings = sorted(strings)
    
    with open(output_path, 'w', newline='', encoding='utf-8') as csvfile:
        writer = csv.writer(csvfile)
        writer.writerow(['Text to Translate'])  # Header
        
        for string in sorted_strings:
            writer.writerow([string])
    
    print(f"Wrote {len(sorted_strings)} translatable strings to {output_path}")

def main():
    if len(sys.argv) != 2:
        print("Usage: python3 translation_scanner.py <project_path>")
        sys.exit(1)
    
    project_path = sys.argv[1]
    
    if not os.path.exists(project_path):
        print(f"Error: Project path {project_path} does not exist")
        sys.exit(1)
    
    print(f"Scanning project: {project_path}")
    
    # Scan for translatable strings
    translatable_strings = scan_project(project_path)
    
    # Write to CSV
    output_path = os.path.join(project_path, "translatable_strings.csv")
    write_csv(translatable_strings, output_path)
    
    print(f"Found {len(translatable_strings)} unique translatable strings")

if __name__ == "__main__":
    main()