using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus.Git;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

// AvatarManager 使用 WebClient 的事件回调模式（DownloadDataCompleted）来下载头像，
// 改写为 HttpClient + Task.Run 涉及异步逻辑重写，风险较大。.NET 10 上 WebClient
// 已过时（SYSLIB0014）但仍可用，本地静默该警告，待后续整体重构时统一迁移到 HttpClient。
#pragma warning disable SYSLIB0014

namespace ForkPlus.UI.Controls
{
	public class AvatarManager
	{
		private static readonly string GravatarUrlFormat = "https://en.gravatar.com/avatar/{0}?d=404";

		private static readonly Uri GitHubEmailLogo = new Uri("avares://ForkPlus/Assets/GitHubAvatar.png");

		private static readonly string GitHubEmail = "noreply@github.com";

		private static readonly string AnonymousEmailSuffix = "@users.noreply.github.com";

		private static readonly Regex AnonymousEmailRegex = new Regex("^(?:(\\d+)\\+)?(.+?)@users\\.noreply\\.github\\.com$");

		private static readonly Size AvatarSize = new Size(42.0, 42.0);

		private static readonly double Radius = 4.0;

		private static readonly object Padlock = new object();

		private readonly LruCache<string, global::Avalonia.Media.IImage> _avatarCache = new LruCache<string, global::Avalonia.Media.IImage>(128);

		private readonly Dictionary<string, List<AvatarImage>> _activeRequests = new Dictionary<string, List<AvatarImage>>();

		private readonly LruCache<string, global::Avalonia.Media.IImage> _urlAvatarCache = new LruCache<string, global::Avalonia.Media.IImage>(128);

		private readonly Dictionary<string, List<AvatarImage>> _urlActiveRequests = new Dictionary<string, List<AvatarImage>>();

		private static AvatarManager _default;

		// Migration note：Avalonia 的 Typeface 是 struct（WPF 是 class），改为可空字段并惰性赋值。
		private static Typeface? _typeface = null;

		private static LinearGradientBrush[] _avatarGradients = null;

		private LruCache<string, global::Avalonia.Media.IImage> AvatarCache => _avatarCache;

		private Dictionary<string, List<AvatarImage>> ActiveRequests => _activeRequests;

		private LruCache<string, global::Avalonia.Media.IImage> UrlAvatarCache => _urlAvatarCache;

		private Dictionary<string, List<AvatarImage>> UrlActiveRequests => _urlActiveRequests;

		public static AvatarManager Default
		{
			get
			{
				lock (Padlock)
				{
					if (_default == null)
					{
						_default = new AvatarManager();
					}
					return _default;
				}
			}
		}

		private static Typeface Typeface
		{
			get
			{
				if (_typeface == null)
				{
					_typeface = new Typeface(new FontFamily("Segoe UI, Malgun Gothic, Yu Gothic"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
				}
				return _typeface.Value;
			}
		}

		private static LinearGradientBrush[] AvatarGradients
		{
			get
			{
				if (_avatarGradients == null)
				{
					_avatarGradients = CreateAvatarGradients();
				}
				return _avatarGradients;
			}
		}

		public void RequestAvatar(AvatarImage avatarImage, UserIdentity userIdentity)
		{
			string text = userIdentity.Email.ToLower();
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				avatarImage.SetImage(GenerateAvatar(userIdentity.Name, text), avatarImage.UserIdentity);
				return;
			}
			if (AvatarCache.TryGet(text, out var value))
			{
				avatarImage.SetImage(value, avatarImage.UserIdentity);
				return;
			}
			global::Avalonia.Media.IImage imageSource = GenerateAvatar(userIdentity.Name, text);
			avatarImage.SetImage(imageSource, avatarImage.UserIdentity);
			DownloadAvatar(text, avatarImage, imageSource);
		}

		public void RequestAvatar(AvatarImage avatarImage, string url)
		{
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				avatarImage.Source = null;
				return;
			}
			if (UrlAvatarCache.TryGet(url, out var value))
			{
				avatarImage.SetImage(value, avatarImage.UserIdentity);
			}
			else
			{
				DownloadAvatar(url, avatarImage);
			}
		}

