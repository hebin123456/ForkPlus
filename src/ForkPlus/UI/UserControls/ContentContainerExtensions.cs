using System;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public static class ContentContainerExtensions
	{
		public static void ShowFallback(this ContentContainer contentContainer, string title = null, string message = null, bool isMonospase = false, string button1Title = null, Action onButton1Click = null)
		{
			FallbackUserControl fallbackUserControl = new FallbackUserControl();
			fallbackUserControl.FallbackTitle = title;
			fallbackUserControl.FallbackMessage = message;
			fallbackUserControl.IsMonospace = isMonospase;
			fallbackUserControl.Button1Title = button1Title;
			fallbackUserControl.Button1Click += delegate
			{
				onButton1Click();
			};
			contentContainer.ShowControl(fallbackUserControl);
		}
	}
}
