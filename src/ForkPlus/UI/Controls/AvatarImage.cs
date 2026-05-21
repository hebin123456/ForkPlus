using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ForkPlus.Git;

namespace ForkPlus.UI.Controls
{
	public class AvatarImage : Image
	{
		public static readonly DependencyProperty UserIdentityProperty = DependencyProperty.Register("UserIdentity", typeof(UserIdentity), typeof(AvatarImage), new PropertyMetadata((object)null));

		public static readonly DependencyProperty UrlProperty = DependencyProperty.Register("Url", typeof(string), typeof(AvatarImage), new PropertyMetadata((object)null));

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

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
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

		public void SetImage(ImageSource imageSource, UserIdentity userIdentity)
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
