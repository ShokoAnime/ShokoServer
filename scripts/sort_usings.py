#!/usr/bin/env python3
"""
Sorts C# using statements in .cs files.

Output order:
  1. System usings (alphabetical)
  2. Other usings (alphabetical)
  3. Static usings (alphabetical)
  4. Blank line
  5. Alias usings with '=' (alphabetical)
  6. Blank line
  7. ReSharper comments (// ReSharper ...)
  8. Blank line
  9. Pragma warnings (preserved from top of file)
  10. #nullable enable/disable (if present, preserved from top)
  11. namespace/class block

Usage:
  sort_usings.py [--strip-bom] [--remove-unused] [file1.cs file2.cs ...]
  sort_usings.py --stdin [--strip-bom] [--remove-unused]
  sort_usings.py --all [--strip-bom] [--remove-unused]

Options:
  --stdin           Read file paths from stdin (one per line) instead of positional args
  --all             Scan all .cs files in the current directory and subdirectories,
                    excluding paths listed in .gitignore (if present)
  --remove-unused   Remove unused usings via dotnet format (IDE0005) before sorting
"""

import argparse
import fnmatch
import glob
import os
import re
import subprocess
import sys

def _parse_gitignore(gitignore_path):
    """Parse a .gitignore file and return a list of (pattern, negated) tuples."""
    patterns = []
    try:
        with open(gitignore_path, 'r', encoding='utf-8', errors='ignore') as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith('#'):
                    continue
                negated = False
                if line.startswith('!'):
                    negated = True
                    line = line[1:]
                # Strip trailing comments (not inside quotes)
                if ' #' in line:
                    line = line[:line.index(' #')].rstrip()
                patterns.append((line, negated))
    except FileNotFoundError:
        pass
    return patterns


def _match_gitignore(rel_path, patterns):
    """Check if a relative path should be excluded by gitignore patterns.

    Returns True if the path should be excluded, False if included or no match.
    """
    excluded = False
    parts = rel_path.replace('\\', '/').split('/')

    for pattern, negated in patterns:
        # Match against full relative path
        if fnmatch.fnmatch(rel_path, pattern):
            excluded = not negated
            continue
        # Match against individual path components (for directory patterns)
        if '/' in pattern or pattern.endswith('/'):
            clean_pattern = pattern.rstrip('/')
            for i in range(len(parts)):
                sub_path = '/'.join(parts[:i + 1])
                if fnmatch.fnmatch(sub_path, clean_pattern):
                    excluded = not negated
                    break
        else:
            # Pattern without slash: match against basename too
            basename = parts[-1]
            if fnmatch.fnmatch(basename, pattern):
                excluded = not negated

    return excluded


def _find_cs_files(all_files=False):
    """Find all .cs files, optionally excluding .gitignore'd paths."""
    file_list = []

    # Find the closest .gitignore from current directory
    gitignore_patterns = []
    for candidate in ('.gitignore', '../.gitignore', '../../.gitignore'):
        if os.path.isfile(candidate):
            gitignore_patterns = _parse_gitignore(candidate)
            break

    for root, dirs, files in os.walk('.'):
        # Filter out directories that match gitignore patterns (in-place to prevent descent)
        if gitignore_patterns:
            filtered_dirs = []
            for d in dirs:
                dir_rel = os.path.join(root, d).lstrip('./')
                if not _match_gitignore(dir_rel, gitignore_patterns):
                    filtered_dirs.append(d)
                else:
                    # Mark for removal - use a sentinel approach
                    pass
            # We need to remove excluded dirs from dirs to prevent os.walk from descending
            excluded_dirs = {d for d in dirs if os.path.join(root, d).lstrip('./') in
                           [_match_gitignore(os.path.join(root, x).lstrip('./'), gitignore_patterns) and x for x in dirs]}
            # Simpler approach: rebuild dirs
            dirs[:] = [d for d in dirs if not _match_gitignore(
                os.path.join(root, d).lstrip('./'), gitignore_patterns)]

        for f in files:
            if f.endswith('.cs'):
                full = os.path.join(root, f)
                rel = full.lstrip('./')
                if not gitignore_patterns or not _match_gitignore(rel, gitignore_patterns):
                    file_list.append(full)

    return file_list


