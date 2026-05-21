using System;
using System.Runtime.InteropServices;
using System.Text;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.Biturbo
{
	internal static class BiturboExtensions
	{
		public static GitCommandError ToGitCommandError(this BtResult btResult)
		{
			switch (btResult)
			{
			case BtResult.Ok:
				return new GitCommandError.Bug("btResult is not an error");
			case BtResult.ErrCanceled:
				return new GitCommandError.Cancelled();
			case BtResult.ErrNotFound:
				return new GitCommandError.NotFound();
			default:
			{
				ulong num = 1024uL;
				IntPtr intPtr = Marshal.AllocHGlobal((int)num);
				long num2 = Bt.bt_get_last_error_message(intPtr, num);
				if (num2 < 0)
				{
					Marshal.FreeHGlobal(intPtr);
					num = (ulong)(~num2);
					intPtr = Marshal.AllocHGlobal((int)num);
					num2 = Bt.bt_get_last_error_message(intPtr, num);
					if (num2 < 0)
					{
						Marshal.FreeHGlobal(intPtr);
						return new GitCommandError.BtError("Cannot read last bt error message");
					}
				}
				byte[] array = new byte[num];
				Marshal.Copy(intPtr, array, 0, (int)num);
				int count = array.IndexOfItem((byte x) => x == 0) ?? array.Length;
				string @string = Encoding.UTF8.GetString(array, 0, count);
				Marshal.FreeHGlobal(intPtr);
				return new GitCommandError.BtError(@string);
			}
			}
		}

		public static Sha ToSha(this BtOid _this)
		{
			return new Sha(_this.s0, _this.s1, _this.s2, _this.s3, _this.s4);
		}

		public static BtOid ToBtOid(this Sha _this)
		{
			BtOid result = default(BtOid);
			result.s0 = _this.DW1;
			result.s1 = _this.DW2;
			result.s2 = _this.DW3;
			result.s3 = _this.DW4;
			result.s4 = _this.DW5;
			return result;
		}

		public static byte[] ToUtf8Bytes(this string _this)
		{
			return Encoding.UTF8.GetBytes(_this);
		}

		public static string GetUtf8String(this IntPtr _this)
		{
			int i;
			for (i = 0; Marshal.ReadByte(_this, i) != 0; i++)
			{
			}
			if (i == 0)
			{
				return "";
			}
			byte[] array = new byte[i];
			Marshal.Copy(_this, array, 0, i);
			return Encoding.UTF8.GetString(array);
		}

		public static byte[] GetData(this IntPtr _this, long length)
		{
			if (length == 0L)
			{
				return new byte[0];
			}
			byte[] array = new byte[length];
			Marshal.Copy(_this, array, 0, (int)length);
			return array;
		}

		public static string GetUtf8String(this IntPtr _this, long length)
		{
			if (length == 0L)
			{
				return "";
			}
			byte[] array = new byte[length];
			Marshal.Copy(_this, array, 0, (int)length);
			return Encoding.UTF8.GetString(array);
		}

		public static string[] GetStringArray(this IntPtr ptr, long length)
		{
			string[] array = new string[length];
			for (int i = 0; i < length; i++)
			{
				IntPtr @this = Marshal.ReadIntPtr(new IntPtr(ptr.ToInt64() + i * IntPtr.Size));
				array[i] = @this.GetUtf8String();
			}
			return array;
		}

		public static uint[] GetUInt32Array(this IntPtr ptr, long length)
		{
			int[] array = new int[length];
			Marshal.Copy(ptr, array, 0, (int)length);
			return Array.ConvertAll(array, x => unchecked((uint)x));
		}

		public static byte[] GetByteArray(this IntPtr ptr, long length)
		{
			byte[] array = new byte[length];
			Marshal.Copy(ptr, array, 0, (int)length);
			return array;
		}

		public static TResult[] GetStructArray<TSource, TResult>(this IntPtr ptr, long length, Func<TSource, TResult> selector)
		{
			int num = Marshal.SizeOf<TSource>();
			TResult[] array = new TResult[length];
			for (int i = 0; i < length; i++)
			{
				TSource arg = Marshal.PtrToStructure<TSource>(new IntPtr(ptr.ToInt64() + i * num));
				array[i] = selector(arg);
			}
			return array;
		}

		public static TResult[] GetStructArray<TSource, TResult>(this IntPtr ptr, long length, Func<int, TSource, TResult> selector)
		{
			int num = Marshal.SizeOf<TSource>();
			TResult[] array = new TResult[length];
			for (int i = 0; i < length; i++)
			{
				TSource arg = Marshal.PtrToStructure<TSource>(new IntPtr(ptr.ToInt64() + i * num));
				array[i] = selector(i, arg);
			}
			return array;
		}

		[Null]
		public static object AsManagedObject(this IntPtr ptr)
		{
			try
			{
				GCHandle gCHandle = GCHandle.FromIntPtr(ptr);
				if (!gCHandle.IsAllocated)
				{
					Log.Error("Failed to create GC object from IntPtr. Object is deallocated");
					return null;
				}
				return gCHandle.Target;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create GC object from IntPtr", ex);
				return null;
			}
		}
	}
}
