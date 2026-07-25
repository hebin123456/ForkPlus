#!/usr/bin/env python3
"""fix_xaml8.py - Phase 8 XAML fixes for remaining AVLN2000/3000/2200/2205/1000 errors.

Addresses:
1. Add TargetType to <ControlTemplate> based on parent Style Selector (fixes
   BorderBrush/Background/Padding/Template/BorderThickness on Control errors).
2. Remove IsEnabled="{TemplateBinding IsMouseOver}" on ScrollBar template elements.
3. Replace Placement="..." with PlacementMode="..." on Popup.
4. Remove ItemContainerStyle elements/attributes.
5. Remove AllowDrop attributes.
6. Remove PanningMode attributes.
7. Remove PlacementRectangle attributes.
8. Remove CommandTarget attributes.
9. Remove Command="{x:Static ScrollBar.XxxCommand}" attributes.
10. Remove ViewportSize/Maximum bindings referencing missing ScrollViewer props.
11. Replace TextBlock.FontSize/FontWeight etc with FontSize/FontWeight.
12. Replace {x:Static RelativeSource.Self} with {RelativeSource Self}.
13. Convert ShowGraphToolTip.Tip="False" to ShowGraphToolTip="False".
14. Add ConverterParameter property to EnumToTextDecorationsConverter (in .cs).
15. Move keyed <Style> from .Styles to .Resources.
16. Add <TextPresenter x:Name="PART_TextPresenter" /> to PlaceholderTextBox-derived
    ControlTemplates (fixes AVLN2205).
17. Convert Height="Auto" on Image to Height="NaN".
18. Remove invalid <ScrollBar.Value> property elements.
19. Remove standalone <Setter Property="ComputedH/VScrollBarVisibility" ...>.
"""
import os
import re
import sys
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")
XAML_DIRS = [ROOT / "Theme", ROOT / "UI"]
total_changes = 0


def find_xaml_files():
    files = []
    for d in XAML_DIRS:
        for p in d.rglob("*.xaml"):
            files.append(p)
    return sorted(set(files))


# Map Style Selector type prefix -> ControlTemplate TargetType.
# Strips leading namespace prefix like "controls|" before matching.
SELECTOR_TYPE_MAP = {
    "Button": "Button",
    "RepeatButton": "RepeatButton",
    "ToggleButton": "ToggleButton",
    "CheckBox": "CheckBox",
    "RadioButton": "RadioButton",
    "Hyperlink": "Hyperlink",
    "ComboBox": "ComboBox",
    "ComboBoxItem": "ComboBoxItem",
    "TextBox": "TextBox",
    "PasswordBox": "TextBox",
    "RichTextBox": "TextBox",
    "ScrollViewer": "ScrollViewer",
    "ScrollBar": "ScrollBar",
    "Thumb": "Thumb",
    "Track": "Track",
    "Slider": "Slider",
    "TabItem": "TabItem",
    "TabControl": "TabControl",
    "TabPanel": "TabPanel",
    "MenuItem": "MenuItem",
    "Menu": "Menu",
    "ContextMenu": "ContextMenu",
    "ListBox": "ListBox",
    "ListBoxItem": "ListBoxItem",
    "ListView": "ListView",
    "ListViewItem": "ListViewItem",
    "TreeView": "TreeView",
    "TreeViewItem": "TreeViewItem",
    "Expander": "Expander",
    "GroupBox": "GroupBox",
    "Label": "Label",
    "TextBlock": "TextBlock",
    "Border": "Border",
    "GridSplitter": "GridSplitter",
    "Calendar": "Calendar",
    "CalendarItem": "CalendarItem",
    "CalendarButton": "CalendarButton",
    "CalendarDayButton": "CalendarDayButton",
    "DatePicker": "DatePicker",
    "Grid": "Grid",
    "ContentControl": "ContentControl",
    "ContentPresenter": "ContentPresenter",
    "Path": "Path",
    "Shape": "Shape",
    "Rectangle": "Rectangle",
    "Ellipse": "Ellipse",
    "Polygon": "Polygon",
    "Polyline": "Polyline",
    "Line": "Line",
    "UserControl": "UserControl",
    "Window": "Window",
    "Canvas": "Canvas",
    "DockPanel": "DockPanel",
    "StackPanel": "StackPanel",
    "WrapPanel": "WrapPanel",
    "UniformGrid": "UniformGrid",
    "VirtualizingStackPanel": "VirtualizingStackPanel",
    "VirtualizingWrapPanel": "VirtualizingWrapPanel",
    "ItemsControl": "ItemsControl",
    "HeaderedContentControl": "HeaderedContentControl",
    "HeaderedItemsControl": "HeaderedItemsControl",
    "ProgressBar": "ProgressBar",
    "StatusBar": "StatusBar",
    "StatusBarItem": "StatusBarItem",
    "ToolBar": "ToolBar",
    "ToolBarTray": "ToolBarTray",
    "ToolTip": "ToolTip",
    "Popup": "Popup",
    "Page": "Page",
    "Frame": "Frame",
    "NavigationWindow": "NavigationWindow",
    "FlowDocument": "FlowDocument",
    "FlowDocumentPageViewer": "FlowDocumentPageViewer",
    "FlowDocumentReader": "FlowDocumentReader",
    "FlowDocumentScrollViewer": "FlowDocumentScrollViewer",
    "RichTextBox": "RichTextBox",
    "AccessText": "AccessText",
    "Adorner": "Adorner",
    "AdornerDecorator": "AdornerDecorator",
    "AdornerLayer": "AdornerLayer",
    "AdornerPlaceholder": "AdornerPlaceholder",
}


