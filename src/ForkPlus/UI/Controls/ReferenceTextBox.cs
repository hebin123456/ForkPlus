using System;
using System.Media;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ForkPlus.Settings;

namespace ForkPlus.UI.Controls
{
	public class ReferenceTextBox : AutoCompleteTextBox
	{
		public ReferenceTextBox()
		{
			DataObject.AddPastingHandler(this, OnPaste);
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Space)
			{
				int caretIndex = base.CaretIndex;
				base.Text = base.Text.Insert(caretIndex, ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement);
				base.CaretIndex = caretIndex + 1;
				e.Handled = true;
			}
			else
			{
				base.OnPreviewKeyDown(e);
			}
		}

		private void OnPaste(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(typeof(string)))
			{
				string text = (string)e.DataObject.GetData(typeof(string));
				string data = ReplaceInvalidCharactersWithSpace(text);
				DataObject dataObject = new DataObject();
				dataObject.SetData(DataFormats.Text, data);
				e.DataObject = dataObject;
			}
			else
			{
				SystemSounds.Exclamation.Play();
				e.CancelCommand();
			}
		}

		private string ReplaceInvalidCharactersWithSpace(string text)
		{
			string referenceSpaceCharacterReplacement = ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement;
			if (text == "@")
			{
				return referenceSpaceCharacterReplacement;
			}
			StringBuilder stringBuilder = new StringBuilder(text);
			stringBuilder.Replace(" ", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("\n", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("..", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("//", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("@{", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("\\", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("/.", referenceSpaceCharacterReplacement);
			stringBuilder.Replace(".lock", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("~", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("^", referenceSpaceCharacterReplacement);
			stringBuilder.Replace(":", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("?", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("*", referenceSpaceCharacterReplacement);
			stringBuilder.Replace("[", referenceSpaceCharacterReplacement);
			stringBuilder.Replace(Environment.NewLine, referenceSpaceCharacterReplacement);
			return stringBuilder.ToString();
		}
	}
}
