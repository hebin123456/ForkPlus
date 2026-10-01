// v4.3.2（图片对比：动图直接播放 + 播放控制）的测试。
// 覆盖三层：
//   1) 解码层：AnimatedImage 用 SkiaSharp SKCodec 解 GIF 帧/时长/循环；静态图与超阈值回退 null；
//   2) 播放层：AnimatedImagePlayer 的播放/暂停、逐帧步进（含环绕）、有限循环、速度夹取；
//   3) UI 层：AnimatedImagePlaybackBar 的按钮驱动播放器；BinaryContentUserControl 装配动图时
//      显示底部控制条并自动播放，静态图不显示；ImageData.IsAnimated 供关闭像素差异高亮。
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ImageAnimationTests
	{
		// 2 帧 4×4 动图（每帧 120ms，loop=0 无限循环），由 Pillow 生成后 base64 内联。
		private const string AnimatedGifBase64 =
			"R0lGODlhBAAEAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQEDAAAACwAAAAABAAEAAAICQABCBxIsCCAgAAh+QQFDAABACwAAAAABAAEAIEA/wAAAAAAAAAAAAAICQABCBxIsCCAgAA7";

		// 4×4 静态 PNG，用于对照（非动图 → 不显示控制条）。
		private const string StaticPngBase64 =
			"iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAFUlEQVR4nGP8z8DwnwEJMDGgAcICAIPRAgYCkO9YAAAAAElFTkSuQmCC";

		private static MemoryStream GifStream()
		{
			return new MemoryStream(Convert.FromBase64String(AnimatedGifBase64));
		}

		private static MemoryStream PngStream()
		{
			return new MemoryStream(Convert.FromBase64String(StaticPngBase64));
		}

		private static WriteableBitmap NewBitmap()
		{
			return new WriteableBitmap(new PixelSize(4, 4), new Vector(96.0, 96.0), PixelFormat.Bgra8888, AlphaFormat.Premul);
		}

		[Fact]
		public void AnimatedImage_TryDecode_AnimatedGif_YieldsFramesAndDelays()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				AnimatedImage image = AnimatedImage.TryDecode(GifStream());
				Assert.NotNull(image);
				try
				{
					Assert.True(image.IsAnimated);
					Assert.Equal(2, image.FrameCount);
					Assert.Equal(2, image.FrameDelays.Length);
					Assert.True(image.FrameDelays[0] >= 20, "帧时长应有下限归一");
					Assert.Equal(0, image.LoopCount); // loop=0 → 无限循环
					Assert.Equal(4, image.Frames[0].PixelSize.Width);
					Assert.Equal(4, image.Frames[1].PixelSize.Width);
				}
				finally
				{
					image.Dispose();
				}
			});
		}

		[Fact]
		public void AnimatedImage_TryDecode_StaticPng_ReturnsNull()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				Assert.Null(AnimatedImage.TryDecode(PngStream()));
				Assert.False(AnimatedImage.IsAnimatedStream(PngStream()));
				Assert.True(AnimatedImage.IsAnimatedStream(GifStream()));
			});
		}

		[Fact]
		public void AnimatedImage_Constructor_NormalizesDelaysAndLoopCount()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				WriteableBitmap first = NewBitmap();
				WriteableBitmap second = NewBitmap();
				try
				{
					AnimatedImage image = new AnimatedImage(new Bitmap[2] { first, second }, new int[2] { 0, 500 }, -3);
					Assert.Equal(100, image.FrameDelays[0]); // 过短 → 默认 100ms
					Assert.Equal(500, image.FrameDelays[1]);
					Assert.Equal(0, image.LoopCount); // 负数 → 0（无限）
					Assert.True(image.IsAnimated);
				}
				finally
				{
					first.Dispose();
					second.Dispose();
				}
			});
		}

		[Fact]
		public void AnimatedImagePlayer_PlaysStepsWrapsAndHonorsFiniteLoops()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				WriteableBitmap f0 = NewBitmap();
				WriteableBitmap f1 = NewBitmap();
				WriteableBitmap f2 = NewBitmap();
				try
				{
					AnimatedImage image = new AnimatedImage(new Bitmap[3] { f0, f1, f2 }, new int[3] { 40, 40, 40 }, 0);
					using (AnimatedImagePlayer player = new AnimatedImagePlayer(image))
					{
						Assert.False(player.IsPlaying);
						Assert.Equal(0, player.CurrentFrame);

						player.Play();
						Assert.True(player.IsPlaying);
						player.Advance();
						Assert.Equal(1, player.CurrentFrame);
						player.Advance();
						player.Advance(); // 无限循环：越过末帧回到 0
						Assert.Equal(0, player.CurrentFrame);
						Assert.True(player.IsPlaying);

						player.StepBackward(); // 回退环绕到末帧，且步进会暂停
						Assert.Equal(2, player.CurrentFrame);
						Assert.False(player.IsPlaying);
						player.StepForward();
						Assert.Equal(0, player.CurrentFrame);

						player.Speed = 10.0;
						Assert.Equal(4.0, player.Speed); // 上限夹取
						player.Speed = 0.1;
						Assert.Equal(0.25, player.Speed); // 下限夹取
					}

					// 有限循环：LoopCount=1 播完一圈停在最后一帧并停止
					AnimatedImage once = new AnimatedImage(new Bitmap[2] { f0, f1 }, new int[2] { 30, 30 }, 1);
					using (AnimatedImagePlayer player2 = new AnimatedImagePlayer(once))
					{
						player2.Play();
						player2.Advance();
						Assert.Equal(1, player2.CurrentFrame);
						player2.Advance(); // 完成第 1 圈 → 停
						Assert.False(player2.IsPlaying);
						Assert.Equal(1, player2.CurrentFrame);
					}
				}
				finally
				{
					f0.Dispose();
					f1.Dispose();
					f2.Dispose();
				}
			});
		}

		[Fact]
		public void PlaybackBar_ButtonsDrivePlayer()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				WriteableBitmap f0 = NewBitmap();
				WriteableBitmap f1 = NewBitmap();
				try
				{
					AnimatedImage image = new AnimatedImage(new Bitmap[2] { f0, f1 }, new int[2] { 40, 40 }, 0);
					using (AnimatedImagePlayer player = new AnimatedImagePlayer(image))
					{
						AnimatedImagePlaybackBar bar = new AnimatedImagePlaybackBar { Player = player };
						Window window = new Window { Width = 320.0, Height = 120.0, Content = bar };
						try
						{
							window.Show();
							Dispatcher.UIThread.RunJobs();

							UiClick.Click(bar.PlayPauseButton);
							Assert.True(player.IsPlaying);
							UiClick.Click(bar.PlayPauseButton);
							Assert.False(player.IsPlaying);

							UiClick.Click(bar.NextFrameButton);
							Assert.Equal(1, player.CurrentFrame);
							UiClick.Click(bar.PrevFrameButton);
							Assert.Equal(0, player.CurrentFrame);

							bar.SpeedComboBox.SelectedIndex = 2;
							Dispatcher.UIThread.RunJobs();
							Assert.Equal(2.0, player.Speed);
							Assert.Equal("1/2", bar.FrameTextBlock.Text);
						}
						finally
						{
							window.Close();
						}
					}
				}
				finally
				{
					f0.Dispose();
					f1.Dispose();
				}
			});
		}

		[Fact]
		public void BinaryContentUserControl_AnimatedGif_ShowsPlaybackBar_StaticImageHides()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				BinaryContentUserControl control = new BinaryContentUserControl();
				Window window = new Window { Width = 500.0, Height = 400.0, Content = control };
				try
				{
					window.Show();
					control.SetContent(new ImageContent("anim.gif", true, GifStream()));
					Dispatcher.UIThread.RunJobs();

					Assert.True(control.HasAnimatedImage);
					Assert.NotNull(control.AnimatedPlayer);
					Assert.NotNull(control.ImageControl.Player);
					Assert.True(control.PlaybackBarContainer.IsVisible);
					Assert.True(control.AnimatedPlayer.IsPlaying, "动图加载后应自动播放");

					// 换成静态图：控制条隐藏、播放器摘除
					control.SetContent(new ImageContent("static.png", true, PngStream()));
					Dispatcher.UIThread.RunJobs();
					Assert.False(control.HasAnimatedImage);
					Assert.Null(control.ImageControl.Player);
					Assert.False(control.PlaybackBarContainer.IsVisible);
				}
				finally
				{
					window.Close();
				}
			});
		}

		[Fact]
		public void ImageData_FlagsAnimatedImagesForHighlightSuppression()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ImageData animated = ImageData.Create(new ImageContent("anim.gif", true, GifStream()));
				ImageData still = ImageData.Create(new ImageContent("still.png", true, PngStream()));
				Assert.True(animated.IsAnimated);   // → GetDiffImage 返回 null，关闭像素差异高亮
				Assert.False(still.IsAnimated);
			});
		}
	}
}