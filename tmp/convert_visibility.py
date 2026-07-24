#!/usr/bin/env python3
"""Convert active (non-commented) WPF Visibility literals to Avalonia IsVisible.

Rules:
  - Visibility="Collapsed" -> IsVisible="False"
  - Visibility="Hidden"    -> IsVisible="False"
  - Visibility="Visible"   -> IsVisible="True"

Skips lines that are inside XML comments (<!-- ... -->) or whose Visibility
attribute is itself on a commented line.
"""
import os
import re
import sys

ROOT = "/workspace/src/ForkPlus"
# Negative lookbehind on \w avoids matching VerticalScrollBarVisibility="..." etc.
VIS_RE = re.compile(r'(?<!\w)Visibility="(Collapsed|Hidden|Visible)"')

# Files to skip (explicitly preserved, e.g. theme files needing careful review)
SKIP_FILES = {
    # None for now - process all
}

def convert_file(path: str) -> int:
    with open(path, "r", encoding="utf-8") as f:
        lines = f.readlines()

    changed = 0
    in_comment = False
    out = []
    for line in lines:
        # Track XML comment state (multi-line aware, naive but effective for our XAML)
        # A line is "in comment" if we are inside an open <!-- that has not been closed.
        # We process comment markers on the stripped line for tracking only.
        cursor = 0
        stripped = line
        # Determine comment state at start of line
        line_in_comment = in_comment
        # Walk through the line and update comment state, but only to detect
        # whether the Visibility attribute we will replace is inside a comment.
        # We'll do a simpler approach: find all Visibility="..." positions and
        # check whether that position is inside a comment region.
        new_in_comment = in_comment
        # Build a comment-region mask for this line
        i = 0
        pos_in_comment = []  # list of (start, end, in_comment) for each char
        cur_state = in_comment
        while i < len(line):
            if line[i:i+4] == "<!--":
                cur_state = True
                for _ in range(4):
                    pos_in_comment.append((i, cur_state))
                    i += 1
                continue
            if line[i:i+3] == "-->":
                # closing marker chars themselves are still "in comment"
                for _ in range(3):
                    pos_in_comment.append((i, True))
                    i += 1
                cur_state = False
                continue
            pos_in_comment.append((i, cur_state))
            i += 1
        new_in_comment = cur_state

        # Now find Visibility="..." occurrences on this line
        new_line = line
        offset = 0
        for m in VIS_RE.finditer(line):
            pos = m.start()
            # Determine comment state at this position
            state = pos_in_comment[pos][1] if pos < len(pos_in_comment) else new_in_comment
            if state:
                continue  # skip commented occurrence
            val = m.group(1)
            repl = "True" if val == "Visible" else "False"
            replacement = f'IsVisible="{repl}"'
            new_line = new_line[:m.start()+offset] + replacement + new_line[m.end()+offset:]
            offset += len(replacement) - len(m.group(0))
            changed += 1

        out.append(new_line)
        in_comment = new_in_comment

    if changed > 0:
        with open(path, "w", encoding="utf-8") as f:
            f.writelines(out)
    return changed


def main():
    total_changed = 0
    total_files = 0
    for dirpath, _, filenames in os.walk(ROOT):
        for fn in filenames:
            if not fn.endswith(".xaml"):
                continue
            full = os.path.join(dirpath, fn)
            if full in SKIP_FILES:
                continue
            n = convert_file(full)
            if n > 0:
                total_files += 1
                total_changed += n
                print(f"{full}: {n} replacements")
    print(f"\nTOTAL: {total_changed} replacements in {total_files} files")


if __name__ == "__main__":
    main()
