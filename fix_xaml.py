#!/usr/bin/env python3
"""Systematic XAML fixes for WPF→Avalonia migration.
Targets recurring AVLN2000 error categories."""
import re
import os
import sys
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")

def fix_style_dynamic_resource(content):
    """Remove <Control.Style><DynamicResource .../></Control.Style> blocks.
    Avalonia does not support setting Style via DynamicResource."""
    pattern = re.compile(
        r'\s*<(\w+)\.Style>\s*<DynamicResource\s+ResourceKey="[^"]+"\s*/>\s*</\1\.Style>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_tooltip_tip_property_element(content):
    """Convert <Control.ToolTip.Tip>...</Control.ToolTip.Tip> to <ToolTip.Tip>...</ToolTip.Tip>."""
    pattern = re.compile(
        r'<(\w[\w\.]*)\.ToolTip\.Tip>(.*?)</\1\.ToolTip\.Tip>',
        re.DOTALL
    )
    new_content, n = pattern.subn(r'<ToolTip.Tip>\2</ToolTip.Tip>', content)
    return new_content, n

def fix_tooltip_property_element(content):
    """Convert <Control.ToolTip>...</Control.ToolTip> to <ToolTip.Tip>...</ToolTip.Tip>."""
    pattern = re.compile(
        r'<(\w[\w\.]*)\.ToolTip>(.*?)</\1\.ToolTip>',
        re.DOTALL
    )
    new_content, n = pattern.subn(r'<ToolTip.Tip>\2</ToolTip.Tip>', content)
    return new_content, n

def fix_selector_namespace(content):
    """Change Selector="ns:Type" to Selector="ns|Type" (Avalonia uses | separator)."""
    def replacer(m):
        sel = m.group(1)
        new_sel = re.sub(r'^([a-zA-Z_][a-zA-Z0-9_]*):([A-Z][a-zA-Z0-9_]*)', r'\1|\2', sel)
        return f'Selector="{new_sel}"'
    
    pattern = re.compile(r'Selector="([^"]+)"')
    new_content, n = pattern.subn(replacer, content)
    return new_content, n

def fix_content_source(content):
    """Remove ContentSource="..." attribute from ContentPresenter."""
    pattern = re.compile(r'\s+ContentSource="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_rectangle_cornerRadius(content):
    """Convert CornerRadius="N" on Rectangle to RadiusX="N" RadiusY="N"."""
    def replacer(m):
        tag = m.group(0)
        if re.match(r'<Rectangle\b', tag):
            return re.sub(r'CornerRadius="([^"]*)"', r'RadiusX="\1" RadiusY="\1"', tag)
        return tag
    
    pattern = re.compile(r'<\w[\w\.]*(\s+[^>]*)?/?>', re.DOTALL)
    new_content, n = pattern.subn(replacer, content)
    return new_content, n

def fix_xname_on_columndefinition(content):
    """Remove x:Name from ColumnDefinition and RowDefinition (Avalonia doesn't support)."""
    def remove_xname(m):
        tag = m.group(0)
        return re.sub(r'\s+x:Name="[^"]*"', '', tag)
    
    new_content = content
    n = 0
    for tag_name in ['ColumnDefinition', 'RowDefinition']:
        pattern = re.compile(rf'<{tag_name}\b[^>]*?/?>', re.DOTALL)
        new_content, count = pattern.subn(remove_xname, new_content)
        n += count
    return new_content, n

def fix_remove_allows_transparency(content):
    pattern = re.compile(r'\s+AllowsTransparency="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_popup_animation(content):
    pattern = re.compile(r'\s+PopupAnimation="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_shadow_depth(content):
    pattern = re.compile(r'\s+ShadowDepth="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_can_content_scroll(content):
    pattern = re.compile(r'\s+CanContentScroll="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_content_string_format(content):
    pattern = re.compile(r'\s+ContentStringFormat="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_directional_navigation(content):
    pattern = re.compile(r'\s+KeyboardNavigation\.DirectionalNavigation="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_horizontal_vertical_scrollbar_isvisible(content):
    n = 0
    new_content = content
    for prop in ['HorizontalScrollBarIsVisible', 'VerticalScrollBarIsVisible']:
        pattern = re.compile(rf'\s+{prop}="[^"]*"')
        new_content, count = pattern.subn('', new_content)
        n += count
    return new_content, n

def fix_remove_uid(content):
    pattern = re.compile(r'\s+x:Uid="[^"]*"|\s+Uid="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_mouse_double_click(content):
    pattern = re.compile(r'\s+MouseDoubleClick="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_context_menu_opening(content):
    pattern = re.compile(r'\s+ContextMenuOpening="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_item_container_style(content):
    pattern = re.compile(r'\s+ItemContainerStyle="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_increase_decrease_repeat_button(content):
    n = 0
    new_content = content
    for prop in ['IncreaseRepeatButton', 'DecreaseRepeatButton']:
        pattern = re.compile(
            rf'\s*<(\w+)\.{prop}>.*?</\1\.{prop}>',
            re.DOTALL
        )
        new_content, count = pattern.subn('', new_content)
        n += count
    return new_content, n

def fix_stylus_namespace(content):
    pattern = re.compile(r'\s+Stylus\.\w+="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_target_type_in_standalone_controltemplate(content):
    pattern = re.compile(r'TargetType="\{x:Type\s+([\w:]+)\}"')
    new_content, n = pattern.subn(lambda m: f'TargetType="{m.group(1)}"', content)
    return new_content, n

def process_file(path):
    try:
        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception as e:
        return 0
    
    original = content
    total_changes = 0
    
    fixers = [
        fix_style_dynamic_resource,
        fix_tooltip_tip_property_element,
        fix_tooltip_property_element,
        fix_selector_namespace,
        fix_content_source,
        fix_rectangle_cornerRadius,
        fix_xname_on_columndefinition,
        fix_remove_allows_transparency,
        fix_remove_popup_animation,
        fix_remove_shadow_depth,
        fix_remove_can_content_scroll,
        fix_remove_content_string_format,
        fix_remove_directional_navigation,
        fix_remove_horizontal_vertical_scrollbar_isvisible,
        fix_remove_uid,
        fix_remove_mouse_double_click,
        fix_remove_context_menu_opening,
        fix_remove_item_container_style,
        fix_remove_increase_decrease_repeat_button,
        fix_stylus_namespace,
        fix_target_type_in_standalone_controltemplate,
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
