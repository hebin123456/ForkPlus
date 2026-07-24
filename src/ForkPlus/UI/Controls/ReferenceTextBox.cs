// 阶段 4.5：WPF System.Windows.* → Avalonia.*。
// WPF System.Windows.DataObject.AddPastingHandler → Avalonia 无等价 API，粘贴处理待自定义。
// WPF OnPreviewKeyDown (tunneling) → Avalonia OnKeyDown (bubbling)。
using System;
using System.Text;
using Avalonia;
using Avalonia.Input;
using ForkPlus.Settings;

namespace ForkPlus.UI.Controls
{
	public class ReferenceTextBox : AutoCompleteTextBox
	{
		public ReferenceTextBox()
		{
			// TODO(4.5): Avalonia 无 DataObject.AddPastingHandler 等价 API，需自定义粘贴处理。
			// DataObject.AddPastingHandler(this, OnPaste);
		}

		// 阶段 4.5：WPF OnPreviewKeyDown (tunneling) → Avalonia OnKeyDown (bubbling)。
		// 通过 e.Handled = true 阻止后续处理，达到 Preview 的效果。
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

		// TODO(4.5): Avalonia 无 DataObject.AddPastingHandler 等价 API，需自定义粘贴处理。
		// 以下 OnPaste 逻辑保留以便后续实现粘贴过滤时参考（DataObjectPastingEventArgs 为 WPF 类型，Avalonia 无等价）。
		// private void OnPaste(object sender, DataObjectPastingEventArgs e)
		// {
		// 	if (e.DataObject.GetDataPresent(typeof(string)))
		// 	{
		// 		string text = (string)e.DataObject.GetData(typeof(string));
		// 		string data = ReplaceInvalidCharactersWithSpace(text);
		// 		DataObject dataObject = new DataObject();
		// 		dataObject.SetData(DataFormats.Text, data);
		// 		e.DataObject = dataObject;
		// 	}
		// 	else
		// 	{
		// 		SystemSounds.Exclamation.Play();
		// 		e.CancelCommand();
		// 	}
		// }

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
