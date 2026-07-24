// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（Brush/IBrush）
// - using System.Windows.Media.Imaging → using Bitmap = Avalonia.Media.Imaging.Bitmap（别名替代 BitmapSource）
// - BitmapSource → Bitmap（Avalonia.Media.Imaging.Bitmap，参考 IconTools）
// - CreateBitmapSource：WPF BitmapImage+BeginInit/EndInit+StreamSource+FormatConvertedBitmap(Pbgra32)
//   → new Bitmap(stream)（Avalonia 不可变 Bitmap，构造时自动解码并处理格式，无需 Freeze，参考 AvatarManager）
// - GetDiffImage：WPF BitmapSource.CopyPixels + FormatConvertedBitmap(Bgra32) + BitmapSource.Create
//   → System.Drawing.Bitmap(LockBits/Format32bppArgb) 读取像素 + System.Drawing.Bitmap 写回 + PNG 编码 + new Bitmap(ms)
//   （Avalonia 不可变 Bitmap 不暴露 CopyPixels/Create；System.Drawing 已在项目内使用，参考 IconTools）
// - PixelWidth/PixelHeight → 通过 System.Drawing.Bitmap.Width/Height（像素比较）或 Bitmap.PixelSize.Width/Height
// - base.Dispatcher.Async → 保持（自定义扩展方法 DispatcherExtension.Async，内部转发 Dispatcher.Post，参考 MultiselectionTreeView）
// - DiffImageSource 属性类型 BitmapSource → Bitmap
// - Grid.SetColumn/SetColumnSpan/Thickness → API 兼容（Avalonia.Controls.Grid/Avalonia.Thickness）
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Bitmap = Avalonia.Media.Imaging.Bitmap;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls.Editor.Hex;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;
using SD = System.Drawing;
using SDI = System.Drawing.Imaging;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public partial class BinaryDiffUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private bool _showTitle;

		private readonly JobQueue _jobQueue = new JobQueue();

		[Null]
		private RepositoryUserControl _repositoryUserControl;

		[Null]
		private ImageData _srcImageData;

		[Null]
		private ImageData _dstImageData;

		[Null]
		private BinaryContent _srcBinaryContent;

		[Null]
		private BinaryContent _dstBinaryContent;

		[Null]
		private Job _activeSrcSmudgeJob;

		[Null]
		private Job _activeDstSmudgeJob;

		[Null]
		private Bitmap _diffImageSource;

		// v3.4.1：Hex 视图 — 存储原始字节和 ChangedFile 用于创建 HexDiffContent
		[Null]
		private ChangedFile _changedFile;
		[Null]
		private MemoryStream _hexSrcData;
		[Null]
		private MemoryStream _hexDstData;
		[Null]
		private HexDiffUserControl _hexDiffView;

		[Null]
		public Bitmap DiffImageSource
		{
			get
			{
				return _diffImageSource;
			}
			private set
			{
				_diffImageSource = value;
				this.DiffImageSourceChanged?.Invoke(this, _diffImageSource != null);
			}
		}

		public event EventHandler<bool> DiffImageSourceChanged;

		public BinaryDiffUserControl()
		{
			InitializeComponent();
			// v3.4.1：让 RadioButton 内容（Side-by-Side/Swipe/Onion Skin/Hex）在构造时翻译
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			// v3.4.1：Hex 视图容器初始隐藏
			HexDiffViewContainer.Collapse();
			BinaryContentUserControl srcFileContentUserControl = SrcFileContentUserControl;
			srcFileContentUserControl.ShowLfsImageButtonClick = (EventHandler<EventArgs>)Delegate.Combine(srcFileContentUserControl.ShowLfsImageButtonClick, (EventHandler<EventArgs>)delegate
			{
				RepositoryUserControl repositoryUserControl4 = _repositoryUserControl;
				if (repositoryUserControl4 != null)
				{
					GitModule gitModule4 = _repositoryUserControl.GitModule;
					if (gitModule4 != null)
					{
						BinaryContent srcBinaryContent2 = _srcBinaryContent;
						LfsContent srcLfsContent = srcBinaryContent2 as LfsContent;
						if (srcLfsContent != null)
						{
							SrcFileContentUserControl.SetProgress(0.0);
							_activeSrcSmudgeJob?.Monitor.Cancel();
							_activeSrcSmudgeJob = StartSmudgeLfsImageJob(srcLfsContent.LfsPointer, gitModule4, delegate(JobMonitor monitor)
							{
								SrcFileContentUserControl.SetProgress(monitor.Progress.GetValueOrDefault());
							}, delegate(GitCommandResult<MemoryStream> imageDataResponse)
							{
								_activeSrcSmudgeJob = null;
								SrcFileContentUserControl.SetProgress(null);
								if (!imageDataResponse.Succeeded)
								{
									new ErrorWindow(repositoryUserControl4, imageDataResponse.Error).ShowDialog();
								}
								else
								{
									MemoryStream result2 = imageDataResponse.Result;
									if (Path.GetExtension(srcLfsContent.Path) == ".tga" && result2 != null)
									{
										GitCommandResult<MemoryStream> gitCommandResult2 = DecodeImageData(result2.ToArray());
										if (gitCommandResult2.Succeeded)
										{
											result2 = gitCommandResult2.Result;
										}
										else
										{
											Log.Error(gitCommandResult2.Error.FriendlyDescription);
										}
									}
									_srcImageData = ImageData.Create(result2, isLfs: true, srcLfsContent.IsTracked);
									_hexSrcData = result2; // v3.4.1：存原始字节供 Hex 视图
									DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
									DstFileContentUserControl.DiffImageSource = DiffImageSource;
									SrcFileContentUserControl.SetLfsImageData(result2);
									RefreshViewModes();
								}
							});
						}
					}
				}
			});
			BinaryContentUserControl srcFileContentUserControl2 = SrcFileContentUserControl;
			srcFileContentUserControl2.CancelLfsButtonClick = (EventHandler<EventArgs>)Delegate.Combine(srcFileContentUserControl2.CancelLfsButtonClick, (EventHandler<EventArgs>)delegate
			{
				_activeSrcSmudgeJob?.Monitor.Cancel();
			});
			BinaryContentUserControl dstFileContentUserControl = DstFileContentUserControl;
			dstFileContentUserControl.ShowLfsImageButtonClick = (EventHandler<EventArgs>)Delegate.Combine(dstFileContentUserControl.ShowLfsImageButtonClick, (EventHandler<EventArgs>)delegate
			{
				RepositoryUserControl repositoryUserControl3 = _repositoryUserControl;
				if (repositoryUserControl3 != null)
				{
					GitModule gitModule3 = _repositoryUserControl.GitModule;
					if (gitModule3 != null)
					{
						BinaryContent dstBinaryContent2 = _dstBinaryContent;
						LfsContent dstLfsContent = dstBinaryContent2 as LfsContent;
						if (dstLfsContent != null)
						{
							DstFileContentUserControl.SetProgress(0.0);
							_activeDstSmudgeJob?.Monitor.Cancel();
							_activeDstSmudgeJob = StartSmudgeLfsImageJob(dstLfsContent.LfsPointer, gitModule3, delegate(JobMonitor monitor)
							{
								DstFileContentUserControl.SetProgress(monitor.Progress.GetValueOrDefault());
							}, delegate(GitCommandResult<MemoryStream> imageDataResponse)
							{
								_activeDstSmudgeJob = null;
								DstFileContentUserControl.SetProgress(null);
								if (!imageDataResponse.Succeeded)
								{
									new ErrorWindow(repositoryUserControl3, imageDataResponse.Error).ShowDialog();
								}
								else
								{
									MemoryStream result = imageDataResponse.Result;
									if (Path.GetExtension(dstLfsContent.Path) == ".tga" && result != null)
									{
										GitCommandResult<MemoryStream> gitCommandResult = DecodeImageData(result.ToArray());
										if (gitCommandResult.Succeeded)
										{
											result = gitCommandResult.Result;
										}
										else
										{
											Log.Error(gitCommandResult.Error.FriendlyDescription);
										}
									}
									_dstImageData = ImageData.Create(result, isLfs: true, dstLfsContent.IsTracked);
									_hexDstData = result; // v3.4.1：存原始字节供 Hex 视图
								DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
								DstFileContentUserControl.SetLfsImageData(result, DiffImageSource);
								RefreshViewModes();
								}
							});
						}
					}
				}
			});
			BinaryContentUserControl dstFileContentUserControl2 = DstFileContentUserControl;
			dstFileContentUserControl2.CancelLfsButtonClick = (EventHandler<EventArgs>)Delegate.Combine(dstFileContentUserControl2.CancelLfsButtonClick, (EventHandler<EventArgs>)delegate
			{
				_activeDstSmudgeJob?.Monitor.Cancel();
			});
			BinaryContentUserControl srcFileContentUserControl3 = SrcFileContentUserControl;
			srcFileContentUserControl3.SaveAsMenuItemClick = (EventHandler<EventArgs>)Delegate.Combine(srcFileContentUserControl3.SaveAsMenuItemClick, (EventHandler<EventArgs>)delegate
			{
				RepositoryUserControl repositoryUserControl2 = _repositoryUserControl;
				if (repositoryUserControl2 != null)
				{
					GitModule gitModule2 = _repositoryUserControl.GitModule;
					if (gitModule2 != null)
					{
						BinaryContent srcBinaryContent = _srcBinaryContent;
						if (srcBinaryContent != null)
						{
							string initialDirectory2 = RepositoryManager.Instance.DefaultSourceDir();
							if (OpenDialog.SelectFileSaveLocation(null, "Select location", initialDirectory2, Path.GetFileName(srcBinaryContent.Path), out var directory2))
							{
								_activeSrcSmudgeJob?.Monitor.Cancel();
								if (srcBinaryContent is LfsContent lfsContent2)
								{
									SrcFileContentUserControl.SetProgress(0.0);
									_activeSrcSmudgeJob = StartSmudgeLfsImageJob(lfsContent2.LfsPointer, gitModule2, delegate(JobMonitor monitor)
									{
										SrcFileContentUserControl.SetProgress(monitor.Progress.GetValueOrDefault());
									}, delegate(GitCommandResult<MemoryStream> imageDataResponse)
									{
										_activeSrcSmudgeJob = null;
										SrcFileContentUserControl.SetProgress(null);
										if (!imageDataResponse.Succeeded)
										{
											new ErrorWindow(repositoryUserControl2, imageDataResponse.Error).ShowDialog();
										}
										else
										{
											SaveFile(directory2, imageDataResponse.Result);
										}
									});
								}
								else if (srcBinaryContent is ImageContent imageContent2)
								{
									SaveFile(directory2, imageContent2.Data);
								}
							}
						}
					}
				}
			});
			BinaryContentUserControl dstFileContentUserControl3 = DstFileContentUserControl;
			dstFileContentUserControl3.SaveAsMenuItemClick = (EventHandler<EventArgs>)Delegate.Combine(dstFileContentUserControl3.SaveAsMenuItemClick, (EventHandler<EventArgs>)delegate
			{
				RepositoryUserControl repositoryUserControl = _repositoryUserControl;
				if (repositoryUserControl != null)
				{
					GitModule gitModule = _repositoryUserControl.GitModule;
					if (gitModule != null)
					{
						BinaryContent dstBinaryContent = _dstBinaryContent;
						if (dstBinaryContent != null)
						{
							string initialDirectory = RepositoryManager.Instance.DefaultSourceDir();
							if (OpenDialog.SelectFileSaveLocation(null, "Select location", initialDirectory, Path.GetFileName(dstBinaryContent.Path), out var directory))
							{
								_activeDstSmudgeJob?.Monitor.Cancel();
								if (dstBinaryContent is LfsContent lfsContent)
								{
									DstFileContentUserControl.SetProgress(0.0);
									_activeDstSmudgeJob = StartSmudgeLfsImageJob(lfsContent.LfsPointer, gitModule, delegate(JobMonitor monitor)
									{
										DstFileContentUserControl.SetProgress(monitor.Progress.GetValueOrDefault());
									}, delegate(GitCommandResult<MemoryStream> imageDataResponse)
									{
										_activeDstSmudgeJob = null;
										DstFileContentUserControl.SetProgress(null);
										if (!imageDataResponse.Succeeded)
										{
											new ErrorWindow(repositoryUserControl, imageDataResponse.Error).ShowDialog();
										}
										else
										{
											SaveFile(directory, imageDataResponse.Result);
										}
									});
								}
								else if (dstBinaryContent is ImageContent imageContent)
								{
									SaveFile(directory, imageContent.Data);
								}
							}
						}
					}
				}
			});
		}

		public void UpdateDiff(RepositoryUserControl repositoryUserControl, DiffContent diffContent, bool showTitle = true)
		{
			_repositoryUserControl = repositoryUserControl;
			_showTitle = showTitle;
			ChangedFile changedFile = diffContent.ChangedFile;
			// v3.4.1：存储 ChangedFile 和原始字节用于 Hex 视图
			_changedFile = changedFile;
			_hexSrcData = null;
			_hexDstData = null;
			if (_hexDiffView != null)
			{
				HexDiffViewContainer.Content = null;
				_hexDiffView = null;
			}
			if (diffContent is BinaryDiffContent binDiff)
			{
				_hexSrcData = binDiff.SrcData;
				_hexDstData = binDiff.DstData;
			}
			if (diffContent is BinaryDiffContent binaryDiffContent)
			{
				ImageContent srcContent = null;
				MemoryStream srcData = binaryDiffContent.SrcData;
				if (srcData != null)
				{
					srcContent = new ImageContent(changedFile.OldPath ?? changedFile.Path, changedFile.Tracked, srcData);
				}
				ImageContent dstContent = null;
				MemoryStream dstData = binaryDiffContent.DstData;
				if (dstData != null)
				{
					dstContent = new ImageContent(changedFile.Path, changedFile.Tracked, dstData);
				}
				UpdateContent(repositoryUserControl.GitModule, srcContent, dstContent, showTitle);
			}
			else if (diffContent is UnknownBinaryDiffContent unknownBinaryDiffContent)
			{
				BinaryContent srcContent2 = null;
				long? srcSize = unknownBinaryDiffContent.SrcSize;
				if (srcSize.HasValue)
				{
					long valueOrDefault = srcSize.GetValueOrDefault();
					srcContent2 = new BinaryContent(changedFile.OldPath ?? changedFile.Path, changedFile.Tracked, valueOrDefault);
				}
				BinaryContent dstContent2 = null;
				srcSize = unknownBinaryDiffContent.DstSize;
				if (srcSize.HasValue)
				{
					long valueOrDefault2 = srcSize.GetValueOrDefault();
					dstContent2 = new BinaryContent(changedFile.Path, changedFile.Tracked, valueOrDefault2);
				}
				UpdateContent(repositoryUserControl.GitModule, srcContent2, dstContent2, showTitle);
			}
			else if (diffContent is LfsDiffContent lfsDiffContent)
			{
				LfsContent srcContent3 = null;
				LfsPointer src = lfsDiffContent.Src;
				if (src != null)
				{
					srcContent3 = new LfsContent(changedFile.OldPath ?? changedFile.Path, changedFile.Tracked, src, lfsDiffContent.BinaryFileType);
				}
				LfsContent dstContent3 = null;
				LfsPointer dst = lfsDiffContent.Dst;
				if (dst != null)
				{
					dstContent3 = new LfsContent(changedFile.Path, changedFile.Tracked, dst, lfsDiffContent.BinaryFileType);
				}
				UpdateContent(repositoryUserControl.GitModule, srcContent3, dstContent3, showTitle);
			}
		}

		private void UpdateContent(GitModule gitModule, [Null] BinaryContent srcContent, [Null] BinaryContent dstContent, bool showTitle)
		{
			_srcBinaryContent = srcContent;
			_dstBinaryContent = dstContent;
			_srcImageData = null;
			_dstImageData = null;
			DiffImageSource = null;
			if (!SideBySideRadioButton.IsChecked.GetValueOrDefault())
			{
				SideBySideRadioButton.IsChecked = true;
			}
			FallbackUserControl.Hide();
			bool flag = false;
			if (srcContent != null && dstContent != null)
			{
				if (srcContent is ImageContent imageContent && dstContent is ImageContent imageContent2)
				{
					if (CanBeLfs(imageContent.Data))
					{
						LfsPointer lfsPointer = LfsPointer.Parse(Encoding.UTF8.GetString(imageContent.Data.ToArray()));
						if (lfsPointer != null)
						{
							_srcBinaryContent = new LfsContent(srcContent.Path, srcContent.IsTracked, lfsPointer, BinaryFileType.LfsImage);
						}
						else
						{
							_srcImageData = ImageData.Create(imageContent);
						}
					}
					else
					{
						_srcImageData = ImageData.Create(imageContent);
					}
					if (CanBeLfs(imageContent2.Data))
					{
						LfsPointer lfsPointer2 = LfsPointer.Parse(Encoding.UTF8.GetString(imageContent2.Data.ToArray()));
						if (lfsPointer2 != null)
						{
							_dstBinaryContent = new LfsContent(dstContent.Path, dstContent.IsTracked, lfsPointer2, BinaryFileType.LfsImage);
						}
						else
						{
							_dstImageData = ImageData.Create(imageContent2);
						}
					}
					else
					{
						_dstImageData = ImageData.Create(imageContent2);
					}
				}
				flag = true;
			}
			else if (srcContent != null)
			{
				Grid.SetColumnSpan(SrcFileContentUserControl, 2);
				SrcFileContentUserControl.Margin = new Thickness(10.0, 0.0, 10.0, 0.0);
				string statusLabel = (showTitle ? "removed" : null);
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				SrcFileContentUserControl.SetContent(srcContent, statusLabel, Theme.Diff.RemovedForegroundBrush);
				SrcFileContentUserControl.Show();
				DstFileContentUserControl.Collapse();
			}
			else if (dstContent != null)
			{
				Grid.SetColumn(DstFileContentUserControl, 0);
				Grid.SetColumnSpan(DstFileContentUserControl, 2);
				DstFileContentUserControl.Margin = new Thickness(10.0, 0.0, 10.0, 0.0);
				string statusLabel2 = (showTitle ? "created" : null);
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				DstFileContentUserControl.SetContent(dstContent, statusLabel2, Theme.Diff.AddedForegroundBrush, DiffImageSource);
				DstFileContentUserControl.Show();
				SrcFileContentUserControl.Collapse();
			}
			else
			{
				FallbackUserControl.Show();
			}
			if (flag)
			{
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				Grid.SetColumnSpan(SrcFileContentUserControl, 1);
				SrcFileContentUserControl.Margin = new Thickness(10.0, 0.0, 5.0, 0.0);
				string statusLabel3 = (showTitle ? "old" : null);
				SrcFileContentUserControl.SetContent(_srcBinaryContent, statusLabel3, Theme.Diff.RemovedForegroundBrush);
				Grid.SetColumn(DstFileContentUserControl, 1);
				Grid.SetColumnSpan(DstFileContentUserControl, 1);
				DstFileContentUserControl.Margin = new Thickness(5.0, 0.0, 10.0, 0.0);
				string statusLabel4 = (showTitle ? "new" : null);
				DstFileContentUserControl.SetContent(_dstBinaryContent, statusLabel4, Theme.Diff.AddedForegroundBrush, DiffImageSource);
				SrcFileContentUserControl.Show();
				DstFileContentUserControl.Show();
			}
			if (_srcBinaryContent is LfsContent { BinaryFileType: BinaryFileType.LfsImage } lfsContent)
			{
				GitCommandResult<MemoryStream> gitCommandResult = new GitLfsGetCachedFileGitCommand().Execute(gitModule.CommonGitDir, lfsContent.LfsPointer.Sha256String);
				if (gitCommandResult.Succeeded)
				{
					MemoryStream result = gitCommandResult.Result;
					if (Path.GetExtension(lfsContent.Path) == ".tga" && result != null)
					{
						GitCommandResult<MemoryStream> gitCommandResult2 = DecodeImageData(result.ToArray());
						if (gitCommandResult2.Succeeded)
						{
							result = gitCommandResult2.Result;
						}
						else
						{
							Log.Error(gitCommandResult2.Error.FriendlyDescription);
						}
					}
					_srcImageData = ImageData.Create(result, isLfs: true, lfsContent.IsTracked);
					_hexSrcData = result; // v3.4.1：存原始字节供 Hex 视图
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				SrcFileContentUserControl.SetLfsImageData(result);
				}
			}
			if (_dstBinaryContent is LfsContent { BinaryFileType: BinaryFileType.LfsImage } lfsContent2)
			{
				GitCommandResult<MemoryStream> gitCommandResult3 = new GitLfsGetCachedFileGitCommand().Execute(gitModule.CommonGitDir, lfsContent2.LfsPointer.Sha256String);
				if (gitCommandResult3.Succeeded)
				{
					MemoryStream result2 = gitCommandResult3.Result;
					if (Path.GetExtension(lfsContent2.Path) == ".tga" && result2 != null)
					{
						GitCommandResult<MemoryStream> gitCommandResult4 = DecodeImageData(result2.ToArray());
						if (gitCommandResult4.Succeeded)
						{
							result2 = gitCommandResult4.Result;
						}
						else
						{
							Log.Error(gitCommandResult4.Error.FriendlyDescription);
						}
					}
					_dstImageData = ImageData.Create(result2, isLfs: true, lfsContent2.IsTracked);
					_hexDstData = result2; // v3.4.1：存原始字节供 Hex 视图
				DiffImageSource = GetDiffImage(_srcImageData, _dstImageData);
				DstFileContentUserControl.SetLfsImageData(result2, DiffImageSource);
				}
			}
			RefreshViewModes();
		}

		public static GitCommandResult<MemoryStream> DecodeImageData(byte[] data)
		{
			return BtRequest.Run(() => default(BtDecodeImageResult), delegate(ref BtDecodeImageResult x)
			{
				return Bt.bt_decode_image(data, data.Length, ref x);
			}, delegate(ref BtDecodeImageResult x)
			{
				return GitCommandResult<MemoryStream>.Success(new MemoryStream(x.data.GetByteArray(x.data_len)));
			}, delegate(ref BtDecodeImageResult x)
			{
				Bt.bt_release_decode_image(ref x);
			});
		}

		private bool CanBeLfs(MemoryStream memoryStream)
		{
			if (memoryStream.Length <= 120 || memoryStream.Length >= 1024)
			{
				return false;
			}
			return true;
		}

		private void ImageDiffSelectedItem_Changed(object sender, RoutedEventArgs e)
		{
			if (SideBySideRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.Show();
				DstFileContentUserControl.Show();
				SwipeImageDiffView.Hide();
				OnionSkinImageDiffView.Hide();
				HexDiffViewContainer.Collapse();
			}
			else if (SwipeRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.Hide();
				DstFileContentUserControl.Hide();
				OnionSkinImageDiffView.Hide();
				HexDiffViewContainer.Collapse();
				SwipeImageDiffView.Show();
				SwipeImageDiffView.Refresh(_srcImageData, _dstImageData, DiffImageSource, _showTitle);
			}
			else if (OnionSkinRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.Hide();
				DstFileContentUserControl.Hide();
				SwipeImageDiffView.Hide();
				HexDiffViewContainer.Collapse();
				OnionSkinImageDiffView.Show();
				OnionSkinImageDiffView.Refresh(_srcImageData, _dstImageData, DiffImageSource, _showTitle);
			}
			else if (HexRadioButton.IsChecked.GetValueOrDefault())
			{
				// v3.4.1：Hex 视图 — 显示原始字节的 side-by-side 十六进制比较
				SrcFileContentUserControl.Hide();
				DstFileContentUserControl.Hide();
				SwipeImageDiffView.Hide();
				OnionSkinImageDiffView.Hide();
				ShowHexDiffView();
				HexDiffViewContainer.Show();
			}
		}

		/// <summary>v3.4.1：懒创建 HexDiffUserControl 并加载原始字节。</summary>
		private void ShowHexDiffView()
		{
			if (_hexDiffView == null)
			{
				_hexDiffView = new HexDiffUserControl();
				HexDiffViewContainer.Content = _hexDiffView;
			}
			if (_changedFile != null && (_hexSrcData != null || _hexDstData != null))
			{
				HexDiffContent hexContent = new HexDiffContent(_changedFile, _hexSrcData, _hexDstData);
				_hexDiffView.SetContent(hexContent);
			}
		}

		private void RefreshViewModes()
		{
			if (_srcImageData != null && _dstImageData != null)
			{
				ViewModeButtonsContainer.Show();
			}
			else
			{
				ViewModeButtonsContainer.Hide();
			}
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			SrcFileContentUserControl.ApplyLocalization();
			DstFileContentUserControl.ApplyLocalization();
		}

		private Job StartSmudgeLfsImageJob(LfsPointer lfsPointer, GitModule gitModule, Action<JobMonitor> progressCallback, Action<GitCommandResult<MemoryStream>> completedCallback)
		{
			return _jobQueue.Add(PreferencesLocalization.Translate("Smudge LFS image", ForkPlusSettings.Default.UiLanguage), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					monitor.SetProgressAction(delegate
					{
						base.Dispatcher.Async(delegate
						{
							progressCallback(monitor);
						});
					});
					GitCommandResult<MemoryStream> imageDataResponse = new SmudgeLfsFileCommand().Execute(gitModule, lfsPointer, monitor);
					monitor.SetProgressAction(null);
					base.Dispatcher.Async(delegate
					{
						if (!monitor.IsCanceled)
						{
							completedCallback(imageDataResponse);
						}
					});
				}
			});
		}

		[Null]
		public static Bitmap CreateBitmapSource(MemoryStream stream)
		{
			// 阶段 4.5：WPF BitmapImage+BeginInit/EndInit+StreamSource+FormatConvertedBitmap(Pbgra32)
			// → Avalonia new Bitmap(stream)。Avalonia Bitmap 构造时自动解码并归一化格式，不可变无需 Freeze。
			// 参考 AvatarManager.cs（new Bitmap(memoryStream)）与 IconTools.cs（new Bitmap(ms)）。
			try
			{
				if (stream == null)
				{
					return null;
				}
				stream.Position = 0L;
				return new Bitmap(stream);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create Bitmap", ex);
				return null;
			}
		}

		private void SaveFile(string filePath, [Null] MemoryStream data)
		{
			byte[] array = data?.ToArray();
			if (array == null)
			{
				return;
			}
			try
			{
				File.WriteAllBytes(filePath, array);
			}
			catch (Exception ex)
			{
				Log.Error($"Cannot save file: {ex}");
				new ErrorWindow(ex.ToString()).ShowDialog();
			}
		}

		[Null]
		private Bitmap GetDiffImage([Null] ImageData lhsImageData, [Null] ImageData rhsImageData)
		{
			// 阶段 4.5：WPF BitmapSource.CopyPixels + FormatConvertedBitmap(Bgra32) + BitmapSource.Create
			// → System.Drawing.Bitmap.LockBits(Format32bppArgb) 读取像素 + System.Drawing.Bitmap 写回 + PNG 编码 + new Bitmap(ms)。
			// Avalonia 不可变 Bitmap 不暴露 CopyPixels/Create；System.Drawing 已在项目内使用（参考 IconTools）。
			// Format32bppArgb 在 little-endian 内存布局为 BGRA，与 WPF Bgra32 字节序一致，像素比较逻辑无需调整。
			byte[] lhsRaw = lhsImageData?.RawBytes;
			byte[] rhsRaw = rhsImageData?.RawBytes;
			if (lhsRaw == null || rhsRaw == null)
			{
				return null;
			}
			try
			{
				using (SD.Bitmap lhsBmp = new SD.Bitmap(new MemoryStream(lhsRaw)))
				using (SD.Bitmap rhsBmp = new SD.Bitmap(new MemoryStream(rhsRaw)))
				{
					if (lhsBmp.Width != rhsBmp.Width || lhsBmp.Height != rhsBmp.Height)
					{
						return null;
					}
					int pixelWidth = lhsBmp.Width;
					int pixelHeight = lhsBmp.Height;
					byte[] lhsPixels = ExtractBgraPixels(lhsBmp);
					byte[] rhsPixels = ExtractBgraPixels(rhsBmp);
					int num = 4; // 32bpp = 4 bytes per pixel
					int num2 = pixelWidth * num;
					byte[] array3 = new byte[pixelHeight * num2];
					for (int i = 0; i < pixelHeight; i++)
					{
						for (int j = 0; j < pixelWidth; j++)
						{
							int offset = i * num2 + j * num;
							byte lhsB = lhsPixels[offset];
							byte lhsG = lhsPixels[offset + 1];
							byte lhsR = lhsPixels[offset + 2];
							byte lhsA = lhsPixels[offset + 3];
							byte rhsB = rhsPixels[offset];
							byte rhsG = rhsPixels[offset + 1];
							byte rhsR = rhsPixels[offset + 2];
							byte rhsA = rhsPixels[offset + 3];
							if (!SamePixel(lhsR, rhsR) || !SamePixel(lhsG, rhsG) || !SamePixel(lhsB, rhsB) || !SamePixel(lhsA, rhsA))
							{
								array3[offset] = byte.MaxValue;       // B = 255
								array3[offset + 1] = 0;               // G = 0
								array3[offset + 2] = byte.MaxValue;   // R = 255
								array3[offset + 3] = byte.MaxValue;   // A = 255
							}
						}
					}
					using (SD.Bitmap diffBmp = new SD.Bitmap(pixelWidth, pixelHeight, SDI.PixelFormat.Format32bppArgb))
					{
						SD.Rectangle rect = new SD.Rectangle(0, 0, pixelWidth, pixelHeight);
						SDI.BitmapData data = diffBmp.LockBits(rect, SDI.ImageLockMode.WriteOnly, SDI.PixelFormat.Format32bppArgb);
						try
						{
							Marshal.Copy(array3, 0, data.Scan0, array3.Length);
						}
						finally
						{
							diffBmp.UnlockBits(data);
						}
						using (MemoryStream ms = new MemoryStream())
						{
							diffBmp.Save(ms, SDI.ImageFormat.Png);
							ms.Position = 0;
							return new Bitmap(ms);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create diff image", ex);
				return null;
			}
		}

		/// <summary>从 System.Drawing.Bitmap 提取 BGRA 像素数据（Format32bppArgb，little-endian 内存布局与 WPF Bgra32 一致）。</summary>
		private static byte[] ExtractBgraPixels(SD.Bitmap bmp)
		{
			SD.Rectangle rect = new SD.Rectangle(0, 0, bmp.Width, bmp.Height);
			SDI.BitmapData data = bmp.LockBits(rect, SDI.ImageLockMode.ReadOnly, SDI.PixelFormat.Format32bppArgb);
			try
			{
				byte[] pixels = new byte[data.Stride * bmp.Height];
				Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
				return pixels;
			}
			finally
			{
				bmp.UnlockBits(data);
			}
		}

		private bool SamePixel(byte lhs, byte rhs)
		{
			return Math.Abs(lhs - rhs) < 5;
		}

	}
}
