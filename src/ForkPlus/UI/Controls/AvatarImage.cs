using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class AvatarImage : Image
	{
		public static readonly StyledProperty<UserIdentity> UserIdentityProperty =
			AvaloniaProperty.Register<AvatarImage, UserIdentity>(nameof(UserIdentity));

		public static readonly StyledProperty<string> UrlProperty =
			AvaloniaProperty.Register<AvatarImage, string>(nameof(Url));

		[Null]
		public UserIdentity UserIdentity
		{
			get => GetValue(UserIdentityProperty);
			set => SetValue(UserIdentityProperty, value);
		}

		[Null]
		public string Url
		{
			get => GetValue(UrlProperty);
			set => SetValue(UrlProperty, value);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == UserIdentityProperty)
			{
				ShowAvatar(UserIdentity);
			}
			else if (change.Property == UrlProperty)
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

		public void SetImage(IImage imageSource, UserIdentity userIdentity)
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