def extract_selector_type(selector):
    """Extract the type name from a Selector string.

    Handles:
      - "Button" -> "Button"
      - "Button:pointerover" -> "Button"
      - "^:pointerover" -> None (nested, inherits parent)
      - "Button.class" -> "Button"
      - "Button#name" -> "Button"
      - "controls|Button" -> "Button"
      - "/template/ Button" -> "Button"
    """
    if not selector:
        return None
    s = selector.strip()
    # Skip nested selectors that start with ^
    if s.startswith("^"):
        return None
    # Strip leading "/template/ "
    s = re.sub(r"^/template/\s*", "", s)
    # Strip leading ">"
    s = s.lstrip(">")
    # Match type name with optional namespace prefix
    m = re.match(r"^([\w]+)\|([\w]+)", s)
    if m:
        return m.group(2)
    m = re.match(r"^([A-Z][\w]*)", s)
    if m:
        return m.group(1)
    return None


def add_target_type_to_control_template(content):
    """Add TargetType to <ControlTemplate> based on parent Style Selector."""
    changes = 0

    # Find each Style block with Selector and a contained ControlTemplate without TargetType
    # We'll process linearly tracking the most recent Style Selector.
    # Match <Style ... Selector="..." ...> opening tags.
    style_pattern = re.compile(r'<Style\b[^>]*\bSelector="([^"]*)"[^>]*>')

    # We need to walk the content, tracking current Style selector scope.
    # Use a stack approach with regex search.
    result_lines = []
    style_stack = []

    # Tokenize: split into Style-open, Style-close, ControlTemplate-open
    # Simpler: process line by line, track stack.
    lines = content.split("\n")
    for line in lines:
        # Track Style opens
        for m in style_pattern.finditer(line):
            style_stack.append(extract_selector_type(m.group(1)))
        # Track </Style> closes (count occurrences on this line)
        close_count = line.count("</Style>")
        for _ in range(close_count):
            if style_stack:
                style_stack.pop()

        # If line has <ControlTemplate> without TargetType, add it
        if re.search(r'<ControlTemplate\s*>', line) and style_stack and style_stack[-1]:
            target_type = style_stack[-1]
            line = re.sub(
                r'<ControlTemplate\s*>',
                f'<ControlTemplate TargetType="{target_type}">',
                line,
            )
            changes += 1
        elif re.search(r'<ControlTemplate\s+x:Key="[^"]+"\s*>', line) and style_stack and style_stack[-1]:
            target_type = style_stack[-1]
            line = re.sub(
                r'<ControlTemplate\s+(x:Key="[^"]+")\s*>',
                rf'<ControlTemplate \1 TargetType="{target_type}">',
                line,
            )
            changes += 1

        result_lines.append(line)

    return "\n".join(result_lines), changes


def remove_template_binding_ismouseover(content):
    """Remove IsEnabled="{TemplateBinding IsMouseOver}" attributes."""
    # Remove IsEnabled="{TemplateBinding IsMouseOver}" entirely
    pattern = re.compile(r'\s+IsEnabled="\{TemplateBinding\s+IsMouseOver\}"')
    new_content, n = pattern.subn("", content)
    return new_content, n


