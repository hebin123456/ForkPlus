#!/usr/bin/env python3
"""Phase 5: Fix 'multiple assignments to Content' in ControlTemplate.
Avalonia's ControlTemplate can only have ONE direct child element.
Move sibling Style elements into the root visual's .Styles property."""
import re
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")

def find_matching_close(content, start_pos, tag_name):
    """Find the matching close tag for an opening tag at start_pos."""
    # Match <tag_name followed by whitespace, >, or / (not . or :)
    open_tag_pattern = re.compile(rf'<{re.escape(tag_name)}(?=[\s/>])')
    close_tag = f'</{tag_name}>'

    pos = content.find('>', start_pos)
    if pos == -1:
        return -1

    if content[pos-1] == '/':
        return pos + 1

    pos += 1
    depth = 1

    while depth > 0 and pos < len(content):
        open_match = open_tag_pattern.search(content, pos)
        next_open = open_match.start() if open_match else len(content)
        next_close = content.find(close_tag, pos)
        if next_close == -1:
            return -1

        if next_open < next_close:
            open_end = content.find('>', next_open)
            if open_end != -1 and content[open_end-1] == '/':
                pos = open_end + 1
            else:
                depth += 1
                pos = (open_end + 1) if open_end != -1 else (next_open + len(open_tag_pattern.pattern))
        else:
            depth -= 1
            pos = next_close + len(close_tag)

    return pos if depth == 0 else -1

def find_first_child_element(content, start_pos, end_pos):
    """Find the first child element (not comment) between start_pos and end_pos.
    Returns (element_start, element_end, tag_name) or None."""
    pos = start_pos
    while pos < end_pos:
        # Skip whitespace
        while pos < end_pos and content[pos] in ' \t\n\r':
            pos += 1
        if pos >= end_pos:
            break
        # Skip comments
        if content[pos:pos+4] == '<!--':
            comment_end = content.find('-->', pos)
            if comment_end == -1:
                break
            pos = comment_end + 3
            continue
        # Found an element
        if content[pos] == '<':
            # Extract tag name
            tag_match = re.match(r'<([\w:]+)', content[pos:])
            if tag_match:
                tag_name = tag_match.group(1)
                elem_start = pos
                elem_end = find_matching_close(content, pos, tag_name)
                if elem_end != -1:
                    return (elem_start, elem_end, tag_name)
            break
        pos += 1
    return None

def find_all_style_elements(content, start_pos, end_pos):
    """Find all top-level <Style> elements between start_pos and end_pos.
    Returns list of (start, end) tuples."""
    styles = []
    pos = start_pos
    while pos < end_pos:
        # Skip whitespace
        while pos < end_pos and content[pos] in ' \t\n\r':
            pos += 1
        if pos >= end_pos:
            break
        # Skip comments
        if content[pos:pos+4] == '<!--':
            comment_end = content.find('-->', pos)
            if comment_end == -1:
                break
            pos = comment_end + 3
            continue
        # Check for Style element
        if content[pos:pos+7] == '<Style ' or content[pos:pos+7] == '<Style>':
            style_end = find_matching_close(content, pos, 'Style')
            if style_end != -1:
                styles.append((pos, style_end))
                pos = style_end
                continue
        # If we hit a non-Style element, stop
        if content[pos] == '<':
            break
        pos += 1
    return styles

def fix_controltemplate_multiple_content(content):
    """For each ControlTemplate with multiple children (root + Style siblings),
    move the Style elements into the root element's .Styles property."""
    
    # Find all ControlTemplate tags
    changes = 0
    # Process from last to first to maintain positions
    matches = list(re.finditer(r'<ControlTemplate\b[^>]*>', content))
    
    for m in reversed(matches):
        ct_start = m.start()
        ct_end = find_matching_close(content, ct_start, 'ControlTemplate')
        if ct_end == -1:
            continue
        
        # Find the inner content (between opening tag and closing tag)
        inner_start = m.end()
        inner_end = content.rfind('</ControlTemplate>', inner_start, ct_end)
        if inner_end == -1:
            continue
        
        # Find the first child element (root visual)
        first_child = find_first_child_element(content, inner_start, inner_end)
        if not first_child:
            continue
        
        child_start, child_end, child_tag = first_child
        
        # Find all Style elements AFTER the first child
        styles_after = find_all_style_elements(content, child_end, inner_end)
        if not styles_after:
            continue
        
        # We have styles to move
        # Extract the styles content (including comments between them)
        first_style_start = styles_after[0][0]
        last_style_end = styles_after[-1][1]
        
        # Get all content between child_end and last_style_end (includes comments and styles)
        # But we want to extract just the Style elements (and their adjacent comments)
        # For simplicity, extract everything from first_style_start to last_style_end
        styles_block = content[first_style_start:last_style_end]
        
        # Now we need to:
        # 1. Remove the styles block from after the root element
        # 2. Insert <RootElement.Styles>styles_block</RootElement.Styles> inside the root element
        
        # Find the end of the root element's opening tag (the > after attributes)
        # Check if it's self-closing
        opening_tag_end = content.find('>', child_start)
        if opening_tag_end == -1 or opening_tag_end > child_end:
            continue
        
        is_self_closing = content[opening_tag_end-1] == '/'
        if is_self_closing:
            # Convert self-closing to opening+closing
            # <Border .../> -> <Border ...></Border>
            new_opening = content[child_start:opening_tag_end-1] + '>'  # remove the /
            new_element = new_opening + f'<{child_tag}.Styles>\n{styles_block}\n</{child_tag}.Styles></{child_tag}>'
            
            # Replace the entire old element + styles with new element
            new_content = (
                content[:child_start] + 
                new_element + 
                content[last_style_end:]
            )
        else:
            # Element has opening and closing tags
            # Insert <Tag.Styles>...</Tag.Styles> right after the opening tag
            insert_pos = opening_tag_end + 1
            new_styles_block = f'\n<{child_tag}.Styles>\n{styles_block}\n</{child_tag}.Styles>\n'
            
            # Remove the styles block from after the element
            # Also remove any trailing whitespace/comments
            # Find the position right after child_end
            remove_start = child_end
            # Find the next non-whitespace position
            while remove_start < len(content) and content[remove_start] in ' \t\n\r':
                remove_start += 1
            
            # Build new content:
            # - Keep content up to insert_pos
            # - Add new_styles_block
            # - Keep content from insert_pos to child_end (the element's existing content)
            # - Skip the styles block (from child_end to last_style_end)
            # - Keep content from last_style_end onwards
            
            new_content = (
                content[:insert_pos] + 
                new_styles_block + 
                content[insert_pos:child_end] + 
                content[last_style_end:]
            )
        
        # Verify content length is reasonable
        if abs(len(new_content) - len(content)) > 10000:
            print(f"  WARNING: large content change, skipping")
            continue
        
        content = new_content
        changes += 1
    
    return content, changes

def process_file(path):
    try:
        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception:
        return 0
    
    original = content
    total_changes = 0
    
    content, n = fix_controltemplate_multiple_content(content)
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
