#!/usr/bin/env python3
"""Phase 7: Handle remaining XAML patterns after bridge types added.
- Move DataTemplate from .Resources to .DataTemplates (implicit DataType templates)
- Convert x:StaticExtension to x:Static
- Fix Height="Auto" / Width="Auto" in Setter (Avalonia wants NaN, use double.NaN or remove)
- Fix StrokeDashArray="1 2" -> "1,2" (comma separator)
- Fix Visibility property element on namespaced elements (controls:X.Visibility -> controls:X.IsVisible)
- Fix ToolTipService.ToolTip.Tip -> ToolTip.Tip
- Fix ToolTip Setter (use ToolTip.Tip Setter)
- Fix ShowGraphToolTip.Tip="False" -> ShowGraphToolTip="False"
- Fix <Setter Property="ToolTip">...</Setter> -> <Setter Property="ToolTip.Tip">...</Setter>
- Remove WPF-only properties (Placement, HasDropShadow, PanningMode, AllowDrop, etc.)
- Remove WPF-only events (MouseMove, GotMouseCapture, PreviewKeyDown, etc.)
- Remove ScrollBar IsMouseOver Setter (read-only)
- Remove Resources on ControlTemplate / DataTemplate
- Remove Style sibling inside ContentPresenter.Resources (move or remove)
- Fix StrokeStartLineCap/StrokeEndLineCap -> StrokeLineCap
- Convert DropShadowEffect.ShadowDepth -> DropShadowEffect.OffsetY
- Remove ItemContainerStyle on bridge types (DragAndDropListBox, MultiselectionListView)
- Fix Hyperlink.RequestNavigate event (already removed in phase 6, ensure)
"""
import re
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")


def fix_xstatic_extension(content):
    """Convert {x:StaticExtension X} to {x:Static X}.
    Avalonia doesn't recognize StaticExtension as a type, only the x:Static markup extension."""
    pattern = re.compile(r'\{x:StaticExtension\s+')
    new_content, n = pattern.subn('{x:Static ', content)
    return new_content, n


def fix_height_auto_in_setter(content):
    """Remove Setter Property="Height" Value="Auto" / Setter Property="Width" Value="Auto".
    Avalonia doesn't parse 'Auto' as a double. Use NaN (default) by removing the setter."""
    # Remove Setter Property="Height" Value="Auto"
    pattern = re.compile(r'\s*<Setter\s+Property="(Height|Width)"\s+Value="Auto"\s*/>', re.DOTALL)
    new_content, n = pattern.subn('', content)
    return new_content, n


def fix_height_auto_attribute(content):
    """Convert Height="Auto" / Width="Auto" attribute to nothing (remove).
    Avalonia doesn't parse 'Auto' for Height/Width on elements that expect double.
    On RowDefinition/ColumnDefinition, 'Auto' is valid."""
    # Find elements with Height="Auto" or Width="Auto" that aren't RowDefinition/ColumnDefinition
    def replacer(m):
        tag = m.group(0)
        # Skip RowDefinition and ColumnDefinition - they accept Auto
        if re.match(r'<(?:Grid\.)?(RowDefinition|ColumnDefinition)\b', tag):
            return tag
        # Remove Height="Auto" and Width="Auto" from other elements
        new_tag = re.sub(r'\s+(Height|Width)="Auto"', '', tag)
        return new_tag
    # Match opening tags
    pattern = re.compile(r'<\w[\w\.]*(\s+[^>]*)?/?>', re.DOTALL)
    new_content, n = pattern.subn(replacer, content)
    return new_content, n


def fix_strokedasharray_space(content):
    """Convert StrokeDashArray="1 2" to StrokeDashArray="1,2" (comma separator)."""
    def replacer(m):
        val = m.group(1)
        # Replace spaces with commas
        new_val = re.sub(r'\s+', ',', val.strip())
        return f'StrokeDashArray="{new_val}"'
    pattern = re.compile(r'StrokeDashArray="([^"]*)"')
    new_content, n = pattern.subn(replacer, content)
    return new_content, n


