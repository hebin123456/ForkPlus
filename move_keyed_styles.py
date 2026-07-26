#!/usr/bin/env python3
"""Move <Style x:Key="..."> elements from <Styles> root into <Styles.Resources>.

In Avalonia 11, a <Style> with x:Key cannot be a direct child of <Styles> root
(causes AVLN3000 error). Keyed styles must live inside <Styles.Resources> so
they can be referenced via Style="{DynamicResource ...}" or
Style="{StaticResource ...}".

This script:
1. Parses each XAML file with <Styles> root.
2. Identifies TOP-LEVEL <Style> elements with x:Key (direct children of <Styles>).
3. Moves them into <Styles.Resources> (creating the section if absent).
4. Leaves nested <Style> elements (inside <Grid.Styles>, <Border.Styles>, etc.) alone.
5. Leaves non-keyed top-level <Style> elements in place.
"""
import os
import re
import sys


def find_top_level_styles(content: str):
    """Find top-level <Style> elements with x:Key that are direct children of <Styles> root.

    Returns a list of (start, end, text) tuples for each such element.
    """
    # Find <Styles ...> opening tag
    styles_open = re.search(r'<Styles\b[^>]*>', content)
    if not styles_open:
        return []

    # Find </Styles> closing tag (last occurrence)
    styles_close_idx = content.rfind('</Styles>')
    if styles_close_idx == -1:
        return []

    # Content between <Styles> and </Styles>
    inner_start = styles_open.end()
    inner_end = styles_close_idx
    inner = content[inner_start:inner_end]

    # We need to find top-level <Style> elements with x:Key.
    # A "top-level" Style is one that is NOT nested inside another element's
    # property setter (like <Grid.Styles>) or inside a <Setter.Value>.
    #
    # Strategy: walk through the inner content tracking depth. When we're at
    # depth 0 (direct child of <Styles>) and encounter <Style x:Key="...">,
    # capture the entire element (including its closing tag or self-closing).

    results = []
    depth = 0
    i = 0
    while i < len(inner):
        # Look for element start
        if inner[i] == '<':
            # Skip comments
            if inner[i:i+4] == '<!--':
                end_comment = inner.find('-->', i)
                if end_comment == -1:
                    break
                i = end_comment + 3
                continue

            # Find end of tag
            tag_end = inner.find('>', i)
            if tag_end == -1:
                break

            tag = inner[i:tag_end+1]

            # Check if it's a closing tag
            if tag.startswith('</'):
                depth -= 1
                i = tag_end + 1
                continue

            # Check if it's a self-closing tag
            is_self_closing = tag.endswith('/>')

            # Extract element name
            m = re.match(r'<([\w:|]+)', tag)
            if m:
                elem_name = m.group(1)

                # If we're at depth 0 and it's a <Style> with x:Key, capture it
                if depth == 0 and elem_name == 'Style' and 'x:Key=' in tag:
                    if is_self_closing:
                        # Self-closing: <Style x:Key="..." Selector="..." />
                        style_start = inner_start + i
                        style_end = inner_start + tag_end + 1
                        results.append((style_start, style_end, content[style_start:style_end]))
                        i = tag_end + 1
                    else:
                        # Need to find matching </Style>
                        # Walk forward tracking depth of Style element
                        style_depth = 1
                        search_start = tag_end + 1
                        j = search_start
                        while j < len(inner):
                            if inner[j] == '<':
                                if inner[j:j+4] == '<!--':
                                    end_comment = inner.find('-->', j)
                                    if end_comment == -1:
                                        break
                                    j = end_comment + 3
                                    continue

                                inner_tag_end = inner.find('>', j)
                                if inner_tag_end == -1:
                                    break
                                inner_tag = inner[j:inner_tag_end+1]

                                if inner_tag.startswith('</'):
                                    inner_m = re.match(r'</([\w:|]+)', inner_tag)
                                    if inner_m and inner_m.group(1) == 'Style':
                                        style_depth -= 1
                                        if style_depth == 0:
                                            style_start = inner_start + i
                                            style_end = inner_start + inner_tag_end + 1
                                            results.append((style_start, style_end, content[style_start:style_end]))
                                            i = inner_tag_end + 1
                                            break
                                    else:
                                        # Some other closing tag - shouldn't happen if depth tracking is correct
                                        pass
                                    j = inner_tag_end + 1
                                else:
                                    inner_m = re.match(r'<([\w:|]+)', inner_tag)
                                    if inner_m and inner_m.group(1) == 'Style':
                                        if not inner_tag.endswith('/>'):
                                            style_depth += 1
                                    j = inner_tag_end + 1
                            else:
                                j += 1
                        else:
                            # Didn't find matching </Style>
                            print(f"  WARNING: unmatched <Style> at offset {i}")
                            i = tag_end + 1
                        continue

                # Not a top-level keyed Style - update depth
                if not is_self_closing:
                    # Check if it's a property element like <Styles.Resources> or <Setter.Value>
                    # These don't increase depth in the same way... actually they do.
                    # <Styles.Resources> ... </Styles.Resources> is a child element.
                    depth += 1
                i = tag_end + 1
            else:
                i = tag_end + 1
        else:
            i += 1

    return results