		private void DownloadAvatar(string email, AvatarImage imageControl, global::Avalonia.Media.IImage defaultAvatar)
		{
			if (ActiveRequests.TryGetValue(email, out var value))
			{
				value.Add(imageControl);
				return;
			}
			ActiveRequests[email] = new List<AvatarImage> { imageControl };
			WebClient client = new WebClient();
			client.DownloadDataCompleted += delegate(object sender, DownloadDataCompletedEventArgs args)
			{
				client.Dispose();
				Dispatcher dispatcher = Application.Current?.Dispatcher;
				if (dispatcher != null)
				{
					if (args.Error != null)
					{
						HttpWebResponse obj = (args.Error as WebException)?.Response as HttpWebResponse;
						if (obj == null || obj.StatusCode != HttpStatusCode.NotFound)
						{
							Log.Warn("Avatar downloading failed with error: '" + args.Error.Message + "'");
						}
						dispatcher.Invoke(delegate
						{
							ActiveRequests.Remove(email);
							AvatarCache.Put(email, defaultAvatar);
						});
					}
					else
					{
						global::Avalonia.Media.IImage downloadedImage = null;
						try
						{
							downloadedImage = LoadImage(args.Result);
						}
						catch (NotSupportedException arg)
						{
							Log.Error($"Image decoding failed: '{arg}'");
							dispatcher.Invoke(delegate
							{
								ActiveRequests.Remove(email);
								AvatarCache.Put(email, defaultAvatar);
							});
							return;
						}
						dispatcher.Invoke(delegate
						{
							if (ActiveRequests.TryGetValue(email, out var value2))
							{
								foreach (AvatarImage item in value2)
								{
									item.SetImage(downloadedImage, imageControl.UserIdentity);
								}
							}
							ActiveRequests.Remove(email);
							AvatarCache.Put(email, downloadedImage);
						});
					}
				}
			};
			Task.Run(delegate
			{
				Uri uri = GitHubUri(email);
				if ((object)uri != null)
				{
					client.DownloadDataAsync(uri);
				}
				else
				{
					client.DownloadDataAsync(GravatarUri(email));
				}
			});
		}

		private void DownloadAvatar(string url, AvatarImage imageControl)
		{
			if (UrlActiveRequests.TryGetValue(url, out var value))
			{
				value.Add(imageControl);
				return;
			}
			UrlActiveRequests[url] = new List<AvatarImage> { imageControl };
			WebClient client = new WebClient();
			client.DownloadDataCompleted += delegate(object sender, DownloadDataCompletedEventArgs args)
			{
				client.Dispose();
				Dispatcher dispatcher = Application.Current?.Dispatcher;
				if (dispatcher != null)
				{
					if (args.Error != null)
					{
						HttpWebResponse obj = (args.Error as WebException)?.Response as HttpWebResponse;
						if (obj == null || obj.StatusCode != HttpStatusCode.NotFound)
						{
							Log.Warn("Avatar downloading failed with error: '" + args.Error.Message + "'");
						}
						dispatcher.Invoke(delegate
						{
							UrlActiveRequests.Remove(url);
							UrlAvatarCache.Put(url, null);
						});
					}
					else
					{
						global::Avalonia.Media.IImage downloadedImage = null;
						try
						{
							downloadedImage = LoadImage(args.Result);
						}
						catch (NotSupportedException arg)
						{
							Log.Error($"Image decoding failed: '{arg}'");
							dispatcher.Invoke(delegate
							{
								UrlActiveRequests.Remove(url);
								UrlAvatarCache.Put(url, null);
							});
							return;
						}
						dispatcher.Invoke(delegate
						{
							if (UrlActiveRequests.TryGetValue(url, out var value2))
							{
								foreach (AvatarImage item in value2)
								{
									item.SetImage(downloadedImage, imageControl.UserIdentity);
								}
							}
							UrlActiveRequests.Remove(url);
							UrlAvatarCache.Put(url, downloadedImage);
						});
					}
				}
			};
			Task.Run(delegate
			{
				client.DownloadDataAsync(new Uri(url));
			});
		}

