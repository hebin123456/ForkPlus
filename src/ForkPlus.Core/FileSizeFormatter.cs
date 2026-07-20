using System.Runtime.InteropServices;
using System.Text;

namespace ForkPlus
{
	public class FileSizeFormatter
	{
		public static string Format(long fileSize)
		{
			StringBuilder stringBuilder = new StringBuilder(11);
			StrFormatByteSize(fileSize, stringBuilder, stringBuilder.Capacity);
			return stringBuilder.ToString();
		}

		[DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
		private static extern long StrFormatByteSize(long fileSize, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);
	}
}