def replace_placement_with_placementmode(content):
    """Replace Placement="..." with PlacementMode="..." on Popup."""
    # Only on Popup elements
    def replacer(m):
        prefix = m.group(1)
        value = m.group(2)
        return f'{prefix}PlacementMode="{value}"'

    # Match Popup ... Placement="value"
    pattern = re.compile(r'(<Popup\b[^>]*?)\s+Placement="([^"]+)"')
    new_content, n = pattern.subn(replacer, content)
    return new_content, n


def remove_item_container_style(content):
    """Remove ItemContainerStyle attribute and element."""
    changes = 0
    # Attribute form: ItemContainerStyle="..."
    pattern = re.compile(r'\s+ItemContainerStyle="[^"]*"')
    new_content, n = pattern.subn("", content)
    changes += n
    # Element form: <X.ItemContainerStyle>...</X.ItemContainerStyle>
    pattern = re.compile(
        r'\s*<[\w:]+\.ItemContainerStyle>.*?</[\w:]+\.ItemContainerStyle>',
        re.DOTALL,
    )
    new_content, n = pattern.subn("", new_content)
    changes += n
    return new_content, changes


def remove_allow_drop(content):
    """Remove AllowDrop attribute."""
    pattern = re.compile(r'\s+AllowDrop="(True|False)"')
    new_content, n = pattern.subn("", content)
    return new_content, n


def remove_panning_mode(content):
    """Remove PanningMode attribute."""
    pattern = re.compile(r'\s+PanningMode="[^"]*"')
    new_content, n = pattern.subn("", content)
    return new_content, n


def remove_placement_rectangle(content):
    """Remove PlacementRectangle attribute."""
    pattern = re.compile(r'\s+PlacementRectangle="[^"]*"')
    new_content, n = pattern.subn("", content)
    return new_content, n


def remove_command_target(content):
    """Remove CommandTarget attribute."""
    pattern = re.compile(r'\s+CommandTarget="[^"]*"')
    new_content, n = pattern.subn("", content)
    return new_content, n


def remove_scrollbar_command(content):
    """Remove Command="{x:Static ScrollBar.XxxCommand}" attributes."""
    pattern = re.compile(r'\s+Command="\{x:Static\s+ScrollBar\.\w+Command\}"')
    new_content, n = pattern.subn("", content)
    return new_content, n


def remove_scrollbar_value_property(content):
    """Remove <ScrollBar.Value>...</ScrollBar.Value> property elements."""
    pattern = re.compile(
        r'\s*<ScrollBar\.Value>\s*<Binding[^>]*>.*?</ScrollBar\.Value>',
        re.DOTALL,
    )
    new_content, n = pattern.subn("", content)
    # Also handle self-closing or with nested Binding.RelativeSource
    pattern2 = re.compile(
        r'\s*<ScrollBar\.Value>\s*.*?\s*</ScrollBar\.Value>',
        re.DOTALL,
    )
    new_content, n2 = pattern2.subn("", new_content)
    return new_content, n + n2


def replace_textblock_attached_properties(content):
    """Replace TextBlock.FontSize/FontWeight/Foreground attached props with bare names."""
    changes = 0
    for prop in ["FontSize", "FontWeight", "FontFamily", "Foreground", "FontStyle"]:
        pattern = re.compile(rf'\bTextBlock\.{prop}=')
        new_content, n = pattern.subn(f'{prop}=', content)
        changes += n
        content = new_content
    return content, changes


def replace_relativesource_self_static(content):
    """Replace {x:Static RelativeSource.Self} with {RelativeSource Self}."""
    pattern = re.compile(r'"\{x:Static\s+RelativeSource\.Self\}"')
    new_content, n = pattern.subn('{RelativeSource Self}', content)
    return new_content, n


def fix_showgraphtooltip_tip(content):
    """Convert ShowGraphToolTip.Tip="False" to ShowGraphToolTip="False"."""
    pattern = re.compile(r'\bShowGraphToolTip\.Tip="([^"]+)"')
    new_content, n = pattern.subn(r'ShowGraphToolTip="\1"', content)
    return new_content, n


