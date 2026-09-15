using System;
using System.Collections.Generic;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	/// <summary>
	/// SideBySide 左右编辑器行高统一同步器（"左右视图行不对齐"修复，2026-09-15）。
	/// 根因（诊断探针 + 反编译 AvaloniaEdit 12.0.0 实证）：
	///   1) Avalonia TextLine 的行高 = 行内所有 run 的最大字体度量（TextLineImpl）；
	///      App 全局把内嵌 Noto Sans CJK SC 注册为 CJK 回退字体（FontSetup），含中文的行
	///      自然高（v4.1.3 度量收紧前 21.05px@Win/18.82px@Linux）、纯 ASCII 行 15.22px
	///      （Consolas 13px，探针实测）；
	///   2) AvaloniaEdit 行槽高 = max(自然高, DefaultTextHeight × LineHeightFactor=17.66px)
	///      （factor 默认 1.16）——CJK 行槽超默认行槽，逐行相差 3.39px+；
	///   3) SideBySide 左右两侧内容不同（Add 场景左侧全为对齐空行、右侧含大量中文注释），
	///      行槽逐行累积差 → 左右行错位（探针实测 12 行内漂移 13.55px，common.ts
	///      1226 行含数百中文注释行时达数百 px）；行号与代码的槽内居中偏移也随之逐行不等。
	/// 修复（共享最大行高）：跟踪两侧可见行的最大自然行高 h（单调不缩），把两侧
	/// Options.LineHeightFactor 同步抬到 h / DefaultTextHeight——DefaultLineHeight = h 后
	/// 所有行槽统一为 max(自然高, h) = h，左右行逐行等高对齐（纯 ASCII 文件 h=15.22 ≤
	/// 17.66，factor 保持初始值，视觉零变化）。换文件/改字号时 Reset() 重建。
	/// v4.1.3 更新（行间距收紧，两步）：本同步器曾把含中文文件的全部行槽抬到 CJK 自然高
	/// （Win 21.05px / Linux 18.82px@13px），行间距过宽。根因治理见 FontSetup——内嵌
	/// CJK 子集字体垂直度量收紧到 1.16em（hhea/OS/2 三处一致），CJK 自然高 15.08px@13px
	/// ≤ 默认行槽（v4.1.3 起 CodeEditor 置 LineHeightFactor=1.0，行槽 = ASCII 自然高
	/// ≈ 15.22px@Win / 15.13px@Linux，即 WPF 3.13.2 的自然行高密度），行槽回到统一
	/// 默认值（左右天然等高），正常字体下本同步器不再需要抬 factor；保留为异常字体度量
	/// （用户换更宽 CJK 字体/超常规字号）下的对齐安全网，行为不变（初始 factor 随
	/// CodeEditor 构造即为 1.0，Reset 还原到 1.0）。
	/// 兼容性：只用公共 API（Options.LineHeightFactor / VisualLines / TextLines），
	/// 无反射；TextView 对 LineHeightFactor 变更自带 InvalidateDefaultTextMetrics+Redraw
	///（OnOptionChanged），设值即全量重排。
	/// </summary>
	internal sealed class SideBySideLineHeightSynchronizer : IDisposable
	{
		private sealed class Side
		{
			[Null]
			public TextEditor Editor;

			[Null]
			public TextView TextView;

			[Null]
			public EventHandler Handler;
		}

		private readonly List<Side> _sides = new List<Side>();

		// 构造时两侧 Options.LineHeightFactor 的初始值（AvaloniaEdit 12.0.0 默认 1.16），
		// Reset() 时还原；纯 ASCII 文件全程保持该值，视觉与修复前完全一致。
		private readonly double _initialFactor;

		// 本轮 diff（两次 Reset 之间）出现过的最大自然行高，单调不缩：
		// 垂直滚动把更高的行滚进视口时只增不减，行槽高不跳变。
		private double _maxNaturalHeight;

		// 当前已应用的 factor（恒等于两侧 Options.LineHeightFactor）。
		private double _appliedFactor;

		private bool _updating;

		private bool _disposed;

		public SideBySideLineHeightSynchronizer(params TextEditor[] editors)
		{
			double initial = double.NaN;
			foreach (TextEditor editor in editors)
			{
				if (editor?.TextArea?.TextView == null)
				{
					continue;
				}
				if (double.IsNaN(initial))
				{
					initial = editor.Options.LineHeightFactor;
				}
				Side side = new Side
				{
					Editor = editor,
					TextView = editor.TextArea.TextView
				};
				_sides.Add(side);
				side.Handler = delegate
				{
					SyncFrom(side);
				};
				side.TextView.VisualLinesChanged += side.Handler;
			}
			_initialFactor = double.IsNaN(initial) ? 1.0 : initial;
			_appliedFactor = _initialFactor;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			foreach (Side side in _sides)
			{
				if (side.Handler != null)
				{
					side.TextView.VisualLinesChanged -= side.Handler;
				}
			}
			_sides.Clear();
		}

		/// <summary>换文件（SetDiff）或改字号（RefreshDiffFont）时调用：还原初始 factor、
		/// 清零共享行高，下轮排版按新内容/新字号重建（改字号后 DefaultTextHeight 变化，
		/// 旧 factor 已失义）。</summary>
		public void Reset()
		{
			_maxNaturalHeight = 0.0;
			if (Math.Abs(_appliedFactor - _initialFactor) > 0.0001)
			{
				_appliedFactor = _initialFactor;
				foreach (Side side in _sides)
				{
					side.Editor.Options.LineHeightFactor = _initialFactor;
				}
			}
		}

		private void SyncFrom(Side side)
		{
			if (_updating || _disposed)
			{
				return;
			}
			_updating = true;
			try
			{
				// 1) 采集两侧可见行的最大自然行高（视觉行刚重建，VisualLinesValid 恒真；
				//    防御性跳过无效侧）。只看可见行即可：不可见行迟早滚进视口，届时单调扩张。
				double maxNatural = 0.0;
				foreach (Side s in _sides)
				{
					if (!s.TextView.VisualLinesValid)
					{
						continue;
					}
					foreach (VisualLine visualLine in s.TextView.VisualLines)
					{
						foreach (TextLine textLine in visualLine.TextLines)
						{
							if (textLine.Height > maxNatural)
							{
								maxNatural = textLine.Height;
							}
						}
					}
				}
				if (maxNatural <= 0.0)
				{
					return;
				}
				if (maxNatural > _maxNaturalHeight)
				{
					_maxNaturalHeight = maxNatural;
				}
				// 2) 目标：DefaultLineHeight ≥ 最大自然行高 → 所有行槽统一。
				//    DefaultLineHeight 与 factor 线性（DefaultTextHeight × factor）：
				//    factor′ = factor × h / DefaultLineHeight 恰使 DefaultLineHeight′ = h。
				//    只增不减（减少会让已见过的更高行再次超槽，行间跳动）。
				double defaultLineHeight = side.TextView.DefaultLineHeight;
				if (defaultLineHeight <= 0.0)
				{
					return;
				}
				double targetFactor = _appliedFactor * (_maxNaturalHeight / defaultLineHeight);
				if (targetFactor > _appliedFactor + 0.0001)
				{
					_appliedFactor = targetFactor;
					foreach (Side s2 in _sides)
					{
						s2.Editor.Options.LineHeightFactor = targetFactor;
					}
					// 设值触发两侧 InvalidateDefaultTextMetrics + Redraw，下一轮排版
					// VisualLinesChanged 再次进入本方法：h 与 factor 已匹配，targetFactor
					// 不再增长，收敛无循环。
				}
			}
			finally
			{
				_updating = false;
			}
		}
	}
}
