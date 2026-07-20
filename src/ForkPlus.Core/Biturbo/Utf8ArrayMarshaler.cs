using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ForkPlus.Biturbo
{
	internal class Utf8ArrayMarshaler : ICustomMarshaler
	{
		public static ICustomMarshaler GetInstance(string cookie)
		{
			return new Utf8ArrayMarshaler();
		}

		public void CleanUpManagedData(object ManagedObj)
		{
		}

		public void CleanUpNativeData(IntPtr pNativeData)
		{
			IntPtr intPtr = Marshal.ReadIntPtr(pNativeData);
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
			}
			Marshal.FreeHGlobal(pNativeData);
		}

		public int GetNativeDataSize()
		{
			throw new NotImplementedException();
		}

		public IntPtr MarshalManagedToNative(object managedObj)
		{
			string[] array = (string[])managedObj;
			if (array.Length == 0)
			{
				IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf<IntPtr>());
				Marshal.WriteIntPtr(intPtr, IntPtr.Zero);
				return intPtr;
			}
			byte[][] array2 = new byte[array.Length][];
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				array2[i] = Encoding.UTF8.GetBytes(array[i]);
				num += array2[i].Length + 1;
			}
			int num2 = 0;
			IntPtr intPtr2 = Marshal.AllocHGlobal(Marshal.SizeOf<IntPtr>() * array2.Length);
			IntPtr intPtr3 = Marshal.AllocHGlobal(num);
			for (int j = 0; j < array2.Length; j++)
			{
				Marshal.WriteIntPtr(intPtr2, j * Marshal.SizeOf<IntPtr>(), intPtr3 + num2);
				byte[] array3 = array2[j];
				Marshal.Copy(array3, 0, intPtr3 + num2, array3.Length);
				num2 += array3.Length;
				Marshal.WriteByte(intPtr3, num2, 0);
				num2++;
			}
			return intPtr2;
		}

		public object MarshalNativeToManaged(IntPtr pNativeData)
		{
			throw new NotImplementedException();
		}
	}
}
