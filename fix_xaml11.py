#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
fix_xaml11.py - 阶段 5 第 11 轮修复

针对 fix_xaml10 后剩余的 99 个错误：
  1. DataTemplate 标签末尾多余的 `>`（造成 "multiple assignments to Content" 错误）
  2. Commonresources.xaml: 缺少 CodeEditor/CommandTextBox 命名空间前缀
  3. Commonresources.xaml: ToolTip.HasDropShadow 不存在 → 删除
  4. Commonresources.xaml: 无 Selector 的 Style
  5. Combobox.xaml: ComboBox PlaceholderTextBox 等自定义控件 TargetType 前缀
  6. Combobox.xaml: Control 元素上 BorderBrush/BorderThickness/Background 等问题（应改 Selector）
  7. Listview.xaml: ListBox.VerticalContentAlignment/HorizontalContentAlignment
  8. Listview.xaml: ScrollViewer.ComputedHorizontal/VerticalScrollBarVisibility 不存在
  9. Listview.xaml: ControlTemplate 多内容
 10. Menu.xaml: RepeatButton.CommandTarget, Popup.PlacementRectangle, Menu.VerticalContentAlignment,
              MenuItem.Horizontal/VerticalContentAlignment, ButtonBase 类型解析, MenuItem.SeparatorStyleKey
 11. Sidebar.xaml: DataTemplate 末尾 `>>`
 12. Listview.xaml: BoolToBrushConverter TrueBrush/FalseBrush 绑定（已通过 C# 修复）
 13. GeneralUserControl.xaml: Control.BorderBrush Setter
 14. Tabcontrol.xaml: ClosableTabItem.ContextMenu 等
 15. Focusvisual.xaml: 无 Selector 的 Style
