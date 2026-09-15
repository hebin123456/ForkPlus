using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.UI.CircularProgressBar
{
	/// <summary>
	/// 修复（2026-09-14，"loading 动画转不起来"）：WPF 原版 CircularProgressBar 靠
	/// BusyIndicatorStoryboard 驱动 12 个矩形的透明度追逐（周期 0.55s、相邻矩形相位差
	/// 0.05s、亮度 1→0.85→0.6→0.3 线性衰减后保持 0.3）；迁移 Avalonia 时 Storyboard/
	/// MultiTrigger 无对应物被整体注释（见 Theme/Styles/Progressbar.axaml），spinner
	/// 退化为 12 个静止半透明点。本附加行为用 DispatcherTimer 按原 Storyboard 时序逐帧
	/// 计算各矩形 Opacity，在 CircularProgressBar ControlTheme 中统一挂载（IsActive=True），
	/// 所有该主题的 spinner（通知面板/Issue/PR 页签/各弹窗 footer 等）自动恢复旋转；
	/// 不可见或脱离可视树时停表并把矩形复位为模板默认 0.3。
	/// </summary>
	public class SpinnerBehavior
	{
		private const double CycleSeconds = 0.55;
		private const double StepSeconds = 0.05;
		private const double IdleOpacity = 0.3;
		private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(30);

		public static readonly AttachedProperty<bool> IsActiveProperty =
			AvaloniaProperty.RegisterAttached<SpinnerBehavior, ProgressBar, bool>("IsActive");

		private static readonly ConditionalWeakTable<ProgressBar, SpinnerState> States =
			new ConditionalWeakTable<ProgressBar, SpinnerState>();

		static SpinnerBehavior()
		{
			IsActiveProperty.Changed.Subscribe(new ActionObserver<AvaloniaPropertyChangedEventArgs<bool>>(OnIsActiveChanged));
		}

		/// <summary>极简 IObserver 适配器（同 WpfPropertyCompat.ActionObserver，该类为 private 无法复用）。</summary>
		private sealed class ActionObserver<T> : IObserver<T>
		{
			private readonly Action<T> _onNext;

			public ActionObserver(Action<T> onNext)
			{
				_onNext = onNext;
			}

			void IObserver<T>.OnCompleted()
			{
			}

			void IObserver<T>.OnError(Exception error)
			{
			}

			void IObserver<T>.OnNext(T value)
			{
				_onNext(value);
			}
		}

		private static void OnIsActiveChanged(AvaloniaPropertyChangedEventArgs<bool> e)
		{
			ProgressBar control = (ProgressBar)e.Sender;
			if (e.NewValue.Value)
			{
				control.AttachedToVisualTree += Control_AttachedToVisualTree;
				control.DetachedFromVisualTree += Control_DetachedFromVisualTree;
				control.PropertyChanged += Control_PropertyChanged;
				control.TemplateApplied += Control_TemplateApplied;
				States.GetOrCreateValue(control).TryStart(control);
			}
			else
			{
				control.AttachedToVisualTree -= Control_AttachedToVisualTree;
				control.DetachedFromVisualTree -= Control_DetachedFromVisualTree;
				control.PropertyChanged -= Control_PropertyChanged;
				control.TemplateApplied -= Control_TemplateApplied;
				if (States.TryGetValue(control, out SpinnerState state))
				{
					state.Stop(detached: false);
				}
			}
		}

		private static void Control_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
		{
			ProgressBar control = (ProgressBar)sender;
			States.GetOrCreateValue(control).TryStart(control);
		}

		private static void Control_DetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
		{
			if (States.TryGetValue((ProgressBar)sender, out SpinnerState state))
			{
				state.Stop(detached: true);
			}
		}

		private static void Control_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property != Visual.IsVisibleProperty)
			{
				return;
			}
			ProgressBar control = (ProgressBar)sender;
			if (!States.TryGetValue(control, out SpinnerState state))
			{
				return;
			}
			if (control.IsVisible)
			{
				state.TryStart(control);
			}
			else
			{
				state.Stop(detached: false);
			}
		}

		private static void Control_TemplateApplied(object sender, TemplateAppliedEventArgs e)
		{
			// 模板重应用（主题切换等）会重建矩形，丢弃缓存，下个 tick 重新解析。
			if (States.TryGetValue((ProgressBar)sender, out SpinnerState state))
			{
				state.ForgetRectangles();
			}
		}

		/// <summary>单个 spinner 的运行时状态：矩形缓存 + 计时器 + 秒表。</summary>
		private sealed class SpinnerState
		{
			private Rectangle[] _rectangles;
			private DispatcherTimer _timer;
			private Stopwatch _stopwatch;
			private ProgressBar _owner;

			public void TryStart(ProgressBar owner)
			{
				if (!owner.IsVisible || !owner.IsAttachedToVisualTree())
				{
					return;
				}
				_owner = owner;
				if (_timer == null)
				{
					_timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TickInterval };
					_timer.Tick += Timer_Tick;
					_stopwatch = new Stopwatch();
				}
				if (!_timer.IsEnabled)
				{
					_stopwatch.Restart();
					_timer.Start();
				}
			}

			public void Stop(bool detached)
			{
				if (_timer != null && _timer.IsEnabled)
				{
					_timer.Stop();
				}
				if (detached)
				{
					// 脱离可视树后模板可能被重应用，矩形引用一并丢弃。
					ForgetRectangles();
				}
				else
				{
					ResetOpacity();
				}
			}

			public void ForgetRectangles()
			{
				_rectangles = null;
			}

			private void ResetOpacity()
			{
				if (_rectangles != null)
				{
					for (int i = 0; i < _rectangles.Length; i++)
					{
						_rectangles[i].Opacity = IdleOpacity;
					}
				}
			}

			private void Timer_Tick(object sender, EventArgs e)
			{
				if (_rectangles == null)
				{
					_rectangles = FindRectangles(_owner);
					if (_rectangles == null)
					{
						// 模板尚未应用完成，下个 tick 再找。
						return;
					}
				}
				double elapsed = _stopwatch.Elapsed.TotalSeconds;
				for (int i = 0; i < _rectangles.Length; i++)
				{
					_rectangles[i].Opacity = OpacityAt(elapsed, i);
				}
			}

			/// <summary>取模板 Canvas 下的矩形（声明顺序即绕圈顺序）。</summary>
			private static Rectangle[] FindRectangles(ProgressBar owner)
			{
				if (owner == null)
				{
					return null;
				}
				foreach (Visual visual in owner.GetVisualDescendants())
				{
					if (visual is Canvas canvas)
					{
						List<Rectangle> rectangles = new List<Rectangle>();
						foreach (Visual child in canvas.Children)
						{
							if (child is Rectangle rectangle)
							{
								rectangles.Add(rectangle);
							}
						}
						if (rectangles.Count != 0)
						{
							return rectangles.ToArray();
						}
					}
				}
				return null;
			}

			/// <summary>WPF Storyboard 等价时序：矩形 i 的"波峰"在 0.05*i 秒处抵达，随后线性衰减并保持暗态。</summary>
			private static double OpacityAt(double elapsed, int index)
			{
				double u = elapsed - StepSeconds * index;
				u -= Math.Floor(u / CycleSeconds) * CycleSeconds;
				if (u < StepSeconds)
				{
					return 1.0 + (0.85 - 1.0) * (u / StepSeconds);
				}
				if (u < 2.0 * StepSeconds)
				{
					return 0.85 + (0.6 - 0.85) * ((u - StepSeconds) / StepSeconds);
				}
				if (u < 3.0 * StepSeconds)
				{
					return 0.6 + (0.3 - 0.6) * ((u - 2.0 * StepSeconds) / StepSeconds);
				}
				return IdleOpacity;
			}
		}
	}
}