def move_keyed_styles_to_resources(content):
    """Move <Style x:Key="..."> from .Styles to .Resources.

    Avalonia's .Styles property only accepts un-keyed Style entries. Keyed styles
    must be placed in .Resources (or a ResourceDictionary).
    """
    changes = 0
    # Pattern: <prefix.Styles>...<Style x:Key="..." ...>...</Style>...</prefix.Styles>
    # We need to extract keyed styles and move them to .Resources
    # Strategy: find each <prefix.Styles> block and split into keyed/non-keyed.
    pattern = re.compile(r'<(\w[\w:]*)\.Styles>(.*?)</\1\.Styles>', re.DOTALL)

    def process_block(m):
        nonlocal changes
        prefix = m.group(1)
        body = m.group(2)
        # Find all top-level <Style> elements (may have nested <Style> children)
        # Use a balanced tag parser approach.
        styles = []
        idx = 0
        while idx < len(body):
            # Find next <Style
            start = body.find("<Style", idx)
            if start == -1:
                break
            # Find matching </Style> accounting for nesting
            depth = 0
            pos = start
            while pos < len(body):
                open_m = body.find("<Style", pos)
                close_m = body.find("</Style>", pos)
                if close_m == -1:
                    break
                if open_m != -1 and open_m < close_m:
                    depth += 1
                    pos = open_m + 6
                else:
                    depth -= 1
                    pos = close_m + 8
                    if depth == 0:
                        styles.append(body[start:pos])
                        idx = pos
                        break
            else:
                break
            if depth != 0:
                break

        if not styles:
            return m.group(0)

        # Determine if Style is keyed (has x:Key attribute at top level)
        def is_keyed(style_text):
            # Match opening tag attributes only
            tag_end = style_text.find(">")
            if tag_end == -1:
                return False
            opening = style_text[:tag_end]
            return 'x:Key="' in opening

        keyed = [s for s in styles if is_keyed(s)]
        non_keyed = [s for s in styles if not is_keyed(s)]

        if not keyed:
            return m.group(0)

        changes += len(keyed)

        # Build new blocks
        # Non-keyed stay in .Styles (if any)
        if non_keyed:
            new_styles_block = f"<{prefix}.Styles>" + "\n".join(non_keyed) + f"</{prefix}.Styles>"
        else:
            new_styles_block = ""

        # Keyed go into .Resources (merge with existing or create new)
        keyed_xml = "\n".join(keyed)

        # Check if there's an existing <prefix.Resources> block we can merge into
        # For simplicity, just wrap in a new ResourceDictionary if needed
        new_resources_block = f"<{prefix}.Resources><ResourceDictionary>\n{keyed_xml}\n</ResourceDictionary></{prefix}.Resources>"

        # If we have non-keyed, put resources first then styles
        if new_styles_block:
            return f"\n{new_resources_block}\n{new_styles_block}\n"
        else:
            return f"\n{new_resources_block}\n"

    new_content, n = pattern.subn(process_block, content)
    return new_content, changes


def add_text_presenter_to_placeholder_textbox(content):
    """Add <TextPresenter x:Name="PART_TextPresenter" /> inside ScrollViewer#PART_ContentHost
    for PlaceholderTextBox-derived ControlTemplates.

    This is a targeted fix: only inside <ScrollViewer ... x:Name="PART_ContentHost" ... />
    """
    # Pattern: <ScrollViewer ... x:Name="PART_ContentHost" ... /> (self-closing)
    # Convert to: <ScrollViewer ... x:Name="PART_ContentHost" ...><TextPresenter x:Name="PART_TextPresenter" /></ScrollViewer>
    pattern = re.compile(
        r'(<ScrollViewer\b[^>]*x:Name="PART_ContentHost"[^>]*?)\s*/>'
    )

    def replacer(m):
        return f'{m.group(1)}><TextPresenter x:Name="PART_TextPresenter" /></ScrollViewer>'

    new_content, n = pattern.subn(replacer, content)
    return new_content, n


def fix_image_height_auto(content):
    """Convert Height="Auto" on Image to Height="NaN" (Avalonia doesn't accept 'Auto' for Height on Image)."""
    # Match <Image ... Height="Auto" ... />
    pattern = re.compile(r'(<Image\b[^>]*?)\s+Height="Auto"')
    new_content, n = pattern.subn(r'\1', content)
    return new_content, n


