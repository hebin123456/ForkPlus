#!/usr/bin/env python3
"""Restore default styles for custom controls.

After move_keyed_styles.py moved all keyed <Style> elements into
<Styles.Resources>, custom controls (like CustomWindow, ToolbarButton, etc.)
lost their default styles because keyed styles don't auto-apply via Selector.

This script identifies "default styles" - where x:Key matches the type name
in the Selector (e.g., x:Key="CustomWindow" with Selector="ui|CustomWindow") -
and moves them back to top-level <Styles> children WITHOUT x:Key so they
auto-apply via Selector.

Named styles (where x:Key != type name, e.g., x:Key="InlineButton" with
Selector="Button") stay in <Styles.Resources> as keyed resources.
"""
import os
import re
import sys


def get_type_name_from_selector(selector: str):
    """Extract the type name from a Selector.

    Examples:
      "Button" -> "Button"
      "ui|CustomWindow" -> "CustomWindow"
      "controls|ToolbarButton" -> "ToolbarButton"
      "Border#MyBorder" -> "Border"
      "Button:pointerover" -> "Button"
    """
    # Remove pseudoclasses and class selectors
    # Selector syntax: namespace|Type.class#name:pseudoclass
    # We want just the Type part
    m = re.match(r'^(?:[\w]+:)?([^\.#:]+)', selector)
    if not m:
        return None
    type_part = m.group(1)
    # Handle namespace prefix: "ns|Type" -> "Type"
    if '|' in type_part:
        type_part = type_part.split('|')[-1]
    return type_part


def find_keyed_styles_in_resources(content: str):
    """Find all <Style x:Key="..." Selector="..."> elements inside <Styles.Resources>.

    Returns list of (start, end, text, x:Key, selector) tuples.
    """
    # Find <Styles.Resources> section
    res_open = re.search(r'<Styles\.Resources\s*>', content)
    if not res_open:
        return []

    res_close = content.find('</Styles.Resources>', res_open.end())
    if res_close == -1:
        return []

    inner = content[res_open.end():res_close]
    base_offset = res_open.end()

    results = []
    i = 0
    while i < len(inner):
        if inner[i] == '<':
            # Skip comments
            if inner[i:i+4] == '<!--':
                end_comment = inner.find('-->', i)
                if end_comment == -1:
                    break
                i = end_comment + 3
                continue

            tag_end = inner.find('>', i)
            if tag_end == -1:
                break

            tag = inner[i:tag_end+1]

            if tag.startswith('</'):
                i = tag_end + 1
                continue

            is_self_closing = tag.endswith('/>')
            m = re.match(r'<([\w:|]+)', tag)
            if m and m.group(1) == 'Style':
                # Extract x:Key and Selector
                key_m = re.search(r'x:Key="([^"]+)"', tag)
                sel_m = re.search(r'Selector="([^"]+)"', tag)

                if key_m and sel_m:
                    x_key = key_m.group(1)
                    selector = sel_m.group(1)

                    if is_self_closing:
                        start = base_offset + i
                        end = base_offset + tag_end + 1
                        results.append((start, end, content[start:end], x_key, selector))
                        i = tag_end + 1
                    else:
                        # Find matching </Style>
                        depth = 1
                        j = tag_end + 1
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
                                        depth -= 1
                                        if depth == 0:
                                            start = base_offset + i
                                            end = base_offset + inner_tag_end + 1
                                            results.append((start, end, content[start:end], x_key, selector))
                                            i = inner_tag_end + 1
                                            break
                                    j = inner_tag_end + 1
                                else:
                                    inner_m = re.match(r'<([\w:|]+)', inner_tag)
                                    if inner_m and inner_m.group(1) == 'Style' and not inner_tag.endswith('/>'):
                                        depth += 1
                                    j = inner_tag_end + 1
                            else:
                                j += 1
                        else:
                            i = tag_end + 1
                        continue

                if not is_self_closing:
                    # Skip nested content for non-keyed styles (shouldn't happen in resources)
                    depth = 1
                    j = tag_end + 1
                    while j < len(inner) and depth > 0:
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
                                m2 = re.match(r'</([\w:|]+)', inner_tag)
                                if m2 and m2.group(1) == 'Style':
                                    depth -= 1
                            elif not inner_tag.endswith('/>'):
                                m2 = re.match(r'<([\w:|]+)', inner_tag)
                                if m2 and m2.group(1) == 'Style':
                                    depth += 1
                            j = inner_tag_end + 1
                        else:
                            j += 1
                    i = j
                else:
                    i = tag_end + 1
            else:
                i = tag_end + 1
        else:
            i += 1

    return results


def is_default_style(x_key: str, selector: str):
    """Check if x:Key matches the type name in Selector (i.e., this is a default style)."""
    type_name = get_type_name_from_selector(selector)
    if not type_name:
        return False
    return x_key == type_name


def restore_default_styles(filepath: str) -> int:
    """Move default styles from <Styles.Resources> back to top-level without x:Key.

    Returns the number of styles moved.
    """
    with open(filepath, 'rb') as f:
        content = f.read().decode('utf-8')

    styles = find_keyed_styles_in_resources(content)
    if not styles:
        return 0

    # Identify default styles to move back
    default_styles = []
    for (start, end, text, x_key, selector) in styles:
        if is_default_style(x_key, selector):
            default_styles.append((start, end, text, x_key, selector))

    if not default_styles:
        return 0

    print(f"  Found {len(default_styles)} default style(s) to restore:")
    for (_, _, _, x_key, selector) in default_styles:
        print(f"    x:Key=\"{x_key}\" Selector=\"{selector}\"")

    # Remove the default styles from Resources (from end to start)
    new_content = content
    for (start, end, _, _, _) in reversed(default_styles):
        # Also remove trailing whitespace/newline
        remove_end = end
        while remove_end < len(new_content) and new_content[remove_end] in ' \t\r\n':
            remove_end += 1
            if remove_end < len(new_content) and new_content[remove_end-1] == '\n':
                break
        new_content = new_content[:start] + new_content[remove_end:]

    # Add the default styles back as top-level <Style> elements (without x:Key)
    # Insert them right before </Styles>
    styles_close_idx = new_content.rfind('</Styles>')
    if styles_close_idx == -1:
        print(f"  ERROR: cannot find </Styles>")
        return 0

    # Build the restored styles block (remove x:Key attribute)
    restored_blocks = []
    for (_, _, text, x_key, _) in default_styles:
        # Remove x:Key="..." from the opening tag
        restored = re.sub(r'\s+x:Key="[^"]+"', '', text, count=1)
        restored_blocks.append(restored)

    insertion = '\n' + '\n\n'.join(restored_blocks) + '\n'
    new_content = new_content[:styles_close_idx] + insertion + new_content[styles_close_idx:]

    with open(filepath, 'wb') as f:
        f.write(new_content.encode('utf-8'))

    return len(default_styles)


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

    total_restored = 0
    for filepath in sorted(files):
        try:
            with open(filepath, 'rb') as f:
                content = f.read().decode('utf-8')
            if '<Styles' not in content or '<Styles.Resources' not in content:
                continue
        except Exception:
            continue

        print(f"Processing: {filepath}")
        count = restore_default_styles(filepath)
        if count:
            total_restored += count
            print(f"  RESTORED {count} default style(s)")

    print(f"\nDone. Restored {total_restored} default style(s).")


if __name__ == '__main__':
    main()
