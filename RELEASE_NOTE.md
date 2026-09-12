# Release Notes

本文件记录 ForkPlus 各版本的变更。从 v1.3.0 开始，每次发布都会在此更新。

## v4.0.12

> 代码编辑器行号边距致命崩溃修复 + 主题切换错误日志修复 + 二进制文件差异"加载更多"修复 + git mm 输出乱码/自动滚动/同步 OOM 修复：查看/切换文件时行号边距渲染在视觉行失效期抛 VisualLinesInvalidException 直接杀死进程（AppDomain 致命），每次启动/切主题必刷 "Cannot initialize TextEditorContextMenu style" 空引用错误日志。

### 修复

- **打开/切换文件时代码编辑器行号边距随机崩溃（AppDomain 致命，VisualLinesInvalidException）**：`CodeEditorLineNumberMargin.Render` 在视觉行失效期（文档变更/Redraw() 后、下一轮 Measure 重建前）直接读 `TextView.VisualLines`，AvaloniaEdit 的 getter 在该状态下抛 `VisualLinesInvalidException`；而 Avalonia 渲染管线存在同步提交路径（窗口消息 WndProc → Compositor.Commit → CompositingRenderer.UpdateCore → margin.Render），异常沿调用链上抛到消息循环即进程终止（IsTerminating=True，crash-20260912-103134/-103202 双转储实证）。修复为与 `TextView.Render` 自身及 `ChunkSelectionLayer` 同款 `VisualLinesValid` 防御：视觉行无效期跳过行号绘制（下一帧排版重建后自然恢复），背景与分隔线照常绘制；Diff / Merge 视图行号边距（`DiffLineNumberMargin` / `MergeLineNumberMargin`）存在同因未防护访问，一并加固。
- **每次启动/切主题必刷 "Cannot initialize TextEditorContextMenu style: Object reference not set to an instance of an object"**：WPF 原版 hack 反射查找 PresentationFramework 内部类型 `TextEditorContextMenu+EditorContextMenu`（TextBox 右键默认菜单）并把隐式 ContextMenu 样式补注册到该类型键下；迁移 Avalonia 后 `typeof(TextElement).Assembly` 是 Avalonia.Controls，不存在该 WPF 内部类型，`GetType()` 返回 null 后直接对 null 调 `GetNestedType` → NullReferenceException 被 catch 打 Error 日志，样式注册从未生效。修复为类型不存在时静默跳过（Avalonia 无此内部菜单机制：本工程 TextBox ControlTheme 自带全套模板未设 ContextFlyout，应用内所有 ContextMenu 均为公开类型、直接命中 `{x:Type ContextMenu}` ControlTheme，无需等价注册）；类型存在（假想 WPF 兼容路径）时幂等注册（原版重复 Add 抛 ArgumentException 也会刷日志）。
- **FileDiff 二进制文件"加载更多"点击无效果 + 加载后 offset 列从 00000000 重新计数 + 文案未国际化**：追加段渲染进度变量在异步批处理完成时才推进，重复点击期间旧任务与新 SetContent 竞争使追加被跳过；追加段 hex 格式化总是从 offset 0 起编号，与已渲染前段形成"每段都从 00000000 重新计数"的断号视觉（且选中反推字节区间错位）；按钮与"部分显示"等文案为硬编码中文。修复为渲染进度紧跟 Append 立即推进 + 复用加载取消令牌机制防竞争，`HexFormatter.Format` 增加 startOffset 接续编号（追加段 offset 从前段末尾连续编号），点击后滚动到新段首行给即时视觉反馈；新增 "Load more (+{0} / {1} remaining)" / "Partially shown" 翻译键补齐 8 种语言。
- **活动管理器 git mm 标签页命令输出显示乱码（不可见字符被当乱码输出，ESC 等）**：输出清洗正则只匹配 CSI 转义序列（`ESC[` 开头），git mm 经管道输出的其他 ANSI 序列全部漏网——OSC 标题/超链接序列（`ESC]0;... BEL`）、字符集指定（`ESC(B`）、两字符转义（`ESC7` / `ESC=`）、行内 CR（git 进度条 "45%\r78%\r100%" 重绘）、BEL 响铃等 C0 控制字符与 DEL 均以乱码/豆腐块渲染。修复为完整四类转义序列匹配 + 残余控制字符清洗（保留制表符），git mm 主视图渲染路径与活动管理器视图共用。
- **git mm sync 过程中 OutOfMemoryException 崩溃（伴随 UI freeze）**：`GitRequest.ExecuteLong` 逐行读管道并把 stdout / stderr 全量累积进无上限 StringBuilder、结尾一次性 `ToString()` 物化；git mm sync 遇凭据失败时 AskPass 按子仓库×认证项循环输出（freeze-20260912 转储实证：`System.OutOfMemoryException at StringBuilder.ToString() at GitRequest.ExecuteLong`，日志数小时 GB 级输出）。修复为有界捕获：每管道 4M 字符上限的滚动窗口（超限丢头部保尾部——错误诊断信息集中在尾部，加截断标记），实时 UI 输出回调不受影响；同时给 git mm 输出渲染的 pending 队列加 8000 行上限（洪峰时一次 Flush 渲染数万行导致 UI freeze 6s 无心跳，内存随输出量线性涨）。
- **git mm 命令输出不自动滚动到最新内容**：活动管理器 git-mm 视图的输出编辑器每次刷新整体替换 Text 会重置视口，命令运行中新输出到来用户却停留在旧位置。修复为终端式 stick-to-bottom：更新前视口已在底部附近（或初次显示/从其他视图切回）→ 更新后自动滚到底跟随最新输出；用户上翻查看历史时不打断，滚回底部后恢复跟随。

### 平台覆盖

| 平台 | RID | 产物 |
|------|-----|------|
| Windows x64 | `win-x64` | `ForkPlus-4.0.12-windows-x64.zip` |
| Linux x64 | `linux-x64` | `ForkPlus-4.0.12-linux-x64.zip` |
| Linux ARM64 | `linux-arm64` | `ForkPlus-4.0.12-linux-arm64.zip` |
| macOS ARM64 | `osx-arm64` | `ForkPlus-4.0.12-macos-arm64.zip` |

## v4.0.11

> 二分查找通知条不可用修复 + 好/坏互斥 + 右键菜单快捷键：仓库菜单"二分查找"后标签下方通知条不出现（WPF 迁移丢失动画），右键菜单快捷键原仅显示不生效，同提交既标好又标坏会毒化 bisect 会话。

### 修复