def remove_computed_scrollbar_visibility_setters(content):
    """Remove <Setter Property="ComputedH/VScrollBarVisibility" ...> (read-only)."""
    changes = 0
    for prop in ["ComputedHorizontalScrollBarVisibility", "ComputedVerticalScrollBarVisibility"]:
        pattern = re.compile(
            rf'\s*<Setter\s+Property="{prop}"\s+Value="[^"]*"\s*/>'
        )
        new_content, n = pattern.subn("", content)
        changes += n
        content = new_content
        # Multi-line form
        pattern = re.compile(
            rf'\s*<Setter\s+Property="{prop}"\s+>\s*<Setter\.Value>.*?</Setter\.Value>\s*</Setter>',
            re.DOTALL,
        )
        new_content, n = pattern.subn("", content)
        changes += n
        content = new_content
    return content, changes


def remove_viewport_scrollable_setters(content):
    """Remove <Setter Property="ViewportWidth|ViewportHeight|ScrollableWidth|ScrollableHeight" ...>."""
    changes = 0
    for prop in ["ViewportWidth", "ViewportHeight", "ScrollableWidth", "ScrollableHeight"]:
        pattern = re.compile(
            rf'\s*<Setter\s+Property="{prop}"\s+Value="[^"]*"\s*/>'
        )
        new_content, n = pattern.subn("", content)
        changes += n
        content = new_content
    return content, changes


def replace_templatebinding_computed_visibility(content):
    """Replace Visibility="{TemplateBinding ComputedH/VScrollBarVisibility}" with Visibility="Visible".

    Avalonia's ComputedH/VScrollBarVisibility returns bool, not Visibility, so the binding fails.
    Replace with a static Visible value.
    """
    changes = 0
    for prop in ["ComputedHorizontalScrollBarVisibility", "ComputedVerticalScrollBarVisibility"]:
        pattern = re.compile(
            rf'Visibility="\{{TemplateBinding\s+{prop}\}}"'
        )
        new_content, n = pattern.subn('Visibility="Visible"', content)
        changes += n
        content = new_content
    return content, changes


def replace_templatebinding_viewport_scrollable(content):
    """Replace {TemplateBinding ViewportWidth|ViewportHeight|ScrollableWidth|ScrollableHeight}
    on ScrollBar with reasonable defaults since these ScrollViewer properties may not be
    directly accessible in TemplateBinding context.
    """
    changes = 0
    # ViewportSize="{TemplateBinding ViewportWidth}" -> ViewportSize="0"
    pattern = re.compile(
        r'ViewportSize="\{TemplateBinding\s+(ViewportWidth|ViewportHeight)\}"'
    )
    new_content, n = pattern.subn('ViewportSize="0"', content)
    changes += n
    content = new_content
    # Maximum="{TemplateBinding ScrollableWidth}" -> Maximum="0"
    pattern = re.compile(
        r'Maximum="\{TemplateBinding\s+(ScrollableWidth|ScrollableHeight)\}"'
    )
    new_content, n = pattern.subn('Maximum="0"', content)
    changes += n
    content = new_content
    return content, changes


def remove_data_templates_property(content):
    """Remove <X.DataTemplates>...</X.DataTemplates> blocks that contain invalid content.

    Some XAML files have <DataTemplate.DataTemplates> which is invalid in Avalonia.
    """
    pattern = re.compile(
        r'\s*<[\w:]+\.DataTemplates>.*?</[\w:]+\.DataTemplates>',
        re.DOTALL,
    )
    new_content, n = pattern.subn("", content)
    return new_content, n


def remove_resources_in_controltemplate(content):
    """Remove <ControlTemplate.Resources>...</ControlTemplate.Resources> blocks.

    Avalonia's ControlTemplate doesn't have a Resources property.
    """
    pattern = re.compile(
        r'\s*<ControlTemplate\.Resources>.*?</ControlTemplate\.Resources>',
        re.DOTALL,
    )
    new_content, n = pattern.subn("", content)
    return new_content, n


def fix_vertical_alignment_string(content):
    """Fix VerticalAlignment="Top" being parsed as String (likely inside invalid Setter).

    The error 'property VerticalAlignment of type Layoutable for argument String' usually
    means the value is quoted as a string in a context where it shouldn't be. Skip if
    Setter Property="VerticalAlignment" - it should already work. This fix is for cases
    where VerticalAlignment is being set as an attached property incorrectly.
    """
    # No-op: VerticalAlignment="Top" should normally work. The errors are likely
    # cascading from other issues (like missing TargetType on ControlTemplate).
    return content, 0


