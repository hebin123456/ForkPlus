using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ForkPlus.Biturbo
{
	internal class UTF8StringCodec : ICustomMarshaler
	{
		public static ICustomMarshaler GetInstance(string cookie)
		{
			return new UTF8StringCodec();
		}

		public void CleanUpManagedData(object ManagedObj)
		{
		}

		public void CleanUpNativeData(IntPtr pNativeData)
		{
			Marshal.FreeHGlobal(pNativeData);
		}

		public int GetNativeDataSize()
		{
			throw new NotImplementedException();
		}

		public IntPtr MarshalManagedToNative(object managedObj)
		{
			string s = (string)managedObj;
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			IntPtr intPtr = Marshal.AllocHGlobal(bytes.Length + 1);
			Marshal.Copy(bytes, 0, intPtr, bytes.Length);
			Marshal.WriteByte(intPtr, bytes.Length, 0);
			return intPtr;
		}

		public object MarshalNativeToManaged(IntPtr pNativeData)
		{
			if (pNativeData == IntPtr.Zero)
			{
				return null;
			}
			MemoryStream memoryStream = new MemoryStream();
			int num = 0;
			while (true)
			{
				byte b = Marshal.ReadByte(pNativeData, num);
				if (b == 0)
				{
					break;
				}
				memoryStream.WriteByte(b);
				num++;
			}
			return Encoding.UTF8.GetString(memoryStream.ToArray());
		}
	}
}