def process_file(filepath, strip_bom=False):
    with open(filepath, 'rb') as f:
        raw = f.read()

    bom = b''
    if raw.startswith(b'\xef\xbb\xbf'):
        bom = raw[:3]
        if strip_bom:
            raw = raw[3:]

    content = raw.decode('utf-8')
    original = content

    lines = content.split('\n')
    idx = 0

    # Collect existing pragmas at the very top (deduplicated)
    existing_pragmas = []
    seen_pragmas = set()
    while idx < len(lines) and lines[idx].strip().startswith('#pragma'):
        if lines[idx].strip() not in seen_pragmas:
            existing_pragmas.append(lines[idx])
            seen_pragmas.add(lines[idx].strip())
        idx += 1

    # Collect #nullable enable/disable if right after pragmas
    nullable_directive = None
    if idx < len(lines) and lines[idx].strip() in ('#nullable enable', '#nullable disable', '#nullable restore'):
        nullable_directive = lines[idx]
        idx += 1

    # Collect all using directives
    usings = []
    while idx < len(lines):
        stripped = lines[idx].strip()
        if stripped.startswith('using ') or stripped.startswith('global using '):
            usings.append(lines[idx])
            idx += 1
        elif stripped == '' and usings:
            idx += 1
        else:
            break

    # Collect footer items (pragmas, ReSharper comments, nullable) in any order
    footer_pragmas = []
    resharper_comments = []
    footer_nullable = None
    seen_pragmas = set()
    while idx < len(lines):
        stripped = lines[idx].strip()
        if stripped.startswith('#pragma'):
            if stripped not in seen_pragmas:
                footer_pragmas.append(lines[idx])
                seen_pragmas.add(stripped)
            idx += 1
        elif stripped.startswith('// ReSharper'):
            resharper_comments.append(lines[idx])
            idx += 1
        elif stripped in ('#nullable enable', '#nullable disable', '#nullable restore'):
            footer_nullable = lines[idx]
            idx += 1
        elif stripped == '':
            idx += 1
        else:
            break
    rest = '\n'.join(lines[idx:])

    # Merge top pragmas with footer pragmas (deduplicated)
    all_pragmas = existing_pragmas + footer_pragmas
    merged_pragmas = []
    seen = set()
    for p in all_pragmas:
        if p.strip() not in seen:
            merged_pragmas.append(p)
            seen.add(p.strip())
    existing_pragmas = merged_pragmas

    # Use footer_nullable if no top nullable was found
    if nullable_directive is None:
        nullable_directive = footer_nullable

    if not usings:
        return False

    # Categorize usings
    def is_system(u):
        s = u.strip()
        return (s.startswith('using System') or s.startswith('global using System')) and \
               'using static' not in s and '=' not in s.split(';')[0]

    def is_static(u):
        s = u.strip()
        return 'using static' in s

    def is_alias(u):
        s = u.strip()
        return '=' in s.split(';')[0] and not s.startswith('using static')

    def is_other(u):
        return not is_system(u) and not is_static(u) and not is_alias(u)

    system_usings = sorted([u for u in usings if is_system(u)], key=_using_key)
    other_usings = sorted([u for u in usings if is_other(u)], key=_using_key)
    static_usings = sorted([u for u in usings if is_static(u)], key=_using_key)
    alias_usings = sorted([u for u in usings if is_alias(u)], key=_using_key)

    # Rebuild:
    #   system + other + static usings (no blank lines between)
    #   blank line
    #   alias usings (if any)
    #   blank line
    #   ReSharper comments (if any)
    #   blank line
    #   pragmas + nullable + namespace (no whitespace between them)
    normal_usings = system_usings + other_usings + static_usings
    new_content = '\n'.join(normal_usings)
    if alias_usings:
        new_content += '\n\n' + '\n'.join(alias_usings)
    if resharper_comments:
        new_content += '\n\n' + '\n'.join(resharper_comments)
    new_content += '\n\n'
    # Footer: pragmas, nullable, namespace — no blank lines between
    if existing_pragmas:
        new_content += '\n'.join(existing_pragmas) + '\n'
    if nullable_directive:
        new_content += nullable_directive + '\n'
    new_content += rest

    if new_content == original:
        return False

    result = new_content.encode('utf-8')
    if bom and not strip_bom:
        result = bom + result
    with open(filepath, 'wb') as f:
        f.write(result)
    return True


def _using_key(u):
    m = re.match(r'(global\s+)?using\s+(static\s+)?([\w.]+)', u.strip())
    return m.group(3).lower() if m else u.lower()


def _find_solution_file():
    """Find the first .sln file in the current directory."""
    matches = glob.glob('*.sln')
    return matches[0] if matches else None


def remove_unused_usings(file_list):
    """Run dotnet format to remove unnecessary usings (IDE0005) on the given files.

    The `style` subcommand targets IDE built-in code style analyzers (not 3rd-party).
    `--severity hidden` catches IDE0005 at its default severity in .NET 10.
    No `.editorconfig` changes are required.

    Args:
        file_list: List of file paths to process. If empty, processes all files
                   in the solution (used with --all).
    """
    sln = _find_solution_file()
    if not sln:
        print("Warning: No .sln file found, skipping unused using removal", file=sys.stderr)
        return

    cmd = ['dotnet', 'format', 'style', sln, '--diagnostics', 'IDE0005',
           '--severity', 'hidden']
    if file_list:
        cmd.extend(['--include', ';'.join(file_list)])
    cmd.extend(['--verbosity', 'minimal'])

    try:
        result = subprocess.run(cmd, capture_output=True, text=True)
        if result.returncode != 0 and result.stderr.strip():
            print(f"dotnet format: {result.stderr.strip()}", file=sys.stderr)
    except FileNotFoundError:
        print("Warning: 'dotnet' not found on PATH, skipping unused using removal", file=sys.stderr)


def main():
    parser = argparse.ArgumentParser(description='Sort C# using statements')
    parser.add_argument('--strip-bom', action='store_true', help='Strip BOM marker if present')
    parser.add_argument('--stdin', action='store_true',
                        help='Read file paths from stdin (one per line)')
    parser.add_argument('--all', action='store_true',
                        help='Scan all .cs files in current directory and subdirectories, '
                             'excluding .gitignore\'d paths')
    parser.add_argument('--remove-unused', action='store_true',
                        help='Remove unused usings via dotnet format style (IDE0005) before sorting')
    parser.add_argument('files', nargs='*', help='Files to process')
    args = parser.parse_args()

    if args.stdin:
        file_list = [line.strip() for line in sys.stdin if line.strip()]
    elif args.files:
        file_list = args.files
    elif args.all:
        file_list = _find_cs_files(all_files=True)
    else:
        print("No files specified. Use --stdin to read from stdin, --all to scan directory, "
              "or pass file paths as arguments.", file=sys.stderr)
        sys.exit(1)

    if args.remove_unused:
        remove_unused_usings(file_list if not args.all else [])

    count = 0
    for filepath in file_list:
        try:
            if process_file(filepath, strip_bom=args.strip_bom):
                count += 1
        except Exception as e:
            print(f"Error processing {filepath}: {e}", file=sys.stderr)

    print(f"Modified {count} files")


if __name__ == '__main__':
    main()
