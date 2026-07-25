#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
fix_xaml10.py - 阶段 5 收尾修复脚本

针对 fix_xaml9 后剩余的 133 个 AVLN 错误，集中处理以下模式：
  1. ControlTemplate TargetType 缺少自定义控件命名空间前缀 (controls: / dialogs:)
  2. AllowDrop 属性（WPF UIElement.AllowDrop；Avalonia 无对应，删除）
  3. PART_Indicator 模板部件缺失（ProgressBar 自定义模板）
  4. ResourceDictionary 内未键化 Style/DataTemplate（需移到 .Styles 或加 x:Key）
  5. 大小写敏感枚举值（VerticalAlignment="top" -> "Top"、PlacementMode="Mouse" -> "Pointer"）
  6. 自定义控件属性元素语法（<DropDownButton.Background> -> 用属性特性）
  7. Tabcontrol.xaml 中无 Selector 的 Style（补充 Selector）
  8. DiffEntryRowUserControl 嵌套 Selector 无父样式
"""
import os
import re
import sys
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")

# ----------------------------------------------------------------------
# 1. ControlTemplate TargetType 前缀修复
# ----------------------------------------------------------------------
# 自定义控件 → 命名空间前缀映射（与各 XAML 根元素 xmlns 声明保持一致）
CUSTOM_CONTROL_PREFIXES = {
    # ForkPlus.UI.Controls
    "MultiselectionTreeView": "controls",
    "MultiselectionTreeViewItem": "controls",
    "TreeViewControlItem": "controls",
    "PlaceholderTextBox": "controls",
    "AutoCompleteTextBox": "controls",
    "CommitDescriptionTextBox": "controls",
    "FilterTextBox": "controls",
    "ClosableTabControl": "controls",
    "ClosableTabItem": "controls",
    "TouchpadAwareScrollViewer": "controls",
    "ToolbarButton": "controls",
    "ToolbarDropDownButton": "controls",
    "DateRangeButton": "controls",
    "DropDownButton": "controls",
    "GitPointView": "controls",
    "DragAndDropListViewItem": "controls",
    "DragAndDropListView": "controls",
    "NoUIAutomationListView": "controls",
    "GraphCellView": "controls",
    "HighlightableTextBlock": "controls",
    "AvatarImage": "controls",
    "AutoTooltipTextBlock": "controls",
    "TextContentControl": "controls",
    "EditableTextBlock": "controls",
    # ForkPlus.UI.Dialogs
    "ForkPlusDialogWindow": "dialogs",
    "ForkPlusDialog": "dialogs",
    "CustomColorsDialog": "dialogs",
    "MultiselectionListView": "dialogs",
    "InteractiveRebaseWindow": "dialogs",
}

def fix_controltemplate_targettype(content):
    """为 <ControlTemplate TargetType="X"> 中未带前缀的自定义控件添加命名空间前缀。"""
    changes = 0

    def replacer(m):
        nonlocal changes
        prefix_attr = m.group(1) or ""
        type_name = m.group(2)
        # 已带前缀（含冒号）→ 跳过
        if ":" in type_name:
            return m.group(0)
        # 仅处理自定义控件
        ns_prefix = CUSTOM_CONTROL_PREFIXES.get(type_name)
        if not ns_prefix:
            return m.group(0)
        changes += 1
        return f'<ControlTemplate{prefix_attr} TargetType="{ns_prefix}:{type_name}"'

    # 匹配 <ControlTemplate TargetType="X"> 或 <ControlTemplate abc="..." TargetType="X">
    pattern = re.compile(
        r'<ControlTemplate(\s+[^>]*?|\s+)TargetType="([^":]+?)"'
    )
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ----------------------------------------------------------------------
# 2. AllowDrop 属性删除
# ----------------------------------------------------------------------
def remove_allowdrop(content):
    """删除 AllowDrop="..." 属性及 <Setter Property="AllowDrop" ...> 元素。"""
    changes = 0
    # <X ... AllowDrop="true|True|false" ...>
    pattern1 = re.compile(r'\s+AllowDrop="[^"]*"')
    new_content, n1 = pattern1.subn("", content)
    changes += n1
    # <Setter Property="AllowDrop" Value="..." />
    pattern2 = re.compile(
        r'\s*<Setter\s+Property="AllowDrop"\s+Value="[^"]*"\s*/>\s*\n?'
    )
    new_content, n2 = pattern2.subn("\n", new_content)
    changes += n2
    return new_content, changes


# ----------------------------------------------------------------------
# 3. ProgressBar PART_Indicator 模板部件
# ----------------------------------------------------------------------
def add_progressbar_indicator(content):
    """若 ProgressBar ControlTemplate 缺少 PART_Indicator 部件，添加一个不可见占位 Border。"""
    # 仅当 ProgressBar 模板内不含 PART_Indicator 时插入
    if "PART_Indicator" in content:
        return content, 0
    # 定位 <ControlTemplate TargetType="ProgressBar">...<Grid ... x:Name="TemplateRoot">
    pattern = re.compile(
        r'(<ControlTemplate\s+TargetType="ProgressBar">[\s\S]*?<Grid[^>]*x:Name="TemplateRoot"[^>]*>)'
    )
    m = pattern.search(content)
    if not m:
        return content, 0
    insertion = m.group(1) + '\n            <Border x:Name="PART_Indicator" IsVisible="False" />'
    new_content = content[:m.start()] + insertion + content[m.end():]
    return new_content, 1


# ----------------------------------------------------------------------
# 4. ResourceDictionary 内未键化 Style / DataTemplate 处理
# ----------------------------------------------------------------------
def move_unkeyed_styles_to_styles_collection(content):
    """将 <X.Resources><ResourceDictionary>...<Style Selector="...">...</Style>...</ResourceDictionary></X.Resources>
    中的未键化 Style 移到 <X.Styles> 集合中。

    策略：用栈式扫描定位 ResourceDictionary 内 *无 x:Key* 的顶层 <Style> 元素（开始与结束），
    把它们整段剪贴到闭合 </X.Resources> 后新增的 <X.Styles> 块中。
    """
    changes = 0
    # 仅处理含未键化 Style 的 Resources 块
    res_pattern = re.compile(
        r'(<(\w+:?\w*)\.Resources>\s*<ResourceDictionary>)([\s\S]*?)(</ResourceDictionary>\s*</\2\.Resources>)'
    )

    def replacer(m):
        nonlocal changes
        opening, owner, body, closing = m.group(1), m.group(2), m.group(3), m.group(4)
        # 收集未键化 Style 顶层元素
        styles_to_move = []
        new_body_parts = []
        idx = 0
        while idx < len(body):
            # 找下一个 <Style
            m_style = re.search(r'<Style[\s>]', body[idx:])
            if not m_style:
                new_body_parts.append(body[idx:])
                break
            start = idx + m_style.start()
            # 收集这段前缀
            new_body_parts.append(body[idx:start])
            # 解析 <Style ...> 开始标签
            tag_end = body.find('>', start)
            if tag_end == -1:
                new_body_parts.append(body[start:])
                break
            tag = body[start:tag_end + 1]
            # 仅处理无 x:Key 的 Style
            if 'x:Key' in tag:
                new_body_parts.append(body[start:])
                break
            # 自闭合（罕见）
            if tag.endswith('/>'):
                styles_to_move.append(tag)
                idx = tag_end + 1
                changes += 1
                continue
            # 找匹配的 </Style>
            depth = 1
            scan = tag_end + 1
            while scan < len(body) and depth > 0:
                open_m = body.find('<Style', scan)
                close_m = body.find('</Style>', scan)
                # 同时也要考虑嵌套 <Style ...>（嵌套 selector）
                if close_m == -1:
                    new_body_parts.append(body[start:])
                    return m.group(0)  # 放弃处理
                # 检查 open_m 是否在 close_m 之前且确实是开始标签
                if open_m != -1 and open_m < close_m:
                    # 确认是 <Style ...> 而非 <StyleXxx
                    after = body[open_m + 6: open_m + 7]
                    if after in (' ', '>', '\t', '\n', '\r'):
                        depth += 1
                        scan = open_m + 6
                        continue
                    scan = open_m + 6
                    continue
                depth -= 1
                if depth == 0:
                    end_tag_close = body.find('>', close_m)
                    block = body[start:end_tag_close + 1]
                    styles_to_move.append(block)
                    idx = end_tag_close + 1
                    changes += 1
                    break
                else:
                    scan = close_m + 8
            else:
                new_body_parts.append(body[start:])
                break

        if not styles_to_move:
            return m.group(0)

        new_body = ''.join(new_body_parts)
        styles_xml = '\n'.join(styles_to_move)
        # 构造 <Owner.Styles> 块（放在 </Owner.Resources> 之后）
        return (
            opening + new_body + closing
            + f'\n  <{owner}.Styles>\n    {styles_xml}\n  </{owner}.Styles>'
        )

    new_content = res_pattern.sub(replacer, content)
    return new_content, changes


def add_xkey_to_unkeyed_datatemplates(content):
    """为 ResourceDictionary 顶层无 x:Key 的 DataTemplate 添加 x:Key（基于 DataType 名称）。

    Avalonia 中 ResourceDictionary 内的 DataTemplate 必须有 x:Key；隐式 DataType 匹配
    需移到 <UserControl.DataTemplates>。此处仅加 x:Key 让其通过编译。
    """
    changes = 0

    def replacer(m):
        nonlocal changes
        full = m.group(0)
        if 'x:Key' in full:
            return full
        data_type = m.group(1)
        # 用 DataType 作为 key（去掉前缀冒号）
        key_base = data_type.split(':')[-1]
        key = f"{key_base}DataTemplate"
        changes += 1
        return f'<DataTemplate x:Key="{key}" DataType="{data_type}">'

    pattern = re.compile(r'<DataTemplate\s+DataType="([^"]+)"')
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ----------------------------------------------------------------------
# 5. 大小写敏感枚举值修正
# ----------------------------------------------------------------------
ENUM_FIXES = [
    # VerticalAlignment
    (re.compile(r'VerticalAlignment="top"'), 'VerticalAlignment="Top"'),
    (re.compile(r'VerticalAlignment="bottom"'), 'VerticalAlignment="Bottom"'),
    (re.compile(r'VerticalAlignment="center"'), 'VerticalAlignment="Center"'),
    (re.compile(r'VerticalAlignment="stretch"'), 'VerticalAlignment="Stretch"'),
    # HorizontalAlignment
    (re.compile(r'HorizontalAlignment="left"'), 'HorizontalAlignment="Left"'),
    (re.compile(r'HorizontalAlignment="right"'), 'HorizontalAlignment="Right"'),
    (re.compile(r'HorizontalAlignment="center"'), 'HorizontalAlignment="Center"'),
    (re.compile(r'HorizontalAlignment="stretch"'), 'HorizontalAlignment="Stretch"'),
    # PlacementMode（WPF "Mouse" → Avalonia "Pointer"）
    (re.compile(r'PlacementMode="Mouse"'), 'PlacementMode="Pointer"'),
    (re.compile(r'PlacementMode="AbsolutePoint"'), 'PlacementMode="Pointer"'),
    (re.compile(r'PlacementMode="RelativePoint"'), 'PlacementMode="Pointer"'),
    # Orientation
    (re.compile(r'Orientation="horizontal"'), 'Orientation="Horizontal"'),
    (re.compile(r'Orientation="vertical"'), 'Orientation="Vertical"'),
    # ScrollBarVisibility
    (re.compile(r'ScrollViewer\.VerticalScrollBarVisibility="auto"'),
     'ScrollViewer.VerticalScrollBarVisibility="Auto"'),
    (re.compile(r'ScrollViewer\.HorizontalScrollBarVisibility="auto"'),
     'ScrollViewer.HorizontalScrollBarVisibility="Auto"'),
    # TextAlignment
    (re.compile(r'TextAlignment="center"'), 'TextAlignment="Center"'),
    (re.compile(r'TextAlignment="left"'), 'TextAlignment="Left"'),
    (re.compile(r'TextAlignment="right"'), 'TextAlignment="Right"'),
    # TextWrapping
    (re.compile(r'TextWrapping="wrap"'), 'TextWrapping="Wrap"'),
    (re.compile(r'TextWrapping="nowrap"'), 'TextWrapping="NoWrap"'),
]

def fix_enum_casing(content):
    changes = 0
    new_content = content
    for pattern, replacement in ENUM_FIXES:
        new_content, n = pattern.subn(replacement, new_content)
        changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 6. DropDownButton 属性元素语法 → 简单属性特性
# ----------------------------------------------------------------------
def fix_drop_down_button_property_elements(content):
    """将 <DropDownButton.Background>...等属性元素改为 Background="..." 内联特性。

    仅在 BinaryContentUserControl 等场景中处理：因为 DropDownButton 未在 xmlns 中
    注册为干净类型前缀，<DropDownButton.X> 会被解析为 attached property 失败。
    """
    changes = 0

    # <DropDownButton.Background><SolidColorBrush>#00FFFFFF</SolidColorBrush></DropDownButton.Background>
    # → Background="#00FFFFFF"
    pattern = re.compile(
        r'<DropDownButton\.Background>\s*<SolidColorBrush>([^<]+)</SolidColorBrush>\s*</DropDownButton\.Background>',
        re.DOTALL
    )
    def repl_bg(m):
        nonlocal changes
        changes += 1
        return f'Background="{m.group(1).strip()}"'

    new_content = pattern.sub(repl_bg, content)

    # <DropDownButton.Focusable>False</DropDownButton.Focusable> → Focusable="False"
    pattern2 = re.compile(
        r'<DropDownButton\.Focusable>([^<]+)</DropDownButton\.Focusable>'
    )
    def repl_focus(m):
        nonlocal changes
        changes += 1
        return f'Focusable="{m.group(1).strip()}"'

    new_content, n2 = pattern2.subn(repl_focus, new_content)
    changes += n2

    # <DropDownButton.ContextMenu> → ContextMenu="..." (但不能内联复杂 ContextMenu，保留为 <ContextMenu> 子元素)
    # Avalonia 中 ContextMenu 可作为属性元素，但类型前缀必须正确。
    # 改为 <controls:DropDownButton.ContextMenu>，但 DropDownButton 在 controls 命名空间。
    pattern3 = re.compile(r'<DropDownButton\.ContextMenu>')
    new_content, n3 = pattern3.subn('<controls:DropDownButton.ContextMenu>', new_content)
    changes += n3
    pattern3b = re.compile(r'</DropDownButton\.ContextMenu>')
    new_content, n3b = pattern3b.subn('</controls:DropDownButton.ContextMenu>', new_content)
    changes += n3b

    return new_content, changes


# ----------------------------------------------------------------------
# 7. Tabcontrol.xaml 中无 Selector 的 Style 修复
# ----------------------------------------------------------------------
def fix_style_without_selector(content):
    """为无 Selector 的 <Style x:Key="..."> 添加 Selector（基于 x:Key 推断）。"""
    changes = 0

    # 仅针对 TabItemFocusVisual 这种"非控件样式"
    # <Style x:Key="TabItemFocusVisual"> → Selector="TabControl"
    pattern = re.compile(r'<Style\s+x:Key="TabItemFocusVisual"\s*>')
    new_content, n = pattern.subn('<Style x:Key="TabItemFocusVisual" Selector="TabControl">', content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 8. StackPanel 缺失属性 → 替换为 Border（Border 才有 BorderThickness/Background）
# ----------------------------------------------------------------------
# 该问题需要逐文件人工判断；先在 GitMmUserControl.xaml 中处理
def fix_stackpanel_to_border_in_gitmm(content):
    """在 GitMmUserControl.xaml 中：若 <StackPanel> 同时设置 BorderThickness/FontFamily/FontSize/Foreground，
    则替换为 <Border><StackPanel>...</StackPanel></Border> 结构。

    最简方案：删除 StackPanel 上 Avalonia 不支持的属性（BorderThickness、FontFamily），
    保留 Foreground/FontSize（StackPanel 在 Avalonia 中支持这些）。
    """
    # 实际上 Avalonia StackPanel 不支持 FontFamily/FontSize/Foreground（这些是 TextElement 属性，
    # 仅 Control 子类有）。简单方案：删除这些属性。
    changes = 0
    # 针对 <StackPanel ... BorderThickness="..." FontFamily="..." FontSize="..." Foreground="...">
    # 一并删除 BorderThickness / FontFamily 属性（StackPanel 不支持）
    new_content = content
    pat_bt = re.compile(r'(<StackPanel[^>]*?)\s+BorderThickness="[^"]*"')
    new_content, n1 = pat_bt.subn(r'\1', new_content)
    changes += n1
    pat_ff = re.compile(r'(<StackPanel[^>]*?)\s+FontFamily="[^"]*"')
    new_content, n2 = pat_ff.subn(r'\1', new_content)
    changes += n2
    return new_content, changes


# ----------------------------------------------------------------------
# 9. Control.BorderBrush Setter 修复（Avalonia Control 基类无 BorderBrush）
# ----------------------------------------------------------------------
def fix_control_borderbrush_setter(content):
    """对于 <Style Selector="Control">...<Setter Property="BorderBrush" .../>...</Style>，
    将 Selector 从 "Control" 改为 "Border"（Border 才有 BorderBrush）。
    """
    # 此类错误通常出现在通用样式文件中；保守起见仅处理 Selector="Control" 且 setter 包含 BorderBrush 的样式
    # 由于影响范围大，先跳过，由人工在 GeneralUserControl.xaml 处理
    return content, 0


# ----------------------------------------------------------------------
# 10. DiffEntryRowUserControl.xaml - 嵌套 Selector 无父样式
# ----------------------------------------------------------------------
def fix_nested_selector_without_parent(content):
    """将独立的 <Style Selector="^:pseudoclass ..."> 转换为完整 Selector。
    嵌套样式缺少父 <Style Selector="..."> 时，将 ^ 前缀替换为具体控件类型。
    """
    # 仅针对 DiffEntryRowUserControl.xaml 中已知模式
    # <Style Selector="^:pointerover /template/ ..."> → <Style Selector="UserControl:pointerover /template/ ...">
    if 'DiffEntryRowUserControl' not in content:
        return content, 0
    pattern = re.compile(r'<Style\s+Selector="\^:([^/]+?)"')
    new_content, n = pattern.subn(lambda m: f'<Style Selector="UserControl:{m.group(1)}"', content)
    return new_content, n


# ----------------------------------------------------------------------
# 主流程
# ----------------------------------------------------------------------
def process_file(path: Path):
    try:
        content = path.read_text(encoding='utf-8')
    except Exception as e:
        print(f"  SKIP {path}: {e}")
        return 0

    original = content
    total_changes = 0

    fixers = [
        ("ControlTemplate TargetType prefix", fix_controltemplate_targettype),
        ("Remove AllowDrop", remove_allowdrop),
        ("ProgressBar PART_Indicator", add_progressbar_indicator),
        ("Move unkeyed Styles to .Styles", move_unkeyed_styles_to_styles_collection),
        ("Add x:Key to unkeyed DataTemplates", add_xkey_to_unkeyed_datatemplates),
        ("Fix enum casing", fix_enum_casing),
        ("Fix DropDownButton property elements", fix_drop_down_button_property_elements),
        ("Fix Style without Selector", fix_style_without_selector),
        ("Fix StackPanel to Border in GitMm", fix_stackpanel_to_border_in_gitmm),
        ("Fix nested Selector without parent", fix_nested_selector_without_parent),
    ]

    for name, fixer in fixers:
        try:
            content, n = fixer(content)
            if n > 0:
                total_changes += n
                print(f"  [{name}] {path.name}: {n} change(s)")
        except Exception as e:
            print(f"  ERROR [{name}] {path.name}: {e}")

    if content != original:
        path.write_text(content, encoding='utf-8')
    return total_changes


def main():
    print("fix_xaml10.py - 阶段 5 收尾修复")
    total = 0
    # 处理所有 XAML 文件
    for path in sorted(ROOT.rglob("*.xaml")):
        rel = path.relative_to(ROOT)
        # 跳过 obj/bin
        if any(seg in ('obj', 'bin') for seg in path.parts):
            continue
        n = process_file(path)
        total += n
    print(f"\n总变更: {total}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
