#!/usr/bin/env python3
"""
fix_xaml9.py — Phase 9 XAML/CS fixes for WPF→Avalonia migration (v2).

More careful, targeted fixes based on actual error patterns.
"""
import re
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")

# Custom controls and their namespaces
CONTROLS_NS = "clr-namespace:ForkPlus.UI.Controls"
DIALOGS_NS = "clr-namespace:ForkPlus.UI.Dialogs"
EDITOR_NS = "clr-namespace:ForkPlus.UI.Controls.Editor"

UI_CONTROLS = {
    "ToolbarButton", "ToolbarDropDownButton", "DateRangeButton",
    "EditableTextBlock", "CommandTextBox",
    "MultiselectionTreeView", "TreeViewControlItem",
    "PlaceholderTextBox", "AutoCompleteTextBox", "CommitDescriptionTextBox", "FilterTextBox",
    "ClosableTabControl", "ClosableTabItem",
    "DropDownButton",
    "GitPointView",
    "NoUIAutomationListView",
    "DragAndDropListViewItem",
}
DIALOG_CONTROLS = {"ForkPlusDialogWindow"}
EDITOR_CONTROLS = {"CodeEditor"}


def find_xaml_files():
    return list(ROOT.rglob("*.xaml"))


def read(path):
    return Path(path).read_text(encoding="utf-8")


def write(path, content):
    Path(path).write_text(content, encoding="utf-8")


# ---------------------------------------------------------------------------
# Fix 1: Add xmlns declarations if missing
# ---------------------------------------------------------------------------
def ensure_xmlns(content, ns_decl, ns_value):
    if ns_decl in content:
        return content, False
    # Match root element opening tag with xmlns="https://github.com/avaloniaui"
    pattern = re.compile(
        r'(<(?:ResourceDictionary|\w+:\w+|\w+)\b[^>]*?\bxmlns="https://github\.com/avaloniaui"[^>]*?)(/?>)',
        re.DOTALL,
    )
    m = pattern.search(content)
    if not m:
        return content, False
    new_content = content[:m.end(1)] + f' {ns_decl}="{ns_value}"' + content[m.start(2):]
    return new_content, True


# ---------------------------------------------------------------------------
# Fix 2: ControlTemplate TargetType prefix for custom controls
# ---------------------------------------------------------------------------
def fix_controltemplate_targettype(content):
    """Replace <ControlTemplate TargetType="X"> with <ControlTemplate TargetType="prefix:X">
    when X is a custom control."""
    changes = 0

    def replacer(m):
        nonlocal changes
        type_name = m.group(1)
        if type_name in UI_CONTROLS:
            changes += 1
            return f'<ControlTemplate TargetType="controls:{type_name}"'
        if type_name in DIALOG_CONTROLS:
            changes += 1
            return f'<ControlTemplate TargetType="dialogs:{type_name}"'
        if type_name in EDITOR_CONTROLS:
            changes += 1
            return f'<ControlTemplate TargetType="editor:{type_name}"'
        return m.group(0)

    pattern = re.compile(r'<ControlTemplate\s+TargetType="([^:]+?)"')
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 3: Add TargetType to <ControlTemplate x:Key="..."> based on x:Key name
# ---------------------------------------------------------------------------
XKEY_TO_TARGET_TYPE = {
    "ComboBoxTemplate": "ComboBox",
    "ComboBoxEditableTemplate": "ComboBox",
    "InteractiveRebaseComboBoxTemplate": "ComboBox",
    "TrackerControlDataTemplate": "Control",
}