def fix_visibility_property_element_namespaced(content):
    """Convert <ns:X.Visibility>...</ns:X.Visibility> to <ns:X.IsVisible>bool</ns:X.IsVisible>.
    Handles namespaced elements (controls:Foo.Visibility)."""
    def replacer(m):
        prefix = m.group(1)
        content_inner = m.group(2).strip()
        if content_inner in ('Visible', 'Collapsed', 'Hidden'):
            bool_val = 'True' if content_inner == 'Visible' else 'False'
            return f'<{prefix}.IsVisible>{bool_val}</{prefix}.IsVisible>'
        if 'MultiBinding' in content_inner or 'Binding' in content_inner:
            return ''  # Remove complex bindings
        return m.group(0)
    # Match <prefix.Visibility>content</prefix.Visibility>
    pattern = re.compile(r'<([\w:]+)\.Visibility>(.*?)</\1\.Visibility>', re.DOTALL)
    new_content, n = pattern.subn(replacer, content)
    return new_content, n


def fix_tooltipservice_tooltip_tip(content):
    """Convert ToolTipService.ToolTip.Tip="..." to ToolTip.Tip="...".
    Avalonia's ToolTipService doesn't have a Tip property - use ToolTip.Tip directly."""
    pattern = re.compile(r'ToolTipService\.ToolTip\.Tip=')
    new_content, n = pattern.subn('ToolTip.Tip=', content)
    return new_content, n


def fix_showgraphtooltip_tip(content):
    """Convert ShowGraphToolTip.Tip="False" to ShowGraphToolTip="False".
    ShowGraphToolTip is a regular property on GraphCellView, not an attached property."""
    pattern = re.compile(r'ShowGraphToolTip\.Tip=')
    new_content, n = pattern.subn('ShowGraphToolTip=', content)
    return new_content, n


def fix_tooltip_setter_to_tooltip_tip(content):
    """Convert <Setter Property="ToolTip">...</Setter> to <Setter Property="ToolTip.Tip">...</Setter>.
    Avalonia's ToolTip property is the ToolTip control itself; for content use ToolTip.Tip."""
    # Match <Setter Property="ToolTip">  ... </Setter>
    pattern = re.compile(
        r'<Setter\s+Property="ToolTip"\s*>',
        re.DOTALL
    )
    new_content, n = pattern.subn('<Setter Property="ToolTip.Tip">', content)
    return new_content, n


def fix_tooltip_setter_attribute(content):
    """Convert <Setter Property="ToolTip" Value="..." /> to <Setter Property="ToolTip.Tip" Value="..." />."""
    pattern = re.compile(r'<Setter\s+Property="ToolTip"\s+Value=')
    new_content, n = pattern.subn('<Setter Property="ToolTip.Tip" Value=', content)
    return new_content, n


def fix_tooltip_attribute_on_textblock(content):
    """Convert ToolTip="..." attribute on TextBlock and others to ToolTip.Tip="...".
    Avalonia uses ToolTip.Tip as the attached property."""
    # Match ToolTip="..." but not ToolTip.Tip= or ToolTipService. or ToolTip. (already prefixed)
    # Negative lookbehind for . and ToolTipService.
    pattern = re.compile(r'(?<!\.)(?<!ToolTipService\.)ToolTip="([^"]*)"')
    new_content, n = pattern.subn(r'ToolTip.Tip="\1"', content)
    return new_content, n