		private static global::Avalonia.Media.IImage GenerateAvatar(string username, string email)
		{
			if (email == GitHubEmail)
			{
				global::Avalonia.Media.Imaging.Bitmap bitmapImage = new global::Avalonia.Media.Imaging.Bitmap(global::Avalonia.Platform.AssetLoader.Open(GitHubEmailLogo));
				return bitmapImage;
			}
			// Migration note：WPF 用 DrawingVisual.RenderOpen 生成 DrawingImage；Avalonia 无 DrawingVisual/RenderOpen，
			// 改为离屏渲染到 RenderTargetBitmap（其本身实现 IImage，可直接作为 Image.Source，与原 DrawingImage 等价）。
			// 注意：RenderTargetBitmap 不能 Dispose（返回后仍要作为图像源使用）。
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(new PixelSize((int)AvatarSize.Width, (int)AvatarSize.Height), new Vector(96.0, 96.0));
			using (DrawingContext drawingContext = renderTargetBitmap.CreateDrawingContext())
			{
				LinearGradientBrush backgroundBrush = GetBackgroundBrush(email);
				drawingContext.DrawRectangle(backgroundBrush, null, new Rect(AvatarSize), Radius, Radius);
				FormattedText formattedText = CreateFormattedAbbreviatureText(username);
				double x = (AvatarSize.Width - formattedText.Width) / 2.0;
				double y = (AvatarSize.Height - formattedText.Height) / 2.0 - 1.0;
				drawingContext.DrawText(formattedText, new Point(x, y));
			}
			return renderTargetBitmap;
		}

		private static global::Avalonia.Media.IImage RoundCorners(global::Avalonia.Media.Imaging.Bitmap image)
		{
			// Migration note：WPF DrawingVisual + PushClip(RectangleGeometry)/Pop；Avalonia 改为 RenderTargetBitmap
			// + PushClip(RoundedRect) 离屏合成圆角头像，视觉等价（Push 系列返回 IDisposable，替代 WPF 的 Pop）。
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(new PixelSize((int)AvatarSize.Width, (int)AvatarSize.Height), new Vector(96.0, 96.0));
			using (DrawingContext drawingContext = renderTargetBitmap.CreateDrawingContext())
			{
				using (drawingContext.PushClip(new RoundedRect(new Rect(AvatarSize), Radius)))
				{
					drawingContext.DrawImage(image, new Rect(AvatarSize));
				}
			}
			return renderTargetBitmap;
		}

		private static Uri GravatarUri(string email)
		{
			using MD5 mD = MD5.Create();
			byte[] array = mD.ComputeHash(Encoding.UTF8.GetBytes(email));
			StringBuilder stringBuilder = new StringBuilder(32);
			for (int i = 0; i < array.Length; i++)
			{
				stringBuilder.Append(array[i].ToString("x2"));
			}
			return new Uri(string.Format(GravatarUrlFormat, stringBuilder.ToString()));
		}

		[Null]
		private static Uri GitHubUri(string email)
		{
			if (email.EndsWith(AnonymousEmailSuffix))
			{
				string text = AnonymousGitHubUsername(email);
				if (text != null)
				{
					return new Uri("https://avatars.githubusercontent.com/" + text);
				}
			}
			return null;
		}

		[Null]
		private static string AnonymousGitHubUsername(string email)
		{
			Match match = AnonymousEmailRegex.Match(email);
			if (match.Groups.Count < 3)
			{
				return null;
			}
			return match.Groups[2].Value;
		}

