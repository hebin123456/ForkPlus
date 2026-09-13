using System;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace ForkPlus.AutoUpdater
{
	/// <summary>
	/// 更新包下载：HttpClient 流式下载到临时 zip 文件，回调字节级进度，
	/// 带重试（网络抖动 / GitHub release CDN 偶发中断）。先走系统代理默认
	/// （与主程序 UpdateChecker 的直连优先相反：下载大文件走代理更稳，
	/// 且检测阶段已经证明直连可达与否——此处从简，HttpClient 默认 UseProxy=true
	/// 即跟随系统代理，无代理环境同样直连可达）。
	/// </summary>
	internal sealed class UpdateDownloader
	{
		/// <summary>下载进度节流：两次回调之间的最小间隔（避免高频管道写压垮 UI）。</summary>
		internal static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(150);

		private readonly string _url;

		private readonly string _destinationZipPath;

		private readonly long _minimumBytes;

		public UpdateDownloader(string url, string destinationZipPath, long minimumBytes)
		{
			_url = url;
			_destinationZipPath = destinationZipPath;
			_minimumBytes = minimumBytes;
		}

		/// <summary>下载完成后的 zip 文件路径。</summary>
		public string ZipPath => _destinationZipPath;

		/// <summary>
		/// 执行下载。onProgress(received, total)：total 为 -1 时服务器未给 Content-Length
		///（进度条转不确定模式）。成功返回 true 并把文件落在 ZipPath。
		/// </summary>
		public bool Download(Action<long, long> onProgress, CancellationToken cancellation, int maxAttempts)
		{
			Exception lastError = null;
			for (int attempt = 1; attempt <= Math.Max(1, maxAttempts); attempt++)
			{
				if (cancellation.IsCancellationRequested)
				{
					return false;
				}
				try
				{
					if (TryDownloadOnce(onProgress, cancellation))
					{
						return true;
					}
					// onProgress 后被取消：不是失败，直接结束
					return false;
				}
				catch (OperationCanceledException)
				{
					return false;
				}
				catch (Exception ex)
				{
					lastError = ex;
					// 抖动退避后再试（500ms / 1000ms / ...），取消立即退出
					if (cancellation.IsCancellationRequested)
					{
						return false;
					}
					Thread.Sleep(Math.Min(500 * attempt, 5000));
				}
			}
			throw new InvalidOperationException("Download failed after " + Math.Max(1, maxAttempts) + " attempts: " + lastError?.Message, lastError);
		}

		private bool TryDownloadOnce(Action<long, long> onProgress, CancellationToken cancellation)
		{
			using (HttpClient client = new HttpClient())
			{
				client.Timeout = TimeSpan.FromMinutes(10);
				using (HttpResponseMessage response = client.GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, cancellation).GetAwaiter().GetResult())
				{
					if (!response.IsSuccessStatusCode)
					{
						throw new HttpRequestException("HTTP " + (int)response.StatusCode + " " + response.ReasonPhrase);
					}
					long total = response.Content.Headers.ContentLength ?? -1L;
					if (total >= 0 && total < _minimumBytes)
					{
						// 门户劫持/错误页（几百字节 HTML）当 zip 下载会一路走到解压才失败，提前拦
						throw new HttpRequestException("Downloaded payload too small (" + total + " bytes), expected at least " + _minimumBytes);
					}
					string directory = Path.GetDirectoryName(_destinationZipPath);
					if (!string.IsNullOrEmpty(directory))
					{
						Directory.CreateDirectory(directory);
					}
					using (Stream source = response.Content.ReadAsStream(cancellation))
					using (FileStream target = new FileStream(_destinationZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
					{
						byte[] buffer = new byte[81920];
						long received = 0L;
						DateTime nextReportAt = DateTime.UtcNow; // 首块立即上报
						int read;
						while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
						{
							cancellation.ThrowIfCancellationRequested();
							target.Write(buffer, 0, read);
							received += read;
							DateTime now = DateTime.UtcNow;
							if (now >= nextReportAt)
							{
								nextReportAt = now + ProgressInterval;
								onProgress?.Invoke(received, total);
							}
						}
						onProgress?.Invoke(received, total);
						if (received < _minimumBytes)
						{
							throw new HttpRequestException("Download incomplete (" + received + " bytes), expected at least " + _minimumBytes);
						}
						return true;
					}
				}
			}
		}
	}
}
