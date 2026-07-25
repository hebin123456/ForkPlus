#!/usr/bin/env python3
"""Fix Menu.xaml: remove x:Key from styles inside <Border.Styles> only.

Also applies the previously-done fixes:
- Remove VerticalContentAlignment Setters from ContextMenu/MenuItem/Menu styles
- Change ClickMode Hover -> Press (Avalonia doesn't have Hover)
- Remove Command="ScrollBar.LineUpCommand"/"ScrollBar.LineDownCommand" from RepeatButton
"""
import re
from pathlib import Path

PATH = Path("src/ForkPlus/Theme/Styles/Menu.xaml")
content = PATH.read_text(encoding="utf-8")

# 1. Remove VerticalContentAlignment Setters on ContextMenu/Menu/MenuItem
# ContextMenu (line 43): <Setter Property="VerticalContentAlignment" Value="Center" />
# We need to be careful to only remove in the right scope. The simplest is to remove
# the VerticalContentAlignment Setter that appears immediately after BorderBrush setter.
content = content.replace(
    '    <Setter Property="BorderBrush" Value="{DynamicResource Menu.MenuItem.Static.Border}" />\n    <Setter Property="VerticalContentAlignment" Value="Center" />\n    <Setter Property="Padding" Value="2" />',
    '    <Setter Property="BorderBrush" Value="{DynamicResource Menu.MenuItem.Static.Border}" />\n    <!-- NOTE (Avalonia limitation): VerticalContentAlignment is not a property on Avalonia ContextMenu; removed. -->\n    <Setter Property="Padding" Value="2" />',
)

# MenuItem (line 465/466)
content = content.replace(
    '    <Setter Property="BorderThickness" Value="1" />\n    <Setter Property="HorizontalContentAlignment" Value="Left" />\n    <Setter Property="VerticalContentAlignment" Value="Center" />\n    <Setter Property="Template" Value="{DynamicResource SubmenuItemTemplateKey}" />',
    '    <Setter Property="BorderThickness" Value="1" />\n    <!-- NOTE (Avalonia limitation): HorizontalContentAlignment/VerticalContentAlignment are not properties on Avalonia MenuItem; removed. -->\n    <Setter Property="Template" Value="{DynamicResource SubmenuItemTemplateKey}" />',
)

# Menu (line 500)
content = content.replace(
    '    <Setter Property="FontSize" Value="12" />\n    <Setter Property="VerticalContentAlignment" Value="Center" />\n    <Setter Property="Template">\n      <Setter.Value>\n        <ControlTemplate TargetType="Menu">',
    '    <Setter Property="FontSize" Value="12" />\n    <!-- NOTE (Avalonia limitation): VerticalContentAlignment is not a property on Avalonia Menu; removed. -->\n    <Setter Property="Template">\n      <Setter.Value>\n        <ControlTemplate TargetType="Menu">',
)

# 2. Change ButtonBase.ClickMode -> ClickMode, Hover -> Press
content = content.replace(
    '<Setter Property="ButtonBase.ClickMode" Value="Hover" />',
    '<Setter Property="ClickMode" Value="Press" />',
)

# 3. Remove ScrollBar commands from RepeatButtons
content = re.sub(r'\s+Command="ScrollBar\.\w+"', '', content)

# 4. Remove x:Key from <Style> elements that are inside <Border.Styles> blocks.
# We track whether we're inside <Border.Styles> by scanning the file.
lines = content.splitlines(keepends=True)
in_border_styles = 0  # depth counter for <Border.Styles> blocks
output = []
pattern_style_with_key = re.compile(r'(<Style\s+)x:Key="[^"]+"(\s+Selector=")')

for line in lines:
    stripped = line.strip()
    # Detect entering a <Border.Styles> block
    if '<Border.Styles>' in line:
        in_border_styles += 1
    # Detect leaving a </Border.Styles> block
    if '</Border.Styles>' in line:
        if in_border_styles > 0:
            in_border_styles -= 1
    # If we're inside Border.Styles, remove x:Key from Style elements
    if in_border_styles > 0:
        new_line = pattern_style_with_key.sub(r'\1\2', line)
        output.append(new_line)
    else:
        output.append(line)

new_content = ''.join(output)
PATH.write_text(new_content, encoding="utf-8")

# Count diff
old_count = content.count('<Style x:Key=')
new_count = new_content.count('<Style x:Key=')
print(f"Removed {old_count - new_count} x:Key attributes from inside Border.Styles")
print(f"Done.")