		private static global::Avalonia.Media.IImage LoadImage(byte[] imageData)
		{
			if (imageData == null || imageData.Length == 0)
			{
				return null;
			}
			// Migration note：WPF BitmapImage + CreateOptions(PreservePixelFormat)/CacheOption(OnLoad)/UriSource/StreamSource；
			// Avalonia 无这些属性，直接用 Bitmap(Stream) 同步解码（语义等价于 BitmapCacheOption.OnLoad）。
			using (MemoryStream memoryStream = new MemoryStream(imageData))
			{
				memoryStream.Position = 0L;
				global::Avalonia.Media.Imaging.Bitmap bitmapImage = new global::Avalonia.Media.Imaging.Bitmap(memoryStream);
				global::Avalonia.Media.IImage imageSource = RoundCorners(bitmapImage);
				bitmapImage.Dispose();
				return imageSource;
			}
		}

		private static FormattedText CreateFormattedAbbreviatureText(string username)
		{
			// Migration note：WPF FormattedText 7 参构造（末参 pixelsPerDip）；Avalonia 12 为 6 参
			// (text, culture, flowDirection, typeface, emSize, foregroundBrush)，DPI 缩放由 RenderTargetBitmap 的 dpi 承担。
			return new FormattedText(CreateAbbreviatureText(username), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, 22.0, Brushes.White);
		}

		private static string CreateAbbreviatureText(string username)
		{
			string[] array = username.Split(Consts.Chars.Space);
			string[] array2 = array.Where((string x) => StringStartsFromCapital(username)).ToArray();
			if (array2.Length >= 2)
			{
				return CreateAbbreviature(array2[0], array2[array2.Length - 1]);
			}
			if (array.Length > 1)
			{
				return CreateAbbreviature(array[0], array[1]);
			}
			if (username.Length > 0)
			{
				return $"{username[0]}";
			}
			return "?";
		}

		private static string CreateAbbreviature(string first, string last)
		{
			if (first.Length <= 0 || last.Length <= 0)
			{
				return "?";
			}
			return $"{first[0]}{last[0]}";
		}

		private static bool StringStartsFromCapital(string username)
		{
			if (username.Length <= 0)
			{
				return false;
			}
			return char.IsUpper(username[0]);
		}

		private static LinearGradientBrush GetBackgroundBrush(string email)
		{
			long num = (uint)email.GetHashCode() % AvatarGradients.Length;
			return AvatarGradients[num];
		}

		private static LinearGradientBrush[] CreateAvatarGradients()
		{
			// Migration note：WPF LinearGradientBrush(Color, Color, double angle=90°即自上而下)；
			// Avalonia 无该构造，用相对坐标 StartPoint(0,0)→EndPoint(0,1) 表达同样的自上而下渐变。
			return new LinearGradientBrush[5]
			{
				CreateLinearGradient(Color.FromRgb(55, 159, 239), Color.FromRgb(117, 212, 250)),
				CreateLinearGradient(Color.FromRgb(210, 114, 232), Color.FromRgb(223, 163, 241)),
				CreateLinearGradient(Color.FromRgb(249, 169, 104), Color.FromRgb(251, 203, 120)),
				CreateLinearGradient(Color.FromRgb(250, 84, 107), Color.FromRgb(249, 137, 99)),
				CreateLinearGradient(Color.FromRgb(88, 202, 107), Color.FromRgb(170, 220, 145))
			};
		}

		private static LinearGradientBrush CreateLinearGradient(Color startColor, Color endColor)
		{
			return new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0.0, 0.0, RelativeUnit.Relative),
				EndPoint = new RelativePoint(0.0, 1.0, RelativeUnit.Relative),
				GradientStops =
				{
					new GradientStop(startColor, 0.0),
					new GradientStop(endColor, 1.0)
				}
			};
		}
	}
}

