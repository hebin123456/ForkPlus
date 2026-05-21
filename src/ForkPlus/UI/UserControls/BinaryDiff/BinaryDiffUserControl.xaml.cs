using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	public partial class BinaryDiffUserControl : UserControl
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
		private BitmapSource _diffImageSource;

		[Null]
		public BitmapSource DiffImageSource
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
			}
			else if (SwipeRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.Hide();
				DstFileContentUserControl.Hide();
				OnionSkinImageDiffView.Hide();
				SwipeImageDiffView.Show();
				SwipeImageDiffView.Refresh(_srcImageData, _dstImageData, DiffImageSource, _showTitle);
			}
			else if (OnionSkinRadioButton.IsChecked.GetValueOrDefault())
			{
				SrcFileContentUserControl.Hide();
				DstFileContentUserControl.Hide();
				SwipeImageDiffView.Hide();
				OnionSkinImageDiffView.Show();
				OnionSkinImageDiffView.Refresh(_srcImageData, _dstImageData, DiffImageSource, _showTitle);
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
		public static BitmapSource CreateBitmapSource(MemoryStream stream)
		{
			try
			{
				BitmapImage bitmapImage = new BitmapImage();
				stream.Position = 0L;
				bitmapImage.BeginInit();
				bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.UriSource = null;
				bitmapImage.StreamSource = stream;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
				if (bitmapImage.Format != PixelFormats.Pbgra32)
				{
					FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(bitmapImage, PixelFormats.Pbgra32, null, 0.0);
					formatConvertedBitmap.Freeze();
					return formatConvertedBitmap;
				}
				return bitmapImage;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create BitmapSource", ex);
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
		private BitmapSource GetDiffImage([Null] ImageData lhsImageData, [Null] ImageData rhsImageData)
		{
			BitmapSource bitmapSource = lhsImageData?.ImageSource;
			if (bitmapSource != null)
			{
				BitmapSource bitmapSource2 = rhsImageData?.ImageSource;
				if (bitmapSource2 != null && bitmapSource.PixelWidth == bitmapSource2.PixelWidth && bitmapSource.PixelHeight == bitmapSource2.PixelHeight)
				{
					if (bitmapSource.Format != PixelFormats.Bgra32)
					{
						bitmapSource = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgra32, bitmapSource.Palette, 0.0);
					}
					if (bitmapSource2.Format != PixelFormats.Bgra32)
					{
						bitmapSource2 = new FormatConvertedBitmap(bitmapSource2, PixelFormats.Bgra32, bitmapSource2.Palette, 0.0);
					}
					int num = bitmapSource.Format.BitsPerPixel / 8;
					int num2 = bitmapSource.PixelWidth * num;
					byte[] array = new byte[bitmapSource.PixelHeight * num2];
					bitmapSource.CopyPixels(array, num2, 0);
					byte[] array2 = new byte[bitmapSource2.PixelHeight * num2];
					bitmapSource2.CopyPixels(array2, num2, 0);
					byte[] array3 = new byte[bitmapSource2.PixelHeight * num2];
					int pixelWidth = bitmapSource.PixelWidth;
					int pixelHeight = bitmapSource.PixelHeight;
					for (int i = 0; i < pixelHeight; i++)
					{
						for (int j = 0; j < pixelWidth; j++)
						{
							int num3 = i * pixelWidth * num + j * num;
							int num4 = i * pixelWidth * num + j * num;
							byte lhs = array[num3];
							byte lhs2 = array[num3 + 1];
							byte lhs3 = array[num3 + 2];
							byte lhs4 = array[num3 + 3];
							byte rhs = array2[num4];
							byte rhs2 = array2[num4 + 1];
							byte rhs3 = array2[num4 + 2];
							byte rhs4 = array2[num4 + 3];
							if (!SamePixel(lhs3, rhs3) || !SamePixel(lhs2, rhs2) || !SamePixel(lhs, rhs) || !SamePixel(lhs4, rhs4))
							{
								array3[num4] = byte.MaxValue;
								array3[num4 + 1] = 0;
								array3[num4 + 2] = byte.MaxValue;
								array3[num4 + 3] = byte.MaxValue;
							}
						}
					}
					return BitmapSource.Create(pixelWidth, pixelHeight, bitmapSource2.DpiX, bitmapSource2.DpiY, PixelFormats.Bgra32, bitmapSource2.Palette, array3, num2);
				}
			}
			return null;
		}

		private bool SamePixel(byte lhs, byte rhs)
		{
			return Math.Abs(lhs - rhs) < 5;
		}

	}
}