- **仓库菜单"二分查找"后标签下方通知条不出现**：WPF 原版通知条由 NotificationBarBorder 样式的 DataTrigger + Storyboard 把高度 0↔28 动画；迁移 Avalonia 时该样式被注释成空壳，Border 高度硬编码 0 永远生效，IsControlVisible 属性无人消费——二分查找的 Good/Bad/Skip 按钮只在该通知条上，导致功能整体不可用；同因受害 merge / rebase / cherry-pick / revert / squash / am / unmerged / .gitignore 建议等所有通知条全场景不可见。修复为 IsControlVisible 直接驱动通知条高度、axaml 挂 DoubleTransition(0.7s) 对齐 WPF 原版观感。
- **二分查找同一提交可以同时标记好和坏**：git 对同一提交既标好又标坏是先落库再报错（`<sha> was both good and bad`），双标记一旦落库后所有后续 bisect 命令（good/bad/skip）全部卡死，只有 reset 能救。修复为执行 good/bad 前按 git 实时 refs 预检相反标记（读 refs/bisect/*，不受 UI 异步刷新滞后影响），冲突时直接返回 git 同款错误并弹窗提示"不能同时标记好和坏"，不执行命令、不毒化会话，双向（先好后坏 / 先坏后好）均有拦截。
- **右键菜单快捷键只显示不生效**：Avalonia 的 MenuItem.InputGesture 仅用于显示快捷键提示文本，不像 WPF 会自动响应按键执行命令，因此右键菜单各项的快捷键（如分支右键 Delete 删除分支）全部无效。在 WpfCompat 层补齐 WPF 语义——ContextMenu 自身与宿主窗口均挂 Tunnel KeyDown，菜单打开时按键命中某启用叶子项显示的 InputGesture 即等价点击该项（触发 Click、收起菜单、吞掉按键并避免与窗口级 CommandBinding 双触发），子菜单递归查找。

### 平台覆盖

| 平台 | RID | 产物 |
|------|-----|-----|
| Windows x64 | `win-x64` | `ForkPlus-4.0.11-windows-x64.zip` |
| Linux x64 | `linux-x64` | `ForkPlus-4.0.11-linux-x64.zip` |
| Linux ARM64 | `linux-arm64` | `ForkPlus-4.0.11-linux-arm64.zip` |
| macOS ARM64 | `osx-arm64` | `ForkPlus-4.0.11-macos-arm64.zip` |

## v4.0.10

> SSH 密钥删除修复收官 + 全局滚动滚轮修复 + 统计图表悬浮提示恢复 + 首次启动细条标题栏修复。

### 修复

- **删除 SSH 密钥确认框置底、点"删除"没反应**：确认框默认 owner 是 MainWindow，但 SSH 密钥窗口本身已是模态（MainWindow 已被禁用），确认框挂错模态链导致置底、点击无效、密钥删不掉。改为把确认框 owner 设成当前 SSH 密钥窗口（与账号弹窗 / 变基确认框同款修复），模态链正确嵌套后确认生效，私钥/公钥文件真正删除并从列表消失。
- **删除 SSH 密钥确认框文案未翻译**：补齐全部语言词条——"要删除 SSH 密钥 '{0}' 吗？"、"私钥和公钥文件将从磁盘永久删除……"、"删除 SSH 密钥失败"、"以下 SSH 密钥文件无法删除……"（德/西/法/日/韩/简中/繁中），确认框完整本地化。
- **所有裸 ScrollViewer 点过滚动条后滚轮滚不动 / thumb 不跟随**：v4.0.9 的修复只覆盖显式使用 TouchpadAwareScrollViewer 的地方，普通 ScrollViewer（AI 辅助开发弹窗、提交界面、RevisionSummary 等）仍有同款 bug。新增 ScrollViewerWheelFix 附加属性，在 ScrollViewer 主题里一个 Setter 全局生效：Tunnel（预览）阶段接管滚轮、ScrollBy 设 Offset 后手动同步垂直/水平 ScrollBar 的 Value，任意 ScrollViewer 无需逐处改 XAML 即修复；模板先于属性应用的情况（PART 滚动条挂接竞态）也做了兜底。
- **AI 辅助开发弹窗、RevisionSummary 滚动改用 TouchpadAwareScrollViewer**：消息列表与提交详情这两处原先还是裸 ScrollViewer，替换后获得同款滚动修复（含触控板小步长逐行滚动）。
- **统计页柱状图鼠标悬浮无数据提示**：OxyPlot.Wpf 的 TrackerControl 已在迁移期隔离不可用，原先 tracker 模板被注释成空，悬浮柱状图看不到任何数据。改用 Avalonia 原生 Border+TextBlock 实现 tracker（DataContext 绑定 TrackerHitResult，ToString 输出格式化文本），悬浮即可看到星期/小时维度的数值。
- **首次启动偶发一个细条标题栏的怪弹窗**：WPF 兼容层的代理 owner 窗口（透明 1x1 占位）默认仍带系统 chrome，即便不透明度 0 也会在屏幕上渲染出一条细的空标题栏。改为 SystemDecorations.None 无边框，配合原透明/不进任务栏设置，彻底不可见。

### 平台覆盖

| 平台 | RID | 产物 |
|------|-----|-----|
| Windows x64 | `win-x64` | `ForkPlus-4.0.10-windows-x64.zip` |
| Linux x64 | `linux-x64` | `ForkPlus-4.0.10-linux-x64.zip` |
| Linux ARM64 | `linux-arm64` | `ForkPlus-4.0.10-linux-arm64.zip` |
| macOS ARM64 | `osx-arm64` | `ForkPlus-4.0.10-macos-arm64.zip` |
## v4.0.9

> 拖动残留/滚动/窗口状态专项修复：拖动中断后残留矩形、显示更少后滚动条卡底、滚轮与 thumb 失联、最大化保存与启动竞态、git mm 子仓标签拖动等一批问题。

### 修复

- **拖动后残留矩形挡界面**：拖动被中断（取消/丢焦/释放在非法落点）时 OnDrop/OnDragLeave 可能都不触发，ShowDropAdorner/ShowPreview 加的 DropPlaceAdorner（插入位置矩形）与选中态背景染色残留，视觉上"一块矩形挡住界面"。四类拖拽控件（DragAndDropListViewItem / MultiselectionListViewItem / DragAndDropListBoxItem / MultiselectionTreeView）指针离开控件边界（OnPointerExited）时兜底清一次，覆盖"拖出列表外释放"等全部中断路径。
- **首次启动也偶发矩形挡界面**：虚拟化容器回收/启动期布局竞态可能让 DropPlaceAdorner 残留在图层里、选中态背景染到错位条目上。四类控件加载完成时（OnLoaded）兜底清一次，覆盖启动期与容器回收期的残留。
- **显示更少标签后滚动条卡在底部、滚轮滚不上去（加强力版）**：截断切换（显示全部/更少）后列表项数变化，旧滚动偏移可能超出新 extent，Avalonia 的 ScrollViewer 不自动 clamp，thumb 卡在底部、滚轮向上 offset 不变。原先 ResetScrollOffset 只在调用方延迟一帧设 Offset=0，虚拟化延迟 realize、extent 异步更新等时序会让单次重置"偶尔失效"，且单设 Offset 会被虚拟化面板的 keep-in-view 每帧还原（保持原底部可见项）。改为 EnsureScrollToTop：挂钩 ScrollViewer 的 PropertyChanged，每当 Extent/Offset 变化即把 Offset 钳到 [0, max(0, extent-viewport)]，超出即强制归零，直到稳定为 0 或重试上限（15 次）后自动解钩——直接在 extent 变化的那一刻钳制，不依赖时序。
- **滚轮滚动有用但 thumb 不跟随**：Avalonia 内部 ScrollBar.Value 与 Offset 的双向绑定在拖过 thumb 后偶发不反向同步（Offset 变了 Value 不跟），thumb 卡住不动。ScrollBy 设 Offset 后手动把垂直/水平 ScrollBar 的 Value 设成同一值，强制 thumb 跟随移动。
- **点过滚动条后滚轮滚不动**：点过 ScrollBar thumb 后 ScrollBar 捕获焦点，滚轮事件在 bubble 阶段被 ScrollBar 标记 Handled 并停止冒泡，ScrollViewer 的 OnPointerWheelChanged 永远不触发。改为在 Tunnel（预览）阶段挂处理——ScrollViewer 自身与 PART_VerticalScrollBar / PART_HorizontalScrollBar 两个模板部件（覆盖焦点在 ScrollBar 上、路由路径不经过 ScrollViewer 的情况），先于 ScrollBar 处理并标记 Handled，滚轮总能滚动；垂直滚轮统一自行处理（触控板小步长逐行滚动，鼠标每档 48px）。
- **窗口最大化没保存/下次启动不最大化**：SystemDecorations.None 自绘 chrome 下 Avalonia WindowState 与 Win32 实际状态偶发不一致（Avalonia 已最大化但 Win32 ShowCmd 仍是 Normal），保存端读取状态用 Win32 ShowCmd 会误存成 Normal。改为状态读 Avalonia 的 window.WindowState（用户实际看到的状态），还原矩形仍取 Win32 placement（还原几何正确）。
- **最大化启动时矩形挡界面**：原先在构造期设 WindowState=Maximized——此时窗口未 Show，Win32 未真正最大化，Avalonia 属性与 Win32 实际状态不一致引发布局/渲染竞态（矩形挡界面 + 误存小窗口）。改为 OnOpened 同步几何后，最大化延迟到下一渲染帧（Render 优先级，窗口已 Show + 首帧布局完成、Win32 就绪）再设，保证 Avalonia 与 Win32 同步稳定生效。
- **git mm 子仓标签不能拖动**：上一轮只改了落点读取（GetData("ForkPlusItem")），漏改拖拽发起——DoDragDrop 仍把 WeakReference<TabItem> 直接传入，ToTransfer 的 default 分支把它 ToString 成类型名字符串，落点拿不回原对象，重排不执行。改用 WpfDataObject.SetData 把原始对象引用存进进程内直通表（RuntimePayload），落点用字符串格式名 "ForkPlusItem" 读取，与主窗口 ClosableTabItem 同款做法。
- **活动管理器 git-mm 标签页单仓也显示**：git-mm 标签页仅在 git mm 仓库有意义，单仓/普通仓库的活动管理器不显示该标签（默认隐藏，按当前活动仓库是否 git mm 仓切换可见性）；保存的视图模式是 git-mm 但当前不是 git mm 仓时回退到"全部"（避免无选中 tab），正停在 git-mm 视图而切到非 git mm 仓时自动切回"全部"视图。
- **下拉菜单隐藏 Lean Branching 分组**：分支下拉菜单里的 Lean Branching 分组（Start Branch / Sync / Finish）暂不对外暴露，整块移除。

### 平台覆盖

| 平台 | RID | 产物 |
|------|-----|-----|
| Windows x64 | `win-x64` | `ForkPlus-4.0.9-windows-x64.zip` |
| Linux x64 | `linux-x64` | `ForkPlus-4.0.9-linux-x64.zip` |
| Linux ARM64 | `linux-arm64` | `ForkPlus-4.0.9-linux-arm64.zip` |
| macOS ARM64 | `osx-arm64` | `ForkPlus-4.0.9-macos-arm64.zip` |
## v4.0.7

> 交互专项修复：文本框拖选、侧边栏"显示所有"行为对齐 WPF，外加一批窗口/弹窗/通知问题。

### 修复

- **弹窗 TextBox 长文本无法拖选全部内容（如重命名贮藏的消息框）**：WPF 原版全部 8 个 TextBox 系模板的内容宿主都是 `ScrollViewer PART_ContentHost`（滚动条隐藏但可滚动）；迁移时被误换成裸 TextPresenter——presenter 被裁剪为可视宽度，超出框长的文字永远选不中。全部模板补回 ScrollViewer（`PART_ScrollViewer`，对齐官方 Fluent 主题做法）：鼠标拖到框边缘即可扩选全部文字，光标越界自动滚动。新增 headless 探针测试覆盖"拖出框缘应全选 + 自动滚动"。

## v4.0.8

> 跨平台迁移期 UI/交互修复批次：切主题、弹窗、窗口、凭据、通知、git mm、标签页等一批问题对齐 WPF 原版行为。

### 修复

- **侧边栏"显示所有标签 / 显示所有分支 / 显示所有贮藏"单击无反应、右键弹空菜单、双击误弹检出**：TruncateSidebarItem 的 DataTemplate 用 HyperlinkButton 承载超链接，但未设 x:Name，也未接 RequestNavigate（迁移期丢失 WPF Hyperlink.RequestNavigate 接线）→ 单击无响应；右键释放时 ContextMenu 经 ContextRequested 原生路径重开成空菜单（一个小点）；双击时 TruncateSidebarItem 不可选中，SelectedItem 仍是之前选中的分支/标签/贮藏，会误触发"检出"弹窗。修复：按钮点击处理器按 DataContext 识别 truncate 行，单击/双击均切换该分组的截断状态（同一 TruncateSidebarItem 类型覆盖分支/标签/贮藏三处）；右键复用贮藏分组的输入期菜单抑制（清项 + Close + 临时置空引用，彻底杜绝空菜单闪现）；双击在进入检出逻辑前拦截。对齐 WPF 原版行为。
- **弹窗 TextBox 长文本无法拖选全部内容（如重命名贮藏的消息框）**：WPF 原版全部 8 个 TextBox 系模板（TextBox / PlaceholderTextBox / AutoCompleteTextBox / CommitPlaceholderTextBox / CommitDescriptionTextBox / SearchPanelPlaceholderTextBox / FilterTextBox / 编辑型 ComboBox 内嵌 TextBox）的内容宿主都是 `ScrollViewer PART_ContentHost`（滚动条隐藏但可滚动）；迁移时被统一误换成裸 TextPresenter 直接放进 Border——presenter 被裁剪为可视宽度，而 Avalonia TextBox 拖选时把指针坐标钳制到 presenter 边界（= 可视宽度），超出框长的文字永远选不中、也滚不过去。全部模板补回 ScrollViewer（Avalonia 部件契约名 `PART_ScrollViewer`，对齐官方 Fluent 主题做法）：presenter 以内容全宽测量，鼠标拖到框边缘即可扩选全部文字，光标越界自动滚动（多行提交框的垂直滚动同理受益）；Background 显式透明防止主题 ScrollViewer 背景盖掉文本框底色。新增 headless 探针测试覆盖"拖出框缘应全选 + 自动滚动"。
- **git mm 结束等场景丢失系统原生 Toast 通知**：Toast 服务在 Avalonia 迁移期被降级为空操作（仅记日志）。恢复 Windows 原生 Toast：非 MSIX 桌面应用发 Toast 需先注册 AUMID——幂等写入 HKCU 注册表路径（DisplayName/IconUri）；发送经 PowerShell 子进程走 WinRT ToastNotificationManager（net10.0 无 WinRT 投影，不引入新 NuGet 包），XML 以 base64 传递规避转义问题，fire-and-forget 不阻塞 UI 线程。非 Windows 平台保持降级记日志。
- **切换主题偶发卡死（"Grid already has a visual parent ContentPresenter"）**：切主题 = ControlTheme 换新实例 → 模板重建，旧 PART_SelectedContentHost 的 Host 已被清 null，TabControl 基类的 ClearOwningContentPresenter 失效，旧 presenter 仍把选中内容持为视觉子级，新 presenter 测量时抛"already has a visual parent"——异常虽被全局兜底吞掉，但抛在 Measure 阶段导致布局 pass 反复重试，UI 表现为卡死。新增 GuardedTabControl（裸 TabControl 加兜底释放覆写，StyleKey 沿用原生 TabControl，外观零变化）用于侧边栏；TabControlContentHostGuard 兜底同步加固：新 presenter 注册时无条件强制释放旧 presenter 的 Content/ContentTemplate/DataContext，不再依赖基类赋值时序与内容引用相等。
- **切换某些主题后弹窗外圈边距颜色与内部不一致**：弹窗内容 Grid 有 20/0/20/20 外边距，边距区域露出窗口自身 Background——该值经 ControlTheme 的 DynamicResource Setter 绑定，运行时热替换主题字典后不重新解析（停留在旧主题色，如 Light 灰白），而内容 Grid 走命令式刷新已切到新主题色。初始化与 RefreshBrushes 两条路径同步命令式刷新窗口 Background，外圈与内部配色一致。
- **交互式变基改过内容后点取消，二次确认框没反应**：确认框默认以 MainWindow 为 owner，但变基窗口本身已是模态（MainWindow 已被禁用），嵌套对话框挂错模态链导致输入无法路由。改为以变基窗口为 owner（与 PushWindow / ConfigureSshKeysWindow 等嵌套对话框同款做法）。
- **重启后窗口位置/大小/最大化状态没完全恢复**：Windows 端原先走 Win32 SetWindowPlacement，但 Avalonia 的 WindowState 属性不随 Win32 状态变化更新，且实测两者互相打架，恢复不可靠。改为与 Unix 端完全一致的跨平台原生 API 路径：Width/Height（DIP）+ Position（物理像素 = DIP × RenderScaling）+ WindowState，先设正常态几何再切最大化；Win32 GetWindowPlacement 仍用于保存端读取还原矩形。
- **未暂存区选中文件夹时右上角 Stage 按钮点不动**：按钮启用条件含 `!IsDirectory`，选中目录时直接置灰——但 StageSelectedFiles 走目录展开成文件再暂存，目录本就是可暂存单元；Unstage 侧用 `Length != 0`（含目录）却正常。改为选中目录（无论 ChangeType）或非 Unchanged 文件均可启用，两侧一致。
- **窗口非最大化时边缘无 resize 箭头/感知不到可调整大小**：SystemDecorations=None 后系统原生 resize 边框（含悬停箭头）不再渲染，仅剩按住拖拽。PointerMoved（Tunnel 预览，先于子控件）里按与拖拽同口径的 6px 边缘命中设置对应 SizeXxx 光标（角/边分级），离开边缘还原默认，子控件自身光标不受影响。
- **凭据 helper 对 git 2.39+ 协议 v2 参数误警**：`capability[]=authtype` / `wwwauth[]=Basic realm=...` 等数组型参数此前落 default 分支，每条打 "Unknown credentials description parameter" 警告。解析器按前缀识别这两类参数（本 helper 是 v1-only，仅识别不消费，响应时不回显）。
- **二分查找找到首个 bad commit 时弹错误窗**：git bisect 缩到唯一提交时输出 "is the first bad commit" 并退出码 0（成功），但 BisectGitCommand 把它当 Failure 返回，弹 ErrorWindow（红色错误图标），用户误以为出错。改为识别该完成分支，用 MessageBoxWindow 信息窗展示结果，其余真正失败仍走 ErrorWindow。
- **另存为补丁点保存后取消文件选择窗口，补丁弹窗也被关掉**：OnSubmit 里 Close() 在 if 块外无条件执行——用户在系统文件对话框点取消时补丁弹窗仍被关闭，无法重试。改为仅在确实选了保存位置时才关闭补丁弹窗。
- **另存为补丁弹窗保存按钮灰色不可点击**：ForkPlusDialogWindow.IsOperationInProgress 原为自动属性，SetStatus 把它从 InProgress 切回 None 后不刷新 Submit 按钮——UpdateSubmitButton 只在 AddFooter/Enable/Disable 里调。改为带副作用的属性，赋值时若变化即调 UpdateSubmitButton，所有用 SetStatus 切换 InProgress↔None 的弹窗都受益。
- **git mm 命令输出收编到活动管理器**：活动管理器新增 "git-mm" 标签页（独立内容区，排在 全部/用户/后台 右边），右侧直接展示当前活动 GitMmUserControl 的命令输出（经 GetOutputText() 读取，_refreshTimer 周期刷新）；git mm 窗口不再显示命令输出覆盖层（SetOutputOverlayVisible 一律隐藏，输出仍写 _outputLines 供活动管理器读取），顶部 Output 切换按钮隐藏。
- **git mm 操作时下面的子仓库界面被锁住**：SetBusy 期间把 SubreposTabControl / SubrepoFilterButton 一并禁用，命令运行时整个子仓库界面无法切换/查看。其他 git 操作（fetch/pull 等）不禁用主界面。改为只禁用 Start/Sync/Upload 三个命令按钮（防并发，RunBackground 已会先 Cancel 旧任务），子仓库界面保持可交互。
- **账号弹窗点一下被置底、可无限开新账号弹窗**：AddAccountWindow / AccountsWindow 打开的 loginWindow 默认 owner 是 MainWindow，但当前已是嵌套模态（MainWindow 被 AccountsWindow 禁用），loginWindow 挂错模态链导致置底、下方可交互、能再开新弹窗。改为把 loginWindow 的 owner 设成当前活跃模态窗口（AddAccountWindow / AccountsWindow），正确嵌套（与 IR 确认框、PushWindow 编辑远端同款修复）。
- **标签页不能拖动换位置**：ClosableTabItem 的 TabItem_PreviewMouseMove / TabItem_Drop 要求 e.Source 是 ClosableTabItem，但 PointerMoved/Drop 的 e.Source 通常是标签头里的子控件（CenteredDockPanel/TextBlock 等），条件恒假，拖拽永不发起。改为用 this（事件订阅者本身即标签页）作拖拽源/落点，排除点中按钮的情况。

### 平台覆盖

| 平台 | RID | 产物 |
|------|-----|-----|
| Windows x64 | `win-x64` | `ForkPlus-4.0.8-windows-x64.zip` |
| Linux x64 | `linux-x64` | `ForkPlus-4.0.8-linux-x64.zip` |
| Linux ARM64 | `linux-arm64` | `ForkPlus-4.0.8-linux-arm64.zip` |
| macOS ARM64 | `osx-arm64` | `ForkPlus-4.0.8-macos-arm64.zip` |

## v4.0.6

> 崩溃/卡顿专项版本：让每一次崩溃和卡住都留下现场，并关掉三条已知的进程级死亡路径。

### 新增

- **native 崩溃转储（dump-\*.dmp）**：启用 .NET 运行时自带的 createdump（`DOTNET_DbgEnableMiniDump` + WithHeap 类型），SIGSEGV/SIGABRT 等运行时级硬崩不再"无声消失"——崩溃瞬间自动写转储到日志目录（保留最近 5 份），`dotnet-dump analyze` 可直接看到崩溃线程的完整托管栈。v4.0.5 的 crash-\*.log 只覆盖托管异常，native 层硬崩此前进程内零现场。
- **UI 冻结看门狗（freeze-\*.log + freeze-\*.dmp）**：独立后台线程每秒向 UI 线程投递心跳，连续 6 秒无响应即写冻结报告（版本/系统/冻结时长/运行中的全部后台任务/GC 状态），并调用 createdump 对自身做一份冻结现场转储（冷却 60 秒）。"卡着卡着崩溃"的前半段——卡住当下 UI 线程在做什么——从此有据可查。单调时钟计时，系统休眠唤醒不误报；环境变量 `FORKPLUS_DISABLE_FREEZE_WATCHDOG=1` 可整体停用。
- **诊断包一键导出（帮助菜单 → Export Diagnostics...）**：把日志目录（fork.log 滚动归档、crash-\*.log、freeze-\*.log、\*.dmp）打包成单个 zip 供直接反馈，内附版本/系统 README；超过 100MB 的单个文件（通常是 WithHeap 转储）默认跳过并列明。崩溃现场从"藏在 %LOCALAPPDATA% 深处用户很难找到"变成"一次另存为"。八语言词条齐备。

### 修复

- **跑着带输出的 git 命令（fetch/push/stage）时进程无声消失**：biturbo 读管道线程反向调用托管回调（`SpawnWithCallbackInner.HandleCallback`）处无异常防护——任何解析器抛出的托管异常都会试图展开穿过 native（Rust）栈帧，CLR 检测到后直接 FailFast 终止进程，v4.0.5 的三路异常兜底（Dispatcher/AppDomain/TaskScheduler）与 CrashDumper 全部拦不住。边界处就地拦截：异常吞噬并落 crash-\*.log（kind=SpawnCallback，含命令路径与流类型），最坏丢一行输出，进程存活。
- **文件/文件夹选择对话框卡死界面（慢目录/网络盘死锁）**：WPF 兼容层 OpenFileDialog/SaveFileDialog 的阻塞等待 `GetAwaiter().GetResult()` 在 UI 线程上裸等异步任务——任务完成需要 UI 线程派发（StorageProvider 内部回调）时即死锁；即使不死锁，等待期间主窗口也全程无响应。改为 PushFrame 嵌套消息循环等待（与 ShowDialog/Clipboard 兼容层同款既有模式），等待期间界面持续泵消息，异常语义与原先一致。AI 对话框"复制"按钮的剪贴板同步等待同款修复（async/await 化）。
- **native 互操作边界零防御**：`BiturboExtensions` 的 Marshal.Copy 此前按对端返回的长度裸拷贝（数组/缓冲/字符串/nul 终止符扫描），对端未来任何回归（Rust bug、ABI 错位、内存踩踏）都会把异常长度直接变成 SIGSEGV 硬崩。全面钳制：数组 ≤1000 万元素 / 缓冲 ≤256MB / nul 扫描有界，越限按损坏数据降级为空结果 + 日志；revision 列表的 indexes 前置范围校验，越界降级为带定位信息的命令失败而非 IndexOutOfRangeException。当前 Biturbo v1.1.3 契约已逐项核对无问题，此为纯防御层（防未来回归时"硬崩"变"报错"）。
- **冻结报告的任务可见性**：JobQueue 增加进程内全部队列实例的弱引用注册表（主窗口/仓库页/账号页/统计页等十余处各自建队列），冻结报告能看到"卡住当下"真正在跑的全部后台任务（名称/已耗时/进度），而不只是个别静态可达队列。

### 平台覆盖

| 平台 | RID | 产物 |
|------|-----|------|
| Windows x64 | `win-x64` | `ForkPlus-4.0.6-windows-x64.zip` |
| Linux x64 | `linux-x64` | `ForkPlus-4.0.6-linux-x64.zip` |
| Linux ARM64 | `linux-arm64` | `ForkPlus-4.0.6-linux-arm64.zip` |
| macOS ARM64 | `osx-arm64` | `ForkPlus-4.0.6-macos-arm64.zip` |

## v4.0.5

> v4.0.4 是一个玩笑版本（404 Not Found，没有任何构建产物 😄）——真正的修复都在这里。

### 修复

- **Linux ARM64 版本配置文件不持久化（x86_64 版正常）**：仓库列表（repositories.toml）的读写走 biturbo 原生库（libgit2 封装），而 `libbiturbo.so` 要求 GLIBC ≥ 2.34——较老的 ARM 发行版（Debian 11、Ubuntu 20.04 等）加载即失败，`RepositoryManager.Save()` 的 21 处调用点全部零防护：异常直接炸 UI（"无响应崩溃"表象），且配置一个字节都写不出去，表现为"ARM 版没有持久化配置"。修复：原生保存失败（加载异常或返回错误码）时自动降级为托管兜底——完整仓库状态镜像写入 settings.json 的 RepositoryManager 节，与 `Load()` 既有回退路径（从该节导入）闭环，原生读写不可用的环境配置不再丢失。
- **无 CJK 字体的环境界面全是方框（□□□）**：minimal Linux / 部分 ARM 发行版 / 精简容器没装中文字体，中日韩文本全部渲染为方框。内置 Noto Sans CJK SC 子集字体（Regular + Bold 双字重，覆盖 CJK 统一表意、谚文、假名、CJK 标点、全半角形式等区间），经 Avalonia `FontManagerOptions` 字形级回退生效——系统字体缺字时自动落到内嵌字体，中文界面开箱即用，不依赖目标机安装任何字体。
- **UI 无响应/崩溃后无从排障**：三层兜底——① 新增 CrashDumper 独立崩溃转储（crash-*.log，与应用日志同目录，最多保留 10 份）：UI 线程未处理异常、AppDomain 终结性异常、未观测任务异常全部落盘，且 UI 线程异常置 `Handled=true` 让应用存活（正在进行的合并/暂存不再因一次异常全丢）；② 修复"每次启动删除旧日志"（DeleteOldFileOnStartup）——崩溃后一重启现场就没了，改为按天滚动归档保留 14 份 + 即时刷盘（KeepFileOpen + AutoFlush）；③ 未观测任务异常显式置 Observed，防御任何遗留的终止配置。崩溃后把 logs/ 目录打包反馈即可定位。
- **老版本 git 上变基功能整体不可用**：交互式变基硬编码 `--update-refs`（git 2.38 才引入），变基冲突预检硬走 `git replay`（git 2.44 才引入）——内置 git 实例缺失回退系统 git 时，Ubuntu 22.04（git 2.34）/ 24.04（git 2.43）等主流发行版上交互式变基被 git 整条拒收（"unknown option"）、普通变基弹窗直接报错。修复：新增 git 版本能力探测（按实例缓存），`--update-refs` 仅在 git ≥2.38 时附加（老版本变基弹窗同步隐藏该开关，预览与执行一致），变基预检在 git <2.44 时自动降级为旧式三参数 `git merge-tree` 三方合并预演（与拣选/回退预检同款方案，git 1.4 时代即可用）。
- **构建告警清零**：清理 200 个 AVLN3001（XAML 视图缺公共无参构造——全工程零运行时 XAML 加载路径，项目级抑制并注释论证）及 3 个真实告警（CS0109 多余 `new` 修饰符、CS0219 死变量、AVP1001 兼容封装层局部抑制），主工程 Release 构建达到 **0 警告 0 错误**。

### 平台覆盖

| 平台 | RID | 产物 |
|------|-----|------|
| Windows x64 | `win-x64` | `ForkPlus-4.0.5-windows-x64.zip` |
| Linux x64 | `linux-x64` | `ForkPlus-4.0.5-linux-x64.zip` |
| Linux ARM64 | `linux-arm64` | `ForkPlus-4.0.5-linux-arm64.zip` |
| macOS ARM64 | `osx-arm64` | `ForkPlus-4.0.5-macos-arm64.zip` |

## v4.0.3

### 修复

- **已暂存/未暂存区域键盘上下键选择文件时右侧 FileDiff 不刷新**：键盘导航（上下键、`SelectNextFile`/`SelectPreviousFile`）不更新 `TreeView.LastClickedItem`——它仍指向上次鼠标点击的文件，而该文件已不在当前选中集合中，选中事件携带过时文件，`CommitUserControl.UpdateDiff` 末尾的"文件仍在选中集合"守卫丢弃结果，diff 面板停留旧内容（鼠标点击一切正常）。修复：`NotifySelectionChangedFromCurrentItems` 仅当 `LastClickedItem` 仍在当前选中集合中（多选场景下的主选中项）才使用它，否则回退到选中集合的第一个文件；补 E2E 回归测试（键盘迁移选中 + 鼠标多选两个方向）。
- **界面重构（v4.0.0 WPF → Avalonia）后大量界面国际化丢失**：约 100 个对话框与控件（创建/切换/变基/合并/推送/拉取等全部操作弹窗、仓库设置页、偏好设置的提交/AI/导入导出页、合并冲突视图、变基改写弹窗、服务标签页、修订搜索面板等）在重构中丢失了 `PreferencesLocalization.Apply()` 调用，非英文界面下整页显示英文。统一在构造函数 `InitializeComponent()` 后补回本地化调用（翻译机制幂等，与父级调度共存）；同时补齐 zh-Hans / zh-Hant 各 6 条缺失词条（"记住密码"、"SSH 配置："、"API 密钥"等），并去重语言文件中 2 条完全相同的重复键。
- **检查更新在多平台 Release 下下载链接错位**：v4.0.2 起四平台安装包（windows-x64 / linux-x64 / linux-arm64 / macos-arm64）并行上传，GitHub API 返回的资产顺序不可控，旧实现固定取 `assets[0]`——三平台时代恰好只有三个 zip 时碰巧可用，四平台后可能给 Windows 用户下载 Linux 包。修复：按运行时平台（OS + 架构）匹配资产名 `ForkPlus-{版本}-{平台}.zip`，精确匹配失败时回退平台后缀匹配，再失败回退 Release 页让用户手动选择。

## v4.0.2

### 新增 Linux ARM64（aarch64）平台

- **第四个平台**：构建矩阵从三平台扩展为四平台（windows-x64 / linux-x64 / linux-arm64 / macos-arm64），Linux ARM64 在 GitHub 原生 ARM64 runner（`ubuntu-22.04-arm`，公开仓库免费）上原生构建发布，不做 x86_64→aarch64 交叉。产物为 self-contained publish（自带 .NET 10 运行时），树莓派 4/5、ARM 服务器、Apple Silicon 虚拟机（UTM/Virtualizor）等 ARM64 Linux 目标机解压即用。
- **native 依赖链路全面适配 aarch64**：
  - **biturbo**：消费 [Biturbo v1.1.3](https://github.com/hebin123456/Biturbo/releases/tag/v1.1.3) 新增的 `libbiturbo-arm64.so` release 资产（落地重命名为 `libbiturbo.so`，运行时 DllImport 库名不变）。该资产依赖 Biturbo v1.1.3 的 rust-lld 链接修复（rustc 注入的 anonymous 版本脚本与 `biturbo.exports.map` 在 GNU ld 下冲突，aarch64 默认 GNU ld，v1.1.1 已知限制自此解除）。
  - **tokei**：消费 [tokei v14.0.1](https://github.com/hebin123456/tokei/releases/tag/v14.0.1) 的 `tokei-aarch64-unknown-linux-gnu.tar.gz` 资产。
  - `RestoreBiturbo` / `RestoreTokei` target（Unix 腿）在构建机 `uname -m` 为 `aarch64` 时自动分流对应资产，本地在 ARM64 Linux 上构建同样开箱即用。
- **SkiaSharp / HarfBuzzSharp**：linux-arm64 的 native 运行时包按 RID 自动解析（Avalonia 12 / SkiaSharp 3 均原生支持），无需额外配置。

### 平台覆盖

| 平台 | RID | 产物 |
|------|-----|------|
| Windows x64 | `win-x64` | `ForkPlus-4.0.2-windows-x64.zip` |
| Linux x64 | `linux-x64` | `ForkPlus-4.0.2-linux-x64.zip` |
| Linux ARM64 | `linux-arm64` | `ForkPlus-4.0.2-linux-arm64.zip` |
| macOS ARM64 | `osx-arm64` | `ForkPlus-4.0.2-macos-arm64.zip` |

### 修复

- **彩色主题下弹窗按钮/Tab 指示条等浮出一圈系统默认蓝**：v4.0.1 已修窗口边框刷的同一类问题，但还有一处漏网——`SystemAccentBrush`（默认按钮"继续/确定"的描边、Tab 指示条、进度条、超链接等全部强调色引用点的基础资源）在 Windows 上仍读取系统 DWM 着色色（注册表 `HKCU\...\DWM\ColorizationColor`，常见为 #0078D4 系统蓝），非 Windows 平台则回退主题色——同一主题跨平台表现不一致，Windows 上 Purple / Green 等彩色主题的弹窗里"继续"按钮一圈仍是系统蓝。跨平台统一：`SystemAccentBrush` 一律取当前主题的 AccentColor（与窗口边框刷同一策略），任意主题、任意平台下强调色与主题配色完全一致；新增 SystemAccentBrushThemeConsistencyTests 回归测试（多主题遍历断言资源值 = 主题 AccentColor + PurpleDark 下默认按钮描边 = #A855F7）。
- **git mm 命令输出弹窗内容被裁剪、超链接不可点击**：迁移时输出区降级为 TextBox，而应用自定义 TextBox 主题模板内只有 TextPresenter、没有 ScrollViewer，长输出纵向直接被裁剪显示不完整。重构为显式 ScrollViewer + SelectableTextBlock 富文本（保留 ANSI 颜色分段与文本选择/复制能力，NoWrap + 横向滚动保留终端语义）：纵向/横向均可滚动；输出中的 URL 渲染为可点击超链接（主题强调色 + 下划线 + 手型光标，悬停加亮，点击即打开系统浏览器，点按不误触文本选择）。

## v4.0.0

### 里程碑：跨平台版本重构（WPF → Avalonia 12）

- **支持 Linux 与 macOS**：UI 层从 WPF 全面迁移至 Avalonia 12（.NET 10），一套代码运行于 Windows / Linux / macOS 三平台。底层 Rust 引擎（biturbo native）、git mm 工作流、git-ai 集成（AI 归属、Blame 徽标、统计区块）、AI 辅助开发、8 种语言、主题皮肤、贡献热力图、仓库树图等全部能力三平台可用，GitHub Actions CI 并行产出三平台构建（windows-x64 / linux-x64 / macos-arm64）。
- **自包含发布扩展至三平台**：延续 v3.10.0 的自包含策略，Linux 与 macOS 产物同样自带 .NET 10 运行时，目标机无需安装任何框架。主程序与 AskPass / RI 两个 git 子进程辅助程序（凭证输入、交互式变基）全部自包含发布到同一目录，git 拉起 helper 的完整链路在无运行时环境下可用。
- **配置与状态跨平台持久化**：设置、仓库列表、窗口布局等全部状态在 Linux / macOS 上正确持久化（迁移期修复了配置写入不生效的问题）；HTTP(S) 凭据以独立存储（credentials.json）跨平台持久化，不再依赖 Windows 专用的凭据管理器。

### 新功能

- **凭据管理器三档记忆**：
  - **第一档：自动记住账号（默认行为）**——HTTP(S) Username 询问提交后自动记住，下次弹窗预填；
  - **第二档：记住密码**——密码落盘，下次弹窗仍出现但密码框自动预填且预勾选；
  - **第三档：记住密码 + 不再弹出**——credential get 与 askpass 全链路静默回填，完全不弹窗；凭据失效被 git erase 后快速失败，不会死循环弹窗。
  - **偏好设置新增 Credentials 页**：可提前录入账号密码并设置"不再弹出"，支持单条编辑 / 删除、"不再弹出"开关即时生效（随时恢复弹窗）与全局恢复询问。
- **测试体系重建为跨平台 E2E**：原 Windows-only FlaUI 自动化套件替换为 Avalonia.Headless 无头测试（4400+ 用例，覆盖 25 个功能模块的端到端测试），随 CI 全量运行；滚动同步、滚动条渲染、主题色加载、列表虚拟化等关键 UI 行为均有布局级回归防线。

### 修复

- **git mm 子命令"命令行可用、GUI 报 'mm' is not a git command"**：三层修复——跨平台可执行名（原版硬编码 `git-mm.exe`，Unix 上无法解析，改为按平台解析）；系统位置兜底探测（PATH 中各 git 的 exec-path、`~/.local/bin`、`~/bin` 均纳入解析链，桌面启动的 GUI 不再因进程 PATH 是 shell 子集而找不到命令行里可用的 git-mm）；git 子进程 PATH 注入（git mm 所在目录前置进每次 git 请求的子进程 PATH，封堵"GUI 用哪个 git 实例 + 进程 PATH 初始如何"的所有组合）。
- **git-ai stats 报 'stats' is not a git command（argv[0] 代理模式）**：git-ai 二进制按自身文件名分发命令——非标准名安装（`git-ai-linux-x64`、改名副本）会进入 git 透明代理模式，把 stats 原样转发给 git 导致报错。现在非标准名自动在数据目录建标准名符号链接执行（argv[0] 修正）；Windows 无符号链接权限时优雅降级，并识别代理症状翻译成可定位的提示。
- **FileDiff 左右视图滚动不同步**：三方件 AvaloniaEdit 12 的 `ScrollToVerticalOffset` / `ScrollToHorizontalOffset` 为空操作，Side-by-side 文本 diff、十六进制 diff、Blame 列表↔编辑器同步、切换文件恢复滚动位置等链路全部改走模板 ScrollViewer 的 `Offset`，恢复与 WPF 原版一致的同步行为。
- **"在文件资源管理器中显示"打开文档目录而非目标目录**：git 相对路径的正斜杠未规范化，explorer.exe 无法解析 `/select` 参数而回退打开"文档"库——修复 Windows 分支的路径规范化（Unix 的 xdg-open 分支不受影响）。
- **横向滚动条渲染为 13px 小方块**：WPF 样式迁移时丢失横向分支的宽度重置与 Track 方向绑定——修复后横向滚动条正确铺满视口宽度，thumb 沿水平方向布局。
- **自定义颜色窗口显示全白**：Avalonia 资源索引器不穿透合并字典（与 WPF 语义不同），30 个颜色 key 全部取不到主题色——改用链式资源查找，主题当前色与用户自定义覆盖色均正确加载。
- **偏好设置 Credentials 页未国际化**：页面全部文案（描述、表头、按钮、开关、占位符）均未录入语言包，非英文界面下整页显示英文——补齐简体/繁体中文、日语、韩语、法语、德语、西班牙语 8 语词条，含"凭据"页签名。
- **偏好设置 Credentials 页输入框无占位提示**：自定义 TextBox 主题不渲染 Avalonia 的 Watermark，"提前录入"三个输入框与列表行内编辑框显示为无提示白框——统一改用带 Placeholder 的 PlaceholderTextBox（与登录窗口同模式），行内账号/密码框也补齐占位符，"不再询问"开关增加悬浮提示。
- **添加账号窗口平台卡片选中无反馈**：WPF DataTrigger（监听 ListBoxItem.IsSelected 改卡片边框/背景色）在 Avalonia 迁移时无模板级等价物被丢弃——改为类选择器方案（`ListBoxItem:selected Border.tileBorder`），选中卡片恢复主题色边框与背景。
- **登录窗口"管理个人访问令牌"文字贴到上方输入框**：TextButtonStyle 模板将按钮 Margin 透传给内部 TextBlock，WPF 时代用于抵消按钮内边距的负 Margin（`2,-3` / `0,-6`）在 Avalonia 下使文字双重上移——清除全部负 Margin 并统一字号与垂直对齐（GitHub / GitHub Enterprise / GitLab / Gitea / Bitbucket / Bitbucket Server / OpenAI 七个登录窗口）。
- **彩色浅色主题弹窗内外配色不一致**：Blue / Cyan / Green / Orange / Purple / Red / Yellow 七套浅色主题的弹窗表面色（Window.Dialog / Panel / ListBox / TreeView / MenuItem / StatusBar / Sidebar / Tab / ComboBox 等 19 项）漏配为纯白/中性灰，导致弹窗仅外圈为彩色、内部整片白色——对照 SolarizedLight 的同构配色模式补齐（大面积表面=各主题次背景色，滚动视图/Tab/日历=主背景色，滚动条拇指改主题淡色）。
- **彩色主题弹窗四周浮出一圈亮蓝边框**：窗口边框刷（WindowBorderBrush，两套窗口模板的 1px BorderBrush 均引用）在浅色/深色下都写死为 #3BACED 亮蓝——Solarized（蓝色系）下恰好协调，但 Purple / Green / Orange 等彩色主题下弹窗左下右浮出突兀的亮蓝外圈——改为跟随当前主题的 AccentColor，取不到时回退默认蓝；同时修复主题切换命令里边框刷在新主题字典加载前刷新的时序问题（切到紫色后边框仍是旧主题色）。
- **彩色主题弹窗外圈颜色仍不一致（残余根因：系统强调色分支漏网）**：Windows 注册表 DWM\ColorPrevalence（系统"在标题栏/窗口边框显示强调色"，Win10/11 常见开启）生效时，窗口边框刷取的是【系统】强调色（常见默认蓝）而非当前【主题】的 AccentColor——SolarizedLight 恰为蓝色系看不出异常，Purple / Green 等彩色主题下弹窗四周仍浮一圈系统蓝。跨平台移植后边框一律跟随主题 AccentColor（三平台一致），彻底移除系统强调色分支，任意主题下边框与主题配色统一。
- **合并冲突窗口（Side-by-side）打开即卡死、随后 UI 崩溃**：三个编辑器（本地/远端/合并结果）各自的 MergeConflictView 行数不同 → 滚动范围（Extent）不同 → 纯事件驱动的滚动同步在钳制边界互相拉扯形成回声链（A 滚到 X → 同步 B/C → B 钳到自己的 max ≠ X → 回调再同步 A → …… 100ms 防抖每到期放行一轮，三编辑器全量重排 ~10 次/秒；大文件/高 DPI 下布局永远追不上 → UI 永久卡死，Windows 上即"打开即卡住 → 崩溃"）。三重防线修复：1) 回声断路器——同步写入引发的连锁滚动回调直接忽略；2) 熔断器——2s 内同步超 40 次自动暂停联动 5s（兜底保证 UI 永不因同步卡死）；3) 初始定位与前后冲突块导航改为按冲突节点直接滚动三个编辑器，不再依赖事件链。同类防线同步应用到 Side-by-side 文本 diff 与提交 diff 控件。
- **合并结果栏从不滚动到冲突块、一直停在文件开头**：上条防线 3 把初始定位改为"按冲突节点直接滚动三个编辑器"，但 `ScrollEditorChunkIntoView` 的守卫 `Lines.Length == 0` 把合并结果视图的未解决冲突块整个拦掉——该视图里未解决冲突块没有内容行（ResultLines 为空，块内只有 "--- Merge Conflict ---" 对齐占位行），守卫直接 return，合并结果栏从不定位到冲突块、一直停在文件开头（本地/远端视图的块带各自内容行不受影响；remote-only 冲突在本地视图的块同理被拦）。修正：内容行为空时回退用块首对齐行定位（对齐行行号与块在各视图中的行号同步推进），初始定位与 Next/Prev 冲突块导航时三个编辑器真正滚到同一冲突块；E2E 回归测试补充 Merged 视图跟随导航的断言。
- **QuickFetch 偶发弹 "cannot lock ref" 错误窗**：QuickFetch（Ctrl+Shift+Alt+F）与后台自动 fetch（Fetch remotes automatically，默认开启）并发时，两个 git fetch 进程竞争 refs/remotes/* 的乐观锁——后完成的一方 compare-and-swap 失败，git 报 "cannot lock ref 'refs/remotes/...': is at X but expected Y" 并弹 ErrorWindow。QuickFetch 现在与后台自动 fetch 同款防重入：同名 fetch job 已在跑时直接跳过（在跑的那个做的是同一件事），E2E 测试环境同时全局禁用后台自动 fetch 消除偶发红。

### 性能

- **提交列表（轨道树）性能与 WPF 原版一致**：迁移后全面审计确认数据层与原版逐字节相同、列表虚拟化生效（5000 项首帧约 14ms、滚动后容器回收不累积），并新增三条性能回归防线（虚拟化失效、容器回收失效、全量实化即测试红灯），防止后续改动破坏。


## v3.13.2

### 新功能

- **ForkPlus 内置 AI 接入 git-ai checkpoint**：AI 开发与 AI 代码审查修改的文件现在会自动上报给 git-ai，进入其作者归属体系（refs/notes/ai），提交后可在 Blame 视图与统计页看到 "ForkPlus" 智能体的行级归属：
  - **遵循官方 agent-v1 preset 协议**：AI 编辑文件前上报 human 检查点（把上次 AI 插入之后的人工改动标记为人类，并附带 will_edit_filepaths 让 git-ai 收窄 diff 范围），编辑完成后上报 ai_agent 检查点（携带完整对话 transcript、agent 名 ForkPlus、模型、会话 id 与实际编辑的文件列表）。
  - **AI 开发窗口**：每轮 AI 修改文件前后自动打检查点；transcript 取自完整多轮对话历史（工具调用中间轮次天然被过滤，符合协议建议）；清空对话时会话 id 自动重置。
  - **AI 代码审查**：应用审查建议（Apply suggestion）前后自动打检查点，transcript 如实记录审查建议原文。
  - **独立开关**：偏好设置 → Git 新增 "将 ForkPlus AI 的修改上报给 git-ai（checkpoint）" 复选框（默认开启），与 "启用 AI 归属" 总开关独立控制；需两者同时开启且 git-ai 可用才生效。
  - **完全无感降级**：checkpoint 上报全部尽力而为——git-ai 未安装、版本过旧不识别 agent-v1、或上报失败时仅记日志并静默跳过，绝不影响 AI 功能本身的文件修改；上报带 15 秒超时，编辑后的上报在线程池执行，不阻塞 UI。
  - **基础设施**：ShellRequest 新增标准输入（stdin）支持（agent-v1 协议要求 JSON 走 stdin），读取输出与写入输入并行处理避免管道死锁。
  - 8 种语言补齐开关文案与提示翻译。

## v3.13.1

### 性能优化

- **大幅降低 git-ai 集成的卡顿**：修复启用 AI 归属后 Blame 窗口与统计页长时间等待的问题：
  - **Blame 并行加载**：git blame 与 `git-ai diff` 由串行改为并行执行，blame 结果一到立即渲染列表（不再等归属数据），AI 徽标在归属数据到达后通过属性变更通知异步浮现，无闪烁；git-ai 冷启动（首次调用启动 daemon 可达数秒）不再拖住整个 blame 流程。
  - **结果缓存**：新增 git-ai 查询 LRU 缓存（64 条）——同一提交反复 Blame、再次打开统计页或切回同一统计区间直接命中缓存，零开销秒出；未使用 git-ai 的仓库（空归属）也会缓存，不再重复 spawn 进程；失败不缓存，下次自动重试。
  - **超时保护**：`git-ai diff` 15 秒、`git-ai stats` 60 秒超时后强制结束进程并优雅降级（Blame 正常显示、统计区显示错误可重试），彻底杜绝 git-ai 卡死导致界面永久等待。
  - **缓存正确失效**：统计区间的 revSpec 解析为真实 sha（而非字面 HEAD），新提交产生后缓存 key 自动变化，绝不展示陈旧数据；Refresh 按钮强制跳过缓存重查。

## v3.13.0

### 新功能

- **集成 git-ai：在 GUI 中识别与统计 AI 生成代码**：接入 [git-ai](https://usegitai.com)（基于 Git Notes `refs/notes/ai` 记录行级 AI 作者归属的官方 Git 扩展），无需改变任何工作流即可在 ForkPlus 内查看哪些代码由 AI 写成：
  - **检测与配置**：启动时自动检测 git-ai（PATH / 配置目录），偏好设置 → Git 新增 "git-ai 实例" 选择器（与 git 实例选择器同构，支持自定义路径）与 "启用 AI 归属（git-ai）" 总开关；未安装或关闭时相关 UI 自动隐藏，全部功能优雅降级，不影响原有 blame / 统计。
  - **Blame 窗口 AI 归属徽标**：开启后 Blame 视图按所查提交调用 `git-ai diff` 获取行级归属数据，AI 生成的行所在 blame 块在作者栏显示紫色 "AI" 徽标，鼠标悬停可见生成该代码块的智能体（tool · model）、"n of m lines" 部分归属行数与提交该代码的人类作者。归属数据缺失或命令失败时静默降级为标准 blame 视图，不影响原有功能。
  - **统计窗口 AI Authorship 区块**：统计页新增 AI 作者归属区块，调用 `git-ai stats` 展示区间内 AI 生成行占比饼图（人类 / 混合 / AI 三分），以及按智能体（工具 · 模型）维度的 AI 行数、直接采纳行数与占比列表；支持最近 100 / 500 / 1000 个提交与全历史区间切换。
  - **AI diff 归属数据层**：新增 `git-ai diff --json` 解析（hunks + sessions + annotations 三级结构），兼容单提交与区间两种 JSON 形态，供后续 diff 视图标注复用。
  - **主题与国际化**：22 套主题全部适配 AI 徽标配色（紫色系，明暗皮肤分别调校）；新增 22 条文案，覆盖 zh-Hans / zh-Hant / ja-JP / ko-KR / fr-FR / de-DE / es-ES 全部 8 种语言。

## v3.12.3

### 修复

- **AI 辅助开发窗口：AI 回复气泡未靠左的问题**：AI 对话气泡设置了 `MaxWidth = 700` 但未显式指定水平对齐，WPF 中 `Stretch` 对齐被 `MaxWidth` 截断时元素会居中放置，导致窗口较宽时 AI 回复气泡悬在消息区中间、左右留白不对称。现在流式回复气泡（`CreateStreamingResponseBubble`）与完整回复气泡（`AddAiResponseMessage`）均显式 `HorizontalAlignment = Left`，AI 气泡贴左、用户气泡靠右，形成标准对话布局。已排查确认欢迎横幅、状态文本、diff 结果容器等其他消息元素不受影响。
- **AI 辅助开发窗口：修复靠左后气泡宽度塌陷的问题**：`WebView2` 为 HwndHost 系控件，期望宽度极小，气泡改为靠左对齐后失去 `Stretch` 撑满机制，宽度收缩到仅剩内边距的窄条（约几十像素），内容无法查看。现在气泡宽度按消息面板实际宽度显式指定（上限 `MaxWidth = 700`），窗口缩放时通过 `MessagePanel.SizeChanged` 同步更新所有 AI 气泡；面板尚未布局时（首次显示前）跳过，待首次布局触发 `SizeChanged` 补齐。
- **补齐 v3.12.2 MessageBox 替换引入的 8 个缺失国际化键**：v3.12.2 将原生 MessageBox 替换为 `MessageBoxWindow` 时，部分新引入的文案键未写入语言包，非英文界面回退显示英文。现已补齐 7 个语言包（zh-Hans / zh-Hant / ja-JP / ko-KR / fr-FR / de-DE / es-ES）：`Stash changes`（贮藏更改）、`Unsupported Platform`、`Failed to load file`、`Confirm Import`、`Import Complete`、`Import` 覆盖确认与导入完成两条多行提示、`Force push`（强制推送）。术语与各语言包既有译法对齐（stash → 贮藏/貯藏/スタッシュ/스태시/Remisage/Stash/stash）。已全量校验 9 个改动文件中 81 处 `MessageBoxWindow` 字面量全部命中语言包。

## v3.12.2

### 修复

- **清理原生 MessageBox 残留，统一使用自定义 MessageBoxWindow**：全仓库 29 处 `System.Windows.MessageBox.Show` 残留全部替换为 ForkPlus 自定义弹窗 `MessageBoxWindow`，弹窗样式与主题（明暗皮肤、自定义配色）保持一致，不再出现系统原生灰框：
  - 覆盖 9 个文件：CustomColorsDialog（12 处，配色导入/导出提示与校验错误）、CheckForkSyncCommand（4 处，远端同步预检警告）、CreatePartialStashWindow（3 处）、App.xaml.cs（3 处，32 位系统警告与 git 版本检测）、RepositoryUserControl（2 处，Undo/Redo 前确认）、SaveStashWindow（2 处）、ImportExportUserControl（2 处，配置导入确认与完成提示）、RevisionListViewUserControl（1 处，AI 解释 commit 未配置提示）、AiReviewPreferencesUserControl（1 处，技能文件加载失败）。
  - 图标语义映射：原 `Warning`/`Error` → 警告图标；`Information` → 无图标；纯通知单按钮 OK。
  - 原 `YesNo` 场景（stash 前确认）：提交键 = 先 stash 并继续，取消键 = 中止，语义不变。
  - 原 `YesNoCancel` 场景（已推送提交的 Undo 确认）：拆为两步弹窗——第一步确认是否继续（取消即中止），第二步选择"强制推送远端"或"仅本地撤销"，三条路径全部保留。
  - 文案继续走 `PreferencesLocalization` 国际化查表，语言包中已有键的翻译不丢失，无键文案回退英文（与替换前行为一致）。
  - 清理后全仓库不再残留 `MessageBox.Show` / `MessageBoxButton` / `MessageBoxImage` / `MessageBoxResult` 引用。

## v3.12.1

### 新功能

- **mm 子仓 Push 防呆（与 v3.11.2 Pull 防呆同构）**：在 git mm 子仓中触发推送时，与拉取一样弹出告警引导：
  - 检测逻辑与 Pull 防呆完全一致（`FindGitMmWorkspacePathForSubrepo`），同时覆盖"mm 页签内选中子仓"与"单仓页签打开但路径位于 mm 工作区内"两种场景。
  - 推荐路径：切换到所属 git mm 工作区并自动打上传窗口执行 `git mm upload`，多个子仓变更一起推送，保持子仓间一致性。
  - 逃生口：用户明确选择"仅推送该仓库"时按普通单仓 push 继续；取消则中止。
  - 覆盖全部三个推送入口：Push 窗口（菜单/快捷键 Ctrl+Shift+P）、Quick Push、分支右键 Push。
  - 新增公共 `GitMmUserControl.OpenUploadWindow()`（与 `OpenSyncWindow` 同构），供上传按钮与引导流程复用。
  - 新增文案已国际化（zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES）。

## v3.12.0

### 新功能

- **拉取默认使用 Rebase（全局开关）**：偏好设置 → 通用新增"默认使用变基方式拉取"复选框。开启后拉取代码走 `--rebase` 而非 merge，从源头保持提交历史线性，本地不再产生 "Merge branch 'xxx'" 这类合并提交。该开关与 Pull 窗口 / Quick Pull 的既有 rebase 选项同源（同一设置项），一处开启处处生效。
- **推送前 Squash（合并多条未推送提交）**：Push 窗口新增"Squash 未推送的提交"选项，解决"多次 commit → 拉取 → 解冲突 → 再 commit → 推送"后远端出现多条零碎提交的问题：
  - 勾选后，推送前自动把本地所有未推送提交合并为一条（`reset --soft` 到上游 + 重新提交），内容零丢失。
  - 仅当分支有上游且未推送提交数 ≥ 2 时可用，复选框自动显示待合并条数（如"压缩 3 个未推送的提交"）。
  - 合并后的提交信息默认取最早一条提交的标题 + 全部标题清单，可在窗口内直接编辑后再推送。
  - 命令预览会同步展示实际执行的完整命令链（reset --soft → commit → push），所见即所得。
  - 推送前会重新校验未推送提交数，若期间已被推送（少于 2 条）则自动跳过 Squash 直接推送。
  - 两项功能的新增文案均已国际化（en/zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES）。

## v3.11.2

### 新功能

- **mm 子仓单仓 Pull 防呆（检测 + 引导 + 逃生口）**：git mm 工作区由多个子仓组成，单独对某个子仓执行 `git pull` 会破坏子仓间版本一致性。现在在 mm 子仓内触发 Quick Pull 或 Pull 窗口时：
  - **检测**：自动判定当前仓库是否隶属某个 git mm 工作区（覆盖"mm 页签内选中子仓"与"单仓页签打开但路径位于 mm 工作区内"两种场景，向上递归查找 `.repo/.mm` 工作区根）。
  - **引导**：弹出引导窗口说明风险，默认推荐切换到所属 git mm 工作区并打开同步窗口执行 `git mm sync`，保持所有子仓版本一致。
  - **逃生口**：用户明确选择"仅拉取当前仓库"时，按普通单仓 pull 继续，不阻断有意的单仓操作。
  - 引导窗口文案已国际化（en/zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES 8 种语言）。

### 修复

- **补漏引导窗口描述文案的国际化**：mm 子仓 pull 引导窗口中"仅拉取当前仓库"单选框下方的描述文本（带句号版本 "Skip git mm sync and pull this repository as a standalone repository."）此前只添加了 ToolTip 用的无句号 key，导致该描述在非英文界面一直显示英文。现已在 7 个非英语语言文件中补齐该 key。
- **移除误提交的 libbiturbo.so**：`third_party/libbiturbo.so`（Linux 平台原生库）被误提交进了仓库。biturbo 原生库本就由构建期 `RestoreBiturbo` target 按编译平台从 Biturbo 仓库最新 release 直接拉取，不应纳入版本管理；且仓库中残留的 `.so` 会让 macOS 本地构建误判"已存在"而跳过拉取正确的 `.dylib`。现已删除，并在 `.gitignore` 中补齐三个平台产物（biturbo.dll / libbiturbo.so / libbiturbo.dylib）的忽略规则，防止再次误提交。

## v3.11.1

### 修复

- **过滤嵌套子仓与 Windows NUL 文件**：git mm 子仓目录下若嵌套另一个子仓（untracked 的 git worktree 目录条目），或在 mm 标签页/单仓打开时出现 Windows 保留设备名文件（NUL/CON/PRN 等），这些条目完全无法操作，现已在未暂存区过滤：
  - mm 标签页：过滤 untracked 且本身是 git worktree 且路径在 mm 子仓列表中的条目（嵌套子仓入口）。
  - 单仓打开：同样过滤 untracked 的 git worktree 目录条目，与 mm 标签页行为一致。
  - 两类场景均过滤 Windows 保留设备名文件。
  - 修复判定根因：git status 输出中 untracked（`??`）条目在 `ChangedFile.NewChangeType` 中映射为 `ChangeType.Added` 而非 `Untracked`，导致过滤条件从未命中；改用 `StatusType.Untracked` 判定并清理路径尾部斜杠。

## v3.11.0

### 新功能

- **git mm 界面重构**：git mm 页签界面布局大幅优化，与单仓界面风格统一：
  - git mm 自建状态栏融入主活动状态栏（与单仓 NotificationBar 同款样式：中间显示当前选中子仓名，左侧活动管理器按钮，右侧按当前过滤按钮）。
  - 原底部命令输出区域移除，命令输出改为浮层 Popup，点击活动管理器右侧"输出"按钮切换显隐（仅在用户主动点击时弹出，不再自动弹出）。
  - 新增命令历史按钮，与输出按钮并排；按钮 tips 国际化。

## v3.10.2

### 修复

- **git mm 子仓状态刷新与大列表渲染**：修复 git mm 打开子仓时感知不到文件变化、或只显示变化数量看不到 diff 内容的问题（根因是 git fsmonitor daemon 与 diff/status 命令口径不一致，现统一绕过 fsmonitor 并对齐 core.checkStat 配置）；修复文件数量多时未暂存树文件夹图标不绘制的问题；移除 5000 文件变更数加载阈值，有多少加载多少。
- **交付件精简**：移除发布包中冗余的语言卫星程序集文件夹（各语言文件夹内 dll 内容相同）。

## v3.10.1

### 修复

- **右键菜单文案修正**：仓库树图（Repository Overview）、Blame 视图、文件历史视图右键提交时的菜单项"在 Fork 中显示"已改为"在 ForkPlus 中显示"。该项此前残留了上游 Fork 的品牌名，未随 ForkPlus 改名。同步更新 8 种语言的翻译 key 与值。

## v3.10.0

### 新功能

- **自带 .NET 10 运行时，用户无需另装运行时**：发布包改为 **self-contained 自包含**模式，把 .NET 10 运行时（coreclr、基础类库等）直接打进发行包里。用户下载解压即可运行，不再需要先去微软官网下载安装 ".NET 10 Desktop Runtime"。
  - ForkPlus / ForkPlus.AskPass / ForkPlus.RI 三个 exe 共用同一份运行时文件（同目录加载），不重复占用空间。
  - 取舍：发行包体积由原来约 20MB 增至约 70–85MB（zip），这是打包运行时的必然代价，换来零安装依赖的体验。
  - 不做 trimming（WPF 大量使用反射，裁剪后运行时易崩）、不做 single-file（现有原生库加载逻辑依赖目录结构）。
  - 说明：仅打包了 .NET 运行时；WebView2 运行时仍依赖系统随 Edge 预装（Win10/11 默认都有），不在本次打包范围。

### 修复

- **CI 触发分支修正**：`build.yml` 的 push 触发分支由已删除的 `master-update` 改回 `master`。此前推送到 `master` 不会触发 CI 构建，只有打 tag 时才构建；现在推送 `master` 也会正常跑 CI。

## v3.9.2

### 修复

- **AI 解决冲突按钮无反应**：修复按钮点击后完全没反应的问题。此前 `_aiResolving` 标志若卡在 true（如上次请求异常退出未重置），按钮会永远静默 return。现在：
  - 正在处理中再次点击 → 弹窗提示"AI 正在解决冲突，请等待当前任务完成"，不再没反应
  - 文件/仓库未就绪 → 弹窗提示，不再静默
  - 文件路径解析失败 → 弹窗提示错误，不再静默
  - 异常退出时自动重置 `_aiResolving` 和按钮 loading 态
- **AI 解决冲突文案国际化**：修复解决冲突后显示的文字只有英文、未走国际化的问题。此前所有面向用户的文案（"AI Resolve"、"AI is not configured..."、"No conflict markers found..."、"AI returned empty content..."、"AI output still contains conflict markers..."、"AI resolved all conflicts. Apply..."、"Apply"/"Cancel" 等）都是硬编码英文字符串。现已全部改为 `PreferencesLocalization.Current` 调用，并为 8 种语言（en/zh-Hans/zh-Hant/ja-JP/ko-KR/fr-FR/de-DE/es-ES）补充翻译。

## v3.9.1

### 新功能

- **"Open in" 菜单智能推荐 IDE**：工具栏的 "Open in" 下拉菜单现在会根据当前仓库的项目类型，只显示匹配的 JetBrains IDE，不再无差别列出所有已安装的 IDE：
  - Node 仓库（有 `package.json`）→ 仅显示 WebStorm
  - Maven 仓库（有 `pom.xml`）→ 仅显示 IntelliJ IDEA
  - Android 仓库（`build.gradle` + `AndroidManifest.xml`）→ 仅显示 Android Studio（新增检测）
  - Python 仓库（`requirements.txt`/`pyproject.toml`/`setup.py`）→ 仅显示 PyCharm
  - Go 仓库（`go.mod`）→ 仅显示 GoLand
  - PHP 仓库（`composer.json`）→ 仅显示 PhpStorm
  - .NET 仓库（`*.sln`/`*.csproj`）→ 仅显示 Rider / Visual Studio
  - 识别不到项目类型 → 仅保留通用编辑器（VSCode/Cursor/Sublime 等）和终端/文件管理器
- 不匹配项目类型的 IDE（例如 Node 仓库里已装的 PyCharm）不再出现在菜单，保持菜单简洁。

## v3.9.0

### 优化

- 统一所有 AI 辅助界面（AI 解决冲突、AI 辅助开发、AI 解释代码、AI 代码检视、AI Commit Composer、AI PR 描述生成）的设计与实现，消除此前各窗口"各写一套"的重复代码与不一致体验：
  1. **统一 AI 操作按钮**：新增 `AiActionButton` 自定义控件，封装一致的样式（统一 padding、高度、字号、emoji 前缀）、加载状态管理（`SetBusy` 方法）及根据 AI 配置状态自动控制可见性。将 5 处原生 `Button` 替换为 `AiActionButton`。
  2. **抽取结果窗口基类**：新增 `AiResultWindowBase` 抽象基类，封装 ModelComboBox 初始化/切换、CSS 资源读取、Markdown→HTML 转换等共享逻辑。`AiTextResultWindow`、`AiCodeReviewWindow`、`AiCommitComposerWindow` 均继承此类，消除三份几乎相同的模型下拉初始化代码。CSS 读取和 Markdown 转换委托给 `AiStreamingWebView` 静态方法，与 `AiDevelopmentWindow` 共用同一份实现。
  3. **统一流式 Markdown 渲染控件**：新增 `AiStreamingWebView` 控件，封装 WebView2 初始化、节流渲染（避免每个 chunk 都触发 markdown→html→NavigateToString 卡顿）、滚动位置跟随（用户在底部附近时自动跟随新内容，主动上滚时不打断阅读）、错误处理与加载动画。`AiTextResultWindow` 和 `AiCodeReviewWindow` 用此控件替换各自约 250 行重复的流式渲染代码。`AiDevelopmentWindow` 的聊天气泡复用控件的静态 CSS/markdown 转换方法。`AiStreamingWebView` 还支持 `ResumeStreaming()` 用于部分重试时保留已有内容继续渲染。
  4. **统一窗口基类**：所有 AI 窗口统一继承 `CustomWindow`（`AiSuggestionPreviewWindow` 经 `ForkPlusDialogWindow` 间接继承），与程序其余界面视觉风格一致。
- 统一加载状态指示：所有 AI 界面使用一致的进度条 + 状态文字 + 取消按钮组合，替代此前各窗口手动管理 `IsEnabled`/`ToolTip` 的方式。
- `AiDevelopmentWindow` 中文硬编码字符串收口为英文资源键，并为 zh-Hans、zh-Hant、ja-JP、ko-KR、fr-FR、de-DE、es-ES 共 7 个语言文件补充翻译。
- 修复 `OpenAiLoginWindow` 标题错误显示为 "Login to GitLab" 的问题，改为 "Login to OpenAI"。

## v3.8.3

### Bug 修复

- 全面排查并修复多处可能导致应用闪退的 Bug：
  1. **[高] AI 解决冲突按钮闪退**：`MergeConflictUserControl` 和 `SideBySideMergeWindow` 中的 `AiResolveButton_Click` 是 `async void` 事件处理器，`await Task.Run` 之后直接访问 UI 元素（`AiResolveButton`、`_repositoryUserControl` 等），若用户在 AI 请求期间切换 Tab 或关闭窗口，会抛出未捕获异常导致应用闪退。修复方案：将 `async void` 拆分为 try-catch 包装器 + `async Task` 核心方法，确保所有异常被捕获并记录日志；await 后增加 `IsLoaded` 检查，控件/窗口已卸载时安全退出。
  2. **[高] 未变更文件视图空引用闪退**：`FileDiffControl.LoadUnchangedFileContent` 中 `repositoryUserControl` 来自 DependencyProperty（默认值 null），未做 null 检查直接访问 `.GitModule` 导致 NullReferenceException。`ShowUnchangedFileContentView` 同样存在 `RepositoryUserControl` 属性在异步回调期间被清空的空引用风险。均已补充 null 检查。
  3. **[中] 合并冲突面板多处空引用**：`UpdateResolveButton`（CheckBox 在 `SetConflict` 完成前被操作时 `_changedFile` 为 null）、`StageButton_Click`（链式访问 `_repositoryUserControl.Content.CommitUserControl` 未做 null 检查）、`ShaButton_Click`（`_repositoryUserControl` 和 `_changedFile` 未做 null 检查）均已补充防护。
- 顺带将 `SideBySideMergeWindow` 中 AI 解决冲突流程的 7 处原生 `MessageBox` 也替换为 ForkPlus 自定义弹窗 `MessageBoxWindow`，与 v3.8.2 保持一致。

## v3.8.2

### 优化

- 优化「AI 解决冲突」（AI Resolve）功能的弹窗体验：将 `MergeConflictUserControl` 中 AI 冲突解决流程里使用的 7 处原生 `System.Windows.MessageBox` 全部替换为 ForkPlus 自定义弹窗 `MessageBoxWindow`，使其与程序其余界面的视觉风格统一。涵盖的场景包括：AI 未配置提示、读取冲突文件失败、未检测到冲突标记、AI 请求失败、AI 返回空内容、AI 输出仍含冲突标记、应用解决内容前的确认、以及写回失败提示。确认弹窗由 Yes/No 改为「应用/取消」按钮，单按钮提示采用只读「确定」按钮，错误/警告类提示显示警告图标。

## v3.8.1

### Bug 修复

- 修复 v3.8.0 新增的"显示完整工作目录"功能存在的三个问题：
  1. 菜单项 "Show Full Working Directory" 未做国际化，非英文界面下显示英文原文。现已为 zh-Hans、zh-Hant、ja-JP、ko-KR、fr-FR、de-DE、es-ES 共 7 个语言文件补充翻译。
  2. 该视图为只读，但选中未变更文件时 Stage 按钮仍可点击。现已将 Stage 按钮在选中项全部为未变更文件（`ChangeType.Unchanged`）时置灰，避免对只读文件误触发暂存操作。
  3. 点击未变更文件时右侧视图仅显示 "File has no changes"，未展示文件内容。现已在 `FileDiffControl` 中对未变更文件特殊处理：通过 `git rev-parse HEAD` 取当前 HEAD sha，用 `GetFileContentGitCommand` 从仓库读取完整文件内容（文本走 `TextContentControl`、二进制走 `HexContentControl`/`BinaryFileContentControl`，与文件树浏览视图一致），后台线程加载避免阻塞 UI，加载期间可被切换文件取消。

## v3.8.0

### 新功能

- Commit 视图新增"显示完整工作目录文件树"选项。在文件列表的视图设置下拉菜单（View as Tree/List/CombinedList 同一处）追加一项 "Show Full Working Directory"，勾选后未变更的已跟踪文件也会出现在未暂存列表中，按目录树结构展示完整工作目录，未变更文件不显示状态图标（与变更文件的 M/A/D 等图标区分）。该选项与视图模式正交，默认关闭，关闭时行为与旧版完全一致。开启后未变更文件不参与 Stage/Discard/SaveAsPatch/StageAll 操作（菜单项在仅选中未变更文件时自动禁用），避免误操作。数据源通过 `git ls-files --cached -z` 获取全部已跟踪文件，与变更文件做差集后合并，git 调用在后台线程执行不阻塞 UI。新增 `ChangeType.Unchanged` 枚举值表示未变更文件。设置持久化到 `ShowFullWorkingDirectory` 配置项。Amend 模式下不启用此功能（staged 数据源不同，避免混淆）。

## v3.7.2

### Bug 修复

- 修复"检查远端同步状态"和"跟踪"三级菜单中，分支名含多个 `_` 时第一个 `_` 后内容丢失不显示的问题。根因是 WPF 的 `MenuItem.Header` 当为 string 时，`_` 是助记符（mnemonic）前缀：第一个 `_` 会被吞掉，其后的字符带下划线作为 Alt 快捷键。例如分支名 `dev_test_branch` 会显示成 `devtest_branch`（`t` 带下划线、第一个 `_` 消失）。修复方式：对分组 Header（远端名）和分支项 Header（分支短名）中的 `_` 转义为 `__`（WPF 渲染为单个 `_`），并把原始未转义名存入 `MenuItem.Tag` 供搜索框过滤使用，避免转义后的 `__` 干扰用户输入的 `_` 匹配。

## v3.7.1

### Bug 修复

- 修复二进制文件（约 2MB）在变更视图展开时界面卡死的问题。根因是 Hex Diff 视图的 SetContent 在 UI 线程同步执行了字节拷贝、hex 文本格式化、逐字节差异比较、MD5 计算等重活。现已将数据准备阶段移至后台线程，UI 线程只保留 AvalonEdit 文档赋值与高亮重绘（DispatcherObject 必须在 UI 线程访问）。同时加入 CancellationToken：快速切换文件或控件移除时取消未完成的加载，避免旧内容回填。
- 继续修复 1.7MB 量级二进制文件（如 mp4）展开后界面仍卡死的问题。异步化后真正的卡点是 UI 线程上 AvalonEdit 对超长 hex 文本（1.7MB 字节 → 约 8MB 文本 ×2 editor）同步重建 DocumentLine 行树。新增 256KB 单边渲染截断阈值：超过则只格式化并渲染前 256KB，末尾追加截断提示，逐字节 diff 高亮也限定在截断范围内；MD5 仍对完整字节计算（后台线程），hash 完整性不受影响。同时 UI 回调用 Dispatcher.Yield 分帧执行两侧 editor 的赋值与高亮，避免单次回调长时间占用 UI 线程。
- 再次修复 256KB 截断阈值下 1.7MB mp4 仍卡死的问题。根因有二：其一，单边渲染阈值 256KB 偏高，格式化后约 1.2MB 文本 ×2 editor 串行赋值 + WPF 首帧布局仍可达秒级阻塞；其二，HexDiffUserControl 误实现为 FileContentControl.IFileContentControlSubControl，而它实际宿主在 FileDiffControl（DiffControlContainer）下，DiffControlContainer.ShowSubView 切换子控件时只识别 IFileDiffControlSubControl，导致旧控件的取消回调永不触发、_loadCts 不被取消，旧后台 Task.Run 继续往 UI 线程投递大文本赋值，多次切换后重活排队累积成卡死。现已将单边渲染阈值降至 64KB（格式化后约 315KB 文本，AvalonEdit 处理 <80ms，绝不卡顿），并将接口修正为 DiffControlContainer.IFileDiffControlSubControl，使切换文件时正确取消未完成的异步加载。
- 进一步改为增量加载模式，彻底消除 64KB 仍偶发卡顿的问题。首屏只渲染前 16KB（格式化后约 80KB 文本，AvalonEdit 处理 <30ms，绝不卡），编辑器下方显示"加载更多"按钮，点击后后台格式化下一段 16KB 并通过 TextDocument.Insert 增量追加到文档末尾（避免整串 base.Text= 重建行树），同时刷新差异高亮范围。完整字节保留在内存供后续加载，MD5 仍对完整字节计算。按钮文案显示本次追加量与剩余未加载量。

## v3.7.0

### 新特性

- 偏好设置新增「导入/导出」页：支持将 ForkPlus 配置（settings.json、custom-commands.json、accounts.json）打包导出为 zip 文件，或从 zip 导入以在另一台机器上恢复。导出时可选是否包含账号（含 API token 等敏感凭据）。导入会覆盖当前配置并自动重启应用以生效。zip 内只接受白名单文件，防止路径穿越。

## v3.6.5

### 新特性

- 二进制文件 Hex Diff 视图新增 MD5 行：在"每行字节数"工具栏下方、"修改前/修改后"列头上方插入一行，左右两列分别显示修改前、修改后字节流的 MD5（小写十六进制），用等宽字体显示，便于快速对比两侧内容是否一致。

## v3.6.4

### 新特性

- 菜单栏「窗口 → 切换主题」从原来只能在 Light/Dark 之间 toggle 的单项，改造为与工具栏 Appearance 下拉一致的二级菜单：非纯色主题直接列出、"纯色"三级菜单装 14 套纯色主题、"自定义颜色..."单项打开自定义颜色对话框。菜单栏现在也能选到全部 22 套预设皮肤及自定义颜色。

## v3.6.3

### Bug 修复

- 修复添加账号窗口所有服务图标都显示成 Remote.png 的问题：根因是业务层 `IconKeys` 用带 `Remote.` 前缀的键（`Remote.Azure` / `Remote.Bitbucket` / `Remote.Github` / `Remote.Gitlab` / `Remote.Gitea` / `Remote.Generic`），而 XAML 资源字典里只定义了 `XxxIcon` 键（`AzureIcon` / `BitbucketIcon` 等），两套键对不上，`FindImage` 全部返回 null 退化成 `GenericRemoteIcon`。已在两个 Images 主题字典和 Geometries 字典中为每个 `Remote.Xxx` 键追加别名条目指向同一份资源，图标现在能正确区分。

## v3.6.2

### Bug 修复

- 继续修复 .NET 10 迁移后 `UseShellExecute` 默认值变更导致的功能失效：用默认编辑器打开工作区文件、打开历史版本临时文件、打开应用数据文件夹三处 `Process.Start` 均失效，现已补齐 `UseShellExecute = true`。

## v3.6.1

### Bug 修复

- 修复"在文件资源管理器打开/显示"功能失效：根因是 .NET 10 迁移后 `UseShellExecute` 默认值变为 false，工具栏"Open in File Explorer"用文件夹路径直接 `Process.Start` 时因非可执行文件抛异常失效；同时修正 `explorer /select,` 逗号后多余空格导致新版 Windows 不选中目标文件而是打开"文档"库的问题。

## v3.6.0

### 新特性

- 代码行数统计支持临时排除目录/文件：统计界面新增"排除"输入框，每行一个 glob 模式（语义同 .gitignore，如 `tests/`、`**/*.Tests/`、`bin/`），点"按排除重新统计"即可重跑 tokei 并过滤对应路径。配置不持久化，关闭即清空。

## v3.5.3

### Bug 修复

- 修复检查更新点击"下载"按钮无响应、不跳转浏览器的问题：根因是 .NET 10 迁移后 `UseShellExecute` 默认值变为 false，导致 `Process.Start` 不会唤起默认浏览器。

### 优化

- tokei（代码行数统计）改为从 hebin123456/tokei 仓库最新 release 拉取预编译二进制，不再从源码 cargo 编译，CI 构建更快、无需 Rust 工具链。
- CI 触发条件调整：master 分支 push 不再触发构建，仅 tag（`v*`）和 master-update 分支触发。

## v3.5.2

### Bug 修复

- 修复 AI 辅助开发界面 AI 回答内容过长时溢出消息容器、撑爆整页滚动的问题（单条消息 WebView2 限高 + 内部滚动）。
- 修复未配置 AI 时点开"AI 开发"弹出的原生 MessageBox 未国际化的问题，改用 ForkPlus 自带提示框，并提供"打开偏好设置"按钮直达 AI Enhancement 配置页。
- 修复 AI 总是回复"没有目录读取权限"的问题：AI 辅助开发此前没有任何文件访问能力，导致它误报无权限。现已为其提供仓库内只读文件系统访问能力。

### 新特性

- AI 辅助开发新增仓库文件系统只读访问能力：AI 可通过 `<list_dir>`/`<read_file>` 标签请求列出目录、读取文件内容，由 ForkPlus 本地执行后回填给 AI，使其能自主浏览代码、理解上下文，不再误报权限问题。
- AI 未配置提示框新增"打开偏好设置"入口，一键跳转到 AI Enhancement 标签页。

## v3.5.1

### Bug 修复

- 修复运行时缺少 `ForkPlus.AskPass.dll` / `ForkPlus.RI.dll`，导致凭据/SSH 询问弹窗无法弹出、交互式 rebase 助手无法启动的问题。

## v3.5.0

### 框架迁移：.NET Framework 4.7.2 → .NET 10

- 整个解决方案从 .NET Framework 4.7.2 迁移到 .NET 10 LTS（TFM：`net10.0-windows10.0.19041.0`，Windows-only）
- 主工程 `ForkPlus.csproj` 改为 SDK 风格工程：`Microsoft.NET.Sdk.WindowsDesktop` → `Microsoft.NET.Sdk` + `<UseWPF>true</UseWPF>`
- 子进程工程 `ForkPlus.AskPass` / `ForkPlus.RI` 同步迁移到 `net10.0-windows10.0.19041.0`
- 删除 6 个旧 Reference（PresentationCore / WindowsBase / System.Core / System.Net.Http / System.IO.Compression / System.IO.Compression.FileSystem），由 SDK 隐式提供
- 删除 `App.config`（assemblyBinding / enforceFIPSPolicy / supportedRuntime 在 .NET 10 无效）

### 跨平台原生库加载

- `Bt.cs` 引入 `NativeLibrary.SetDllImportResolver`：51 处 `[DllImport("biturbo.dll")]` 在静态构造里按 OS 重定向到 `biturbo.dll` / `libbiturbo.so` / `libbiturbo.dylib`
- `RestoreBiturbo` MSBuild target 多平台化：Windows 用 PowerShell 拉 `biturbo.dll`，Unix 用 bash + curl 按 `uname -s` 选 `.so` / `.dylib`

### 测试框架升级

- 单元测试工程 `ForkPlus.Tests` / `ForkPlus.AskPass.Tests` / `ForkPlus.RI.Tests`：`Microsoft.NET.Test.Sdk` 升级到 17.13.0，TFM 同步迁移
- 系统测试工程 `ForkPlus.AutomationTests`：FlaUI 从 3.2.0 升级到 5.0.0（API 无破坏性改动，仅移除旧 TFM + 添加 nullable 注解）
- 反射加载改用 `AssemblyLoadContext.Default.LoadFromAssemblyPath` 替代 `Assembly.LoadFrom`（.NET 10 推荐方式）
- 路径查找改用 `AppContext.BaseDirectory` 替代 `AppDomain.CurrentDomain.BaseDirectory`
- .NET 10 下 `.exe` 是 native apphost，托管代码在同名 `.dll` 中：测试工程同时拷贝 `.exe` 和 `.dll`

### 过时 API 升级

- NLog：`LayoutRenderer.Register<T>(string)` → `LogManager.Setup().SetupExtensions(s => s.RegisterLayoutRenderer<T>("..."))`
- `WebClient` → `HttpClient`（NetworkHelper.cs）；AvatarManager.cs 局部 `#pragma` 静默（事件回调模式留待后续重构）
- `PipeStream.Read` 改循环读满（修复 CA2022 inexact read bug）

