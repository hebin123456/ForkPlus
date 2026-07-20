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
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

		private static readonly Uri GitHubEmailLogo = new Uri("pack://application:,,,/ForkPlus;component/Assets/GitHubAvatar.png");

		private static readonly string GitHubEmail = "noreply@github.com";

		private static readonly string AnonymousEmailSuffix = "@users.noreply.github.com";

		private static readonly Regex AnonymousEmailRegex = new Regex("^(?:(\\d+)\\+)?(.+?)@users\\.noreply\\.github\\.com$");

		private static readonly Size AvatarSize = new Size(42.0, 42.0);

		private static readonly double Radius = 4.0;

		private static readonly object Padlock = new object();

		private readonly LruCache<string, ImageSource> _avatarCache = new LruCache<string, ImageSource>(128);

		private readonly Dictionary<string, List<AvatarImage>> _activeRequests = new Dictionary<string, List<AvatarImage>>();

		private readonly LruCache<string, ImageSource> _urlAvatarCache = new LruCache<string, ImageSource>(128);

		private readonly Dictionary<string, List<AvatarImage>> _urlActiveRequests = new Dictionary<string, List<AvatarImage>>();

		private static AvatarManager _default;

		private static Typeface _typeface = null;

		private static LinearGradientBrush[] _avatarGradients = null;

		private LruCache<string, ImageSource> AvatarCache => _avatarCache;

		private Dictionary<string, List<AvatarImage>> ActiveRequests => _activeRequests;

		private LruCache<string, ImageSource> UrlAvatarCache => _urlAvatarCache;

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
			ImageSource imageSource = GenerateAvatar(userIdentity.Name, text);
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

		private void DownloadAvatar(string email, AvatarImage imageControl, ImageSource defaultAvatar)
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
						ImageSource downloadedImage = null;
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
						ImageSource downloadedImage = null;
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

		private static ImageSource GenerateAvatar(string username, string email)
		{
			if (email == GitHubEmail)
			{
				BitmapImage bitmapImage = new BitmapImage(GitHubEmailLogo);
				bitmapImage.Freeze();
				return bitmapImage;
			}
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				LinearGradientBrush backgroundBrush = GetBackgroundBrush(email);
				drawingContext.DrawRoundedRectangle(backgroundBrush, null, new Rect(AvatarSize), Radius, Radius);
				FormattedText formattedText = CreateFormattedAbbreviatureText(username, VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);
				double x = (AvatarSize.Width - formattedText.Width) / 2.0;
				double y = (AvatarSize.Height - formattedText.Height) / 2.0 - 1.0;
				drawingContext.DrawText(formattedText, new Point(x, y));
			}
			return new DrawingImage(drawingVisual.Drawing);
		}

		private static ImageSource RoundCorners(BitmapImage image)
		{
			DrawingVisual drawingVisual = new DrawingVisual();
			using (DrawingContext drawingContext = drawingVisual.RenderOpen())
			{
				drawingContext.PushClip(new RectangleGeometry(new Rect(AvatarSize), Radius, Radius));
				drawingContext.DrawImage(image, new Rect(AvatarSize));
				drawingContext.Pop();
			}
			return new DrawingImage(drawingVisual.Drawing);
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

		private static ImageSource LoadImage(byte[] imageData)
		{
			if (imageData == null || imageData.Length == 0)
			{
				return null;
			}
			BitmapImage bitmapImage = new BitmapImage();
			using (MemoryStream memoryStream = new MemoryStream(imageData))
			{
				memoryStream.Position = 0L;
				bitmapImage.BeginInit();
				bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.UriSource = null;
				bitmapImage.StreamSource = memoryStream;
				bitmapImage.EndInit();
				bitmapImage.Freeze();
			}
			ImageSource imageSource = RoundCorners(bitmapImage);
			imageSource.Freeze();
			return imageSource;
		}

		private static FormattedText CreateFormattedAbbreviatureText(string username, double pixelsPerDip)
		{
			return new FormattedText(CreateAbbreviatureText(username), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface, 22.0, Brushes.White, pixelsPerDip);
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
			return new LinearGradientBrush[5]
			{
				new LinearGradientBrush(Color.FromRgb(55, 159, 239), Color.FromRgb(117, 212, 250), 90.0),
				new LinearGradientBrush(Color.FromRgb(210, 114, 232), Color.FromRgb(223, 163, 241), 90.0),
				new LinearGradientBrush(Color.FromRgb(249, 169, 104), Color.FromRgb(251, 203, 120), 90.0),
				new LinearGradientBrush(Color.FromRgb(250, 84, 107), Color.FromRgb(249, 137, 99), 90.0),
				new LinearGradientBrush(Color.FromRgb(88, 202, 107), Color.FromRgb(170, 220, 145), 90.0)
			};
		}
	}
}