def has_styles_resources(content: str):
    """Check if <Styles.Resources> section exists and return its position."""
    m = re.search(r'<Styles\.Resources\s*>', content)
    if not m:
        return None
    close_idx = content.find('</Styles.Resources>', m.end())
    if close_idx == -1:
        return None
    return (m.start(), m.end(), close_idx)


def move_keyed_styles(filepath: str) -> bool:
    with open(filepath, 'rb') as f:
        raw = f.read()

    content = raw.decode('utf-8')

    # Find top-level keyed styles
    styles = find_top_level_styles(content)
    if not styles:
        return False

    print(f"  Found {len(styles)} keyed style(s) to move")

    # Extract style texts (in order)
    style_texts = [text for (_, _, text) in styles]

    # Remove styles from their original positions (from end to start to preserve offsets)
    new_content = content
    for (start, end, _) in reversed(styles):
        # Also remove trailing whitespace/newline after the style
        # to avoid leaving blank lines
        remove_end = end
        # Skip trailing whitespace and newlines
        while remove_end < len(new_content) and new_content[remove_end] in ' \t\r\n':
            remove_end += 1
            # Only consume one newline
            if remove_end < len(new_content) and new_content[remove_end-1] == '\n':
                break
        new_content = new_content[:start] + new_content[remove_end:]

    # Now add the styles to <Styles.Resources>
    resources_info = has_styles_resources(new_content)

    # Build the styles block to insert
    styles_block = '\n'.join(style_texts)

    if resources_info:
        # Append to existing <Styles.Resources>
        res_start, res_open_end, res_close_idx = resources_info
        # Insert before </Styles.Resources>
        # Add proper indentation
        insertion = f'\n    {styles_block.replace(chr(10), chr(10) + "    ")}\n  '
        new_content = new_content[:res_close_idx] + insertion + new_content[res_close_idx:]
    else:
        # Create new <Styles.Resources> section right after <Styles ...> opening tag
        styles_open = re.search(r'<Styles\b[^>]*>', new_content)
        if not styles_open:
            print(f"  ERROR: cannot find <Styles> root after removal")
            return False

        insertion = f'\n  <Styles.Resources>\n    {styles_block.replace(chr(10), chr(10) + "    ")}\n  </Styles.Resources>\n'
        new_content = new_content[:styles_open.end()] + insertion + new_content[styles_open.end():]

    if new_content != content:
        with open(filepath, 'wb') as f:
            f.write(new_content.encode('utf-8'))
        return True

    return False


def main():
    styles_dir = '/workspace/src/ForkPlus/Theme/Styles'

    if len(sys.argv) > 1:
        files = sys.argv[1:]
    else:
        files = []
        for root, _, names in os.walk(styles_dir):
            for name in names:
                if name.endswith('.xaml'):
                    files.append(os.path.join(root, name))

    changed = 0
    for filepath in sorted(files):
        # Skip Brushes and Geometries (those are ResourceDictionary root)
        try:
            with open(filepath, 'rb') as f:
                head = f.read().decode('utf-8', errors='replace')
            if '<Styles' not in head:
                continue
        except Exception:
            continue

        print(f"Processing: {filepath}")
        if move_keyed_styles(filepath):
            changed += 1
            print(f"  MODIFIED")

    print(f"\nDone. Modified {changed} file(s).")


if __name__ == '__main__':
    main()