def add_targettype_to_keyed_controltemplate(content):
    """For <ControlTemplate x:Key="X"> without TargetType, add TargetType based on x:Key."""
    changes = 0

    def replacer(m):
        nonlocal changes
        full = m.group(0)
        if "TargetType=" in full:
            return full
        xkey = m.group(1)
        target = XKEY_TO_TARGET_TYPE.get(xkey)
        if not target:
            # Try to infer from x:Key name: e.g. "ButtonTemplate" -> "Button"
            for ctl in ["ComboBox", "Button", "TextBox", "CheckBox", "ListBox",
                        "ListView", "TabItem", "TabControl", "ProgressBar"]:
                if ctl in xkey:
                    target = ctl
                    break
        if not target:
            return full
        changes += 1
        return full.rstrip(">") + f' TargetType="{target}">'

    pattern = re.compile(r'<ControlTemplate\s+x:Key="([^"]+)"[^>]*>')
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 4: Add Selector to <Style x:Key="X"> without Selector
# ---------------------------------------------------------------------------
def add_selector_to_unkeyed_style(content):
    """For <Style x:Key="X"> without Selector, add Selector="TemplatedControl" (or Control).
    Used by FocusVisual/OptionMarkFocusVisual keyed styles."""
    changes = 0

    def replacer(m):
        nonlocal changes
        full = m.group(0)
        if "Selector=" in full:
            return full
        xkey = m.group(1)
        # FocusVisual styles target Control (but Control has no Template in Avalonia)
        # Use TemplatedControl so the Template setter works
        changes += 1
        return full.rstrip(">") + ' Selector="TemplatedControl">'

    pattern = re.compile(r'<Style\s+x:Key="([^"]+)"\s*>')
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 5: Add TargetType to bare <ControlTemplate> (no attributes)
# ---------------------------------------------------------------------------
def add_targettype_to_bare_controltemplate(content):
    """For <ControlTemplate> without any attributes (inside FocusVisual styles),
    add TargetType="TemplatedControl"."""
    changes = 0

    def replacer(m):
        nonlocal changes
        changes += 1
        return '<ControlTemplate TargetType="TemplatedControl">'

    pattern = re.compile(r'<ControlTemplate(?!\s)[^>]*>')
    # Match <ControlTemplate> with no attributes (immediately followed by >)
    new_content = re.sub(r'<ControlTemplate>', replacer, content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 6: Fix bare attribute removal (AllowDrop, HasDropShadow, etc.)
# ---------------------------------------------------------------------------
WPF_ONLY_ATTRS = [
    "AllowDrop",
    "HasDropShadow",
    "PanningMode",
    "IsManipulationEnabled",
    "Stylus.IsFlicksEnabled",
    "VirtualizingPanel.IsVirtualizing",
    "VirtualizingPanel.VirtualizationMode",
    "ScrollViewer.CanContentScroll",
    "ScrollViewer.IsDeferredScrollingEnabled",
    "ComputedHorizontalScrollBarVisibility",
    "ComputedVerticalScrollBarVisibility",
    "CalendarDayButtonStyle",
    "CalendarButtonStyle",
    "ItemContainerStyle",
    "ItemContainerStyleSelector",
    "ItemTemplateSelector",
    "AlternationCount",
    "IsTextSearchEnabled",
    "TextSearch.TextPath",
    "VirtualizingStackPanel.IsVirtualizing",
    "VirtualizingStackPanel.VirtualizationMode",
]


def remove_wpf_only_attrs(content):
    """Remove WPF-only attributes (single-line Attr="value" patterns)."""
    changes = 0
    new_content = content
    for attr in WPF_ONLY_ATTRS:
        pattern = re.compile(r'\s+' + re.escape(attr) + r'="[^"]*"')
        new_content, n = pattern.subn("", new_content)
        changes += n
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 7: Remove WPF-only Setter (single-line: <Setter Property="X" Value="..." />)
# ---------------------------------------------------------------------------
WPF_ONLY_SETTER_PROPS = [
    "AllowDrop",
    "ToolTip.HasDropShadow",
    "HasDropShadow",
    "ContextMenu.VerticalContentAlignment",
    "ContextMenu.HorizontalContentAlignment",
    "Menu.VerticalContentAlignment",
    "Menu.HorizontalContentAlignment",
    "MenuItem.VerticalContentAlignment",
    "MenuItem.HorizontalContentAlignment",
    "CalendarDayButtonStyle",
    "CalendarButtonStyle",
]


def remove_wpf_only_setters(content):
    """Remove single-line <Setter Property="X" Value="..." /> for WPF-only properties.
    Only matches self-closing setters — does NOT touch multi-line setters."""
    changes = 0
    new_content = content
    for prop in WPF_ONLY_SETTER_PROPS:
        # Match <Setter Property="X" Value="..." /> (self-closing only)
        pattern = re.compile(
            r'[ \t]*<Setter\s+Property="' + re.escape(prop) + r'"\s+Value="[^"]*"\s*/>[ \t]*\n?',
        )
        new_content, n = pattern.subn("", new_content)
        changes += n
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 8: Remove WPF-only multi-line Setter (with <Setter.Value>...)
# ---------------------------------------------------------------------------
WPF_ONLY_MULTILINE_SETTER_PROPS = [
    "ComputedHorizontalScrollBarVisibility",
    "ComputedVerticalScrollBarVisibility",
]


def remove_wpf_only_multiline_setters(content):
    """Remove multi-line <Setter Property="X"> ... </Setter> for WPF-only properties."""
    changes = 0
    new_content = content
    for prop in WPF_ONLY_MULTILINE_SETTER_PROPS:
        # Match <Setter Property="X"> ... </Setter>
        pattern = re.compile(
            r'[ \t]*<Setter\s+Property="' + re.escape(prop) + r'">[ \t]*\n'
            r'.*?</Setter>[ \t]*\n?',
            re.DOTALL,
        )
        new_content, n = pattern.subn("", new_content)
        changes += n
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 9: Remove WPF-only property elements (<Popup.PlacementRectangle>...)
# ---------------------------------------------------------------------------
def remove_wpf_only_property_elements(content):
    """Remove property elements like <Popup.PlacementRectangle>...</Popup.PlacementRectangle>."""
    changes = 0
    new_content = content
    prop_elements = [
        "Popup.PlacementRectangle",
        "RepeatButton.CommandTarget",
    ]
    for prop_elem in prop_elements:
        # Match <Prop> ... </Prop>
        pattern = re.compile(
            r'[ \t]*<' + prop_elem + r'\b[^>]*>.*?</' + prop_elem + r'>[ \t]*\n?',
            re.DOTALL,
        )
        new_content, n = pattern.subn("", new_content)
        changes += n
        # Self-closing: <Prop ... />
        pattern_sc = re.compile(r'[ \t]*<' + prop_elem + r'\b[^/]*/>[ \t]*\n?')
        new_content, n2 = pattern_sc.subn("", new_content)
        changes += n2
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 10: Fix x:Key="{x:Static MenuItem.SeparatorStyleKey}"
# ---------------------------------------------------------------------------
def fix_menu_separator_style_key(content):
    pattern = re.compile(r'x:Key="\{x:Static\s+MenuItem\.SeparatorStyleKey\}"')
    new_content, n = pattern.subn('x:Key="MenuItemSeparatorStyle"', content)
    return new_content, n


# ---------------------------------------------------------------------------
# Fix 11: Fix Popup PlacementMode="Mouse" → "Pointer"
# ---------------------------------------------------------------------------
def fix_placement_mouse(content):
    pattern = re.compile(r'PlacementMode="Mouse"')
    new_content, n = pattern.subn('PlacementMode="Pointer"', content)
    return new_content, n


# ---------------------------------------------------------------------------
# Fix 12: Fix enum value casing (VerticalAlignment="top" → "Top")
# ---------------------------------------------------------------------------
def fix_enum_casing(content):
    changes = 0
    new_content = content
    for low, high in [("top", "Top"), ("bottom", "Bottom"), ("center", "Center"), ("stretch", "Stretch")]:
        pattern = re.compile(r'(VerticalAlignment=")' + low + r'(")')
        new_content, n = pattern.subn(r'\g<1>' + high + r'\g<2>', new_content)
        changes += n
    for low, high in [("left", "Left"), ("right", "Right"), ("center", "Center"), ("stretch", "Stretch")]:
        pattern = re.compile(r'(HorizontalAlignment=")' + low + r'(")')
        new_content, n = pattern.subn(r'\g<1>' + high + r'\g<2>', new_content)
        changes += n
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 13: Fix <DropDownButton.X> → <controls:DropDownButton.X>
# ---------------------------------------------------------------------------
def fix_drop_down_button_property_elements(content):
    changes = 0
    pattern = re.compile(r'<DropDownButton\.(\w+)>')
    new_content, n = pattern.subn(r'<controls:DropDownButton.\g<1>>', content)
    return new_content, n + changes


def fix_drop_down_button_property_close(content):
    changes = 0
    pattern = re.compile(r'</DropDownButton\.(\w+)>')
    new_content, n = pattern.subn(r'</controls:DropDownButton.\g<1>>', content)
    return new_content, n + changes


# ---------------------------------------------------------------------------
# Fix 14: Fix <Control.X> → <TemplatedControl.X>
# ---------------------------------------------------------------------------
def fix_control_property_elements(content):
    changes = 0
    templated_props = ["BorderBrush", "BorderThickness", "Background", "Foreground",
                       "FontFamily", "FontSize", "FontWeight", "FontStyle", "Padding",
                       "CornerRadius"]
    new_content = content
    for prop in templated_props:
        pattern = re.compile(r'<Control\.' + prop + r'\b')
        new_content, n = pattern.subn('<TemplatedControl.' + prop, new_content)
        changes += n
        pattern_close = re.compile(r'</Control\.' + prop + r'>')
        new_content, n = pattern_close.subn('</TemplatedControl.' + prop + '>', new_content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 15: Add TextElement. prefix to font properties on Panel-derived elements
# ---------------------------------------------------------------------------
PANEL_TAGS = ["StackPanel", "Canvas", "WrapPanel", "UniformGrid", "RelativePanel"]


def fix_panel_font_properties(content):
    """For StackPanel/Canvas/WrapPanel/etc (Panel-derived, not TemplatedControl),
    change FontFamily/FontSize/FontWeight/FontStyle/Foreground to TextElement.* attached."""
    changes = 0
    new_content = content
    for tag in PANEL_TAGS:
        def replacer(m):
            nonlocal changes
            tag_open = m.group(0)
            new_tag = tag_open
            for prop in ["FontFamily", "FontSize", "FontWeight", "FontStyle", "Foreground"]:
                # Only prefix if not already prefixed
                pat = re.compile(r'(?<!TextElement\.)\b' + prop + r'="')
                new_tag, n = pat.subn('TextElement.' + prop + '="', new_tag)
                changes += n
            return new_tag

        pattern = re.compile(r'<' + tag + r'\b[^>]*?(?<!/)>', re.DOTALL)
        new_content = pattern.sub(replacer, new_content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 16: Add x:Key to DataTemplate in ResourceDictionary (when no x:Key but has DataType)
# ---------------------------------------------------------------------------
def add_xkey_to_datatemplate_in_resourcedict(content):
    """When <DataTemplate DataType="prefix:TypeName"> is in a ResourceDictionary context,
    ensure it has an x:Key. We add x:Key="TypeNameDataTemplate" if missing."""
    changes = 0

    def replacer(m):
        nonlocal changes
        full_match = m.group(0)
        if "x:Key" in full_match:
            return full_match
        type_name = m.group(1)
        local_name = type_name.split(":", 1)[1] if ":" in type_name else type_name
        changes += 1
        return full_match.rstrip(">") + f' x:Key="{local_name}DataTemplate">'

    pattern = re.compile(r'<DataTemplate\s+DataType="([^"]+)"[^>]*>')
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 17: Move <Style> from <X.Resources> to <X.Styles> (when Style has no x:Key)
# ---------------------------------------------------------------------------
def move_unkeyed_style_out_of_resources(content):
    """If <X.Resources> contains <Style Selector="..."> (without x:Key),
    move it to <X.Styles>. Keyed Styles are kept in Resources."""
    changes = 0
    root_match = re.search(r'<([\w:]+)\b([^>]*)>', content)
    if not root_match:
        return content, 0
    root_tag = root_match.group(1)

    res_pattern = re.compile(
        r'<' + re.escape(root_tag) + r'\.Resources>\s*(.*?)(</' + re.escape(root_tag) + r'\.Resources>)',
        re.DOTALL,
    )
    res_match = res_pattern.search(content)
    if not res_match:
        return content, 0

    res_content = res_match.group(1)
    # Find <Style Selector="..."> without x:Key
    full_style_pattern = re.compile(
        r'(\s*)(<!--[^>]*-->\s*)?<Style\s+Selector="[^"]+"[^>]*>.*?</Style>\s*',
        re.DOTALL,
    )
    fsm = full_style_pattern.search(res_content)
    if not fsm:
        return content, 0

    # Verify the matched Style doesn't have x:Key
    if 'x:Key=' in fsm.group(0):
        return content, 0

    style_block = fsm.group(0).rstrip()
    new_res_content = res_content[:fsm.start()] + res_content[fsm.end():]
    new_content = content[:res_match.start(1)] + new_res_content + content[res_match.end(1):]

    styles_block = f"\n  <{root_tag}.Styles>{style_block}\n  </{root_tag}.Styles>\n"
    res_close_pattern = re.compile(r'</' + re.escape(root_tag) + r'\.Resources>')
    res_close_match = res_close_pattern.search(new_content)
    if not res_close_match:
        return content, 0
    new_content = (
        new_content[:res_close_match.end()]
        + styles_block
        + new_content[res_close_match.end():]
    )
    changes += 1
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 18: ProgressBar PART_Indicator
# ---------------------------------------------------------------------------
def fix_progressbar_template_part(content):
    pattern = re.compile(
        r'(<ControlTemplate\s+TargetType="ProgressBar"\s*>)(.*?)(</ControlTemplate>)',
        re.DOTALL,
    )
    m = pattern.search(content)
    if not m:
        return content, 0
    if "PART_Indicator" in m.group(0):
        return content, 0
    ct_inner = m.group(2)
    child_match = re.search(r'(<\w[\w:]*\b[^>]*>)(.*?)(</\w[\w:]*>)', ct_inner, re.DOTALL)
    if not child_match:
        return content, 0
    new_ct_inner = (
        ct_inner[:child_match.end(1)]
        + '<Border x:Name="PART_Indicator" IsVisible="False" />'
        + ct_inner[child_match.end(1):]
    )
    new_content = content[:m.start(2)] + new_ct_inner + content[m.end(2):]
    return new_content, 1


# ---------------------------------------------------------------------------
# Fix 19: Fix ButtonBase type in ControlTemplate TargetType
# ---------------------------------------------------------------------------
def fix_buttonbase_targettype(content):
    pattern = re.compile(r'(<ControlTemplate\s+TargetType=")ButtonBase(")')
    new_content, n = pattern.subn(r'\g<1>Button\g<2>', content)
    return new_content, n


# ---------------------------------------------------------------------------
# Fix 20: Fix Placement attribute on Popup → PlacementMode
# ---------------------------------------------------------------------------
def fix_placement_to_placementmode(content):
    changes = 0
    new_content = content
    pattern = re.compile(r'(<Popup\b[^>]*?)\s+Placement="([^"]+)"')
    new_content, n = pattern.subn(r'\g<1> PlacementMode="\g<2>"', new_content)
    changes += n
    new_content, n = re.subn(r'PlacementMode="Mouse"', 'PlacementMode="Pointer"', new_content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 21: Wrap inner <Style> in <Element.Styles> for ControlTemplate with multiple children
# ---------------------------------------------------------------------------
def wrap_style_in_element_styles(content):
    """For ControlTemplate with sibling Border + Style (where Style references the Border),
    wrap the Style inside <Border.Styles>."""
    changes = 0
    ct_pattern = re.compile(
        r'(<ControlTemplate\s+TargetType="[^"]+"\s*>)(.*?)(</ControlTemplate>)',
        re.DOTALL,
    )

    def ct_replacer(m):
        nonlocal changes
        ct_open, ct_inner, ct_close = m.group(1), m.group(2), m.group(3)
        # Find Border with x:Name="Bd" (or similar)
        border_match = re.search(
            r'(<Border\b[^>]*\bx:Name="(\w+)"[^>]*>)(.*?)(</Border>)',
            ct_inner,
            re.DOTALL,
        )
        if not border_match:
            return m.group(0)
        # Find sibling <Style> after the Border (with optional comment between)
        after_border = ct_inner[border_match.end():]
        style_match = re.search(
            r'\s*(<!--[^>]*-->\s*)?<Style\s+Selector="\^[:\w][^"]*">.*?</Style>\s*',
            after_border,
            re.DOTALL,
        )
        if not style_match:
            return m.group(0)
        border_name = border_match.group(2)
        style_content = style_match.group(0).strip()
        # Match <!-- comment --> + Style
        full_match_text = style_match.group(0)
        # Check if Style references the Border by name
        if f"#{border_name}" not in style_content:
            return m.group(0)
        # Check if Border already has .Styles
        if "<Border.Styles>" in border_match.group(0) or ".Styles>" in border_match.group(3):
            return m.group(0)
        # Move Style inside Border
        border_open = border_match.group(1)
        border_inner = border_match.group(3)
        border_close = border_match.group(4)
        new_border = (
            border_open
            + "\n<Border.Styles>\n"
            + style_content
            + "\n</Border.Styles>\n"
            + border_inner
            + border_close
        )
        new_ct_inner = (
            ct_inner[:border_match.start()] + new_border + ct_inner[border_match.end():]
        )
        # Remove the Style (and optional preceding comment) from outer content
        new_ct_inner = (
            new_ct_inner[:border_match.end() + style_match.start()]
            + "\n      "
            + new_ct_inner[border_match.end() + style_match.end():]
        )
        changes += 1
        return ct_open + new_ct_inner + ct_close

    new_content = ct_pattern.sub(ct_replacer, content)
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 22: Fix TextElement.Foreground in Hyperlink Style (Hyperlink doesn't have TextElement.Foreground directly)
# Actually it does — TextElement is an attached property. Skip.
# ---------------------------------------------------------------------------


# ---------------------------------------------------------------------------
# Fix 23: Fix ScrollViewer.ComputedVerticalScrollBarVisibility in TemplateBinding
# ---------------------------------------------------------------------------
def fix_templatebinding_computed_visibility(content):
    changes = 0
    new_content = content
    for prop in ["ComputedHorizontalScrollBarVisibility", "ComputedVerticalScrollBarVisibility"]:
        pattern = re.compile(r'\{TemplateBinding\s+' + prop + r'\}')
        new_content, n = pattern.subn("Visible", new_content)
        changes += n
    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 24: Fix TextElement.FontXxx in Panel-derived Setter (StackPanel styles etc.)
# ---------------------------------------------------------------------------
def fix_setter_font_properties_on_panel(content):
    """For <Setter Property="FontSize" ...> in <Style Selector="StackPanel"> (or similar Panel-derived),
    change to <Setter Property="TextElement.FontSize" ...>."""
    changes = 0
    new_content = content

    for tag in PANEL_TAGS:
        style_pattern = re.compile(
            r'(<Style\b[^>]*Selector="' + tag + r'"[^>]*>)(.*?)(</Style>)',
            re.DOTALL,
        )

        def style_replacer(m):
            nonlocal changes
            style_open, style_inner, style_close = m.group(1), m.group(2), m.group(3)
            new_inner = style_inner
            for prop in ["FontFamily", "FontSize", "FontWeight", "FontStyle", "Foreground"]:
                pat = re.compile(r'<Setter\s+Property="' + prop + '"')
                new_inner, n = pat.subn('<Setter Property="TextElement.' + prop + '"', new_inner)
                changes += n
            return style_open + new_inner + style_close

        new_content = style_pattern.sub(style_replacer, new_content)

    return new_content, changes


# ---------------------------------------------------------------------------
# Fix 25: Fix SelectionMode="Multiple,Toggle" - Avalonia SelectionMode enum doesn't have "Multiple,Toggle"
# ---------------------------------------------------------------------------
def fix_selection_mode_multiple_toggle(content):
    """Avalonia SelectionMode is a [Flags] enum: None, Single, Multiple, Toggle, AutoSelect, AlwaysSelected.
    'Multiple,Toggle' is valid as combined flags but needs proper syntax."""
    # Actually 'Multiple,Toggle' should work in XAML as combined enum flags. Skip.
    return content, 0


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    xaml_files = find_xaml_files()
    total_changes = 0
    for xaml_file in xaml_files:
        path = str(xaml_file)
        try:
            content = read(path)
        except Exception as e:
            print(f"  ! Read error: {path}: {e}")
            continue
        original = content
        file_changes = 0

        # Fix 1: ensure xmlns declarations
        for prefix, ns_value in [
            ("xmlns:controls", CONTROLS_NS),
            ("xmlns:dialogs", DIALOGS_NS),
            ("xmlns:editor", EDITOR_NS),
        ]:
            ns_needed = False
            if prefix == "xmlns:controls":
                for ctl in UI_CONTROLS:
                    if re.search(r'\b' + ctl + r'\b', content):
                        ns_needed = True
                        break
            elif prefix == "xmlns:dialogs":
                for ctl in DIALOG_CONTROLS:
                    if re.search(r'\b' + ctl + r'\b', content):
                        ns_needed = True
                        break
            elif prefix == "xmlns:editor":
                for ctl in EDITOR_CONTROLS:
                    if re.search(r'\b' + ctl + r'\b', content):
                        ns_needed = True
                        break
            if ns_needed:
                content, n = ensure_xmlns(content, prefix, ns_value)
                file_changes += n

        # Apply fixes
        for fix_fn in [
            fix_controltemplate_targettype,
            fix_buttonbase_targettype,
            fix_placement_to_placementmode,
            fix_placement_mouse,
            fix_enum_casing,
            fix_drop_down_button_property_elements,
            fix_drop_down_button_property_close,
            fix_control_property_elements,
            fix_panel_font_properties,
            fix_setter_font_properties_on_panel,
            remove_wpf_only_attrs,
            remove_wpf_only_setters,
            remove_wpf_only_multiline_setters,
            remove_wpf_only_property_elements,
            fix_templatebinding_computed_visibility,
            fix_menu_separator_style_key,
            add_xkey_to_datatemplate_in_resourcedict,
            wrap_style_in_element_styles,
            add_targettype_to_keyed_controltemplate,
            add_selector_to_unkeyed_style,
            add_targettype_to_bare_controltemplate,
            move_unkeyed_style_out_of_resources,
            fix_progressbar_template_part,
        ]:
            try:
                content, n = fix_fn(content)
                file_changes += n
            except Exception as e:
                print(f"  ! Error in {fix_fn.__name__} on {xaml_file}: {e}")

        if content != original:
            write(path, content)
            print(f"  ✓ {xaml_file.relative_to(ROOT.parent.parent)}: {file_changes} changes")
            total_changes += file_changes

    print(f"\nTotal: {total_changes} changes across {len(xaml_files)} files")


if __name__ == "__main__":
    main()
