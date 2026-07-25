#!/usr/bin/env python3
"""Phase 4: Handle remaining patterns.
- Style property element with namespaced prefix (controls:X.Style)
- ItemContainerStyle property element
- Foreground Setter for non-Foreground controls (use TextElement.Foreground)
- Line X1/Y1/X2/Y2 (Avalonia uses different syntax)
- IsMouseOver Setter (WPF-only, replace with :pointerover pseudo)
- BorderBrush Setter for specific types
- ListView/ListViewItem bridge
- ToolTip.Tip remaining cases
- StaysOpen on Popup
- PlacementRectangle on Popup
- Other patterns"""
import re
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")

def fix_namespaced_style_property(content):
    """Remove <ns:Control.Style>...</ns:Control.Style> blocks (with namespace prefix)."""
    # Pattern: <prefix:Type.Style>...</prefix:Type.Style>
    pattern = re.compile(
        r'\s*<([\w]+:[\w]+)\.Style>\s*<(?:DynamicResource|StaticResource)\s+[^/]*/>\s*</\1\.Style>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_item_container_style_property_element(content):
    """Remove <X.ItemContainerStyle>...</X.ItemContainerStyle> blocks."""
    # Property element: <X.ItemContainerStyle><Style .../>...</X.ItemContainerStyle>
    pattern = re.compile(
        r'\s*<(\w[\w\.]*)\.ItemContainerStyle>.*?</\1\.ItemContainerStyle>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_item_container_style_attribute(content):
    """Remove ItemContainerStyle attribute (catch any we missed)."""
    # Match as attribute, including with whitespace variations
    pattern = re.compile(r'\s+ItemContainerStyle="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_foreground_setter_on_border(content):
    """Convert Setter Property="Foreground" to Property="TextElement.Foreground"
    when targeting Border/Popup (which don't have Foreground in Avalonia)."""
    # This is tricky - need to know what type the Setter is targeting.
    # Simple approach: convert all "Foreground" Setter properties inside Style selectors
    # targeting Border/Popup to "TextElement.Foreground"
    
    # Actually, simpler: just convert all <Setter Property="Foreground" ...> to 
    # <Setter Property="TextElement.Foreground" ...> in style contexts.
    # TextElement.Foreground works on all elements that support foreground inheritance.
    pattern = re.compile(r'<Setter\s+Property="Foreground"')
    new_content, n = pattern.subn('<Setter Property="TextElement.Foreground"', content)
    return new_content, n

def fix_line_x_y_properties(content):
    """Convert Line X1/Y1/X2/Y2 to Avalonia's StartPoint/EndPoint.
    Avalonia's Line uses StartPoint and EndPoint instead of X1/Y1/X2/Y2.
    Actually, Avalonia 11 Line DOES have X1/Y1/X2/Y2 properties. The error must be something else.
    Skip - investigate later."""
    return content, 0

def fix_is_mouse_over_setter(content):
    """Replace Setter Property="IsMouseOver" with appropriate pseudo-class.
    Actually IsMouseOver is read-only in Avalonia - we can't set it. Remove these setters."""
    # <Setter Property="IsMouseOver" Value="..." />
    pattern = re.compile(
        r'\s*<Setter\s+Property="IsMouseOver"\s+Value="[^"]*"\s*/>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_stays_open_on_popup(content):
    """Convert Popup StaysOpen to Popup IsLightDismissEnabled (inverted).
    StaysOpen=True -> IsLightDismissEnabled=False
    StaysOpen=False -> IsLightDismissEnabled=True"""
    def replacer(m):
        val = m.group(1)
        new_val = 'False' if val == 'True' else 'True'
        return f'IsLightDismissEnabled="{new_val}"'
    
    pattern = re.compile(r'StaysOpen="(True|False)"')
    new_content, n = pattern.subn(replacer, content)
    return new_content, n

def fix_placement_rectangle_on_popup(content):
    """Remove PlacementRectangle from Popup (WPF-only)."""
    pattern = re.compile(r'\s+PlacementRectangle="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_tooltip_tip_property_element_v2(content):
    """Convert <Control.ToolTip.Tip>...</Control.ToolTip.Tip> to <ToolTip.Tip>...</ToolTip.Tip>.
    Handles namespaced prefixes too."""
    # Pattern: <prefix:Type.ToolTip.Tip>...</prefix:Type.ToolTip.Tip>
    pattern = re.compile(
        r'<([\w]+:[\w]+)\.ToolTip\.Tip>(.*?)</\1\.ToolTip\.Tip>',
        re.DOTALL
    )
    new_content, n = pattern.subn(r'<ToolTip.Tip>\2</ToolTip.Tip>', content)
    return new_content, n

def fix_tooltip_property_element_v2(content):
    """Convert <prefix:Control.ToolTip>...</prefix:Control.ToolTip> to <ToolTip.Tip>...</ToolTip.Tip>."""
    pattern = re.compile(
        r'<([\w]+:[\w]+)\.ToolTip>(.*?)</\1\.ToolTip>',
        re.DOTALL
    )
    new_content, n = pattern.subn(r'<ToolTip.Tip>\2</ToolTip.Tip>', content)
    return new_content, n

def fix_remove_view_property_element(content):
    """Remove <X.View>...</X.View> blocks (WPF GridView)."""
    pattern = re.compile(
        r'\s*<(\w[\w\.]*)\.View>.*?</\1\.View>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_view_attribute(content):
    """Remove View="..." attribute."""
    pattern = re.compile(r'\s+View="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_template_setter(content):
    """Handle Setter Property="Template" errors - probably issue with the template content."""
    # Skip - investigate
    return content, 0

def fix_xkey_with_xtype_v2(content):
    """Convert x:Key="{x:Type X}" to x:Key="X" - broader pattern."""
    pattern = re.compile(r'x:Key="\{x:Type\s+([\w:]+)\}"')
    new_content, n = pattern.subn(lambda m: f'x:Key="{m.group(1)}"', content)
    return new_content, n

def fix_remove_resources_on_setter(content):
    """Remove <Setter.Resources> blocks (WPF-only)."""
    pattern = re.compile(
        r'\s*<Setter\.Resources>.*?</Setter\.Resources>',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n

def fix_remove_path_property_element(content):
    """Remove <X.Path>...</X.Path> property element (WPF-only on certain types)."""
    # Skip - investigate
    return content, 0

def fix_remove_borderbrush_setter_on_path(content):
    """For Path shape, BorderBrush doesn't exist. Skip - investigate."""
    return content, 0

def fix_xstatic_in_setter_value(content):
    """Convert Setter Value="{x:Static SystemColors.XKey}" to a DynamicResource."""
    # <Setter Property="X" Value="{x:Static SystemColors.GrayTextBrushKey}" />
    # -> <Setter Property="X" Value="{DynamicResource SystemGrayTextBrush}" />
    # We need a mapping of SystemColors.XKey -> resource name
    color_map = {
        'GrayTextBrushKey': 'SystemGrayTextBrush',
        'ControlTextBrushKey': 'SystemControlTextBrush',
        'WindowBrushKey': 'SystemWindowBrush',
        'ActiveCaptionBrushKey': 'SystemActiveCaptionBrush',
        'InactiveCaptionBrushKey': 'SystemInactiveCaptionBrush',
        'MenuBrushKey': 'SystemMenuBrush',
        'MenuBarBrushKey': 'SystemMenuBarBrush',
        'MenuTextBrushKey': 'SystemMenuTextBrush',
        'WindowTextBrushKey': 'SystemWindowTextBrush',
        'HighlightBrushKey': 'SystemHighlightBrush',
        'HighlightTextBrushKey': 'SystemHighlightTextBrush',
        'ControlBrushKey': 'SystemControlBrush',
        'ControlDarkBrushKey': 'SystemControlDarkBrush',
        'ControlLightBrushKey': 'SystemControlLightBrush',
        'InactiveSelectionHighlightBrushKey': 'SystemInactiveSelectionHighlightBrush',
        'InactiveSelectionHighlightTextBrushKey': 'SystemInactiveSelectionHighlightTextBrush',
    }
    
    def replacer(m):
        prop = m.group(1)
        key_name = m.group(2)
        resource_name = color_map.get(key_name, f'System{key_name}')
        return f'<Setter Property="{prop}" Value="{{DynamicResource {resource_name}}}" />'
    
    pattern = re.compile(
        r'<Setter\s+Property="([^"]+)"\s+Value="\{x:Static\s+SystemColors\.(\w+)\}"\s*/>'
    )
    new_content, n = pattern.subn(replacer, content)
    return new_content, n

def fix_xstatic_systemparams_in_tooltip(content):
    """Convert ToolTip.Tip="{x:Static SystemParameters.X}" to literal string or empty."""
    # For ToolTip.Tip, just remove
    pattern = re.compile(r'\s+ToolTip\.Tip="\{x:Static\s+SystemParameters\.[^"]*\}"')
    new_content, n = pattern.subn('', content)
    # Also handle attribute form
    pattern = re.compile(r'\s+ToolTip="\{x:Static\s+SystemParameters\.[^"]*\}"')
    new_content, n2 = pattern.subn('', new_content)
    return new_content, n + n2

def fix_xstatic_systemcolors_in_tooltip(content):
    """Convert ToolTip="{x:Static SystemColors.X}" to DynamicResource."""
    pattern = re.compile(r'ToolTip="\{x:Static\s+SystemColors\.(\w+)\}"')
    def replacer(m):
        key_name = m.group(1)
        # Convert to DynamicResource
        if key_name.endswith('Key'):
            resource_name = 'System' + key_name[:-3]
        else:
            resource_name = 'System' + key_name
        return f'ToolTip.Tip="{{DynamicResource {resource_name}}}"'
    new_content, n = pattern.subn(replacer, content)
    return new_content, n

def fix_xstatic_in_attribute_general(content):
    """Handle other {x:Static X.Y} patterns in attributes."""
    # Generic: replace {x:Static SomeType.Member} with empty string for problematic cases
    # Only do this if the attribute is optional
    # Skip - too risky
    return content, 0

def fix_remove_static_extension_type(content):
    """Fix 'Unable to resolve type StaticExtension' errors - these are usually from
    {x:Static ...} usage in unexpected places. Investigate."""
    return content, 0

def fix_borderbrush_on_path_setter(content):
    """For Path, BorderBrush doesn't exist. Replace with Stroke? No, Path has Stroke already."""
    # Skip
    return content, 0

def fix_remove_actual_width_height_binding(content):
    """Replace Path="ActualWidth" with Path="Bounds.Width" - already done in phase 2.
    Catch any remaining."""
    new_content = content
    n = 0
    if 'Path="ActualWidth"' in content:
        new_content = new_content.replace('Path="ActualWidth"', 'Path="Bounds.Width"')
        n += content.count('Path="ActualWidth"')
    if 'Path="ActualHeight"' in content:
        new_content = new_content.replace('Path="ActualHeight"', 'Path="Bounds.Height"')
        n += content.count('Path="ActualHeight"')
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
        fix_namespaced_style_property,
        fix_item_container_style_property_element,
        fix_item_container_style_attribute,
        fix_foreground_setter_on_border,
        fix_is_mouse_over_setter,
        fix_stays_open_on_popup,
        fix_placement_rectangle_on_popup,
        fix_tooltip_tip_property_element_v2,
        fix_tooltip_property_element_v2,
        fix_remove_view_property_element,
        fix_remove_view_attribute,
        fix_xkey_with_xtype_v2,
        fix_remove_resources_on_setter,
        fix_xstatic_in_setter_value,
        fix_xstatic_systemparams_in_tooltip,
        fix_xstatic_systemcolors_in_tooltip,
        fix_remove_actual_width_height_binding,
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
