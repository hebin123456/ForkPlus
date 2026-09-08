using System;
using ForkPlus.UI.WpfCompat;
using System.Media;
using System.Text;
using Avalonia;
using Avalonia.Input;
using ForkPlus.Settings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class ReferenceTextBox : AutoCompleteTextBox
	{
		public ReferenceTextBox()
		{
			this.AddPastingHandler(OnPaste);
		}

		protected override void OnKeyDown(KeyEventArgs e)
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
				base.OnKeyDown(e);
			}
		}

		private void OnPaste(object sender, DataObjectPastingEventArgs e)
		{
			// Migration note：WPF 原实现重建 DataObject 后替换 e.DataObject；
			// Avalonia 无粘贴拦截事件，PasteGuard shim 用 PastingDataObject.SetData 原位改写文本。
			if (e.DataObject.GetDataPresent(typeof(string)))
			{
				string text = (string)e.DataObject.GetData(typeof(string));
				string data = ReplaceInvalidCharactersWithSpace(text);
				e.DataObject.SetData(DataFormats.Text, data);
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
