#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
fix_xaml12.py - 阶段 5 第 12 轮修复

针对 fix_xaml11 后剩余的 67 个错误：
  1. Combobox.xaml: 3 个 ControlTemplate 缺 TargetType="ComboBox"
  2. Commonresources/Focusvisual/Listview: Selector="Control" + Template Setter → Selector="TemplatedControl"
  3. Commonresources: ToolTip.HasDropShadow Setter 删除
  4. CommandTextBox 模板缺少 PART_TextPresenter
  5. Menu.xaml: RepeatButton.CommandTarget 属性元素删除
  6. Menu.xaml: MenuItem.InputGestureText → MenuItem.InputGesture
  7. Menu.xaml: ButtonBase 类型 TargetType → Button
  8. ListBox VerticalContentAlignment/HorizontalContentAlignment 桥接（C# 已加，但需检查 Selector）
  9. Listview.xaml: ControlTemplate 内嵌 <Style> 移到外层 <Style>
"""
import re
from pathlib import Path

ROOT = Path("/workspace/src/ForkPlus")


# ----------------------------------------------------------------------
# 1. 为缺 TargetType 的 ControlTemplate 添加 TargetType
# ----------------------------------------------------------------------
def add_targettype_to_controltemplate(content):
    """为 <ControlTemplate x:Key="..."> 添加 TargetType。

    仅处理命名模板（x:Key 存在但 TargetType 缺失）：
      - 含 "ComboBox" 关键字的 → TargetType="ComboBox"
      - 含 "Button" 关键字的 → TargetType="Button"
      - 否则跳过
    """
    changes = 0

    def replacer(m):
        nonlocal changes
        full = m.group(0)
        if 'TargetType' in full:
            return full
        key = m.group(1)
        # 根据 x:Key 推断类型
        if 'ComboBox' in key:
            target = 'ComboBox'
        elif 'Button' in key:
            target = 'Button'
        elif 'ToggleButton' in key:
            target = 'ToggleButton'
        elif 'TextBox' in key:
            target = 'TextBox'
        elif 'ListBox' in key:
            target = 'ListBox'
        elif 'MenuItem' in key:
            target = 'MenuItem'
        elif 'TabItem' in key:
            target = 'TabItem'
        else:
            return full
        changes += 1
        return f'<ControlTemplate x:Key="{key}" TargetType="{target}">'

    pattern = re.compile(r'<ControlTemplate\s+x:Key="([^"]+)"\s*>')
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ----------------------------------------------------------------------
# 2. Selector="Control" + Template Setter → Selector="TemplatedControl"
# ----------------------------------------------------------------------
def fix_control_selector_with_template_setter(content):
    """将 <Style Selector="Control"> 改为 <Style Selector="TemplatedControl">，
    仅当 Style 中包含 <Setter Property="Template"> 时。
    """
    changes = 0

    def replacer(m):
        nonlocal changes
        full = m.group(0)
        if '<Setter Property="Template"' not in full:
            return full
        # 替换 Selector="Control" 为 Selector="TemplatedControl"
        new = full.replace('Selector="Control"', 'Selector="TemplatedControl"', 1)
        changes += 1
        return new

    # 匹配 <Style ...> ... </Style>（含嵌套需谨慎，但 Style 一般不深嵌）
    pattern = re.compile(r'<Style\s+[^>]*Selector="Control"[^>]*>[\s\S]*?</Style>')
    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ----------------------------------------------------------------------
# 3. 删除 ToolTip.HasDropShadow Setter
# ----------------------------------------------------------------------
def remove_tooltip_hasdropshadow_setter(content):
    """删除 <Setter Property="HasDropShadow" Value="..." /> 在 ToolTip 样式中。"""
    changes = 0
    pattern = re.compile(
        r'\s*<Setter\s+Property="HasDropShadow"\s+Value="[^"]*"\s*/>\s*\n?'
    )
    new_content, n = pattern.subn("\n", content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 4. CommandTextBox 模板 PART_TextPresenter
# ----------------------------------------------------------------------
def add_part_textpresenter_to_commandtextbox(content):
    """若 CommandTextBox ControlTemplate 缺少 PART_TextPresenter，添加占位。

    CommandTextBox 继承自 TextBox，Avalonia TextBox 模板要求 PART_TextPresenter。
    在 <ControlTemplate TargetType="controls:CommandTextBox"> 内的 DockPanel 后添加。
    """
    # 仅在该文件包含 CommandTextBox 模板且无 PART_TextPresenter 时处理
    if 'CommandTextBox' not in content:
        return content, 0
    if 'PART_TextPresenter' in content:
        return content, 0
    # 定位 <ControlTemplate TargetType="controls:CommandTextBox">...<DockPanel>
    pattern = re.compile(
        r'(<ControlTemplate\s+TargetType="controls:CommandTextBox">[\s\S]*?<DockPanel>)'
    )
    m = pattern.search(content)
    if not m:
        return content, 0
    # 在 DockPanel 后插入 TextPresenter
    insertion = m.group(1) + '\n            <TextPresenter x:Name="PART_TextPresenter" />'
    new_content = content[:m.start()] + insertion + content[m.end():]
    return new_content, 1


# ----------------------------------------------------------------------
# 5. 删除 RepeatButton.CommandTarget 属性元素
# ----------------------------------------------------------------------
def remove_repeatbutton_commandtarget(content):
    """删除 <RepeatButton.CommandTarget>...</RepeatButton.CommandTarget> 块。"""
    changes = 0
    pattern = re.compile(
        r'\s*<RepeatButton\.CommandTarget>[\s\S]*?</RepeatButton\.CommandTarget>\s*\n?'
    )
    new_content, n = pattern.subn("\n", content)
    changes += n
    return new_content, changes


# ----------------------------------------------------------------------
# 6. MenuItem.InputGestureText → MenuItem.InputGesture
# ----------------------------------------------------------------------
def fix_menuitem_inputgesture(content):
    """将 MenuItem.InputGestureText 改为 MenuItem.InputGesture（Avalonia 11 命名）。"""
    changes = 0
    # TemplateBinding MenuItem.InputGestureText → TemplateBinding MenuItem.InputGesture
    pattern1 = re.compile(r'\{TemplateBinding\s+MenuItem\.InputGestureText\}')
    new_content, n1 = pattern1.subn('{TemplateBinding MenuItem.InputGesture}', content)
    changes += n1
    # Property="InputGestureText" → Property="InputGesture"
    pattern2 = re.compile(r'Property="InputGestureText"')
    new_content, n2 = pattern2.subn('Property="InputGesture"', new_content)
    changes += n2
    # InputGestureText="..." 属性 → InputGesture="..."
    pattern3 = re.compile(r'\s+InputGestureText="([^"]*)"')
    new_content, n3 = pattern3.subn(r' InputGesture="\1"', new_content)
    changes += n3
    return new_content, changes


# ----------------------------------------------------------------------
# 7. Listview.xaml: ControlTemplate 内嵌 <Style> 移到外层 <Style>
# ----------------------------------------------------------------------
def move_nested_style_out_of_controltemplate(content):
    """将 <ControlTemplate> 内的 <Style Selector="^..."> 块移到父 <Style> 内（与 Setter 同级）。

    策略：栈式扫描，找到 <ControlTemplate>...<Style Selector="^...">...</Style>...</ControlTemplate>
    中的 Style 块，移到 </ControlTemplate></Setter.Value></Setter> 之后、</Style> 之前。
    """
    changes = 0
    # 匹配 <Setter Property="Template"><Setter.Value><ControlTemplate ...>...(嵌套 Style)...</ControlTemplate></Setter.Value></Setter>
    # 简化：找 <ControlTemplate ...> 中的 <Style Selector="^...">...</Style>
    # 然后将其移到 </Setter> 后

    # 用迭代方式处理（一次处理一个嵌套 Style）
    while True:
        # 找 ControlTemplate 内的 Style
        ct_pattern = re.compile(
            r'(<ControlTemplate\s+TargetType="[^"]+"\s*>)([\s\S]*?)(</ControlTemplate>\s*</Setter\.Value>\s*</Setter>)'
        )
        m = ct_pattern.search(content)
        if not m:
            break

        opening, body, closing = m.group(1), m.group(2), m.group(3)

        # 在 body 中找嵌套 <Style Selector="^...">
        style_pattern = re.compile(r'<Style\s+Selector="\^[^"]*"[^>]*>[\s\S]*?</Style>')
        sm = style_pattern.search(body)
        if not sm:
            break  # 没有嵌套 Style

        style_block = sm.group(0)
        # 从 body 中删除 Style 块（及其前面的空白）
        new_body = body[:sm.start()].rstrip() + body[sm.end():]
        # 构造新的 XML：ControlTemplate（清理后）+ </Setter.Value></Setter> + Style 块
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
# 8. Menu.xaml: 处理 ContextMenu/Menu/MenuItem 的 VerticalContentAlignment/HorizontalContentAlignment
#    Avalonia 这些控件没有这些属性。简单删除 Setter。
# ----------------------------------------------------------------------
def remove_menu_contentalignment_setters(content):
    """删除 ContextMenu/Menu/MenuItem 上 HorizontalContentAlignment/VerticalContentAlignment 的 Setter。

    仅处理 Selector 包含 Menu 关键字的样式。
    """
    changes = 0
    # 匹配 <Style Selector="ContextMenu|Menu|MenuItem"...>...</Style>
    pattern = re.compile(
        r'(<Style\s+Selector="(?:ContextMenu|Menu|MenuItem)"[^>]*>)([\s\S]*?)(</Style>)'
    )
    def replacer(m):
        nonlocal changes
        opening, body, closing = m.group(1), m.group(2), m.group(3)
        # 删除 HorizontalContentAlignment/VerticalContentAlignment setter
        new_body = body
        for prop in ('HorizontalContentAlignment', 'VerticalContentAlignment'):
            pat = re.compile(
                r'\s*<Setter\s+Property="' + prop + r'"\s+Value="[^"]*"\s*/>\s*\n?'
            )
            new_body, n = pat.subn("\n", new_body)
            changes += n
        return opening + new_body + closing

    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ----------------------------------------------------------------------
# 9. ListBox VerticalContentAlignment/HorizontalContentAlignment
#    桥接类已加，但 Selector="ListBox" 指向 Avalonia.ListBox（而非桥接类）。
#    解决：删除这些 Setter（Avalonia ListBox 不需要）。
# ----------------------------------------------------------------------
def remove_listbox_contentalignment_setters(content):
    """删除 Selector="ListBox" 样式上的 HorizontalContentAlignment/VerticalContentAlignment Setter。"""
    changes = 0
    pattern = re.compile(
        r'(<Style\s+Selector="ListBox"[^>]*>)([\s\S]*?)(</Style>)'
    )
    def replacer(m):
        nonlocal changes
        opening, body, closing = m.group(1), m.group(2), m.group(3)
        new_body = body
        for prop in ('HorizontalContentAlignment', 'VerticalContentAlignment'):
            pat = re.compile(
                r'\s*<Setter\s+Property="' + prop + r'"\s+Value="[^"]*"\s*/>\s*\n?'
            )
            new_body, n = pat.subn("\n", new_body)
            changes += n
        return opening + new_body + closing

    new_content = pattern.sub(replacer, content)
    return new_content, changes


# ----------------------------------------------------------------------
# 10. ScrollViewer.ComputedHorizontal/VerticalScrollBarVisibility 删除
# ----------------------------------------------------------------------
def remove_scrollviewer_computed_visibility(content):
    """删除 ScrollViewer.ComputedHorizontalScrollBarVisibility / ComputedVerticalScrollBarVisibility 属性。"""
    changes = 0
    for attr in (
        'ScrollViewer.ComputedHorizontalScrollBarVisibility',
        'ScrollViewer.ComputedVerticalScrollBarVisibility',
    ):
        pattern = re.compile(r'\s+' + re.escape(attr) + r'="[^"]*"')
        new_content, n = pattern.subn("", content)
        changes += n
        content = new_content
    return content, changes


# ----------------------------------------------------------------------
# 11. StackPanel FontSize/Foreground 删除
# ----------------------------------------------------------------------
def remove_stackpanel_text_properties(content):
    """删除 StackPanel 上的 FontSize/Foreground/FontFamily/FontWeight Setter 或属性。"""
    changes = 0
    # 仅在 <StackPanel ...> 标签中删除这些属性
    new_content = content
    for prop in ('FontSize', 'Foreground', 'FontFamily', 'FontWeight'):
        pat = re.compile(r'(<StackPanel[^>]*?)\s+' + prop + r'="[^"]*"')
        new_content, n = pat.subn(r'\1', new_content)
        changes += n
    return new_content, changes


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
        ("Add TargetType to ControlTemplate", add_targettype_to_controltemplate),
        ("Fix Selector=Control with Template Setter", fix_control_selector_with_template_setter),
        ("Remove ToolTip.HasDropShadow Setter", remove_tooltip_hasdropshadow_setter),
        ("Add PART_TextPresenter to CommandTextBox", add_part_textpresenter_to_commandtextbox),
        ("Remove RepeatButton.CommandTarget", remove_repeatbutton_commandtarget),
        ("Fix MenuItem.InputGestureText", fix_menuitem_inputgesture),
        ("Move nested Style out of ControlTemplate", move_nested_style_out_of_controltemplate),
        ("Remove Menu ContentAlignment Setters", remove_menu_contentalignment_setters),
        ("Remove ListBox ContentAlignment Setters", remove_listbox_contentalignment_setters),
        ("Remove ScrollViewer ComputedVisibility", remove_scrollviewer_computed_visibility),
        ("Remove StackPanel text properties", remove_stackpanel_text_properties),
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
    print("fix_xaml12.py - 第 12 轮修复")
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
