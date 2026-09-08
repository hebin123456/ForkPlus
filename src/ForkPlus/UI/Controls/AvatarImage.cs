using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Git;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class AvatarImage : Image
	{
		public static readonly global::Avalonia.StyledProperty<UserIdentity> UserIdentityProperty =
    global::Avalonia.AvaloniaProperty.Register<AvatarImage, UserIdentity>("UserIdentity");

		public static readonly global::Avalonia.StyledProperty<string> UrlProperty =
    global::Avalonia.AvaloniaProperty.Register<AvatarImage, string>("Url");

		[Null]
		public UserIdentity UserIdentity
		{
			get
			{
				return (UserIdentity)GetValue(UserIdentityProperty);
			}
			set
			{
				SetValue(UserIdentityProperty, value);
			}
		}

		[Null]
		public string Url
		{
			get
			{
				return (string)GetValue(UrlProperty);
			}
			set
			{
				SetValue(UrlProperty, value);
			}
		}

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == UserIdentityProperty)
			{
				ShowAvatar(UserIdentity);
			}
			else if (e.Property == UrlProperty)
			{
				ShowAvatarUrl(Url);
			}
		}

		public void ShowAvatarUrl([Null] string url)
		{
			if (url == null)
			{
				base.Source = null;
			}
			else
			{
				AvatarManager.Default.RequestAvatar(this, url);
			}
		}

		public void ShowAvatarNoCache(UserIdentity userIdentity)
		{
			new AvatarManager().RequestAvatar(this, userIdentity);
		}

		public void SetImage(global::Avalonia.Media.IImage imageSource, UserIdentity userIdentity)
		{
			if (UserIdentity?.Name == userIdentity?.Name && UserIdentity?.Email.ToLower() == userIdentity?.Email.ToLower())
			{
				base.Source = imageSource;
			}
		}

		private void ShowAvatar([Null] UserIdentity userIdentity)
		{
			if (userIdentity == null)
			{
				base.Source = null;
			}
			else
			{
				AvatarManager.Default.RequestAvatar(this, userIdentity);
			}
		}
	}
}