### CI 工作流

- GitHub Actions 改为多平台 matrix（windows / ubuntu / macos），`fail-fast: false` 并行
- Windows runner 跑完整流程：restore → build → 单元测试 → AskPass/RI 测试 → 上传 artifact → tag 时打 release zip
- Linux/macOS runner 暂只跑 biturbo 原生库拉取冒烟测试（WPF `net10.0-windows` 在非 Windows 上无法构建完整产物）
- `setup-msbuild` / `setup-nuget` 替换为 `setup-dotnet@v4` 安装 .NET 10 SDK
- `msbuild /t:Restore` → `dotnet restore`，`msbuild /p:Configuration=Release` → `dotnet build`
- 修复 .NET 10 + WPF testhost 偶发不退出导致的 CI 误判：解析 `dotnet test` 输出中的 `Passed! - Failed: N` 判定结果

### 构建产物优化

- 7 个 csproj 全部加 `<GenerateDocumentationFile>false</GenerateDocumentationFile>`：不再生成 `*.xml` 文档注释导出文件
- `RemoveDuplicateWebView2Loader` target 重写为 `CopyWebView2LoaderToRoot`：按 `$(PlatformTarget)` 选 `win-x64/win-x86/win-arm64` 对应的 `WebView2Loader.dll` 拷到 bin 根目录，再删 `runtimes\` 子目录
- 警告清理：NoWarn 静默 WPF/INPC 模式常见无害警告（CS0067/CS0108/CS0169/CS0414/CS1522/CS0652/CS8073/CS8632/CA1416），构建日志 `0 Warning(s)`

### 环境要求变更

- Windows 10 或更高版本（不变）
- **Visual Studio 2022 17.13+，或 .NET 10 SDK（含 Windows Desktop runtime）**
- Git 2.31 或更高版本（不变）
- git-mm 3.0 或更高版本（不变）


## v3.4.1

### Bug 修复

- 修复外观下拉"纯色"二级菜单无法展开的问题
- 修复图片 diff 视图模式按钮（Side-by-Side / Swipe / Onion Skin）未国际化的问题
- 修复 Hex Diff 顶部"源/目标"标签未与下方编辑器对齐，并改名为更直观的"修改前/修改后"
- 修复 Reflog History 窗口列头及"View Reflog..."菜单项未国际化的问题
- 修复重启 ForkPlus 后撤销栈为空时无法打开 Reflog History 界面的问题
- 修复 Reflog 跳转对话框（Jump to HEAD to xxxx / This will reset your xxxx）未国际化的问题
- 修复 commit 完成后撤销按钮未激活的问题
- 修复「Compose WIP into commits...」快捷键与「Commit & Push」重叠的问题，改为 Ctrl+Alt+Enter
- 修复撤销/重做过程中状态栏标题（Stage / Unstage / Reset File / Delete 'X' / Add remote 'X' 等）未国际化的问题

### 新特性

- 图片等二进制 diff 新增 Hex 视图切换按钮，可用 side-by-side 十六进制对比原始字节
- 工具栏新增独立的 Reflog 按钮，始终可用（不依赖撤销栈状态）


## v3.4.0

### Layer 2：工作区级快照（追平 Tower）

v3.3.0 只能 undo HEAD 移动类操作（commit/checkout/reset 等）。v3.4.0 把 discard/stage/unstage/delete branch 这 4 类工作区高频操作也纳入 Undo/Redo 栈，追平 Tower 的工作区级 undo 能力。

#### 数据结构扩展：UndoEntry 增加 PreOperationStashSha

- **`UndoEntry` 新增第 5 字段 `PreOperationStashSha`**：操作前用 `git stash create --include-untracked` 抓的工作区快照 sha。
  - 工作区干净时为 null（HEAD 移动类操作通常如此，节省一次 stash apply）
  - 失败时为 null（降级到 v3.3.0 行为，只恢复 HEAD）
  - Undo 时用 `git stash apply --index <sha>` 恢复工作区 + index 状态
- **向后兼容**：构造函数第 5 参数默认 null，v3.3.0 调用方无需修改。

#### 命令扩展

- **`SnapshotGitCommand`**：新增 `ReadStashCreate` 调 `git stash create --include-untracked`（git < 2.35 回退到不带该选项）。从 2 次 git 进程 → 3 次。
- **`RestoreSnapshotGitCommand`**：新增第 3 步，如有 `PreOperationStashSha` 调 `git stash apply --index`。失败不阻断（HEAD 已恢复，工作区冲突让用户手动解决）。

#### 4 类工作区操作纳入 Undo/Redo

| 操作 | 修改前 | 修改后 | Undo 行为 |
|---|---|---|---|
| Discard 文件变更 | `JobQueue.Add`（不进栈） | `AddUndoable` | stash apply 恢复被丢弃的变更 |
| Stage 文件 | `JobQueue.Add`（不进栈） | `AddUndoable` | stash apply --index 恢复 stage 前的 index 状态 |
| Unstage 文件 | `JobQueue.Add`（不进栈） | `AddUndoable` | stash apply --index 恢复 unstage 前的 index 状态 |
| Delete local branch | 直接 Execute（无队列） | `AddUndoable` | stash apply + reset 恢复分支引用和工作区 |
| Delete remote branch | 直接 Execute（无队列） | `AddUndoable` | 恢复本地 tracking ref（远程需 push 重建） |

修改文件：
- `DiscardChangedFilesCommand.cs`：`JobQueue.Add` → `AddUndoable`，返回 `discardResult`
- `ToggleFileStageCommand.cs`：Stage 和 Unstage 两处 `JobQueue.Add` → `AddUndoable`
- `RemoveLocalBranchWindow.xaml.cs`：`JobQueue.Add` → `AddUndoable`，用 `finalResult` 跟踪多分支结果
- `RemoveRemoteBranchWindow.xaml.cs`：同上

### UX 增强：Reflog 视图

v3.3.0 的 Undo 下拉只能看栈内 50 条历史。v3.4.0 新增 Reflog 视图，让用户能看到完整 reflog（默认 200 条），包括超栈深度（LostCount）以外的历史，并能从任意历史状态恢复。

#### 新增 ReflogWindow

- **新建 `ReflogWindow.xaml` + `.xaml.cs`**：非模态工具窗口（可同时操作仓库和看 reflog）。
- **ListView 展示**：Index（HEAD@{N}）/ SHA 前 8 位 / Operation / Commit Subject / Time（本地时区）。
- **`UndoIndexStore` left-outer join**：命中索引显示 UI 友好操作名（如 "Commit 'fix: bug'"），未命中降级显示 reflog 原生 subject（如 "commit: fix: bug"）。
- **双击跳转**：弹窗确认后走 `AddUndoable("Jump to HEAD@{N}", reset --hard <sha>)`，让用户能 Undo 回到跳转前状态。
- **Refresh 按钮**：重新加载 reflog。

#### 工具栏入口

- **Undo 下拉菜单底部**加 "View Reflog..." 入口（始终可见，让用户能看完整 reflog 历史 + 跳转）。
- **Redo 下拉菜单底部**对称加上同样入口。
- `ShowReflogWindow` 方法非模态打开（`window.Show()` 而非 `ShowDialog()`）。

#### ReflogEntry 扩展

- **`ReflogEntry` 新增 `TimestampUtc` 字段**：解析 `git reflog --pretty=%H%x00%gs%x00%s%x00%ci` 的第 4 字段。
  - `%ci` 格式：`yyyy-MM-dd HH:mm:ss ±zz`，解析为 UTC DateTime。
  - 解析失败静默返回 null（不抛出），其他字段仍正常解析。
- **`ReflogHistoryProvider.ReadHeadReflog`**：reflog 格式从 3 字段扩展到 4 字段（增加 `%ci`）。

### Ctrl+Z 快捷键（v3.0.0 已实现，本次验证）

- **Ctrl+Z** → Undo（`UndoCommand.Shortcut`，v3.0.0）
- **Ctrl+Shift+Z** → Redo（`RedoCommand.Shortcut`，v3.0.0）
- **Ctrl+Y** → Redo（`RedoCommand.SecondaryShortcut`，v3.0.0）
- **作用范围**：主窗口内有效（WPF `CommandBindings`，不抢其他应用快捷键）。`MainWindow.InitializeKeyBindings()` v3.0.0 已注册，本次仅验证无需重做。

### 单元测试

- **`UndoRedoStackTests` 新增 5 个 PreOperationStashSha 测试**：默认 null / 显式赋值 / WithOperationName 保留 / null 归一化 / 栈操作中保持完整。
- **`ReflogHistoryProviderTests` 新增 5 个 TimestampUtc 测试**：+0800 时区解析 / +0000 时区解析 / 老格式无时间 / 空时间 / 格式错误静默返回 null。
- **新增 `ReflogViewItemTests`**（14 个测试）：IndexDisplay 格式 / ShaDisplay 截断 / 短 sha / 空 sha / OperationName 传递 / null 归一化 / CommitSubject / TimeDisplay 本地时间转换 / 空 timestamp。

### 设计说明

- **stash create 而非 write-tree**：`git stash create --include-untracked` 是 git 原生命令，能完整捕获 tracked + untracked 文件变更 + index 状态，且不写入 stash list（悬空 commit，对仓库无副作用）。
- **stash apply 失败不阻断**：HEAD 已恢复是核心目标，工作区冲突让用户手动解决（避免强行 reset 丢数据）。
- **Reflog 视图非模态**：用户可以同时操作仓库和看 reflog，符合工具窗口使用习惯。

## v3.3.0

### 重构：Undo/Redo 分层架构

把 Undo/Redo 系统从「单一内存快照栈」改造为「reflog 真相源 + 索引文件元数据」的分层架构，对标 Sublime Merge 的持久化机制。**完全打破旧代码**：删除 `RepositorySnapshot`，用更轻量的 `UndoEntry` 替代。

#### Layer 1：reflog 作为真相源（持久化 + CLI 兼容）

- **新增 `ReflogHistoryProvider`**：读取 `git reflog HEAD --pretty=format:%H%x00%gs%x00%s`，解析为 `List<ReflogEntry>`，NUL 分隔字段避免 commit message 换行干扰。
  - reflog 是 git 原生持久化的（`.git/logs/HEAD`，默认保留 90 天），跨会话保留 + CLI 操作天然兼容 + 无栈深度限制。
  - 默认读取最近 200 条（防止超大 reflog 拖慢 UI）。
  - 读取失败永不抛出，返回空列表（不阻断 Undo/Redo）。

#### Layer 0：索引文件保留 OperationName

- **新增 `UndoIndexStore`**：读写 `.git/forkplus-undo-index.json`，存储 `{HeadSha → UndoIndexEntry}` 映射，为 reflog 条目附加 UI 友好的操作名（如「Commit 'fix: bug'」「Checkout 'feature/x'」）。
  - **位置**：`.git/forkplus-undo-index.json`（与 reflog 同生命周期，clone 后是空的）。
  - **原子写入**：先写 `.tmp` 再 rename，避免崩溃导致文件损坏。
  - **文件损坏静默恢复**：JSON 解析失败时删除文件重建，不阻断 Undo/Redo。
  - **容量上限**：默认 500 条，LRU 淘汰（按 TimestampUtc 排序删最早的）。
  - 索引与 reflog 不同步时降级显示 reflog 原生 message（如 `commit: fix: bug`），不报错。
- **新增 `UndoIndexEntry`**：4 字段（HeadSha / OperationName / TimestampUtc / OperationType）。`OperationType` 预留给 v3.4+ 的 UI 图标。

#### 数据结构精简：UndoEntry 替代 RepositorySnapshot

- **新增 `UndoEntry`**：4 字段（HeadSha / CurrentBranchName / OperationName / TimestampUtc），替代旧 `RepositorySnapshot` 的 11 字段。
  - HEAD sha 是恢复真相源，所有 ref 状态都跟着 sha 走。
  - OperationName 通过 UndoIndexStore 持久化到 `.git/forkplus-undo-index.json`。
  - 当前分支名用于 Undo 后切回原分支（避免进入 detached HEAD）。
  - 含 `WithOperationName()` 副本方法，支持「先抓快照、后赋名」场景。
- **删除 `RepositorySnapshot`**（含 `RepositorySnapshotTests`）：不再保存 branch list / tag list / stash list / ORIG_HEAD / IsWorkingTreeDirty / ChangedFilesCount 等 11 字段。
  - 旧版重建分支 / tag / stash 的逻辑反而可能产生副作用（如重建已被用户故意删除的分支）。
  - 这些状态在 Undo 时由 reflog 兜底恢复，无需在快照里冗余保存。

#### 命令简化

- **`SnapshotGitCommand`**：从 7 次 git 进程调用简化为 2 次（`git rev-parse HEAD` + `git symbolic-ref --short -q HEAD`），性能提升 ~70%（大仓库尤其明显）。
- **`RestoreSnapshotGitCommand`**：从 5 步组合命令（checkout + reset --hard + 重建分支 + 重建 tag + 重建 stash）简化为 2 步（checkout 切回原分支 + `git reset --hard <sha>`）。

#### UI 层适配

- **`RepositoryUserControl.AddUndoable`**：操作成功后写入 `UndoIndexStore.Record(...)`，把 OperationName 持久化到 `.git/forkplus-undo-index.json`。
- **`RepositoryUserControl.IsWorkingTreeDirty()`**：实时调 `git status --porcelain` 检测工作区是否 dirty，替代旧 `RepositorySnapshot.IsWorkingTreeDirty` 字段。
- **`ToolbarUserControl`**：4 处 `RepositorySnapshot` 引用改为 `UndoEntry`，下拉历史列表 / JumpUndoTo / JumpRedoTo 签名同步更新。

#### 单元测试

- **重写 `UndoRedoStackTests`**：20 个测试覆盖栈空 / MaxDepth / LostCount / JumpTo / CancelLastRecord 等纯逻辑，新增 3 个 UndoEntry 数据结构测试（null OperationName 归一化、WithOperationName 副本）。
- **新增 `ReflogHistoryProviderTests`**：11 个测试覆盖 ParseLine 各种输入（合法行 / 缺字段 / 空行 / 短 sha / 多 NUL 字段 / amend / checkout 类 subject）。
- **新增 `UndoIndexStoreTests`**：19 个测试覆盖 GetIndexPath / Load / Record / Lookup / 容量淘汰 / 文件损坏恢复 / 空文件 / 跨实例持久化 / 特殊字符 / 原子写入不留 .tmp。用临时目录 + 真实 GitModule 实例，不依赖真实 git 进程。

### 设计决策（v3.4+ 待办）

- **Layer 2（工作区级快照）**：追平 Tower 的 discard / stage / 删 branch undo 能力，待 v3.4 实现。
- **UX 增强**：Reflog 视图（与 reflog 兜底联动）、全局 Ctrl+Z 快捷键、Reflog 视图入口，待 v3.4 实现。

## v3.2.0

### 新特性

- **AI Commit Composer（WIP 拆分）**：在 Commit 下拉菜单中新增「Compose WIP into commits...」入口，一键把当前所有 staged 文件按逻辑分组拆成多个独立 commit。
  - **AI 流式生成方案**：调 OpenAI Chat Completions API（流式 SSE），让 AI 根据 staged diff 把文件归类成多个 commit 分组，每个分组给出 subject / body / files / reason，diff 体量超 30000 字符时自动截断防爆 token。
  - **三栏预览窗口**（`AiCommitComposerWindow`）：左栏列出所有 commit 分组、中栏列出选中分组包含的文件、右栏可编辑 subject / body；AI 给出但未匹配到 staged 文件的路径会以橙色「(not staged)」标识；底部提示「N 个 staged 文件未分配到任何分组」，方便用户核对。
  - **可编辑 + 可撤销 + 可取消**：用户可在右栏修改任意分组的 subject / body；分组 subject 为空时会弹窗拦截（git 不允许空 message）；执行期间进度条 + 状态文本实时反馈（如「Composing commit 2/5: refactor auth module」），可随时点 Stop 中止。
  - **执行流程**：点 Apply All 后用 `ComposeWipCommitsGitCommand` 按「先 `git reset HEAD --` 清空 staging，再逐组 stage + commit」顺序执行；空仓库（无 HEAD）时容错忽略 `ambiguous argument 'HEAD'` 错误；任一分组失败立即中止，已提交分组不回滚（与手动 commit 行为一致）。
  - **集成 Undo/Redo 栈**：用 `RepositoryUserControl.AddUndoable` 包裹整批 commit，与 v3.0.0 引入的 Undo/Redo 栈联动，用户可一键撤销整批拆分。
  - **模型下拉**：标题栏内置 AI 模型下拉，复用 AI Review 设置，与 AI Development / AI Code Review / AI Text Result 窗口行为一致。
  - **路径匹配鲁棒性**：AI 给出的路径与 staged 文件路径可能存在大小写 / 分隔符差异，`WipCommitPlan.RebuildMatchedFiles` 用 `NormalizePath`（替换 `\\` 为 `/`、`TrimEnd('/')`、`ToLowerInvariant`）做归一化匹配；重命名 / 复制文件的 `OldPath` 也加入索引，让 AI 给出的旧路径也能命中。
  - **JSON 解析鲁棒性**：`ExtractJsonArray` 用状态机遍历字符串字面量，正确处理嵌套方括号和 markdown 围栏；支持 `{ "groups": [...] }` 和直接 `[...]` 两种格式。
- **国际化**：8 种语言（简中 / 繁中 / 日 / 韩 / 法 / 德 / 西 / 英）补齐 AI Commit Composer 相关 26 条文案。

## v3.1.1

### 新特性

- **外观菜单 - 纯色二级菜单**：将原来平铺在外观下拉里的紫色、绿色主题收拢到「纯色」二级菜单中，并新增 5 种纯色配色（红、橙、黄、青、蓝），每种都有浅色 / 深色两个变体，按彩虹色排序（红→橙→黄→绿→青→蓝→紫）。父菜单「纯色」与子菜单中当前选中的颜色都会打勾。
- **Hex Diff - 左右行对齐**：二进制对比界面工具栏新增「左右行对齐」复选框，默认勾上。勾上时左右两个 HexEditor 同步滚动 —— 一侧拉到第 N 行，另一侧立即跟随到第 N 行。采用 100ms 防抖 + 重入守卫，避免两侧相互触发滚动事件形成回环。

### 修复与改进

- **Undo/Redo 默认开启**：`UndoRedoEnabled` 默认值从 `false` 改为 `true`，新用户开箱即用，无需手动到偏好设置中勾选。
- **Undo/Redo 开关文案国际化**：偏好设置中 `Enable Undo/Redo (experimental, may impact performance on large repos)` 此前为硬编码英文，现已本地化到 8 种语言。
- **修复提交后状态栏一直转圈 / 取消不掉**：用户启用 Undo/Redo 后提交一个文件，撤销栈里已出现该提交，但状态栏仍显示「Commit 1 File」并一直转圈、无法取消。根因是 Job 状态机在取消信号与完成信号之间存在多处覆盖漏洞，本次系统性修复：
  - `JobMonitor.Update` / `Success` / `Fail` 在 `_state == Canceled` 时直接返回，不允许把 Canceled 改回 InProgress / Succeeded / Failed（否则取消信号被吞掉，状态栏继续转圈、Job 实际完成、栈里仍入 entry）。
  - `JobQueue.Schedule` 用 `try/finally` 包裹 `job.Run()`，确保 action 抛异常时 Job 也能从 `_runningJobs` 移除、`Status` 置为 `Finished`，否则 `IsIdle` 永远为 `false`、状态栏永远转圈。
  - `RepositoryUserControl.AddUndoable` 在 action 返回后检查 `monitor.IsCanceled`，已取消时调用 `CancelLastRecord` 弹出栈顶 entry，避免栈里留下「已取消但未弹出」的孤儿。
  - `CommitCommand` 在 commit 成功回调里加 `!monitor.IsCanceled` 守卫，已取消时不再调 `monitor.Success(null)`。

## v3.1.0

### 新特性

- **Binary / Hex Viewer**：为二进制文件新增 Hex 视图，复用 AvalonEdit 的虚拟化、选中、搜索能力，替代原先仅显示文件大小的 "Binary file" 占位符。
  - **单文件 Hex 视图**（`HexContentControl` + `HexEditor`）：点击工作区或提交里的任意二进制文件（图片除外，<=10MB 自动加载，>10MB 仍走原 Binary 视图），即以 Offset / Hex / ASCII 三列展示。工具栏支持：
    - 字节宽度切换（8 / 16 / 32 字节每行）
    - 显示/隐藏 ASCII 列、显示/隐藏 Offset 列
    - 搜索（支持 ASCII 文本或十六进制字节，如 `41 42`）
    - 复制选中原始字节到剪贴板
  - **Hex Diff 视图**（`HexDiffUserControl`）：二进制文件的 Diff 不再只显示两侧大小，而是 side-by-side 展示两份字节流，逐字节比较，差异字节以金色（Gold）背景高亮。<=10MB 的二进制 diff 自动加载 Hex 视图，超过则回退到原 `BinaryDiffUserControl`。
  - **三列着色**（`HexColorizer`）：Offset 列灰色、Hex 列蓝色（高位）/暗红色（低位）、ASCII 列绿色；不可打印字符显示为 `.`；差异字节在 Hex 列与 ASCII 列同时加背景。
  - **设置持久化**：在偏好设置中新增 `HexViewBytesPerRow` / `HexViewShowAscii` / `HexViewShowOffset` 三项，记忆用户上次选择的字节宽度和列显示偏好，单文件视图与 Diff 视图共享设置。
  - **头部工具栏**：新增 `FileControlHeaderMode.Hex` 枚举值，Hex 视图下隐藏 Text/Image 工具栏按钮（Hex 视图自带工具栏），仅显示文件路径。
- **国际化**：8 种语言（简中 / 繁中 / 日 / 韩 / 法 / 德 / 西 / 英）补齐 Hex 视图相关文案（Bytes per row / Show ASCII / Show offset / Source / Destination / Search / Copy as raw bytes）。

## v3.0.4

### 修复与改进

- **Undo/Redo 总开关（默认关闭）**：在偏好设置 → 通用 tab 新增 "Enable Undo/Redo" 复选框，默认不勾选。关闭时 `AddUndoable` 直接走原始 `JobQueue.Add`，跳过所有快照抓取逻辑，性能回到 v3.0.0 之前的水平。需要 Undo/Redo 功能的用户可手动开启。
- **Undo/Redo 性能优化**：修复"提交一条信息要转很久、取消也停不下来"的卡顿问题。根因是 `AddUndoable` 在 UI 线程同步抓取 7 次 git 进程快照（包括 `git status --porcelain`，大仓库很慢），且不响应取消。优化后：
  - 开关开启时，`TakeSnapshot` 推迟到 Job 内（后台线程）执行，UI 线程立即返回，不再阻塞
  - 在抓快照阶段检查 `monitor.IsCanceled`，用户取消时立即跳出，不再卡死
- 工具栏 Undo/Redo 按钮根据开关显示/隐藏（关闭时 Collapsed），设置变更后立即刷新

## v3.0.3

### 修复与改进

- **Undo/Redo 图标改为 PNG 资源**：原先用 `Viewbox+Path` 矢量绘制（v3.0.2 改用 Material Design path 但仍是矢量），与工具栏其他按钮（Fetch/Pull/Push/Stash 均为 40×40 PNG 资源）风格不一致。本次新增 4 个 PNG 资源 `Undo.png` / `UndoDark.png` / `Redo.png` / `RedoDark.png`（40×40 RGBA，light=#797979、dark=#CFCFCF，与现有图标颜色规范一致），并在 `Images.Light.xaml` / `Images.Dark.xaml` 注册 `UndoIcon` / `RedoIcon` 资源，工具栏按钮改用 `Image` + `DynamicResource` 引用，行为与 Fetch/Pull/Push/Stash 完全一致（主题切换时自动跟随 light/dark 版本）。

## v3.0.2

### 修复与改进

- **Undo/Redo 图标重绘**：原先的矢量图标（简单弧形 + 三角形箭头）过于粗糙。改用 Material Design 标准的 undo/redo 图标（24×24 viewBox，filled 风格，弯曲箭头更精细），在 20×20 工具栏尺寸下更清晰、与业界习惯一致。
- **Undo/Redo 性能优化**：合并 `SnapshotGitCommand` 和 `RestoreSnapshotGitCommand` 里的冗余 git 进程调用：
  - `git status --porcelain` 从 2 次合并为 1 次（同时拿 `IsWorkingTreeDirty` 和 `ChangedFilesCount`）
  - `git for-each-ref` 从 2 次合并为 1 次（`refs/heads/` + `refs/tags/` 一次拿全，按 `%(refname)` 前缀分发）
  - 每次 Undo/Redo 减少约 3 次 git 进程启动（小仓库约省 150-450ms），缓解"Redo 后状态栏转圈"的卡顿感。

## v3.0.1

### 修复与改进

- **Undo/Redo 工具栏按钮对齐**：按钮样式从 `ToolbarButton` 改为 `StashToolbarButtonStyle`，与 Stash 按钮组视觉一致（左圆角 + 右侧 dropdown 形成"按钮组"整体感）；图标用 `Viewbox` 包裹并限定为 20×20 + `Stretch=Uniform`，与其他按钮（Fetch/Pull/Push/Stash 都是 20×20 Image）大小对齐。
- **右键"AI 解释提交..."位置调整**：移到"还原提交"下面、"另存为补丁..."上面，符合"AI 操作紧跟相关 Git 操作"的菜单分组约定。
- **AI 文本结果窗口（AI 解释 commit / AI 生成 PR 描述）加模型下拉**：在 Copy 按钮左侧新增模型下拉，列表从 `/v1/models` 拉取，切换后立即保存到设置并生效，下次请求使用新模型；与 AI Development / AI Code Review 窗口行为一致。
- **国际化补齐**：`Copy result to clipboard`、`Stop the current AI task`、`Select AI model` 在 AiTextResultWindow 此前为硬编码英文，现已本地化到 8 种语言。

## v3.0.0

### 新特性

- **Undo / Redo 任意 Git 操作**：参考 GitKraken / Tower，引入仓库级 Undo/Redo 能力，覆盖 commit / checkout / reset / merge / rebase / cherry-pick / revert / create branch / create tag / stash 等所有写操作。每次写操作执行前抓取 HEAD/分支/tag/stash 状态快照入栈，失败不入栈，Undo 时按快照恢复。
- **工具栏 Undo/Redo 按钮组**：在工具栏 Stash 按钮组后新增 Undo / Redo 按钮，旁边的下拉箭头展开历史列表，可直接跳转到任意一步。
- **Undo/Redo 快捷键**：`Ctrl+Z` 撤销，`Ctrl+Shift+Z` 或 `Ctrl+Y` 重做。
- **dirty 工作区弹窗**：Undo/Redo 前若工作区有未提交变更会弹窗询问，可选择先 stash 再恢复，避免误丢工作区修改。
- **已 push commit Undo 弹窗**：Undo 一个已推送到远端的 commit 时弹窗询问处理方式（仅本地 Undo / 本地 Undo + 强制推送 / 取消），防止误改远端历史。
- **超栈深度提示**：Undo 栈上限 50 步，超出丢弃最底部并在下拉历史底部提示「X 个早期操作未在历史中（可通过 reflog 恢复）」。
- **跨会话不持久化**：关闭重开仓库清空 Undo/Redo 栈，避免基于过期快照恢复。

### 国际化

- 8 种语言（简中 / 繁中 / 日 / 韩 / 法 / 德 / 西 / 英）补齐 Undo / Redo / Undo History / Redo History / (unknown) / dirty 弹窗 / 已 push 弹窗 / 超栈深度提示 等文案。

## v2.2.3

### 修复

- **AI 输出内容宽度自适应**：修复 AI Explain / AI 生成 PR 描述等窗口的 markdown 渲染 CSS 中 `max-width: 780px` 硬编码导致窗口拉宽后内容右侧留大片空白的问题，改为 `max-width: 100%` 跟随容器宽度。

## v2.2.2

### 新特性

- **AI 解释 commit 右键菜单**：在所有提交列表（commit 列表 / stash 列表）的右键菜单中，"与本地变更比较"下方新增「AI Explain Commit...」选项，无需进入 commit 详情页即可让 AI 解读任意 commit。AI 未配置时菜单项置灰。
- **部分文件贮藏 AI 命名**：选择若干文件贮藏（Partial Stash）对话框新增「🤖 AI」按钮，根据所选文件相对 HEAD 的 diff 自动生成 stash message，与全量贮藏对话框能力对齐。

### 优化

- **AI Explain 按钮国际化**：commit 详情页的「🤖 AI Explain」按钮文本原为硬编码英文，现按 UI 语言本地化显示（8 种语言）。

## v2.2.1

### 新特性

- **Cherry-pick / Revert 冲突预检**：Cherry-pick 和 Revert 对话框打开时自动用 `git merge-tree` 做无副作用的 3-way merge 预演，在对话框底部状态栏显示「可以无冲突完成」或「将产生冲突」，让用户在执行前心里有数。Cherry-pick 多 commit 场景对每个 commit 逐个预检，任一会冲突即整体提示冲突。

## v2.2.0

### 新特性

- **AI 解释 commit**：commit 详情页新增「🤖 AI Explain」按钮，AI 流式输出该 commit 的概述、变更内容、动机和影响，方便快速理解陌生提交。
- **AI 自动命名 stash**：保存贮藏对话框新增「🤖 AI」按钮，根据工作区 diff 自动生成简洁的 stash message，流式写入输入框。
- **AI 生成 PR 描述**：分支/commit range 右键 AI 菜单新增「Generate PR Description...」，基于 commit 列表和聚合 diff 流式生成结构化 PR 描述（概述/变更内容/测试建议）。

### 优化

- **AI 协助冲突解决扩展到冲突列表**：合并冲突列表页每个文件新增「🤖 AI Resolve」按钮，无需打开三方合并窗口即可一键让 AI 解决该文件所有冲突；SideBySideMergeWindow 的 AI 解决逻辑提取到 OpenAiService 公共方法复用。

## v2.1.5

### 新特性

- **仓库树图问号提示**：仓库树图弹窗标题左侧新增问号图标，鼠标悬停显示说明，解释视图用途、面积含义、操作方式等，支持 8 种语言。

### 优化

- **AI 代码检视流式输出滚动跟随**：流式输出时滚到底部查看新内容，下一个内容块到达后自动跟随最新内容，不再弹回顶部；用户主动上滚浏览历史时保持阅读位置不打断。

### 其他

- 删除 docs 目录下的用户手册。

## v2.1.4

### 修复

- **空仓库无限加载**：`git init` 完毕的新仓库用 ForkPlus 打开不再无限转圈卡死。
- **空仓库状态显示"分离 HEAD"**：空仓库状态栏正确显示当前 branch 名（如 master），不再误显示为"分离 HEAD"。
- **空仓库新建文件夹感知不到**：空仓库在工作区新建文件/文件夹后能正常检测显示，与 `git status` 行为一致。

## v2.1.3

### 新特性

- **自定义颜色导入/导出**：自定义颜色对话框新增"导入颜色"和"导出颜色"按钮，支持 JSON 格式的颜色配置文件，方便分享和备份配色方案。导入时严格校验文件格式（schema、颜色 key 白名单、hex 颜色合法性），格式不对阻止导入并提示具体错误。

## v2.1.2

### 修复

- **随机配色时 Diff 颜色不动**：点击 Random Palette 后 Diff 相关颜色项正常随机变化。
- **换色不立即落盘**：换色后立即保存到 settings.json，关闭/重启不丢失。
- **换色后主界面不刷新**：换色后主界面立即刷新生效，无需重启应用（核心刷新机制重写，模仿主题切换的强力刷新）。

### 优化

- 移除自定义颜色对话框的 OK/Cancel 按钮（换色实时落盘后已失去语义）。
- 注释掉 CI 中的系统测试步骤（windows-latest runner 无交互式桌面会话，WPF UIA 不稳定）。

## v2.1.0

### 新特性

- **用户自定义颜色**：在多预设皮肤基础上，支持对任意皮肤的颜色进行自定义覆盖。主题菜单新增"自定义颜色..."入口，提供 18 个核心颜色的 hex 输入和颜色选择器，改动即时生效，持久化到 settings.json。

## v2.0.0

### 新特性

- **多预设皮肤系统**：从只有 Light/Dark 两个硬编码主题升级为可扩展的多预设皮肤架构，内置 8 套皮肤（Light/Dark/Solarized Light/Solarized Dark/GitHub Light/GitHub Dark/Dracula/Monokai）。兼容旧 settings.json。

## v1.7.0

### 新特性

- **代码行数统计**：仓库统计面板新增代码行数统计区域，集成 tokei 支持 200+ 语言，区分 code/comments/blanks。支持统计当前工作区或历史 commit/分支/tag 快照，提供饼图 + 列表双视图，按占比和明细两个角度看语言分布。
- **分支右键"代码统计"入口**：本地分支右键菜单新增"Code statistics..."，点击以该分支为初始 ref 打开统计窗口并自动滚动到代码行数区域。

## v1.6.4

### 修复

- **仓库树图点击崩溃**：在 Repository Overview 窗口点击文件夹后不再整体崩溃；打开窗口加载完成后也不再崩溃。

### 新特性

- **贡献热力图图例与统计摘要**：贡献热力图下方新增色阶图例（Less/More）和统计摘要（总提交数/最长连续提交天数/最活跃日期）。

## v1.6.3

### 新特性

- **贡献热力图**：统计面板新增 GitHub 风格的 53 周 × 7 天提交热力图，一眼看出近一年的提交活跃度分布，支持按作者统计当天提交数。

## v1.6.2

### 优化

- **跟踪右键改为二级菜单 + 分支级搜索框**："跟踪"右键菜单改为按远端分组的二级菜单，分支那一级顶部加搜索框置顶不受滚动影响，跟踪和检查远端同步状态都复用此模板。

## v1.6.1

### 修复

- **远端同步状态弹窗布局拥挤**：图标和文字不再挤在一起。
- **检查更新"已是最新版本"未显示版本号**：现在显示当前版本号。
- **git mm 子仓变更数量"从有到无"**：子仓变更数字短暂显示后不再变成 0。
- **git mm 子仓视图左侧树/未暂存区为空**：子仓自身的变更不再被误过滤。
- **远端同步状态弹窗显示 `[Dialog Description]` 占位符**：占位符不再暴露。

### 优化

- **"检查 Fork 同步状态"改为"检查远端同步状态"**：表述更准确，不限于 fork 工作流。
- **远端同步状态改为二级菜单选择远端分支**：用户显式选择目标远端分支，立即弹框显示检测中。

### 新特性

- **git mm 子仓右键"作为独立仓库打开"**：子仓 tab 右键菜单新增选项，点击用单仓方式新开一个 tab。

## v1.6.0

### 新特性

- **AI 解决合并冲突**：合并冲突解决窗口新增「🤖 AI Resolve」按钮，一键让 AI 合并两侧变更并解决全部冲突。
- **Fork 工作流同步冲突预检**：push 前预检本地分支与 upstream 目标分支是否会冲突，三态结果展示（安全推送/建议同步/有冲突）。
- **Commit 面板 Gitmoji**：commit subject 输入 `:` 时弹出 gitmoji emoji 选择器（如 `:bug:` → 🐛）。
- **AI 辅助开发对话 Markdown 渲染 + Emoji 彩色显示**：AI 回复改用 WebView2 渲染 Markdown，emoji 显示为彩色。

## v1.5.8

### 修复

- **变更数量大时暂存区/未暂存区被强制平铺**：变更文件数达到 5000 时不再从用户选择的树状自动降级为平铺。

## v1.5.7

### 修复

- **git mm 子仓变更仍不显示**：v1.5.6 修复无效，子仓状态检测命令对齐单仓变更列表参数（含 untracked 文件）。

### 易用性

- **子仓页签右键"打开 git mm 仓"快捷入口**：单仓方式打开的子仓页签右键可快捷跳转到对应的 git mm 页签。

## v1.5.6

### 修复

- **git mm 视图子仓变更不显示**：子仓状态检测命令对齐单仓脏检查参数，规避锁竞争和 fsmonitor 误判。

### 重构

- **AI 代码检视页面**：新增模型下拉选择、状态栏进度承载、流式实时输出、Stop 按钮取消任务、排队/重试状态外显。

## v1.5.5

### 修复

- **git 命令预览过长挤掉确认按钮**：对话框的 git 命令预览区限制最大高度并加滚动条，确认按钮不再被裁出可视区。

## v1.5.4

### 修复

- **AI 排队场景返回错误码**：v1.5.3 修复只对非流式路径生效，本次彻底修复流式路径（影响 AI 辅助开发 + AI 代码检视 + commit 消息生成）。

## v1.5.3

### 优化

- **AI 辅助开发体验**：新增模型下拉选择、需求队列不阻塞输入、停止任务按钮、正确处理排队场景、上下文超长自动压缩、commit 消息即时写入。

## v1.5.2

### 优化

- **AI 辅助开发界面**：AI 按钮迁移到顶部工具栏，对话增加记忆支持连续追问，新增清空按钮和欢迎信息。

### 修复

- **检查更新按钮无反应 + 504 网关超时**：改为"先弹窗后检测"交互，独立 HttpClient 直连 GitHub API 避免系统代理 504。

## v1.5.0

### 新特性

- **自动检测更新**：启动后自动检测新版本（默认 24 小时间隔），帮助菜单新增"Check for Updates..."主动检测，发现新版本时弹出提示窗口。

## v1.4.7

### 优化

- **AI 开发窗口改用流式输出**：AI 生成的文本逐 chunk 实时追加到聊天气泡，不再卡一段时间无输出。
- **AI 开发新增"撤销 AI 修改"按钮**：AI 修改文件后可一键撤销，无需手动 `git checkout`。
- **国际化补全**：AiDevelopmentWindow 中文字面量补齐 7 种语言翻译。

### 修复

- **`PathHelper.GetParent` 空路径崩溃**：对 null/空/非法路径返回 null。
- **单元测试超时**：从 300s 缩短到 120s 减少等待。

## v1.4.6

### 优化

- **AI 检视流式输出 + 超时处理**：OpenAI HTTP 路径改用 SSE 流式输出，Claude CLI 路径新增超时处理。
- **工具栏下拉菜单国际化**：Appearance/Stash/Workspaces 三个下拉菜单的硬编码英文改为本地化。

## v1.4.4

### 新特性

- **命令预览收尾**：补全 6 个执行 git 命令但缺命令预览的弹窗（LeanBranchingStart/Finish、InteractiveRebase、SaveSnapshot、GitLfsTrack、AddGitIgnorePattern）。

### 修复

- **LeanBranchingStartWindow FriendlyName 取错**：改为 `Name` 规避显式接口实现问题。
- **LeanBranchingFinishWindow 编译错误**：修正构造函数括号结构。

## v1.4.3

### 修复

- **新建分支/标签/删除分支弹窗显示 git 命令预览**：构造函数末尾补刷 `RefreshCommandPreview`。
- **"Cannot parse revision" 国际化 + AI 生成提交信息取消后仍写入**：补齐翻译 key，Dispatcher 回调补 `monitor.IsCanceled` 检查。

## v1.4.2

### 修复

- **git mm 下拉框两行**：取版本输出首行去除内嵌换行。
- **交互式变基弹窗闪退**：`Close()` 后补 `return` 避免 NRE。
- **右键"在文件树中显示"闪退**：新增延迟展开模式，`RootItem` 就绪后再展开。
- **追溯/历史弹窗显示 "Cannot parse revision"**：Windows `\r\n` 行尾问题，统一替换为 `\n`。
- **变基/重置分支弹窗默认不显示 git 命令预览**：构造函数末尾补刷。
- **追溯/历史弹窗显示类型名而非错误描述**：基类 `GitCommandError` 重写 `ToString` 返回 `FriendlyDescription`。

## v1.4.1

### 新特性

- **git 命令预览复制按钮**：预览右侧新增复制图标按钮。
- **国际化**：git-mm Instance 标签、远端右键菜单 Edit/Delete 'xxx' 补齐 7 种语言。

### 修复

- **偏好设置打开卡顿**：恢复 `GitMmVersionText` 原始实现，修复版本输出含内嵌换行的问题。

## v1.4.0

### 新特性

- **Git 命令预览**：所有对话框窗口（45 个）底部新增 git 命令预览区域，修改选项时实时更新。

### 修复

- **CI 构建失败**：DeleteWorktreeWindow/CheckoutRevisionWindow 的 struct 与 null 比较问题。
- **打开偏好设置异常**：未找到 git-mm 时 `SelectedItem` 设为 null，添加 `_isRefreshingGitMm` 守卫标志。

## v1.3.4

### 修复

- **所有 push 操作报 "src refspec xxx does not match any"**：移除 `PushGitCommand` 中 5 处 `Quotify()` 调用。

## v1.3.3

### 性能优化

- **启动速度**：合并重复的 git version 子进程，缓存 PATH 遍历结果，git-mm 检测改为后台线程。

### 修复

- **窗口位置/大小/状态不按上次保存恢复**：先设置 WPF 依赖属性再调 `SetWindowPlacement`，新增 `OnStateChanged`。

### 国际化

- 补全 18 个未本地化的命令 Title（Remote/Branch/Tag/Worktree 等），7 种语言各新增 16 个 key。

## v1.3.2

### 修复

- **新文件详情页显示原始 diff 头部**：`git diff` 退出码 1（有差异）不再误判为失败。
- **`PatchParser.Parse` 返回 null 导致 NRE**：原生 tokenizer 失败时返回 `Failure` 而非 null。

## v1.3.1

### 新特性

- **git mm 版本检测**：仅当用户打开 git mm 仓库时检测 git-mm 是否存在及版本是否满足 3.0，偏好设置新增 git-mm 实例选择下拉框。

## v1.3.0

### Git 命令健壮性

- 修复 `Quotify()` 未转义参数内嵌引号的问题。
- 修复 `GetChangedFilesGitCommand` 解析 Copied/Renamed 状态时越界访问崩溃。
- `CommitGitCommand` 写入提交信息显式使用 UTF-8 无 BOM 编码。
- 分支名、远程名、refspec 统一通过 `Quotify()` 包裹。

### 修复

- `Connection.cs` 修复 socket 与内存泄漏。
- 12 处 `async void` 事件处理器补充 try/catch。
- `FileHelper.OpenInWindowsExplorer` 改用 `Process.Start(ProcessStartInfo)`。

### 性能优化

- `GitMmUserControl.RefreshSubrepoRuntimeState` subrepo 状态查询从串行改为最多 4 路并发。
- `RevisionFileTreeUserControl.Refresh` 和 `RevisionChangesUserControl.UpdateDiff` 异步化。

### 国际化

- 修复 9 处 `ErrorWindow` 字符串拼接，改为 `FormatCurrent` 模板化翻译。
- 新增 11 个翻译 key，补全 7 种语言。
