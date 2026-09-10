using System;
using System.Runtime.InteropServices;
using System.Text;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.Biturbo
{
	internal static class BiturboExtensions
	{
		// v4.0.6 native 边界钳制：Rust 侧按契约如实填写长度（len/cap 显式传递，已
		// 逐项核对 Biturbo 源码），但互操作层不做防御时，任何一方的未来回归（Rust bug、
		// ABI 错位、内存踩踏）都会把"异常长度"直接变成 Marshal.Copy 读越界——SIGSEGV
		// 硬崩，进程内零现场。上限值远超真实业务规模（revision 单页 1 万、百万级仓库
		// 全量 oid 也在几百万内），超限即按异常数据对待：记日志 + 返回空，让上层命令
		// 走 GitCommandResult.Failure 报错，而不是杀进程。
		private const long MaxNativeArrayElements = 10_000_000;

		private const long MaxNativeBufferBytes = 256L * 1024 * 1024;

		private static bool IsWithinArrayLimit(long length)
		{
			if (length > MaxNativeArrayElements)
			{
				Log.Error("Biturbo native array length " + length + " exceeds sanity cap " + MaxNativeArrayElements + " (treating as corrupted, returning empty)");
				return false;
			}
			return true;
		}

		private static bool IsWithinBufferLimit(long length)
		{
			if (length > MaxNativeBufferBytes)
			{
				Log.Error("Biturbo native buffer length " + length + " exceeds sanity cap " + MaxNativeBufferBytes + " (treating as corrupted, returning empty)");
				return false;
			}
			return true;
		}

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
				if (string.IsNullOrWhiteSpace(@string))
				{
					@string = "Biturbo failed with " + btResult;
				}
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
			// v4.0.6：nul 终止符扫描加上限（Rust 契约保证 \0，但缓冲被踩/句柄错位时
			// 无终止符会一路扫出本进程地址空间）。超限按损坏数据处理。
			int i;
			for (i = 0; i < MaxNativeBufferBytes && Marshal.ReadByte(_this, i) != 0; i++)
			{
			}
			if (i >= MaxNativeBufferBytes)
			{
				Log.Error("Biturbo native string is not null-terminated within " + MaxNativeBufferBytes + " bytes (treating as corrupted, returning empty)");
				return "";
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
			if (length <= 0L || _this == IntPtr.Zero)
			{
				return new byte[0];
			}
			if (!IsWithinBufferLimit(length))
			{
				return new byte[0];
			}
			byte[] array = new byte[length];
			Marshal.Copy(_this, array, 0, (int)length);
			return array;
		}

		public static string GetUtf8String(this IntPtr _this, long length)
		{
			if (length <= 0L || _this == IntPtr.Zero)
			{
				return "";
			}
			if (!IsWithinBufferLimit(length))
			{
				return "";
			}
			byte[] array = new byte[length];
			Marshal.Copy(_this, array, 0, (int)length);
			return Encoding.UTF8.GetString(array);
		}

		public static string[] GetStringArray(this IntPtr ptr, long length)
		{
			if (length <= 0L || ptr == IntPtr.Zero)
			{
				return new string[0];
			}
			if (!IsWithinArrayLimit(length))
			{
				return new string[0];
			}
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
			if (length <= 0L || ptr == IntPtr.Zero)
			{
				return new uint[0];
			}
			if (!IsWithinArrayLimit(length))
			{
				return new uint[0];
			}
			int[] array = new int[length];
			Marshal.Copy(ptr, array, 0, (int)length);
			return Array.ConvertAll(array, x => unchecked((uint)x));
		}

		public static byte[] GetByteArray(this IntPtr ptr, long length)
		{
			if (length <= 0L || ptr == IntPtr.Zero)
			{
				return new byte[0];
			}
			if (!IsWithinBufferLimit(length))
			{
				return new byte[0];
			}
			byte[] array = new byte[length];
			Marshal.Copy(ptr, array, 0, (int)length);
			return array;
		}

		public static TResult[] GetStructArray<TSource, TResult>(this IntPtr ptr, long length, Func<TSource, TResult> selector)
		{
			if (length <= 0L || ptr == IntPtr.Zero)
			{
				return new TResult[0];
			}
			if (!IsWithinStructArrayLimit<TSource>(length))
			{
				return new TResult[0];
			}
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
			if (length <= 0L || ptr == IntPtr.Zero)
			{
				return new TResult[0];
			}
			if (!IsWithinStructArrayLimit<TSource>(length))
			{
				return new TResult[0];
			}
			int num = Marshal.SizeOf<TSource>();
			TResult[] array = new TResult[length];
			for (int i = 0; i < length; i++)
			{
				TSource arg = Marshal.PtrToStructure<TSource>(new IntPtr(ptr.ToInt64() + i * num));
				array[i] = selector(i, arg);
			}
			return array;
		}

		private static bool IsWithinStructArrayLimit<TSource>(long length)
		{
			if (!IsWithinArrayLimit(length))
			{
				return false;
			}
			long totalBytes = length * Marshal.SizeOf<TSource>();
			return IsWithinBufferLimit(totalBytes);
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