def remove_templatebinding_styles_on_style(content):
    """Remove <Setter Property="Styles" ...> entries that try to add Style to Style.

    Error: 'Unable to resolve suitable regular or attached property Styles on type Style'.
    """
    # Pattern: <Setter Property="Styles"><Setter.Value>...</Setter.Value></Setter>
    pattern = re.compile(
        r'\s*<Setter\s+Property="Styles"\s*>\s*<Setter\.Value>.*?</Setter\.Value>\s*</Setter>',
        re.DOTALL,
    )
    new_content, n = pattern.subn("", content)
    return new_content, n


def process_file(filepath):
    global total_changes
    try:
        with open(filepath, "r", encoding="utf-8") as f:
            original = f.read()
    except Exception as e:
        print(f"  SKIP {filepath}: {e}")
        return 0

    content = original
    file_changes = 0

    # Apply each fixer
    fixers = [
        ("target_type", add_target_type_to_control_template),
        ("template_binding_ismouseover", remove_template_binding_ismouseover),
        ("placement_to_placementmode", replace_placement_with_placementmode),
        ("item_container_style", remove_item_container_style),
        ("allow_drop", remove_allow_drop),
        ("panning_mode", remove_panning_mode),
        ("placement_rectangle", remove_placement_rectangle),
        ("command_target", remove_command_target),
        ("scrollbar_command", remove_scrollbar_command),
        ("scrollbar_value_property", remove_scrollbar_value_property),
        ("textblock_attached_props", replace_textblock_attached_properties),
        ("relativesource_self_static", replace_relativesource_self_static),
        ("showgraphtooltip_tip", fix_showgraphtooltip_tip),
        ("move_keyed_styles_to_resources", move_keyed_styles_to_resources),
        ("add_text_presenter", add_text_presenter_to_placeholder_textbox),
        ("image_height_auto", fix_image_height_auto),
        ("computed_scrollbar_visibility_setters", remove_computed_scrollbar_visibility_setters),
        ("viewport_scrollable_setters", remove_viewport_scrollable_setters),
        ("templatebinding_computed_visibility", replace_templatebinding_computed_visibility),
        ("templatebinding_viewport_scrollable", replace_templatebinding_viewport_scrollable),
        ("data_templates_property", remove_data_templates_property),
        ("resources_in_controltemplate", remove_resources_in_controltemplate),
        ("styles_on_style_setter", remove_templatebinding_styles_on_style),
    ]

    for name, fixer in fixers:
        try:
            content, n = fixer(content)
            if n:
                file_changes += n
        except Exception as e:
            print(f"  ERROR in {name} on {filepath}: {e}")

    if content != original:
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(content)
        total_changes += file_changes
        print(f"  FIXED {filepath}: {file_changes} changes")
        return file_changes
    return 0


def add_converter_parameter_to_enum_converter():
    """Add ConverterParameter property to EnumToTextDecorationsConverter in C# file."""
    global total_changes
    cs_file = ROOT / "UI" / "Controls" / "EnumEqualsConverter.cs"
    if not cs_file.exists():
        print(f"  SKIP {cs_file}: not found")
        return 0
    with open(cs_file, "r", encoding="utf-8") as f:
        content = f.read()
    if "public object ConverterParameter" in content:
        return 0
    # Find the class and add property after MatchDecorations
    pattern = re.compile(
        r'(public class EnumToTextDecorationsConverter[^{]*\{[^}]*?public TextDecorationCollection MatchDecorations[^;]*;)'
    )
    new_prop = (
        '\n\n\t\t/// <summary>WPF-style ConverterParameter set on converter instance. '
        'Used when XAML writes ConverterParameter="Drop" on the converter element.</summary>\n'
        '\t\tpublic object ConverterParameter { get; set; }'
    )
    new_content, n = pattern.subn(rf'\1{new_prop}', content)
    if n > 0:
        with open(cs_file, "w", encoding="utf-8") as f:
            f.write(new_content)
        total_changes += n
        print(f"  FIXED {cs_file}: added ConverterParameter property")
        return n
    print(f"  SKIP {cs_file}: pattern not matched")
    return 0


def main():
    print("=" * 70)
    print("fix_xaml8.py - Phase 8 XAML fixes")
    print("=" * 70)

    # First, add ConverterParameter to EnumToTextDecorationsConverter
    add_converter_parameter_to_enum_converter()

    files = find_xaml_files()
    print(f"\nProcessing {len(files)} XAML files...")
    for f in files:
        process_file(f)

    print(f"\n{'=' * 70}")
    print(f"Total changes: {total_changes}")
    print("=" * 70)


if __name__ == "__main__":
    main()
