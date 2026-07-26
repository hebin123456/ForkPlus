#!/usr/bin/env python3
"""
Convert top-level <Style Selector="prefix|Type"> with <Setter Property="Template">
to <ControlTheme x:Key="{x:Type prefix:Type}" TargetType="prefix:Type"> inside <Styles.Resources>.

In Avalonia 11, setting Template via a type-selector Style does NOT override the base
ControlTheme's template for custom controls. The fix is to use ControlTheme with
x:Key="{x:Type T}" placed inside a ResourceDictionary (Styles.Resources).

This script only converts top-level type-selector Styles (no x:Key, has namespace prefix|).
It does NOT touch:
- Keyed styles (<Style x:Key="..." Selector="...">)
- Class selector styles (<Style Selector="Button.custom">)
- Built-in type selectors without namespace prefix (<Style Selector="Button">)
- Nested/pseudoclass styles (<Style Selector="^:pointerover ...">)
"""
import re
import sys
from pathlib import Path


def find_matching_close(content, start_pos, open_tag, close_tag):
    """Find the position of the matching close tag, handling nesting.
    Skips self-closing tags (e.g. <Style ... />).
    Uses regex to avoid matching <Style.Resources> when looking for <Style.
    """
    # Build regex patterns that match the tag followed by whitespace, '>' or '/' (for self-closing)
    # This avoids matching <Style.Resources> when open_tag is '<Style'
    open_pattern = re.compile(re.escape(open_tag) + r'[\s/>]')
    close_pattern = close_tag

    depth = 1
    pos = start_pos
    while pos < len(content):
        open_match = open_pattern.search(content, pos)
        next_open = open_match.start() if open_match else -1
        next_close = content.find(close_pattern, pos)
        if next_close == -1:
            return -1
        if next_open != -1 and next_open < next_close:
            # Check if this is a self-closing tag (ends with />)
            tag_end = content.find('>', next_open)
            if tag_end != -1:
                if content[tag_end - 1] == '/':
                    pos = tag_end + 1
                    continue
            depth += 1
            pos = next_open + len(open_tag)
        else:
            depth -= 1
            if depth == 0:
                return next_close
            pos = next_close + len(close_pattern)
    return -1


def find_styles_resources_section(content):
    """Find the <Styles.Resources>...</Styles.Resources> section bounds."""
    open_tag = "<Styles.Resources>"
    close_tag = "</Styles.Resources>"
    open_pos = content.find(open_tag)
    if open_pos == -1:
        return None
    close_pos = content.find(close_tag, open_pos)
    if close_pos == -1:
        return None
    return (open_pos, close_pos, open_tag, close_tag)


def find_top_level_type_styles(content):
    """
    Find all top-level <Style Selector="prefix|Type"> blocks (not keyed, not nested).
    Returns list of (start, end, selector_prefix, selector_type, full_block).
    """
    results = []
    # Match <Style Selector="prefix|Type"> at the start of a line (top-level, not nested)
    # Must NOT have x:Key attribute
    # Must have a namespace prefix (contains |)
    pattern = re.compile(r'^<Style\s+Selector="([^"]+\|[^"]+)"\s*>', re.MULTILINE)

    for match in pattern.finditer(content):
        selector = match.group(1)
        if '|' not in selector:
            continue
        # Check it's not a nested style (should be at column 0 or close to it)
        line_start = content.rfind('\n', 0, match.start()) + 1
        prefix_str = content[line_start:match.start()]
        # Top-level styles should have no leading whitespace or minimal
        if len(prefix_str.strip()) > 0:
            continue

        prefix, type_name = selector.split('|', 1)

        # Find matching </Style>
        style_open_end = match.end()
        close_pos = find_matching_close(content, style_open_end, '<Style', '</Style>')
        if close_pos == -1:
            continue
        block_end = close_pos + len('</Style>')
        full_block = content[match.start():block_end]

        # Check if this block contains a Template setter
        if '<Setter Property="Template">' not in full_block and \
           '<Setter Property="Template" ' not in full_block:
            continue

        results.append({
            'start': match.start(),
            'end': block_end,
            'selector': selector,
            'prefix': prefix,
            'type_name': type_name,
            'block': full_block,
            'open_match': match,
        })

    return results


