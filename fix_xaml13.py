#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
fix_xaml13.py - 阶段 5 第 13 轮修复

针对 fix_xaml12 后剩余的 26 个错误：
  1. Listview/Menu: 删除 VerticalContentAlignment/HorizontalContentAlignment Setter
     （Selector 写法可能是 x:Key 在前 Selector 在后）
  2. Listview: ComputedHorizontal/VerticalScrollBarVisibility (TemplateBinding 形式) 删除
  3. Listview.xaml line 446: ControlTemplate 多内容（再次处理）
  4. Menu.xaml: ButtonBase 类型 TargetType → Button（再次处理）
  5. Menu.xaml: Command="ScrollBar.LineUpCommand" → 删除（Avalonia 不支持 WPF 路由命令）
  6. GeneralUserControl: Control.BorderBrush → editor:DiffCodeEditor.BorderBrush（已人工修复）
  7. BinaryContentUserControl: DropDownButton 多内容（已人工修复）
"""
import re
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")


# ----------------------------------------------------------------------
# 1. 删除 VerticalContentAlignment/HorizontalContentAlignment Setter（增强版）
# ----------------------------------------------------------------------
def remove_contentalignment_setters_v2(content):
    """删除所有 Selector 上 HorizontalContentAlignment/VerticalContentAlignment 的 Setter。

    增强版：处理 <Style x:Key="X" Selector="ControlType"> 写法（任意控件类型）。
    """
    changes = 0
    for prop in ('HorizontalContentAlignment', 'VerticalContentAlignment'):
        # <Setter Property="X" Value="..." />
        pattern = re.compile(
            r'\s*<Setter\s+Property="' + prop + r'"\s+Value="[^"]*"\s*/>\s*\n?'
        )
        new_content, n = pattern.subn("\n", content)
        changes += n
        content = new_content
    return content, changes


# ----------------------------------------------------------------------
# 2. 删除 ComputedHorizontal/VerticalScrollBarVisibility (TemplateBinding 形式)
# ----------------------------------------------------------------------
def remove_computed_scrollbar_visibility_templatebinding(content):
    """删除 IsVisible="{TemplateBinding ComputedHorizontalScrollBarVisibility}" 等绑定。"""
    changes = 0
    # IsVisible="{TemplateBinding ComputedXxx}" → IsVisible="True"（占位）
    for prop in (
        'ComputedHorizontalScrollBarVisibility',
        'ComputedVerticalScrollBarVisibility',
    ):
        # 形式 1: IsVisible="{TemplateBinding ComputedXxx}"
        pattern1 = re.compile(
            r'IsVisible="\{TemplateBinding\s+' + prop + r'\}"'
        )
        new_content, n1 = pattern1.subn('IsVisible="True"', content)
        changes += n1
        content = new_content
        # 形式 2: <X.IsVisible><Binding Path="ComputedXxx" .../></X.IsVisible>
        # 这种较少见，先跳过
    return content, changes


# ----------------------------------------------------------------------
# 3. Listview.xaml line 446: ControlTemplate 多内容（再处理）
#    <ControlTemplate TargetType="ListBoxItem">
#      <Border ...>...</Border>
#      <!-- comment -->
#      <Style Selector="^...">...</Style>
#    </ControlTemplate>
# ----------------------------------------------------------------------
def move_nested_style_out_of_controltemplate_v2(content):
    """将 ControlTemplate 内的 <Style Selector="^..."> 块移到 </Setter> 之后。

    增强版：使用更宽松的匹配，处理 ControlTemplate 结束标签后没有紧跟 </Setter.Value></Setter> 的情况。
    """
    changes = 0
    while True:
        # 找 <ControlTemplate ...>...(嵌套 Style)...</ControlTemplate>
        ct_pattern = re.compile(
            r'(<ControlTemplate\s+[^>]*>)([\s\S]*?)(</ControlTemplate>)'
        )
        # 找第一个含嵌套 Style 的 ControlTemplate
        match = None
        for m in ct_pattern.finditer(content):
            body = m.group(2)
            # 在 body 中找 <Style Selector="^...">
            style_pattern = re.compile(r'<Style\s+Selector="\^[^"]*"[^>]*>[\s\S]*?</Style>')
            if style_pattern.search(body):
                match = (m, style_pattern)
                break
        if not match:
            break

        m, style_pattern = match
        opening, body, closing = m.group(1), m.group(2), m.group(3)

        sm = style_pattern.search(body)
        if not sm:
            break

        style_block = sm.group(0)
        # 从 body 中删除 Style 块
        new_body = body[:sm.start()].rstrip() + '\n' + body[sm.end():].lstrip()
        # 重建 ControlTemplate
        new_content = (
            content[:m.start()]
            + opening + new_body + closing
            + '\n      ' + style_block
            + content[m.end():]
        )
        content = new_content
        changes += 1

    return content, changes


# ----------------------------------------------------------------------
# 4. Menu.xaml: ButtonBase 类型 → Button
# ----------------------------------------------------------------------
def fix_buttonbase_typename(content):
    """将 TargetType="ButtonBase" 改为 TargetType="Button"（Avalonia 无 ButtonBase 控件）。
    同时将 Selector="ButtonBase" 改为 Selector="Button"。
    """
    changes = 0
    pattern1 = re.compile(r'TargetType="ButtonBase"')
    new_content, n1 = pattern1.subn('TargetType="Button"', content)
    changes += n1
    pattern2 = re.compile(r'Selector="ButtonBase"')
    new_content, n2 = pattern2.subn('Selector="Button"', new_content)
    changes += n2
    return new_content, changes


# ----------------------------------------------------------------------
# 5. Menu.xaml: Command="ScrollBar.LineUpCommand" → 删除
# ----------------------------------------------------------------------
def remove_wpf_scrollbar_command(content):
    """删除 WPF 路由命令 Command="ScrollBar.LineUpCommand" / Command="ScrollBar.LineDownCommand"。

    这些命令在 Avalonia 中不存在。改为普通按钮（无 Command 绑定）。
    """
    changes = 0
    # Command="ScrollBar.LineUpCommand" / Command="ScrollBar.LineDownCommand"
    pattern = re.compile(r'\s+Command="ScrollBar\.\w+"')
    new_content, n = pattern.subn("", content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 6. 处理 ContentControl 多内容
#    <X.Content>...</X.Content> 后又跟 <Y>...</Y> - 需要将后续内容包到 <X.Content> 中
#    或将多余内容删除
# ----------------------------------------------------------------------
def fix_contentcontrol_multi_content(content):
    """简化处理：检测 <ContentControl ...>...<X>...</X>...<Y>...</Y>...</ContentControl>
    中的多个子元素。这种修复复杂，先跳过。
    """
    return content, 0


# ----------------------------------------------------------------------
# 7. <ui:GitPointView> 上 Foreground/FontSize/FontWeight Setter
#    桥接类已加这些属性；但 Selector="controls|GitPointView" 时需要确认类型解析。
# ----------------------------------------------------------------------
def fix_gitpointview_setters(content):
    """GitPointView 桥接类已加 FontSize/FontWeight/Foreground 属性。
    若 Selector 使用 controls|GitPointView 前缀，应该能解析。
    此函数空操作，仅作为占位。"""
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
        ("Remove ContentAlignment Setters v2", remove_contentalignment_setters_v2),
        ("Remove ComputedScrollBarVisibility TemplateBinding", remove_computed_scrollbar_visibility_templatebinding),
        ("Move nested Style out of ControlTemplate v2", move_nested_style_out_of_controltemplate_v2),
        ("Fix ButtonBase typename", fix_buttonbase_typename),
        ("Remove WPF ScrollBar Command", remove_wpf_scrollbar_command),
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
    print("fix_xaml13.py - 第 13 轮修复")
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