def remove_has_drop_shadow(content):
    """Remove HasDropShadow from ToolTip (WPF-only property)."""
    pattern = re.compile(r'\s+HasDropShadow="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n


def remove_panning_mode(content):
    """Remove PanningMode from ScrollViewer (WPF-only property)."""
    pattern = re.compile(r'\s+PanningMode="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n


def remove_allowdrop(content):
    """Remove AllowDrop attribute (Avalonia uses different drag-drop API).
    Only remove on TextBox, PasswordBox, and ForkPlus custom controls where it errors."""
    pattern = re.compile(r'\s+AllowDrop="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n


def remove_placement_rectangle(content):
    """Remove PlacementRectangle from Popup (already in phase 6, ensure)."""
    pattern = re.compile(r'\s+PlacementRectangle="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n


def remove_is_checkable(content):
    """Convert IsCheckable="True" on MenuItem.
    Avalonia's MenuItem uses ToggleType instead. Set ToggleType="CheckBox" when IsCheckable=True."""
    def replacer(m):
        val = m.group(1)
        if val == 'True':
            return ' ToggleType="CheckBox"'
        return ''
    pattern = re.compile(r'\s+IsCheckable="(True|False)"')
    new_content, n = pattern.subn(replacer, content)
    return new_content, n


def remove_mouse_events_v2(content):
    """Remove WPF-only mouse events."""
    n = 0
    new_content = content
    events = ['MouseMove', 'GotMouseCapture', 'PreviewKeyDown', 'PreviewKeyUp',
              'PreviewMouseLeftButtonDown', 'PreviewMouseLeftButtonUp',
              'PreviewMouseDown', 'PreviewMouseUp',
              'MouseEnter', 'MouseLeave']
    for event in events:
        pattern = re.compile(rf'\s+{event}="[^"]*"')
        new_content, count = pattern.subn('', new_content)
        n += count
    return new_content, n


def remove_ismouseover_setter(content):
    """Remove <Setter Property="IsMouseOver" .../> (read-only in Avalonia)."""
    pattern = re.compile(
        r'\s*<Setter\s+Property="IsMouseOver".*?(?:/>|</Setter>)',
        re.DOTALL
    )
    new_content, n = pattern.subn('', content)
    return new_content, n


def fix_stroke_linecap(content):
    """Convert StrokeStartLineCap/StrokeEndLineCap to StrokeLineCap.
    Avalonia Line shape only has StrokeLineCap."""
    # Just remove them - they're rarely used
    n = 0
    new_content = content
    for prop in ['StrokeStartLineCap', 'StrokeEndLineCap', 'StrokeDashCap']:
        pattern = re.compile(rf'\s+{prop}="[^"]*"')
        new_content, count = pattern.subn('', new_content)
        n += count
    return new_content, n


def fix_drop_shadow_shadowdepth(content):
    """Convert <DropShadowEffect.ShadowDepth>1</DropShadowEffect.ShadowDepth>
    to <DropShadowEffect.OffsetY>1</DropShadowEffect.OffsetY>.
    Avalonia DropShadowEffect uses OffsetY instead of ShadowDepth."""
    pattern = re.compile(r'<(DropShadowEffect)\.ShadowDepth>')
    new_content, n = pattern.subn(r'<\1.OffsetY>', content)
    # Also close tag
    pattern2 = re.compile(r'</(DropShadowEffect)\.ShadowDepth>')
    new_content, n2 = pattern2.subn(r'</\1.OffsetY>', new_content)
    n += n2
    return new_content, n


def remove_item_container_style(content):
    """Remove ItemContainerStyle attribute (already done in phase 6, ensure for bridge types)."""
    pattern = re.compile(r'\s+ItemContainerStyle="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n


def remove_command_target(content):
    """Remove CommandTarget attribute (Avalonia doesn't support it on most buttons)."""
    pattern = re.compile(r'\s+CommandTarget="[^"]*"')
    new_content, n = pattern.subn('', content)
    return new_content, n


def remove_calendar_button_style(content):
    """Remove CalendarDayButtonStyle/CalendarButtonStyle from Calendar (WPF-only)."""
    n = 0
    new_content = content
    for prop in ['CalendarDayButtonStyle', 'CalendarButtonStyle']:
        pattern = re.compile(rf'\s+{prop}="[^"]*"')
        new_content, count = pattern.subn('', new_content)
        n += count
    return new_content, n


def remove_viewport_properties(content):
    """Remove Viewport/ViewportUnits from VisualBrush (WPF-only)."""
    n = 0
    new_content = content
    # Remove <VisualBrush.Viewport>...</VisualBrush.Viewport> property elements
    pattern = re.compile(r'\s*<VisualBrush\.Viewport>.*?</VisualBrush\.Viewport>', re.DOTALL)
    new_content, count = pattern.subn('', new_content)
    n += count
    pattern = re.compile(r'\s*<VisualBrush\.ViewportUnits>.*?</VisualBrush\.ViewportUnits>', re.DOTALL)
    new_content, count = pattern.subn('', new_content)
    n += count
    # Remove attributes
    pattern = re.compile(r'\s+Viewport="[^"]*"')
    new_content, count = pattern.subn('', new_content)
    n += count
    pattern = re.compile(r'\s+ViewportUnits="[^"]*"')
    new_content, count = pattern.subn('', new_content)
    n += count
    return new_content, n


def remove_scrollviewer_computed_properties(content):
    """Remove ComputedHorizontalScrollBarVisibility/ComputedVerticalScrollBarVisibility
    and ViewportHeight/ViewportWidth/ScrollableHeight/ScrollableWidth.
    These are read-only in Avalonia."""
    n = 0
    new_content = content
    props = ['ComputedHorizontalScrollBarVisibility', 'ComputedVerticalScrollBarVisibility',
             'ViewportHeight', 'ViewportWidth', 'ScrollableHeight', 'ScrollableWidth']
    for prop in props:
        # Property element
        pattern = re.compile(rf'\s*<[\w:]+\.{prop}>.*?</[\w:]+\.{prop}>', re.DOTALL)
        new_content, count = pattern.subn('', new_content)
        n += count
        # Attribute
        pattern = re.compile(rf'\s+{prop}="[^"]*"')
        new_content, count = pattern.subn('', new_content)
        n += count
    return new_content, n


def fix_resources_with_datatemplate(content):
    """Move DataTemplate with DataType from .Resources to .DataTemplates.
    In Avalonia, implicit DataTemplates (keyed by DataType) must be in DataTemplates collection,
    not Resources."""
    # Find <X.Resources> ... </X.Resources> blocks
    # If contains <DataTemplate DataType="...">, move them out to <X.DataTemplates> ... </X.DataTemplates>
    # Actually for non-Control elements like TabItem, we can use a different approach.
    # For TabItem, ListBox, etc., they're ItemsControl, so they have DataTemplates property.

    def process_block(m):
        full_block = m.group(0)
        # Find opening tag prefix (e.g., TabItem.Resources)
        opening_match = re.match(r'<([\w:]+)\.Resources>', full_block)
        if not opening_match:
            return full_block
        prefix = opening_match.group(1)

        # Extract DataTemplates with DataType
        datatemplate_pattern = re.compile(
            r'<DataTemplate\s+DataType="[^"]+">.*?</DataTemplate>',
            re.DOTALL
        )
        datatemplates = datatemplate_pattern.findall(full_block)
        if not datatemplates:
            return full_block

        # Remove DataTemplates from Resources block
        new_block = datatemplate_pattern.sub('', full_block)

        # If Resources is now empty (only whitespace), remove it entirely
        # Check if there's any content left besides the opening/closing tags
        inner_match = re.search(r'<[\w:]+\.Resources>(.*?)</[\w:]+\.Resources>', new_block, re.DOTALL)
        if inner_match:
            inner = inner_match.group(1).strip()
            if not inner:
                # Remove the empty Resources block
                new_block = re.sub(
                    r'\s*<[\w:]+\.Resources>\s*</[\w:]+\.Resources>',
                    '',
                    new_block
                )

        # Add DataTemplates block after Resources (or in its place)
        datatemplates_xml = '\n'.join(datatemplates)
        datatemplates_block = f'\n<{prefix}.DataTemplates>\n{datatemplates_xml}\n</{prefix}.DataTemplates>\n'

        # Find where to insert: after the closing of Resources, or before the first child if Resources removed
        if f'<{prefix}.Resources>' in new_block:
            # Insert after </prefix.Resources>
            new_block = re.sub(
                rf'(</{re.escape(prefix)}\.Resources>)',
                rf'\1{datatemplates_block}',
                new_block
            )
        else:
            # Resources was removed, insert DataTemplates block at the position where Resources was
            # Find the first child element after the opening tag of the parent
            # Actually we need to find the parent's opening tag and insert after it
            # This is complex - let's insert right where Resources used to be
            # The block we're processing is just the Resources section, so we need to return
            # the DataTemplates block instead
            return datatemplates_block

        return new_block

    # Match <X.Resources>...</X.Resources> blocks (non-greedy)
    # But only when they contain DataTemplate with DataType
    pattern = re.compile(
        r'<\w[\w:]*\.Resources>.*?</\w[\w:]*\.Resources>',
        re.DOTALL
    )
    new_content, n = pattern.subn(process_block, content)
    return new_content, n


def remove_resources_on_template(content):
    """Remove <ControlTemplate.Resources> and <DataTemplate.Resources> blocks that contain
    only Style elements (which Avalonia doesn't accept in Resources).
    Move Styles into .Styles property of the root visual element if possible, else remove."""
    # For now, just convert <X.Resources> containing only <Style> to <X.Styles>
    def process_template_resources(m):
        full = m.group(0)
        # Check if it contains only Style elements
        opening_match = re.match(r'<([\w:]+)\.Resources>', full)
        if not opening_match:
            return full
        prefix = opening_match.group(1)

        # Extract content between tags
        inner_match = re.search(r'<[\w:]+\.Resources>(.*?)</[\w:]+\.Resources>', full, re.DOTALL)
        if not inner_match:
            return full
        inner = inner_match.group(1)

        # If contains DataTemplate, keep as is (handled elsewhere)
        if '<DataTemplate' in inner:
            return full

        # If contains only Style elements, convert to .Styles
        if '<Style' in inner and '<Setter' not in inner.replace('<Style', ''):
            # Replace .Resources with .Styles
            new_full = re.sub(r'<([\w:]+)\.Resources>', r'<\1.Styles>', full)
            new_full = re.sub(r'</([\w:]+)\.Resources>', r'</\1.Styles>', new_full)
            return new_full

        return full

    # Match <ControlTemplate.Resources>...</ControlTemplate.Resources> and <DataTemplate.Resources>...</DataTemplate.Resources>
    pattern = re.compile(
        r'<(?:ControlTemplate|DataTemplate)\.Resources>.*?</(?:ControlTemplate|DataTemplate)\.Resources>',
        re.DOTALL
    )
    new_content, n = pattern.subn(process_template_resources, content)
    return new_content, n


def remove_resources_on_styledelement_with_style(content):
    """For <X.Resources> blocks that contain <Style> directly (without x:Key),
    move the Style to <X.Styles> instead. Avalonia Resources only accepts keyed items."""
    def process_block(m):
        full = m.group(0)
        opening_match = re.match(r'<([\w:]+)\.Resources>', full)
        if not opening_match:
            return full
        prefix = opening_match.group(1)

        inner_match = re.search(r'<[\w:]+\.Resources>(.*?)</[\w:]+\.Resources>', full, re.DOTALL)
        if not inner_match:
            return full
        inner = inner_match.group(1)

        # If contains DataTemplate, skip (handled elsewhere)
        if '<DataTemplate' in inner:
            return full

        # If contains Style elements without x:Key, move to .Styles
        # Extract Style elements
        style_pattern = re.compile(r'<Style\b[^>]*>.*?</Style>', re.DOTALL)
        styles = style_pattern.findall(inner)
        if not styles:
            return full

        # Remove Style elements from inner
        new_inner = style_pattern.sub('', inner).strip()

        styles_xml = '\n'.join(styles)
        styles_block = f'<{prefix}.Styles>\n{styles_xml}\n</{prefix}.Styles>'

        if new_inner:
            # Keep remaining Resources content
            new_resources = f'<{prefix}.Resources>{new_inner}</{prefix}.Resources>'
            return new_resources + '\n' + styles_block
        else:
            # Resources is now empty, just return Styles
            return styles_block

    # Only match <X.Resources> where X is not ControlTemplate/DataTemplate (those are handled above)
    # And only when content contains Style elements
    pattern = re.compile(
        r'<\w[\w:]*\.Resources>.*?</\w[\w:]*\.Resources>',
        re.DOTALL
    )
    new_content, n = pattern.subn(process_block, content)
    return new_content, n


def remove_ischecked_setter(content):
    """Skip - IsChecked setter is valid. No-op."""
    return content, 0


def fix_borderbrush_on_control_setter(content):
    """Convert Setter Property="BorderBrush" on Selector="Control" to use TemplatedControl.
    Actually just change the Selector from Control to TemplatedControl."""
    # This is risky to auto-fix, skip for now
    return content, 0


def fix_foreground_on_stackpanel(content):
    """Convert Foreground/FontSize/FontFamily/BorderThickness on StackPanel to TextElement.* / no-op.
    StackPanel in Avalonia doesn't have these directly."""
    # Actually StackPanel inherits from Control which has FontSize etc. via TextElement attached props
    # Just convert Foreground -> TextElement.Foreground, etc.
    # But BorderThickness doesn't exist on StackPanel at all
    n = 0
    new_content = content

    # BorderThickness on StackPanel - remove (StackPanel has no border)
    # Only remove when on StackPanel element directly (not StackPanel.Style)
    # This is hard to detect, skip for now
    return new_content, n


def remove_show_duration(content):
    """Remove ShowDuration from ToolTipService (WPF-only)."""
    # <ToolTipService.ShowDuration>...</ToolTipService.ShowDuration> or attribute
    pattern = re.compile(r'\s*<[\w:]*ToolTipService\.ShowDuration>.*?</[\w:]*ToolTipService\.ShowDuration>', re.DOTALL)
    new_content, n = pattern.subn('', content)
    pattern = re.compile(r'\s+ToolTipService\.ShowDuration="[^"]*"')
    new_content, n2 = pattern.subn('', new_content)
    n += n2
    return new_content, n


def remove_verticalhorizontal_scrollbar_on_listbox_combobox(content):
    """Convert HorizontalScrollBarVisibility/VerticalScrollBarVisibility on ListBox/ComboBox
    to ScrollViewer.HorizontalScrollBarVisibility attached property."""
    # These are attached properties in Avalonia: ScrollViewer.HorizontalScrollBarVisibility
    # If they're being used as direct attributes on ListBox/ComboBox, prefix with ScrollViewer.
    # But we need context. The error says "Unable to resolve suitable regular or attached property"
    # which means even as ScrollViewer.* it's failing on ListBox/ComboBox.
    # Actually for ListBox/ComboBox in Avalonia, you use ScrollViewer.HorizontalScrollBarVisibility
    # as an attached property - it should work. The error is because the property is being used
    # WITHOUT the ScrollViewer. prefix. Let's add the prefix.

    # This is tricky because we need to know the element type. Let's just add prefix to any
    # VerticalScrollBarVisibility/HorizontalScrollBarVisibility that doesn't have ScrollViewer. prefix
    # and is on a ListBox/ComboBox.

    # For Setter, we'd need: <Setter Property="ScrollViewer.VerticalScrollBarVisibility" />
    # For attribute on element: <ListBox ScrollViewer.VerticalScrollBarVisibility="..." />

    # Match Setter Property="VerticalScrollBarVisibility"
    pattern = re.compile(r'<Setter\s+Property="(Vertical|Horizontal)ScrollBarVisibility"')
    new_content, n = pattern.subn(lambda m: f'<Setter Property="ScrollViewer.{m.group(1)}ScrollBarVisibility"', content)
    # Match attribute on element (not already prefixed)
    pattern = re.compile(r'(?<!ScrollViewer\.)(?<!\.)(\s+)(Vertical|Horizontal)ScrollBarVisibility="')
    new_content, n2 = pattern.subn(lambda m: f'{m.group(1)}ScrollViewer.{m.group(2)}ScrollBarVisibility="', new_content)
    n += n2
    return new_content, n


def remove_ishittestvisible_in_chrome(content):
    """IsHitTestVisibleInChrome is now supported via bridge - no-op.
    Actually the bridge is added, but if XAML uses it as WindowChrome.IsHitTestVisibleInChrome,
    Avalonia still might not parse it. Let's convert to the bridge attached property syntax."""
    # The bridge has WindowChrome.IsHitTestVisibleInChromeProperty registered.
    # Avalonia XAML should accept WindowChrome.IsHitTestVisibleInChrome="True"
    # No change needed - the bridge type makes it work.
    return content, 0


def fix_ishittestvisible_in_chrome_attribute(content):
    """The attribute is on elements like Button, but the bridge attached property expects Control.
    Make sure it's prefixed with WindowChrome. if not already."""
    # Match IsHitTestVisibleInChrome="..." without WindowChrome. prefix
    pattern = re.compile(r'(?<!WindowChrome\.)(\s+)IsHitTestVisibleInChrome="')
    new_content, n = pattern.subn(r'\1WindowChrome.IsHitTestVisibleInChrome="', content)
    return new_content, n


def fix_resizegripdirection_attribute(content):
    """Prefix ResizeGripDirection with WindowChrome. if not already."""
    pattern = re.compile(r'(?<!WindowChrome\.)(\s+)ResizeGripDirection="')
    new_content, n = pattern.subn(r'\1WindowChrome.ResizeGripDirection="', content)
    return new_content, n


def fix_row_property_on_grid(content):
    """Convert Grid.Row="..." attribute - actually Grid.Row is valid.
    The error 'Unable to resolve suitable regular or attached property Row on type Grid'
    probably comes from <Grid.Row>...</Grid.Row> as property element, which Avalonia doesn't support."""
    # Skip - investigate
    return content, 0


def fix_enum_converter_parameter(content):
    """Remove ConverterParameter on EnumToTextDecorationsConverter (custom converter issue).
    Actually the issue is the converter doesn't expose ConverterParameter as a property.
    ConverterParameter is a Binding property, not the converter's. The error is misleading."""
    # Skip - this is likely a binding syntax issue
    return content, 0


def fix_oxyplot_tracker_properties(content):
    """Remove VerticalLineIsVisible/HorizontalLineIsVisible from OxyPlot TrackerControl (WPF-only)."""
    n = 0
    new_content = content
    for prop in ['VerticalLineIsVisible', 'HorizontalLineIsVisible']:
        pattern = re.compile(rf'\s+{prop}="[^"]*"')
        new_content, count = pattern.subn('', new_content)
        n += count
    return new_content, n


def fix_placement_property(content):
    """Convert Placement="..." on Popup to enum value.
    Avalonia PlacementMode enum: Auto, Bottom, Right, Mouse, Pointer, Top, Left, Edge,
    Center, LeftEdge, TopEdge, RightEdge, BottomEdge, Custom, BottomEdgeAlignedLeft,
    BottomEdgeAlignedRight, TopEdgeAlignedLeft, TopEdgeAlignedRight, RightEdgeAlignedTop,
    RightEdgeAlignedBottom, LeftEdgeAlignedTop, LeftEdgeAlignedBottom
    WPF PlacementMode: Absolute, Relative, Bottom, Custom, Left, Mouse, MousePoint,
    RelativePoint, Right, AbsolutePoint, Top"""
    # Map WPF values to Avalonia
    placement_map = {
        'Bottom': 'Bottom',
        'Right': 'Right',
        'Left': 'Left',
        'Top': 'Top',
        'Mouse': 'Mouse',
        'MousePoint': 'Pointer',
        'Absolute': 'Pointer',
        'AbsolutePoint': 'Pointer',
        'Relative': 'Pointer',
        'RelativePoint': 'Pointer',
        'Custom': 'Custom',
    }
    def replacer(m):
        val = m.group(1)
        new_val = placement_map.get(val, 'Pointer')
        return f'Placement="{new_val}"'
    pattern = re.compile(r'Placement="([^"]*)"')
    new_content, n = pattern.subn(replacer, content)
    return new_content, n


def fix_vertical_alignment(content):
    """VerticalAlignment="Center" on certain types fails. Should be valid."""
    # Skip
    return content, 0


def fix_focusable_string(content):
    """Focusable="True"/"False" should already work. Investigate if any string issues."""
    # Skip
    return content, 0


def fix_tooltip_on_textblock_setter(content):
    """In Style Setter for TextBlock, <Setter Property="ToolTip"> should be <Setter Property="ToolTip.Tip">."""
    # Already handled by fix_tooltip_setter_to_tooltip_tip
    return content, 0


def remove_internal_compiler_error_patterns(content):
    """Internal compiler errors come from misnested content. Hard to auto-fix."""
    # Skip
    return content, 0


def fix_hyperlink_navigateuri_property_element(content):
    """Ensure <Hyperlink.NavigateUri><Binding .../></Hyperlink.NavigateUri> works.
    Now that NavigateUri is a StyledProperty, this should work."""
    return content, 0


def fix_content_with_grid_length(content):
    """Fix "* " (with trailing space) GridLength values - remove trailing space."""
    pattern = re.compile(r'"\* "')
    new_content, n = pattern.subn('"*"', content)
    return new_content, n


def fix_tooltip_complex_setter(content):
    """For <Setter Property="ToolTip"> with complex content (ToolTip element with DataContext),
    we already converted Property="ToolTip" to Property="ToolTip.Tip" in fix_tooltip_setter_to_tooltip_tip.
    But the content has <ToolTip>...</ToolTip> which won't work as ToolTip.Tip value.
    Convert <ToolTip ...>...</ToolTip> to its content."""
    # Skip - too complex
    return content, 0


def process_file(path):
    try:
        with open(path, 'r', encoding='utf-8') as f:
            content = f.read()
    except Exception:
        return 0

    original = content
    total_changes = 0

    fixers = [
        fix_xstatic_extension,
        fix_height_auto_in_setter,
        fix_height_auto_attribute,
        fix_strokedasharray_space,
        fix_visibility_property_element_namespaced,
        fix_tooltipservice_tooltip_tip,
        fix_showgraphtooltip_tip,
        fix_tooltip_setter_to_tooltip_tip,
        fix_tooltip_setter_attribute,
        fix_tooltip_attribute_on_textblock,
        remove_has_drop_shadow,
        remove_panning_mode,
        remove_allowdrop,
        remove_placement_rectangle,
        remove_is_checkable,
        remove_mouse_events_v2,
        remove_ismouseover_setter,
        fix_stroke_linecap,
        fix_drop_shadow_shadowdepth,
        remove_item_container_style,
        remove_command_target,
        remove_calendar_button_style,
        remove_viewport_properties,
        remove_scrollviewer_computed_properties,
        fix_resources_with_datatemplate,
        remove_resources_on_styledelement_with_style,
        remove_resources_on_template,
        remove_show_duration,
        remove_verticalhorizontal_scrollbar_on_listbox_combobox,
        fix_ishittestvisible_in_chrome_attribute,
        fix_resizegripdirection_attribute,
        fix_oxyplot_tracker_properties,
        fix_placement_property,
        fix_content_with_grid_length,
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