def convert_block_to_controltheme(block_info):
    """Convert a <Style Selector="prefix|Type"> block to <ControlTheme ...>."""
    block = block_info['block']
    prefix = block_info['prefix']
    type_name = block_info['type_name']
    selector = block_info['selector']

    # Replace opening tag
    old_open = f'<Style Selector="{selector}">'
    new_open = f'<ControlTheme x:Key="{{x:Type {prefix}:{type_name}}}" TargetType="{prefix}:{type_name}">'
    block = block.replace(old_open, new_open, 1)

    # Replace closing tag (the LAST </Style> in the block)
    last_close = block.rfind('</Style>')
    if last_close != -1:
        block = block[:last_close] + '</ControlTheme>' + block[last_close + len('</Style>'):]

    # Update ControlTemplate TargetType to include prefix
    # Match <ControlTemplate TargetType="TypeName"> or <ControlTemplate  TargetType="TypeName">
    # Only if TargetType doesn't already have a prefix (no colon)
    def fix_template_type(m):
        target_type = m.group(2).strip()
        if ':' not in target_type:
            target_type = f'{prefix}:{type_name}'
        return f'{m.group(1)}<ControlTemplate{m.group(1)}TargetType="{target_type}">'

    # This is tricky because ControlTemplate might be inside the block multiple times
    # Let's just replace TargetType="TypeName" with TargetType="prefix:TypeName"
    # where TypeName matches the type_name and doesn't have a colon
    old_ct = f'TargetType="{type_name}"'
    new_ct = f'TargetType="{prefix}:{type_name}"'
    if old_ct in block:
        block = block.replace(old_ct, new_ct)

    return block


def process_file(filepath):
    """Process a single XAML file."""
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    # Find top-level type-selector Styles with Template
    styles = find_top_level_type_styles(content)
    if not styles:
        return 0

    print(f"  Found {len(styles)} top-level Style(s) with Template in {filepath.name}:")
    for s in styles:
        print(f"    - {s['prefix']}|{s['type_name']}")

    # Find Styles.Resources section
    sr = find_styles_resources_section(content)

    # Process from end to start to preserve positions
    # First, collect all changes
    new_blocks = []
    for s in styles:
        converted = convert_block_to_controltheme(s)
        new_blocks.append((s, converted))

    # Remove the original blocks (from end to start) and collect them
    result = content
    offsets = 0
    removed_blocks = []

    # Process from end to start
    for i in range(len(styles) - 1, -1, -1):
        s = styles[i]
        converted = new_blocks[i][1]
        # Remove the original block and any trailing newline
        block_start = s['start']
        block_end = s['end']
        # Also remove trailing newline if present
        if block_end < len(result) and result[block_end] == '\n':
            block_end += 1
        # Also remove leading newline if present (before the block)
        if block_start > 0 and result[block_start - 1] == '\n':
            block_start -= 1

        removed_blocks.insert(0, converted)
        result = result[:block_start] + result[block_end:]

    # Now find Styles.Resources in the modified content and insert the converted blocks
    sr = find_styles_resources_section(result)
    if sr is None:
        # No Styles.Resources section - create one
        # Find the <Styles ...> opening tag
        styles_open = re.search(r'<Styles[^>]*>', result)
        if styles_open is None:
            print(f"  ERROR: No <Styles> root found in {filepath.name}")
            return 0
        insert_pos = styles_open.end()
        # Create Styles.Resources section with all converted blocks
        sr_content = '\n  <Styles.Resources>\n'
        for block in removed_blocks:
            sr_content += '    ' + block.replace('\n', '\n    ') + '\n'
        sr_content += '  </Styles.Resources>\n'
        result = result[:insert_pos] + sr_content + result[insert_pos:]
    else:
        # Insert before </Styles.Resources>
        close_pos = sr[1]
        close_tag = sr[3]
        insertion = '\n'
        for block in removed_blocks:
            # Indent the block
            indented = '\n' + block
            insertion += indented
        result = result[:close_pos] + insertion + result[close_pos:]

    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(result)

    return len(styles)


def main():
    files = [
        '/workspace/src/ForkPlus/Theme/Styles/Button.xaml',
        '/workspace/src/ForkPlus/Theme/Styles/Multiselectiontreeview.xaml',
        '/workspace/src/ForkPlus/Theme/Styles/Commonresources.xaml',
        '/workspace/src/ForkPlus/Theme/Styles/Tabcontrol.xaml',
        '/workspace/src/ForkPlus/Theme/Styles/Placeholdertextbox.xaml',
    ]

    total = 0
    for f in files:
        path = Path(f)
        if not path.exists():
            print(f"Skipping (not found): {f}")
            continue
        print(f"\nProcessing {path.name}...")
        count = process_file(path)
        total += count
        if count == 0:
            print(f"  No changes needed")

    print(f"\nTotal conversions: {total}")


if __name__ == '__main__':
    main()
