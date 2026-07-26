#!/usr/bin/env python3
"""
Convert Avalonia theme XAML files from <ResourceDictionary> root to <Styles> root.

Why: <StyleInclude> in Application.Styles requires the target file's root to be
<Styles> (or a single <Style>). Files with <ResourceDictionary> root fail with
AVLN2000 at compile time and the styles inside never get auto-applied to controls,
which causes "KeyNotFoundException: PART_WindowHeader" at runtime.

Strategy:
  - For each input file, parse the root <ResourceDictionary ...> opening tag and
    remember its xmlns attribute string.
  - Walk the inner content tracking element depth. Top-level (depth==0) children
    are split into:
      * comments           -> preserved inline (kept with the resource group)
      * <Style ...>        -> direct children of the new <Styles> root
      * anything else      -> wrapped in <Styles.Resources>
  - Write back with <Styles {attrs}> ... </Styles>.
"""
import os
import re

# Matches a tag (opening, closing, or self-closing)
TAG_RE = re.compile(rb'<(/?)([a-zA-Z_:][\w:.-]*)((?:[^>"\']|"[^"]*"|\'[^\']*\')*)>', re.DOTALL)


def find_element_end(s: bytes, start: int, name: bytes) -> int:
    """Given position right after an opening tag <name ...>, find the position
    just after the matching </name> close tag."""
    depth = 1
    pos = start
    n = len(s)
    open_re = re.compile(rb'<' + re.escape(name) + rb'(?:\s[^>]*?>|/?>|>)')
    close_re = re.compile(rb'</' + re.escape(name) + rb'\s*>')
    while pos < n and depth > 0:
        cmt = s.find(b'<!--', pos)
        om = open_re.search(s, pos)
        cm = close_re.search(s, pos)

        candidates = []
        if cmt != -1:
            candidates.append(('cmt', cmt))
        if om:
            candidates.append(('open', om.start()))
        if cm:
            candidates.append(('close', cm.start()))

        if not candidates:
            break
        candidates.sort(key=lambda x: x[1])
        kind, cpos = candidates[0]

        if kind == 'cmt':
            cend = s.find(b'-->', cmt + 4)
            if cend == -1:
                break
            pos = cend + 3
            continue

        if kind == 'open':
            tag_text = s[om.start():om.end()]
            if tag_text.rstrip().endswith(b'/>'):
                pos = om.end()
            else:
                depth += 1
                pos = om.end()
            continue

        if kind == 'close':
            depth -= 1
            pos = cm.end()
            if depth == 0:
                return pos

    return pos


def parse_top_level(inner: bytes):
    """Walk inner content (bytes between root open and root close) and return a
    list of (kind, text) tuples where kind is one of: 'comment', 'style', 'resource', 'text'."""
    nodes = []
    pos = 0
    n = len(inner)
    depth = 0
    buf_start = 0

    while pos < n:
        # Try comment first
        if inner[pos:pos+4] == b'<!--':
            end = inner.find(b'-->', pos + 4)
            if end == -1:
                break
            end += 3
            if depth == 0:
                nodes.append(('comment', inner[buf_start:end]))
                buf_start = end
            pos = end
            continue

        m = TAG_RE.match(inner, pos)
        if not m:
            pos += 1
            continue

        slash = m.group(1)
        name = m.group(2)
        attrs = m.group(3)
        self_closing = attrs.rstrip().endswith(b'/')
        tag_end = m.end()

        if slash == b'':
            if depth == 0:
                leading = inner[buf_start:pos]
                if name == b'Style':
                    kind = 'style'
                else:
                    kind = 'resource'
                if self_closing:
                    end = tag_end
                else:
                    end = find_element_end(inner, tag_end, name)
                node_text = leading + inner[pos:end]
                nodes.append((kind, node_text))
                buf_start = end
                pos = end
            else:
                if not self_closing:
                    depth += 1
                pos = tag_end
        else:
            pos = tag_end

    trailing = inner[buf_start:]
    if trailing.strip():
        nodes.append(('text', trailing))

    return nodes


def convert_file(filepath: str) -> bool:
    with open(filepath, 'rb') as f:
        content = f.read()

    open_match = re.search(rb'<ResourceDictionary\b[^>]*>', content)
    if not open_match:
        print(f"  SKIP (no ResourceDictionary root): {filepath}")
        return False

    open_tag = open_match.group(0).decode('utf-8')
    attrs = open_tag[len('<ResourceDictionary'):-1].strip()

    close_idx = content.rfind(b'</ResourceDictionary>')
    if close_idx == -1:
        print(f"  SKIP (no closing tag): {filepath}")
        return False

    inner = content[open_match.end():close_idx]

    nodes = parse_top_level(inner)

    resources = [t for k, t in nodes if k in ('resource', 'comment')]
    styles = [t for k, t in nodes if k == 'style']
    trailing = [t for k, t in nodes if k == 'text']

    # Preserve BOM
    bom = b''
    if content.startswith(b'\xef\xbb\xbf'):
        bom = b'\xef\xbb\xbf'
    before = content[len(bom):open_match.start()].decode('utf-8', errors='replace')
    after = content[close_idx + len(b'</ResourceDictionary>'):].decode('utf-8', errors='replace')

    new_parts = []
    new_parts.append(bom.decode('utf-8'))
    new_parts.append(before)
    new_parts.append(f'<Styles{(" " + attrs) if attrs else ""}>')

    if resources:
        new_parts.append('\n  <Styles.Resources>')
        for r in resources:
            text = r.decode('utf-8', errors='replace')
            text = text.lstrip('\n').rstrip()
            lines = text.split('\n')
            indented = '\n'.join('    ' + ln if ln.strip() else ln for ln in lines)
            new_parts.append('\n' + indented)
        new_parts.append('\n  </Styles.Resources>\n')

    for s in styles:
        new_parts.append(s.decode('utf-8', errors='replace'))
        if not new_parts[-1].endswith('\n'):
            new_parts.append('\n')

    new_parts.append('</Styles>')
    new_parts.append(after)

    new_content = ''.join(new_parts)

    with open(filepath, 'wb') as f:
        f.write(new_content.encode('utf-8'))

    print(f"  OK: {filepath} ({len(resources)} resources, {len(styles)} styles)")
    return True


def main():
    base = '/workspace/src/ForkPlus/Theme/Styles'
    files_to_convert = [
        'Border.xaml',
        'Button.xaml',
        'Calendar.xaml',
        'Checkbox.xaml',
        'Combobox.xaml',
        'Commonresources.xaml',
        'Expander.xaml',
        'Focusvisual.xaml',
        'Listview.xaml',
        'Menu.xaml',
        'Multiselectiontreeview.xaml',
        'Placeholdertextbox.xaml',
        'Progressbar.xaml',
        'Scrollviewer.xaml',
        'Sidebar.xaml',
        'Slider.xaml',
        'Tabcontrol.xaml',
        'Textblock.xaml',
        'Textbox.xaml',
        'Window.xaml',
    ]
    for fname in files_to_convert:
        fpath = os.path.join(base, fname)
        if not os.path.exists(fpath):
            print(f"  MISSING: {fpath}")
            continue
        convert_file(fpath)


if __name__ == '__main__':
    main()
