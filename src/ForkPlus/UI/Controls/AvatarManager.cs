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
using Avalonia.Platform;
using Avalonia.Threading;
using ForkPlus.Git;

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

		private readonly LruCache<string, IImage> _avatarCache = new LruCache<string, IImage>(128);

		private readonly Dictionary<string, List<AvatarImage>> _activeRequests = new Dictionary<string, List<AvatarImage>>();

		private readonly LruCache<string, IImage> _urlAvatarCache = new LruCache<string, IImage>(128);

		private readonly Dictionary<string, List<AvatarImage>> _urlActiveRequests = new Dictionary<string, List<AvatarImage>>();

		private static AvatarManager _default;

		private static Typeface _typeface = null;

		private static LinearGradientBrush[] _avatarGradients = null;

		private LruCache<string, IImage> AvatarCache => _avatarCache;

		private Dictionary<string, List<AvatarImage>> ActiveRequests => _activeRequests;

		private LruCache<string, IImage> UrlAvatarCache => _urlAvatarCache;

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
					// 阶段 4.5：WPF Typeface(FontFamily, FontStyle, FontWeight, FontStretch) → Avalonia Typeface(FontFamily, FontStyle, FontWeight)。
					_typeface = new Typeface(new FontFamily("Segoe UI, Malgun Gothic, Yu Gothic"), FontStyle.Normal, FontWeight.Normal);
				}
				return _typeface;
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
			IImage imageSource = GenerateAvatar(userIdentity.Name, text);
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

		private void DownloadAvatar(string email, AvatarImage imageControl, IImage defaultAvatar)
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
				// 阶段 4.5：WPF Application.Current.Dispatcher → Avalonia Dispatcher.UIThread。
				if (args.Error != null)
				{
					HttpWebResponse obj = (args.Error as WebException)?.Response as HttpWebResponse;
					if (obj == null || obj.StatusCode != HttpStatusCode.NotFound)
					{
						Log.Warn("Avatar downloading failed with error: '" + args.Error.Message + "'");
					}
					Dispatcher.UIThread.Post(delegate
					{
						ActiveRequests.Remove(email);
						AvatarCache.Put(email, defaultAvatar);
					});
				}
				else
				{
					IImage downloadedImage = null;
					try
					{
						downloadedImage = LoadImage(args.Result);
					}
					catch (NotSupportedException arg)
					{
						Log.Error($"Image decoding failed: '{arg}'");
						Dispatcher.UIThread.Post(delegate
						{
							ActiveRequests.Remove(email);
							AvatarCache.Put(email, defaultAvatar);
						});
						return;
					}
					Dispatcher.UIThread.Post(delegate
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
				if (args.Error != null)
				{
					HttpWebResponse obj = (args.Error as WebException)?.Response as HttpWebResponse;
					if (obj == null || obj.StatusCode != HttpStatusCode.NotFound)
					{
						Log.Warn("Avatar downloading failed with error: '" + args.Error.Message + "'");
					}
					Dispatcher.UIThread.Post(delegate
					{
						UrlActiveRequests.Remove(url);
						UrlAvatarCache.Put(url, null);
					});
				}
				else
				{
					IImage downloadedImage = null;
					try
					{
						downloadedImage = LoadImage(args.Result);
					}
					catch (NotSupportedException arg)
					{
						Log.Error($"Image decoding failed: '{arg}'");
						Dispatcher.UIThread.Post(delegate
						{
							UrlActiveRequests.Remove(url);
							UrlAvatarCache.Put(url, null);
						});
						return;
					}
					Dispatcher.UIThread.Post(delegate
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
			};
			Task.Run(delegate
			{
				client.DownloadDataAsync(new Uri(url));
			});
		}

		// 阶段 4.5：WPF DrawingVisual + DrawingContext + DrawingImage → Avalonia DrawingGroup + DrawingContext + DrawingImage。
		// WPF RenderOpen() → DrawingGroup.Open()；DrawingImage(drawing) 构造保持一致（Avalonia.Media.DrawingImage 实现 IImage）。
		private static IImage GenerateAvatar(string username, string email)
		{
			if (email == GitHubEmail)
			{
				return LoadBitmapFromAsset(GitHubEmailLogo);
			}
			DrawingGroup drawingGroup = new DrawingGroup();
			using (DrawingContext drawingContext = drawingGroup.Open())
			{
				LinearGradientBrush backgroundBrush = GetBackgroundBrush(email);
				drawingContext.DrawRoundedRectangle(backgroundBrush, null, new Rect(AvatarSize), Radius, Radius);
				FormattedText formattedText = CreateFormattedAbbreviatureText(username);
				double x = (AvatarSize.Width - formattedText.Width) / 2.0;
				double y = (AvatarSize.Height - formattedText.Height) / 2.0 - 1.0;
				drawingContext.DrawText(formattedText, new Point(x, y));
			}
			return new DrawingImage(drawingGroup);
		}

		private static IImage RoundCorners(Bitmap image)
		{
			DrawingGroup drawingGroup = new DrawingGroup();
			using (DrawingContext drawingContext = drawingGroup.Open())
			{
				// 阶段 4.5：WPF PushClip(RectangleGeometry) → Avalonia PushClip(RoundedRect)。
				drawingContext.PushClip(new RoundedRect(new Rect(AvatarSize), Radius, Radius));
				drawingContext.DrawImage(image, new Rect(AvatarSize));
				drawingContext.Pop();
			}
			return new DrawingImage(drawingGroup);
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

		private static IImage LoadImage(byte[] imageData)
		{
			if (imageData == null || imageData.Length == 0)
			{
				return null;
			}
			// 阶段 4.5：WPF BitmapImage(StreamSource + BeginInit/EndInit + Freeze) → Avalonia Bitmap(MemoryStream)。
			Bitmap bitmap;
			using (MemoryStream memoryStream = new MemoryStream(imageData))
			{
				memoryStream.Position = 0L;
				bitmap = new Bitmap(memoryStream);
			}
			return RoundCorners(bitmap);
		}

		private static Bitmap LoadBitmapFromAsset(Uri assetUri)
		{
			// 阶段 4.5：WPF pack://application URI + BitmapImage → Avalonia avares:// URI + AssetLoader.Open + Bitmap。
			using (Stream stream = AssetLoader.Open(assetUri))
			{
				return new Bitmap(stream);
			}
		}

		private static FormattedText CreateFormattedAbbreviatureText(string username)
		{
			// 阶段 4.5：WPF FormattedText(text, culture, flowDirection, typeface, emSize, foreground, pixelsPerDip)
			// → Avalonia FormattedText(text, culture, flowDirection, typeface, emSize, foreground)。
			// Avalonia 原生使用 DIP，无 pixelsPerDip 参数。
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
			// 阶段 4.5：WPF LinearGradientBrush(Color, Color, angle=90° 垂直) →
			// Avalonia LinearGradientBrush(StartPoint=0,0 EndPoint=0,1 相对坐标，垂直)。
			return new LinearGradientBrush[5]
			{
				CreateLinearGradient(Color.FromRgb(55, 159, 239), Color.FromRgb(117, 212, 250)),
				CreateLinearGradient(Color.FromRgb(210, 114, 232), Color.FromRgb(223, 163, 241)),
				CreateLinearGradient(Color.FromRgb(249, 169, 104), Color.FromRgb(251, 203, 120)),
				CreateLinearGradient(Color.FromRgb(250, 84, 107), Color.FromRgb(249, 137, 99)),
				CreateLinearGradient(Color.FromRgb(88, 202, 107), Color.FromRgb(170, 220, 145))
			};
		}

		private static LinearGradientBrush CreateLinearGradient(Color start, Color end)
		{
			return new LinearGradientBrush
			{
				StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
				EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
				GradientStops = new GradientStops
				{
					new GradientStop(start, 0),
					new GradientStop(end, 1)
				}
			};
		}
	}
}
