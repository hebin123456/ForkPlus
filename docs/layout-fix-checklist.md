# Avalonia 版布局修复清单（对照 WPF 6b4cbff~1）

> 本清单基于 4 个并行对比报告 + 真实 `git show 6b4cbff~1` 验证。
> 仅涵盖**布局级**差距（列宽/Margin/Padding/MinHeight/MaxHeight/Style 引用），不含控件本质重构。
> 状态标记：[ ] 待修复 / [x] 已修复 / [~] 验证后无需修复

---

## A. 列宽约束类（已用真实 WPF 源码验证）

### A1. RevisionFileTreeUserControl.axaml ✅
- **问题**：Col 0 `MinWidth=220 MaxWidth=850` 挂在 TreeView 控件上，应挂在 ColumnDefinition
- **WPF 原版**（已验证）：`<ColumnDefinition MinWidth="220" MaxWidth="850" />`
- **修复**：改用显式 `<Grid.ColumnDefinitions>`，Col 0 加 MinWidth/MaxWidth，移除 TreeView 上的约束
- **状态**：[x] 已修复

### A2. RevisionChangesUserControl.axaml ✅
- **问题**：Col 0 列宽约束丢失
- **WPF 原版**（已验证）：`<ColumnDefinition MinWidth="220" MaxWidth="850" />`
- **修复**：Col 0 补 `MinWidth=220 MaxWidth=850`
- **状态**：[x] 已修复

### A3. RevisionSummaryUserControl.axaml ✅
- **问题**：CommitterDetailsContainer 内层列宽 42，应为 48
- **WPF 原版**（已验证）：外层 Col 2 `Width=48`，内层应对齐 48
- **修复**：内层改为 `48,*`
- **状态**：[x] 已修复

---

## B. MinHeight/MaxHeight 类

### B1. DiffEntryRowUserControl.axaml ✅
- **问题**：DiffContentHost MinHeight=120（应 180），缺 MaxHeight=420
- **WPF 原版**（已验证）：`MinHeight="180" MaxHeight="420"`
- **修复**：MinHeight 改 180，补 MaxHeight=420
- **状态**：[x] 已修复

---

## C. 列宽/尺寸数值类

### C1. RevisionListViewUserControl.axaml ✅
- **问题**：avatar 列 Border Width=18，应为 19
- **WPF 原版**：avatar 列宽 19
- **修复**：Border Width 改 19
- **状态**：[x] 已修复

### C2. RevisionsHeaderUserControl.axaml ✅
- **问题**：SubjectTextBlock Margin `0,1,0,0`，WPF 为 `0,0.8,0,0`
- **WPF 原版**（已验证）：`Margin="0,0.8,0,0"`
- **修复**：SubjectTextBlock 和 OtherSubjectTextBlock Margin 改为 `0,0.8,0,0`
- **状态**：[x] 已修复

---

## D. Style/样式类

### D1. RevisionListStatusBarUserControl.axaml ✅
- **问题**：StatusBarButton 缺 TextButton 样式（WPF Style=TextButtonStyle）
- **修复**：在 Controls.axaml 新增全局 `text-btn` 样式（Transparent 背景 + BorderThickness=0 + hover 背景），StatusBarButton 应用 `Classes="text-btn"`
- **状态**：[x] 已修复

### D2. RevisionSearchPanelUserControl.axaml ✅
- **问题**：ProgressBar 缺 Foreground
- **WPF 原版**：Foreground=ForegroundBrush.Gray
- **修复**：补 `Foreground={DynamicResource ThemeForegroundLowBrush}`
- **状态**：[x] 已修复

---

## E. 多余项移除类

### E1. NotificationBarUserControl.axaml ✅
- **问题**：AbortButton 硬编码 Content="Abort"（WPF 由绑定驱动）
- **修复**：移除硬编码 Content（保留空 Button 供 code-behind 设置）
- **状态**：[x] 已修复

---

## F. 待验证项（已用 git show 确认）

### F1. CommitUserControl.axaml ✅
- **问题**：RecentCommitMessagesButton 左 Margin `0,1,3,1`，WPF 为 `1,1,3,1`
- **WPF 原版**（已验证）：`Margin="1,1,3,1"`
- **修复**：Margin 改为 `1,1,3,1`
- **状态**：[x] 已修复

### F2. FileControlHeaderUserControl.axaml ✅
- **问题**：可能多余的底部 Border
- **WPF 原版**（已验证）：`BorderThickness="0,0,0,1"` + `BorderBrush=BorderBrush`（WPF 确实有）
- **结论**：Avalonia 已正确实现，无需修复
- **状态**：[~] 验证后无需修复

### F3. RevisionChangesUserControl.axaml ✅
- **问题**：右侧两按钮顺序（FileListSettingsDropdownButton vs ShowDiffPopupButton）
- **WPF 原版**（已验证）：DropDownButton(List) 先声明→贴最右，ShowDiffPopupButton(Preview) 后声明→靠左
- **修复**：按 WPF 顺序调整（List 在前，Preview 在后）
- **状态**：[x] 已修复

---

## 进度汇总

- 总计：13 项
- 已修复：12
- 验证后无需修复：1
- 待修复：0

---

## 验证结果

- 构建：0 错误 0 警告
- xvfb-run 启动测试：通过（MainWindow/Toolbar/RepositoryUserControl/Sidebar/RevisionChanges/RevisionDetails 全部正常初始化无崩溃）

---

## 剩余差距（非布局级，需后续处理）

本清单仅涵盖布局级差距。以下差距属于控件本质重构或图标资源迁移，需单独处理：

1. **图标 emoji 化**（~15 个文件）：WPF `Image` PNG 资源 → Avalonia `TextBlock` emoji
2. **核心自定义控件未迁移**：MultiselectionTreeView / FileListUserControl / FileDiffControl / EditableTextBlock / ReferencePanel / ContentContainer / ClosableTabControl
3. **Style 资源未迁移**：IconButtonStyle 全局化 / CommitButtonVisibleDropdownStyle / CommitDropDownButtonStyle / CommitPlaceholderTextBox / CircularProgressBar / PullPushBadge
4. **严重差距控件**（6 个）：StageFileUserControl / FileListUserControl / RevisionGraphTooltipUserControl / MergeConflictUserControl / RevisionListViewUserControl / RevisionChangesUserControl
