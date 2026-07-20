using System.Text;

namespace ForkPlus.Git.Diff
{
	public class PresentationContext
	{
		private StringBuilder _result = new StringBuilder(1024);

		public int Cursor { get; private set; }

		public int LineNumber { get; set; }

		public void Append(char character)
		{
			_result.Append(character);
			Cursor++;
		}

		public void Append(string str)
		{
			_result.Append(str);
			Cursor += str.Length;
		}

		public string ResultString()
		{
			return _result.ToString();
		}
	}
}
