using System.Diagnostics;
using System.Text;

namespace ForkPlus.UI
{
	public class BindingErrorTraceListener : TraceListener
	{
		private readonly StringBuilder _messageBuilder = new StringBuilder();

		public override void Write(string message)
		{
			if (!string.IsNullOrEmpty(message))
			{
				_messageBuilder.Append(message);
			}
		}

		public override void WriteLine(string message)
		{
			if (!string.IsNullOrEmpty(message))
			{
				_messageBuilder.Append(message);
			}
			string text = _messageBuilder.ToString().Trim();
			_messageBuilder.Clear();
			if (text.Length == 0)
			{
				return;
			}
			Debug.WriteLine(text);
			global::ForkPlus.Log.Warn("WPF binding error: " + text);
		}
	}
}
