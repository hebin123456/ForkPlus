using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ForkPlus.UI.Dialogs
{
	public partial class LegalWindow : ForkPlusDialogWindow
	{

		public LegalWindow()
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			base.CancelButtonTitle = "Close";
			base.ShowSubmitButton = false;
			LicencesTextBox.Text = GetLicences();
		}

		private static string GetLicences()
		{
			string result = string.Empty;
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string name = "ForkPlus.Assets.Legal.txt";
				using (Stream stream = executingAssembly.GetManifestResourceStream(name))
				{
					using StreamReader streamReader = new StreamReader(stream);
					result = streamReader.ReadToEnd();
				}
				return result;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read resource stream", ex);
				return result;
			}
		}

	}
}
