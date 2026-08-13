#!/usr/bin/env python3
"""
Fix xUnit1051 warnings by adding TestContext.Current.CancellationToken to method calls.
This script reads the warning locations and intelligently patches the code.
"""

import re
import sys
from pathlib import Path
from typing import Dict, List, Tuple

def read_warning_locations(file_path: str) -> Dict[str, List[Tuple[int, int]]]:
    """Parse the warning locations file and group by file path."""
    locations = {}
    with open(file_path, 'r') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            # Parse: F:\path\to\file.cs(line,col)
            match = re.match(r'^(.+?)\((\d+),(\d+)\)$', line)
            if match:
                file_path = match.group(1)
                line_num = int(match.group(2))
                col_num = int(match.group(3))

                if file_path not in locations:
                    locations[file_path] = []
                locations[file_path].append((line_num, col_num))

    return locations

def fix_line_at_column(line: str, col: int) -> str:
    """
    Add TestContext.Current.CancellationToken at the specified column position.
    The column points to where the token should be added.
    """
    # Common patterns:
    # 1. Method call with no args: SomeAsync()  -> SomeAsync(TestContext.Current.CancellationToken)
    # 2. Method call with args: SomeAsync(arg1, arg2)  -> SomeAsync(arg1, arg2, TestContext.Current.CancellationToken)
    # 3. await using: await LoadAsync(...)  -> await LoadAsync(..., TestContext.Current.CancellationToken)

    # Find the method call that ends near this column
    # Look for patterns like "Async(" or method calls

    # Strategy: Find the closing parenthesis after the column position
    # and insert the token before it

    # Find the next ')' after column position
    search_start = col - 1  # Convert 1-based to 0-based
    paren_pos = line.find(')', search_start)

    if paren_pos == -1:
        return line  # Can't find closing paren

    # Check if there are already arguments
    # Find the matching opening paren
    open_paren = line.rfind('(', 0, paren_pos)
    if open_paren == -1:
        return line

    # Get the content between parens
    args_content = line[open_paren + 1:paren_pos].strip()

    if not args_content:
        # No arguments, add the token
        new_line = line[:paren_pos] + 'TestContext.Current.CancellationToken' + line[paren_pos:]
    else:
        # Has arguments, append with comma
        new_line = line[:paren_pos] + ', TestContext.Current.CancellationToken' + line[paren_pos:]

    return new_line

def process_file(file_path: str, locations: List[Tuple[int, int]]) -> bool:
    """Process a single file and fix all xUnit1051 warnings in it."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()

        # Sort locations by line number in reverse to avoid offset issues
        sorted_locs = sorted(locations, key=lambda x: (x[0], x[1]), reverse=True)

        modified = False
        for line_num, col_num in sorted_locs:
            if 1 <= line_num <= len(lines):
                original = lines[line_num - 1]  # Convert to 0-based index
                fixed = fix_line_at_column(original, col_num)
                if fixed != original:
                    lines[line_num - 1] = fixed
                    modified = True
                    print(f"  Line {line_num}: Fixed")
                else:
                    print(f"  Line {line_num}: Could not auto-fix (manual intervention needed)")

        if modified:
            with open(file_path, 'w', encoding='utf-8', newline='\n') as f:
                f.writelines(lines)
            return True

        return False
    except Exception as e:
        print(f"  Error processing {file_path}: {e}")
        return False

def main():
    if len(sys.argv) < 2:
        print("Usage: python fix_xunit1051.py <warnings_file>")
        sys.exit(1)

    warnings_file = sys.argv[1]

    print("Reading warning locations...")
    file_locations = read_warning_locations(warnings_file)

    print(f"Found warnings in {len(file_locations)} files")

    total_fixed = 0
    for file_path, locations in file_locations.items():
        print(f"\nProcessing {Path(file_path).name} ({len(locations)} locations)...")
        if process_file(file_path, locations):
            total_fixed += 1

    print(f"\n✓ Fixed {total_fixed} files")

if __name__ == '__main__':
    main()