"""
import re
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")

# ----------------------------------------------------------------------
# 1. DataTemplate 末尾多余的 `>` 修复
# ----------------------------------------------------------------------
def fix_datatemplate_extra_gt(content):
    """修复 <DataTemplate ...>> 末尾多余的 >。"""
    changes = 0
    # <DataTemplate x:Key="..." DataType="...">>  → <DataTemplate x:Key="..." DataType="...">
    pattern = re.compile(r'(<DataTemplate\s+[^>]*?)>>\s*$', re.MULTILINE)
    new_content, n = pattern.subn(r'\1>', content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 2. Commonresources.xaml: CodeEditor/CommandTextBox TargetType 前缀
# ----------------------------------------------------------------------
EXTRA_CUSTOM_CONTROLS = {
    "CodeEditor": "editor",
    "CommandTextBox": "controls",
}

def fix_extra_custom_control_targettype(content):
    """为 CodeEditor (editor:) / CommandTextBox (controls:) 等 TargetType 添加前缀。"""
    changes = 0

    def replacer(m):
        nonlocal changes
        prefix_attr = m.group(1) or ""
        type_name = m.group(2)
        if ":" in type_name:
            return m.group(0)
        ns_prefix = EXTRA_CUSTOM_CONTROLS.get(type_name)
        if not ns_prefix:
            return m.group(0)
        changes += 1
        return f'<ControlTemplate{prefix_attr} TargetType="{ns_prefix}:{type_name}"'

    pattern = re.compile(
        r'<ControlTemplate(\s+[^>]*?|\s+)TargetType="([^":]+?)"'
    )
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ----------------------------------------------------------------------
# 3. 删除 WPF-only 属性
# ----------------------------------------------------------------------
WPF_ONLY_ATTRIBUTES = [
    "ToolTip.HasDropShadow",
    "ScrollViewer.ComputedHorizontalScrollBarVisibility",
    "ScrollViewer.ComputedVerticalScrollBarVisibility",
    "Popup.PlacementRectangle",
    "CommandTarget",
]

def remove_wpf_only_attributes(content):
    """删除 WPF-only 属性（attached property 或不支持的事件）。"""
    changes = 0
    new_content = content
    for attr in WPF_ONLY_ATTRIBUTES:
        # <X ... Attr="..." ...>
        pattern = re.compile(r'\s+' + re.escape(attr) + r'="[^"]*"')
        new_content, n = pattern.subn("", new_content)
        changes += n
        # <X.Attr>...</X.Attr> 仅当 attr 含点号时处理
        if '.' in attr:
            parts = attr.split('.', 1)
            ns_part, prop_part = parts[0], parts[1]
            pattern2 = re.compile(
                r'\s*<' + re.escape(ns_part) + r'\.' + re.escape(prop_part) + r'>[\s\S]*?</' +
                re.escape(ns_part) + r'\.' + re.escape(prop_part) + r'>\s*\n?'
            )
            new_content, n2 = pattern2.subn("\n", new_content)
            changes += n2
        else:
            # 简单属性元素：<Attr>value</Attr>
            pattern2 = re.compile(
                r'\s*<' + re.escape(attr) + r'>[\s\S]*?</' + re.escape(attr) + r'>\s*\n?'
            )
            new_content, n2 = pattern2.subn("\n", new_content)
            changes += n2
    return new_content, changes


# ----------------------------------------------------------------------
# 4. Commonresources.xaml 中无 Selector 的 Style 修复
# ----------------------------------------------------------------------
def fix_unkeyed_unselected_styles(content):
    """为无 Selector 的 <Style x:Key="..."> 添加 Selector。

    策略：对于 <Style x:Key="SomeKey">（无 Selector），添加 Selector="Control" 作为兜底。
    """
    changes = 0
    # 仅匹配 <Style x:Key="..." (无 Selector)>
    pattern = re.compile(r'(<Style\s+x:Key="[^"]*")(?!\s+Selector)(\s*>)')
    new_content, n = pattern.subn(r'\1 Selector="Control"\2', content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 5. Combobox.xaml Control 元素属性问题
#    BorderBrush/BorderThickness/Background/Foreground/HorizontalContentAlignment/
#    VerticalContentAlignment/Padding on type "Control" - 改 Selector 为具体类型
# ----------------------------------------------------------------------
# 这部分太复杂，需要逐个分析；先跳过，让其他修复先完成


# ----------------------------------------------------------------------
# 6. Listview.xaml: ListBox VerticalContentAlignment/HorizontalContentAlignment
#    Avalonia ListBox 桥接类已添加这些属性，但 Selector 写法可能有问题
# ----------------------------------------------------------------------
def fix_listbox_contentalignment_setter(content):
    """将 ListBox 上 HorizontalContentAlignment="Left" 等改为 HorizontalAlignment="Left"，
    因为 Avalonia ListBox 没有 HorizontalContentAlignment（WPF 才有）。

    但桥接类 ForkPlus.UI.ListBox 已添加这些属性，所以理论上应该可以工作。
    如果还是报错，可能是 Selector 没有指向我们的桥接类型，而是默认 Avalonia.ListBox。
    """
    # 暂不处理，让桥接类型生效
    return content, 0


# ----------------------------------------------------------------------
# 7. Menu.xaml: MenuItem.SeparatorStyleKey 问题
#    <Style x:Key="{x:Static MenuItem.SeparatorStyleKey}"> 不被支持
#    改为普通 x:Key
# ----------------------------------------------------------------------
def fix_menu_separator_style_key(content):
    """将 x:Key="{x:Static MenuItem.SeparatorStyleKey}" 替换为 x:Key="MenuItemSeparatorStyle"。"""
    changes = 0
    pattern = re.compile(r'x:Key="\{x:Static\s+MenuItem\.SeparatorStyleKey\}"')
    new_content, n = pattern.subn('x:Key="MenuItemSeparatorStyle"', content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 8. Menu.xaml: ButtonBase 类型解析
# ----------------------------------------------------------------------
def fix_menubase_targettype(content):
    """Menu.xaml 中 TargetType="ButtonBase" → "Button"（Avalonia 无 ButtonBase 控件）。"""
    changes = 0
    pattern = re.compile(r'TargetType="ButtonBase"')
    new_content, n = pattern.subn('TargetType="Button"', content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 9. GeneralUserControl.xaml: <Setter Property="BorderBrush" /> on Control
# ----------------------------------------------------------------------
def fix_general_control_borderbrush(content):
    """将 GeneralUserControl.xaml 中 Selector="Control" + BorderBrush Setter 的样式
    Selector 改为 "Border"。
    """
    if 'GeneralUserControl' not in content:
        return content, 0
    changes = 0
    # 仅在该文件中处理
    pattern = re.compile(r'<Style\s+x:Key="[^"]*"\s+Selector="Control"')
    new_content, n = pattern.subn(lambda m: m.group(0).replace('Selector="Control"', 'Selector="Border"'), content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 10. ContentControl 多内容修复（多个子元素需要包装在 Grid/StackPanel 中）
# ----------------------------------------------------------------------
def fix_contentcontrol_multiple_content(content):
    """对于 ControlTemplate/ContentControl 中多个子元素，包裹在 <Grid> 中。

    简单策略：检测 <ControlTemplate ...>XXXX</ControlTemplate> 中有多个顶级子元素时，
    用 <Grid>...</Grid> 包裹。但这需要解析 XML，复杂度高。
    先跳过，由人工处理。
    """
    return content, 0


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
        ("DataTemplate extra >", fix_datatemplate_extra_gt),
        ("Extra custom control TargetType", fix_extra_custom_control_targettype),
        ("Remove WPF-only attributes", remove_wpf_only_attributes),
        ("Fix unkeyed/unSelector'd Styles", fix_unkeyed_unselected_styles),
        ("Fix Menu SeparatorStyleKey", fix_menu_separator_style_key),
        ("Fix ButtonBase TargetType", fix_menubase_targettype),
        ("Fix General Control BorderBrush", fix_general_control_borderbrush),
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
    print("fix_xaml11.py - 第 11 轮修复")
    total = 0
    for path in sorted(ROOT.rglob("*.xaml")):
        if any(seg in ('obj', 'bin') for seg in path.parts):
            continue
        n = process_file(path)
        total += n
    print(f"\n总变更: {total}")
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(main())
