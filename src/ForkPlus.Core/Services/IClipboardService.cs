using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 剪贴板操作抽象（替换 WPF Clipboard）
	/// </summary>
	public interface IClipboardService
	{
		void SetText(string text);
		string GetText();
	}
}
