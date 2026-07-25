#!/usr/bin/env python3
"""Phase 3: Fix standalone ControlTemplate with ^ selectors.
For standalone <ControlTemplate x:Key="..." TargetType="X">, the ^ selector
in nested <Style> elements refers to nothing. Replace ^ with the TargetType.
Also handle complex cases like removing problematic nested content."""
import re
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")

def find_control_templates(content):
    """Find all <ControlTemplate> tags and return list of (start, end, target_type, has_xkey)."""
    templates = []
    # Match ControlTemplate opening tag
    for m in re.finditer(r'<ControlTemplate\b([^>]*)>', content):
        attrs = m.group(1)
        # Check if has x:Key
        has_xkey = 'x:Key=' in attrs
        # Extract TargetType
        tt_match = re.search(r'TargetType="([^"]+)"', attrs)
        target_type = tt_match.group(1) if tt_match else None
        # Strip namespace prefix from target_type (e.g., "controls:MenuItem" -> "MenuItem")
        if target_type and ':' in target_type:
            target_type = target_type.split(':', 1)[1]
        templates.append({
            'start': m.start(),
            'tag_end': m.end(),
            'target_type': target_type,
            'has_xkey': has_xkey,
        })
    return templates

def find_matching_close(content, start_pos, tag_name):
    """Find the matching close tag for an opening tag at start_pos."""
    open_tag = f'<{tag_name}'
    close_tag = f'</{tag_name}>'
    self_close = '/>'
    
    # Find the end of the opening tag
    pos = content.find('>', start_pos)
    if pos == -1:
        return -1
    
    # Check if it's self-closing
    if content[pos-1] == '/':
        return pos + 1
    
    pos += 1
    depth = 1
    
    while depth > 0 and pos < len(content):
        # Find next open or close tag
        next_open = content.find(open_tag, pos)
        if next_open == -1:
            next_open = len(content)
        next_close = content.find(close_tag, pos)
        if next_close == -1:
            return -1
        
        if next_open < next_close:
            # Check if it's actually an open tag (not the same as our open_tag prefix match)
            # Also check if it's self-closing
            open_end = content.find('>', next_open)
            if open_end != -1 and content[open_end-1] == '/':
                # Self-closing, skip
                pos = open_end + 1
            else:
                depth += 1
                pos = open_end + 1 if open_end != -1 else next_open + len(open_tag)
        else:
            depth -= 1
            pos = next_close + len(close_tag)
    
    return pos if depth == 0 else -1

def fix_standalone_controltemplate_selectors(content):
    """For each standalone ControlTemplate (has x:Key), replace ^:pseudo with TargetType:pseudo
    in nested Style selectors."""
    templates = find_control_templates(content)
    
    # Process from last to first to maintain positions
    changes = 0
    for t in reversed(templates):
        if not t['has_xkey'] or not t['target_type']:
            continue
        
        end = find_matching_close(content, t['start'], 'ControlTemplate')
        if end == -1:
            continue
        
        template_content = content[t['start']:end]
        
        # Replace ^:pseudo with TargetType:pseudo in Style selectors
        # Pattern: Selector="^:pseudoclass..." -> Selector="TargetType:pseudoclass..."
        # Also: Selector="^:pseudoclass:pseudoclass2 /template/..." -> Selector="TargetType:pseudoclass:pseudoclass2 /template/..."
        # Just replace ^: at the start of selector with TargetType:
        def replacer(m):
            sel = m.group(1)
            new_sel = re.sub(r'^\^:', f'{t["target_type"]}:', sel)
            # Also handle ^ alone (no pseudoclass) -> TargetType
            new_sel = re.sub(r'^\^(?![\w|])', f'{t["target_type"]}', new_sel)
            return f'Selector="{new_sel}"'
        
        new_template = re.sub(r'Selector="([^"]+)"', replacer, template_content)
        
        if new_template != template_content:
            content = content[:t['start']] + new_template + content[end:]
            changes += 1
    
    return content, changes

def fix_remove_is_checked_setter_on_menuitem(content):
    """In MenuItem templates, IsChecked=true Setter is a WPF-only trigger pattern.
    Skip - this is handled by selector conversion."""
    return content, 0

def fix_datatemplate_without_datatype(content):
    """For DataTemplate without DataType, the bindings inside fail with XamlPseudoType.
    Add DataType where we can infer it from context - but this is hard to do automatically.
    Skip for now."""
    return content, 0

def fix_remove_xkey_in_nested_style(content):
    """Nested Style inside ControlTemplate shouldn't have x:Key."""
    # Find <Style x:Key="..." Selector="..."> inside <ControlTemplate>
    # and remove x:Key
    def replacer(m):
        return m.group(0).replace(' x:Key="' + m.group(1) + '"', '')
    
    # This is tricky - need to find nested styles. Skip for now.
    return content, 0

def fix_remove_setter_targettype_in_style(content):
    """Remove TargetType from <Style> tags (Avalonia uses Selector)."""
    # <Style TargetType="X" Selector="X"> -> <Style Selector="X">
    pattern = re.compile(r'<Style\s+TargetType="[^"]*"\s+Selector=')
    new_content, n = pattern.subn('<Style Selector=', content)
    # Also handle <Style Selector="..." TargetType="...">
    pattern = re.compile(r'(<Style\s+Selector="[^"]+")\s+TargetType="[^"]*"')
    new_content, n2 = pattern.subn(r'\1', new_content)
    return new_content, n + n2

def fix_xkey_with_xtype_in_standalone_resources(content):
    """Convert x:Key="{x:Type X}" to x:Key="X" in resource definitions."""
    pattern = re.compile(r'x:Key="\{x:Type\s+([\w:]+)\}"')
    new_content, n = pattern.subn(lambda m: f'x:Key="{m.group(1)}"', content)
    return new_content, n

def process_file(path):
    try:
        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception:
        return 0
    
    original = content
    total_changes = 0
    
    fixers = [
        fix_standalone_controltemplate_selectors,
        fix_remove_setter_targettype_in_style,
        fix_xkey_with_xtype_in_standalone_resources,
    ]
    
    for fixer in fixers:
        content, n = fixer(content)
        total_changes += n
    
    if content != original:
        with open(path, 'w', encoding='utf-8') as f:
            f.write(content)
        return total_changes
    return 0

def main():
    total_files_changed = 0
    total_changes = 0
    xaml_files = list(ROOT.rglob("*.xaml"))
    for path in xaml_files:
        n = process_file(path)
        if n > 0:
            total_files_changed += 1
            total_changes += n
            print(f"  {path}: {n} changes")
    print(f"\nTotal: {total_files_changed} files, {total_changes} changes")

if __name__ == "__main__":
    main()
