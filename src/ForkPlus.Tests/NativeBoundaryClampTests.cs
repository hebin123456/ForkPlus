using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	// v4.0.6（2026-09-10）native 边界钳制回归测试：
	//   1) BiturboExtensions 的数组/缓冲读取加上限后，异常长度（超 1000 万元素 /
	//      256MB 缓冲）按"损坏数据"降级为空结果 + 日志，不再 Marshal.Copy 读越界
	//      （SIGSEGV 硬崩，进程内零现场）；
	//   2) GetRevisionStorageGitCommand.IntoRevisionStorage 对 indexes 做前置范围
	//      校验：native 返回越界索引时降级为 GitCommandResult.Failure（Bug 错误码），
	//      不再抛 IndexOutOfRangeException。
	// 正常路径（合法长度/索引）行为不变。
	public class NativeBoundaryClampTests
	{
		[Fact]
		public void GetUInt32Array_ValidLength_ReturnsValues()
		{
			IntPtr buffer = Marshal.AllocHGlobal(12);
			try
			{
				Marshal.WriteInt32(buffer, 0, 1);
				Marshal.WriteInt32(buffer, 4, 2);
				Marshal.WriteInt32(buffer, 8, unchecked((int)uint.MaxValue));
				uint[] array = buffer.GetUInt32Array(3);
				Assert.Equal(new uint[3] { 1u, 2u, uint.MaxValue }, array);
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		[Fact]
		public void GetUInt32Array_LengthOverSanityCap_ReturnsEmptyWithoutReading()
		{
			// 1 字节的真实缓冲 + 巨大长度：无钳制实现会 Marshal.Copy 读越界（SIGSEGV）；
			// 钳制后按损坏数据返回空数组。
			IntPtr buffer = Marshal.AllocHGlobal(1);
			try
			{
				Marshal.WriteByte(buffer, 0, 42);
				uint[] array = buffer.GetUInt32Array(50_000_000);
				Assert.Empty(array);
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		[Fact]
		public void GetStructArray_LengthOverSanityCap_ReturnsEmptyWithoutReading()
		{
			IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf<BtOid>());
			try
			{
				Sha[] array = buffer.GetStructArray(20_000_000, (BtOid oid) => oid.ToSha());
				Assert.Empty(array);
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		[Fact]
		public void GetStructArray_ValidLength_ReturnsMappedValues()
		{
			int oidSize = Marshal.SizeOf<BtOid>();
			IntPtr buffer = Marshal.AllocHGlobal(oidSize * 2);
			try
			{
				BtOid first = new BtOid { s0 = 11u, s1 = 12u, s2 = 13u, s3 = 14u, s4 = 15u };
				BtOid second = new BtOid { s0 = 21u, s1 = 22u, s2 = 23u, s3 = 24u, s4 = 25u };
				Marshal.StructureToPtr(first, buffer, false);
				Marshal.StructureToPtr(second, new IntPtr(buffer.ToInt64() + oidSize), false);
				Sha[] array = buffer.GetStructArray(2, (BtOid oid) => oid.ToSha());
				Assert.Equal(2, array.Length);
				Assert.Equal(first.ToSha(), array[0]);
				Assert.Equal(second.ToSha(), array[1]);
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		[Fact]
		public void GetUtf8String_LengthOverSanityCap_ReturnsEmpty()
		{
			IntPtr buffer = Marshal.AllocHGlobal(4);
			try
			{
				Marshal.WriteByte(buffer, 0, 97);
				Marshal.WriteByte(buffer, 1, 98);
				Marshal.WriteByte(buffer, 2, 99);
				Marshal.WriteByte(buffer, 3, 0);
				string text = buffer.GetUtf8String(300_000_000L);
				Assert.Equal(string.Empty, text);
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		[Fact]
		public void GetUtf8String_NullTerminatedScan_ValidString_Returned()
		{
			IntPtr buffer = Marshal.AllocHGlobal(4);
			try
			{
				Marshal.WriteByte(buffer, 0, 104);
				Marshal.WriteByte(buffer, 1, 105);
				Marshal.WriteByte(buffer, 2, 0);
				Marshal.WriteByte(buffer, 3, 99);
				Assert.Equal("hi", buffer.GetUtf8String());
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		[Fact]
		public void IntoRevisionStorage_ValidIndexes_Succeeds()
		{
			BtCommitStorage storage = BuildStorage(oidsCount: 3, indexes: new uint[3] { 0u, 2u, 1u });
			try
			{
				GitCommandResult<RevisionStorage> result = GetRevisionStorageGitCommand.IntoRevisionStorage(ref storage, timestamp: 12345L);
				Assert.True(result.Succeeded, result.Error?.FriendlyDescription);
			}
			finally
			{
				FreeStorage(ref storage);
			}
		}

		[Fact]
		public void IntoRevisionStorage_OutOfRangeIndex_FailsAsBugError_NotException()
		{
			// indexes[1]=5 越界（oids 只有 3 个）——钳制前是 IndexOutOfRangeException，
			// 钳制后是带定位信息的命令失败。
			BtCommitStorage storage = BuildStorage(oidsCount: 3, indexes: new uint[3] { 0u, 5u, 1u });
			try
			{
				GitCommandResult<RevisionStorage> result = GetRevisionStorageGitCommand.IntoRevisionStorage(ref storage, timestamp: 12345L);
				Assert.False(result.Succeeded);
				Assert.Contains("out of range", result.Error.FriendlyDescription, StringComparison.Ordinal);
			}
			finally
			{
				FreeStorage(ref storage);
			}
		}

		/// <summary>构造带 native 缓冲的 BtCommitStorage（调用方负责 FreeStorage）。</summary>
		private static BtCommitStorage BuildStorage(int oidsCount, uint[] indexes)
		{
			int oidSize = Marshal.SizeOf<BtOid>();
			IntPtr oids = Marshal.AllocHGlobal(oidSize * oidsCount);
			for (int i = 0; i < oidsCount; i++)
			{
				Marshal.StructureToPtr(new BtOid { s0 = (uint)i, s1 = 1u, s2 = 2u, s3 = 3u, s4 = 4u },
					new IntPtr(oids.ToInt64() + i * oidSize), false);
			}
			IntPtr indexesPtr = Marshal.AllocHGlobal(4 * indexes.Length);
			for (int j = 0; j < indexes.Length; j++)
			{
				Marshal.WriteInt32(indexesPtr, j * 4, unchecked((int)indexes[j]));
			}
			return new BtCommitStorage
			{
				oids = oids,
				oids_len = oidsCount,
				oids_cap = oidsCount,
				indexes = indexesPtr,
				indexes_len = indexes.Length,
				indexes_cap = indexes.Length,
				has_more = 0
			};
		}

		private static void FreeStorage(ref BtCommitStorage storage)
		{
			if (storage.oids != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(storage.oids);
				storage.oids = IntPtr.Zero;
			}
			if (storage.indexes != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(storage.indexes);
				storage.indexes = IntPtr.Zero;
			}
		}
	}
}
