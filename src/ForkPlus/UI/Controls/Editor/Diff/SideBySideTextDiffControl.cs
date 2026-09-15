using System;
using System.Collections.Generic;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Helpers;
using Avalonia.Threading;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class SideBySideTextDiffControl : Grid, ITextDiffControl, DiffControlContainer.IFileDiffControlSubControl
	{
		private readonly DiffCodeEditor _leftDiffCodeEditor;

		private readonly DiffCodeEditor _rightDiffCodeEditor;

		// 修复（2026-09-14，"左右滚动 diff 对齐"）：
		// 旧方案三道时间性防线（100ms 防抖、_scrollSyncInProgress 断路旗、2s 内 40 次
		// 熔断挂起 5s）全部按"时间/频次"猜测回声，误伤正常使用：
		//   1) 用户快速横滚（触控板惯性/滚动条拖拽）本身就是高频写入，会触发熔断 →
		//      联动暂停 5s → 两栏漂移错位；
		//   2) 100ms 内交替滚左右两栏被防抖直接丢弃 → 不跟随。
		// 新方案：回声按"写入值匹配"确定性识别——每次程序化写入对侧前，把目标 (x,y)
		// 记入 _pendingSyncEcho；对侧随后（同步或布局期）触发的 ScrollOffsetChanged 若
		// 与写入值一致（±0.5px）即联动回声，直接吞掉。用户滚动的值不会恰好等于写入值，
		// 必然放行。回声链不可能形成（回声被消费、不再触发写入），时间性防线全部移除。
		//
		// 修复（2026-09-14，"Add 场景左侧全空，拖右侧水平滚动条左侧有时不跟随"）：
		// 写入链要过三道认识（ScrollViewer.CoerceOffset → presenter coerce →
		// TextView.SetScrollOffset），任一层对补宽 extent 的认识落后一瞬间，写入值就会被
		// 中途钳小。钳后对侧事件的值 ≠ 写入值 → 回声漏判为"用户滚动" → 反写源侧（拖拽发涩）
		// 或源侧继续拖、对侧停在钳后值（单侧失步）。两道确定性加固：
		//   A) 回声匹配扩展为"写入值 或 写入值在对侧文档区的钳制值"——被钳的回声同样是
		//      联动产物，吞掉不反写源侧；
		//   B) 写入后经一帧布局验证（ScheduleSelfHeal）：对侧终值仍偏离写入值（被中途
		//      钳掉未再触发事件）→ 重写一次自愈，限重试 2 次（防极端场景下无限循环）。
		private readonly Dictionary<DiffCodeEditor, SyncEchoState> _pendingSyncEcho = new Dictionary<DiffCodeEditor, SyncEchoState>();

		/// <summary>一次程序化写入的回声跟踪：目标值 + 自愈重试计数。</summary>
		private sealed class SyncEchoState
		{
			public Vector Target;

			public int Retries;
		}

		// 修复（2026-09-14，同上）：AvaloniaEdit TextView 的水平 extent 只由可见行决定
		//（详见 SideBySideExtentSynchronizer 头注释），左右两侧水平滚动范围天然不等且随
		// 垂直滚动实时变化 → 一侧有长行另一侧没有时窄侧被钳在 0/max，两栏列错位。
		// 用同步器把两侧范围统一抬到共同最大值，窄侧可滚入空白区（VS Code 同款行为）。
		private SideBySideExtentSynchronizer _extentSynchronizer;

		// 修复（2026-09-15，"左右视图行不对齐"）：CJK 行（全局回退 Noto Sans CJK SC）比
		// ASCII 行槽高高 3.39px（详见 SideBySideLineHeightSynchronizer 头注释），左右两侧
		// 内容不同时行槽逐行累积差 → 行错位。用同步器把两侧行高统一抬到共同最大值。
		private SideBySideLineHeightSynchronizer _lineHeightSynchronizer;

		[Null]
		public CodeEditorScrollPositionCache PositionCache { get; set; }

		[Null]
		public ForkPlus.Git.Diff.Diff Diff { get; private set; }

		public int TabWidth { get; private set; }

		public bool EntireFile { get; private set; }

		public DiffLocation Location { get; private set; }

		public global::Avalonia.Controls.Primitives.ScrollBarVisibility VerticalScrollBarVisibility
		{
			get
			{
				return _rightDiffCodeEditor.VerticalScrollBarVisibility;
			}
			set
			{
				_rightDiffCodeEditor.VerticalScrollBarVisibility = value;
			}
		}

		public event ContextMenuEventHandler EditorContextMenuOpening
		{
			add
			{
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuOpeningHandler(_leftDiffCodeEditor,(s, e) => value?.Invoke(s, e));
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuOpeningHandler(_rightDiffCodeEditor,(s, e) => value?.Invoke(s, e));
			}
			remove
			{
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.RemoveContextMenuOpeningHandler(_leftDiffCodeEditor,(s, e) => value?.Invoke(s, e));
				global::ForkPlus.UI.WpfCompat.ContextMenuCompat.RemoveContextMenuOpeningHandler(_rightDiffCodeEditor,(s, e) => value?.Invoke(s, e));
			}
		}

		public SideBySideTextDiffControl()
		{
			_leftDiffCodeEditor = new DiffCodeEditor(DiffViewMode.SideBySideOld);
			_rightDiffCodeEditor = new DiffCodeEditor(DiffViewMode.SideBySideNew);
			_leftDiffCodeEditor.ContextMenu = new ContextMenu();
			_rightDiffCodeEditor.ContextMenu = new ContextMenu();
			_leftDiffCodeEditor.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
			_leftDiffCodeEditor.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
			_rightDiffCodeEditor.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
			_rightDiffCodeEditor.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuClosingHandler(_leftDiffCodeEditor,delegate
			{
				_leftDiffCodeEditor.ContextMenu.Items.Clear();
			});
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AddContextMenuClosingHandler(_rightDiffCodeEditor,delegate
			{
				_rightDiffCodeEditor.ContextMenu.Items.Clear();
			});
			base.ColumnDefinitions.Add(new ColumnDefinition());
			base.ColumnDefinitions.Add(new ColumnDefinition());
			base.Children.Add(_leftDiffCodeEditor);
			base.Children.Add(_rightDiffCodeEditor);
			_leftDiffCodeEditor.SetValue(Grid.ColumnProperty, 0);
			_rightDiffCodeEditor.SetValue(Grid.ColumnProperty, 1);
			_leftDiffCodeEditor.VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
			_extentSynchronizer = new SideBySideExtentSynchronizer(_leftDiffCodeEditor, _rightDiffCodeEditor);
			_lineHeightSynchronizer = new SideBySideLineHeightSynchronizer(_leftDiffCodeEditor, _rightDiffCodeEditor);
			_leftDiffCodeEditor.TextArea.TextView.ScrollOffsetChanged += delegate
			{
				OnScrollOffsetChanged(_leftDiffCodeEditor);
			};
			_rightDiffCodeEditor.TextArea.TextView.ScrollOffsetChanged += delegate
			{
				OnScrollOffsetChanged(_rightDiffCodeEditor);
			};
		}

		public void ControlWillBeRemovedFromFileDiffControl()
		{
			PositionCache?.SaveScrollPosition(_leftDiffCodeEditor, _rightDiffCodeEditor);
			_extentSynchronizer?.Dispose();
			_lineHeightSynchronizer?.Dispose();
		}

		public void SetDiff(ForkPlus.Git.Diff.Diff diff, int tabWidth, bool entireFile, DiffLocation location)
		{
			Diff = diff;
			TabWidth = tabWidth;
			EntireFile = entireFile;
			Location = location;
			PositionCache?.SaveScrollPosition(_leftDiffCodeEditor, _rightDiffCodeEditor);
			_extentSynchronizer?.Reset();
			// 换文件：行高共享基准清零（新内容 CJK 占比不同），下轮排版按新内容重建。
			_lineHeightSynchronizer?.Reset();
			VisualPatch.CreateSideBySideVisualPatch(Diff, EntireFile, Location, out var old, out var @new);
			_leftDiffCodeEditor.Options.IndentationSize = tabWidth;
			_leftDiffCodeEditor.VisualPatch = old;
			_rightDiffCodeEditor.Options.IndentationSize = tabWidth;
			_rightDiffCodeEditor.VisualPatch = @new;
			base.Dispatcher.Post(delegate
			{
				PositionCache?.RestoreScrollPosition(_leftDiffCodeEditor, _rightDiffCodeEditor);
			});
		}

		public void RefreshDiffFont(double codeEditorFontSize)
		{
			_leftDiffCodeEditor.FontSize = codeEditorFontSize;
			_rightDiffCodeEditor.FontSize = codeEditorFontSize;
			// 改字号：DefaultTextHeight 随之变化，旧 factor 失义，清零重建。
			_lineHeightSynchronizer?.Reset();
		}

		public void RefreshDiffWordWrap(bool diffWordWrap)
		{
			_leftDiffCodeEditor.WordWrap = false;
			_rightDiffCodeEditor.WordWrap = false;
		}

		public void RefreshDiffShowHiddenSymbols(bool diffShowHiddenSymbols)
		{
			_leftDiffCodeEditor.Options.ShowSpaces = diffShowHiddenSymbols;
			_rightDiffCodeEditor.Options.ShowSpaces = diffShowHiddenSymbols;
			_leftDiffCodeEditor.Options.ShowTabs = diffShowHiddenSymbols;
			_rightDiffCodeEditor.Options.ShowTabs = diffShowHiddenSymbols;
		}

		public void ScrollToPreviousCustomHunk()
		{
			_rightDiffCodeEditor.ScrollToPreviousCustomHunk();
		}

		public void ScrollToNextCustomHunk()
		{
			_rightDiffCodeEditor.ScrollToNextCustomHunk();
		}

		private void OnScrollOffsetChanged(DiffCodeEditor editor)
		{
			DiffCodeEditor peer = ((editor == _leftDiffCodeEditor) ? _rightDiffCodeEditor : _leftDiffCodeEditor);
			Vector offset = editor.TextArea.TextView.ScrollOffset;
			// 1) 回声消费：本侧当前偏移与最近一次程序化写入一致（±0.5px）→ 联动回声，吞掉
			//（写入值匹配的确定性识别，替代旧的 100ms 防抖 + 熔断挂起，详见字段注释）
			if (_pendingSyncEcho.TryGetValue(editor, out SyncEchoState echo))
			{
				if (IsClose(offset, echo.Target))
				{
					_pendingSyncEcho.Remove(editor);
					return;
				}
				// 加固 A：写入值在本侧文档区的钳制值也视为回声——写入链中某一层认识落后时
				// 写入值会被中途钳小，钳后事件值必然 ≠ 写入值。不识别它就会漏判成用户滚动
				// 反写源侧（拖拽发涩/单侧停住）。被钳的回声不反写，交由 ScheduleSelfHeal 自愈。
				Vector clampedTarget = new Vector(
					editor.ClampHorizontalOffsetToDocumentArea(echo.Target.X),
					editor.ClampVerticalOffsetToDocumentArea(echo.Target.Y));
				if (IsClose(offset, clampedTarget))
				{
					ScheduleSelfHeal(editor, echo);
					return;
				}
				// 不匹配的旧表项作废（本次事件来自用户滚动或其它来源）
				_pendingSyncEcho.Remove(editor);
			}
			// 2) 用户滚动 → 对侧跟随。逐轴计算目标：
			//   - 源偏移须在源自身文档区内（Is*WithinDocumentArea，防越界传播）；
			//   - 目标先夹到对侧自己的文档区（2026-09-12 修复保留：偏短侧钳到自身末尾对齐定格；
			//     水平轴配合 ExtentSynchronizer 两侧范围恒等后，钳制实际不再发生）。
			Vector peerOffset = peer.TextArea.TextView.ScrollOffset;
			double targetX = peerOffset.X;
			double targetY = peerOffset.Y;
			bool changed = false;
			if (editor.IsHorizontalOffsetWithinDocumentArea(offset.X))
			{
				double clampedX = peer.ClampHorizontalOffsetToDocumentArea(offset.X);
				if (Math.Abs(peerOffset.X - clampedX) > 0.5)
				{
					targetX = clampedX;
					changed = true;
				}
			}
			if (editor.IsVerticalOffsetWithinDocumentArea(offset.Y))
			{
				double clampedY = peer.ClampVerticalOffsetToDocumentArea(offset.Y);
				if (Math.Abs(peerOffset.Y - clampedY) > 0.5)
				{
					targetY = clampedY;
					changed = true;
				}
			}
			// 3) 双轴一次写入（ScrollToOffsetCompat 单次 Offset 赋值 → 恰一次回声事件，
			//    与 _pendingSyncEcho 的一表一项一一对应）
			if (changed)
			{
				SyncEchoState newEcho = new SyncEchoState { Target = new Vector(targetX, targetY) };
				_pendingSyncEcho[peer] = newEcho;
				peer.ScrollToOffsetCompat(targetX, targetY);
				// 加固 B：布局后验证对侧终值；被中途钳掉且未再触发事件时重写自愈。
				ScheduleSelfHeal(peer, newEcho);
			}
		}

		/// <summary>写入后自愈：经一帧布局（Render 优先级 Post）读对侧终值，仍偏离写入值
		///（被中间层钳掉且钳后值恰好不再触发事件）则重写一次；限重试 2 次防循环。</summary>
		private void ScheduleSelfHeal(DiffCodeEditor peer, SyncEchoState echo)
		{
			if (echo.Retries >= 2)
			{
				_pendingSyncEcho.Remove(peer);
				return;
			}
			echo.Retries++;
			Dispatcher.UIThread.Post(delegate
			{
				// 表项已被正常回声消费（终值达成）或已被更新一次写入替换 → 无需自愈。
				if (!_pendingSyncEcho.TryGetValue(peer, out SyncEchoState current) || !ReferenceEquals(current, echo))
				{
					return;
				}
				Vector actual = peer.TextArea.TextView.ScrollOffset;
				if (IsClose(actual, echo.Target))
				{
					_pendingSyncEcho.Remove(peer);
					return;
				}
				peer.ScrollToOffsetCompat(echo.Target.X, echo.Target.Y);
			}, DispatcherPriority.Render);
		}

		private static bool IsClose(Vector a, Vector b)
		{
			return Math.Abs(a.X - b.X) <= 0.5 && Math.Abs(a.Y - b.Y) <= 0.5;
		}
	}
}
