using System;
using Avalonia.Threading;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	/// <summary>
	/// v4.3.2：动图播放控制器——每张动图一个实例，隔离各自的播放状态与当前帧。
	/// 用 <see cref="DispatcherTimer"/> 按当前帧时长（除以播放速度）驱动帧切换；
	/// 帧切换通过 <see cref="FrameChanged"/> 通知宿主控件重绘。
	/// 播放/暂停由用户控制，切视图或换文件时由宿主暂停并释放。
	/// </summary>
	public class AnimatedImagePlayer : IDisposable
	{
		/// <summary>可选播放速度（倍数），与播放控制条下拉顺序一致。</summary>
		public static readonly double[] SpeedOptions = new double[3] { 0.5, 1.0, 2.0 };

		private const double MinSpeed = 0.25;

		private const double MaxSpeed = 4.0;

		private readonly DispatcherTimer _timer;

		private int _completedLoops;

		private bool _disposed;

		private double _speed = 1.0;

		public AnimatedImage Image { get; }

		public bool IsPlaying { get; private set; }

		public int CurrentFrame { get; private set; }

		public int FrameCount => Image.FrameCount;

		/// <summary>当前帧变化（含手动步进与定时推进）。</summary>
		public event EventHandler FrameChanged;

		/// <summary>播放/暂停状态变化（用于刷新控制条按钮图标）。</summary>
		public event EventHandler PlayStateChanged;

		public AnimatedImagePlayer(AnimatedImage image)
		{
			Image = image ?? throw new ArgumentNullException("image");
			_timer = new DispatcherTimer();
			_timer.Tick += OnTimerTick;
		}

		public double Speed
		{
			get
			{
				return _speed;
			}
			set
			{
				double clamped = Math.Clamp(value, MinSpeed, MaxSpeed);
				if (Math.Abs(clamped - _speed) < 1E-09)
				{
					return;
				}
				_speed = clamped;
				if (IsPlaying)
				{
					RestartTimer();
				}
			}
		}

		public void Play()
		{
			if (_disposed || !Image.IsAnimated || IsPlaying)
			{
				return;
			}
			IsPlaying = true;
			_completedLoops = 0;
			RestartTimer();
			PlayStateChanged?.Invoke(this, EventArgs.Empty);
		}

		public void Pause()
		{
			if (!IsPlaying)
			{
				return;
			}
			IsPlaying = false;
			_timer.Stop();
			PlayStateChanged?.Invoke(this, EventArgs.Empty);
		}

		public void TogglePlay()
		{
			if (IsPlaying)
			{
				Pause();
			}
			else
			{
				Play();
			}
		}

		/// <summary>手动前进一帧（会先暂停播放）。</summary>
		public void StepForward()
		{
			Pause();
			SetFrame(CurrentFrame + 1);
		}

		/// <summary>手动后退一帧（会先暂停播放）。</summary>
		public void StepBackward()
		{
			Pause();
			SetFrame(CurrentFrame - 1);
		}

		/// <summary>推进一帧；有限循环播完后停在最后一帧。定时器与单测都走这里。</summary>
		public void Advance()
		{
			if (_disposed || Image.FrameCount <= 0)
			{
				return;
			}
			int next = CurrentFrame + 1;
			if (next >= Image.FrameCount)
			{
				_completedLoops++;
				if (Image.LoopCount > 0 && _completedLoops >= Image.LoopCount)
				{
					// 有限循环播放结束：停在最后一帧并停止。
					IsPlaying = false;
					_timer.Stop();
					PlayStateChanged?.Invoke(this, EventArgs.Empty);
					return;
				}
				next = 0;
			}
			SetFrame(next);
			if (IsPlaying)
			{
				RestartTimer();
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			IsPlaying = false;
			_timer.Stop();
			_timer.Tick -= OnTimerTick;
		}

		private void SetFrame(int index)
		{
			int count = Image.FrameCount;
			if (count <= 0)
			{
				return;
			}
			int normalized = ((index % count) + count) % count;
			CurrentFrame = normalized;
			FrameChanged?.Invoke(this, EventArgs.Empty);
		}

		private void RestartTimer()
		{
			_timer.Stop();
			_timer.Interval = CurrentInterval();
			_timer.Start();
		}

		private TimeSpan CurrentInterval()
		{
			int count = Image.FrameCount;
			int delay = ((count > 0) ? Image.FrameDelays[Math.Clamp(CurrentFrame, 0, count - 1)] : 100);
			double milliseconds = Math.Max(1.0, delay / _speed);
			return TimeSpan.FromMilliseconds(milliseconds);
		}

		private void OnTimerTick(object sender, EventArgs e)
		{
			Advance();
		}
	}
}