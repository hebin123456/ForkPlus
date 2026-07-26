#!/usr/bin/env python3
# 阶段 6 诊断：检查所有 avares://ForkPlus/assets/ 引用与磁盘文件名大小写是否匹配。
# Avalonia StandardAssetLoader 大小写敏感，任何不匹配都会 FileNotFoundException。
import os
import re

ROOT = "/workspace/src/ForkPlus"
ASSETS_DIR = os.path.join(ROOT, "assets")

# 收集磁盘上 assets/ 下所有文件（相对路径，保留原始大小写）
disk_files = set()
for dirpath, _, filenames in os.walk(ASSETS_DIR):
    for fn in filenames:
        full = os.path.join(dirpath, fn)
        rel = os.path.relpath(full, ASSETS_DIR).replace("\\", "/")
        disk_files.add(rel)

print(f"磁盘上 assets/ 共 {len(disk_files)} 个文件")
print(f"全部小写: {all(f == f.lower() for f in disk_files)}")
print()

# 搜索代码里所有 avares://ForkPlus/assets/xxx 引用
URI_PATTERN = re.compile(r'avares://ForkPlus/assets/([^"\'\s)]+)', re.IGNORECASE)

mismatches = []
checked = 0
for dirpath, _, filenames in os.walk(ROOT):
    for fn in filenames:
        if not fn.endswith((".xaml", ".cs")):
            continue
        path = os.path.join(dirpath, fn)
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()
        for m in URI_PATTERN.finditer(content):
            referenced = m.group(1)
            checked += 1
            # 检查引用的文件名（忽略大小写）是否存在于磁盘
            lower_ref = referenced.lower()
            lower_disk = {f.lower(): f for f in disk_files}
            if lower_ref not in lower_disk:
                mismatches.append((path, referenced, "NOT_FOUND_ON_DISK"))
            elif lower_disk[lower_ref] != referenced:
                mismatches.append((path, referenced, f"DISK={lower_disk[lower_ref]}"))

print(f"代码中共 {checked} 处 avares://ForkPlus/assets/ 引用")
if not mismatches:
    print("✓ 全部大小写匹配")
else:
    print(f"✗ 发现 {len(mismatches)} 处大小写不匹配:")
    for path, ref, reason in mismatches:
        rel_path = os.path.relpath(path, ROOT)
        print(f"  {rel_path}: {ref}  [{reason}]")
