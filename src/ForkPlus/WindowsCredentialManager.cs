using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ForkPlus
{
	// 阶段 5：凭据管理跨平台化。
	// Windows: 仍使用 advapi32 CredRead/CredWrite/CredDelete（CredEnumerate）。
	// Unix:    回退到本地文件存储 ~/.local/share/ForkPlus/credentials/<hash>.json
	//          （无 OS 钥匙串集成，仅作为开发期降级；生产可后续接入 libsecret / Keychain）。
	// P/Invoke 全部加 [SupportedOSPlatform("windows")]，运行时按 OperatingSystem.IsWindows() 分支。
	public static class WindowsCredentialManager
	{
		private enum CredentialPersistence : uint
		{
			Session = 1u,
			LocalMachine,
			Enterprise
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct CREDENTIAL
		{
			public uint Flags;

			public CredentialType Type;

			public IntPtr TargetName;

			public IntPtr Comment;

			public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;

			public uint CredentialBlobSize;

			public IntPtr CredentialBlob;

			public uint Persist;

			public uint AttributeCount;

			public IntPtr Attributes;

			public IntPtr TargetAlias;

			public IntPtr UserName;
		}

		private sealed class CriticalCredentialHandle : CriticalHandleZeroOrMinusOneIsInvalid
		{
			public CriticalCredentialHandle(IntPtr preexistingHandle)
			{
				SetHandle(preexistingHandle);
			}

			[UnconditionalSuppressMessage("AotAnalysis", "IL3050",
				Justification = "Marshal 目标类型为编译期已知的固定布局 struct，非泛型 SizeOf/PtrToStructure 重载在此场景 AOT 安全。")]
			public CREDENTIAL GetCredential()
			{
				if (!IsInvalid)
				{
					return (CREDENTIAL)Marshal.PtrToStructure(handle, typeof(CREDENTIAL));
				}
				throw new InvalidOperationException("Invalid CriticalHandle!");
			}

			protected override bool ReleaseHandle()
			{
				if (!IsInvalid)
				{
					CredFree(handle);
					SetHandleAsInvalid();
					return true;
				}
				return false;
			}
		}

		private static string SshKeyUsernameString = "SSH Key Passphrase";

		public static string QuerySshPassphrase(string sshKey)
		{
			Credential credential = ReadCredential("fork:" + sshKey);
			if (credential != null && credential.UserName == SshKeyUsernameString)
			{
				return credential.Password;
			}
			return null;
		}

		public static void StoreSshPassphrase(string sshKey, string passphrase)
		{
			WriteCredential("fork:" + sshKey, SshKeyUsernameString, passphrase);
		}

		public static string QuerySshUserPassword(Uri url, string username)
		{
			return ReadCredential("fork:ssh://" + url.Host + "." + username + ".password")?.Password;
		}

		public static void StoreSshUserPassword(Uri url, string username, string password)
		{
			WriteCredential("fork:ssh://" + url.Host + "." + username + ".password", username, password);
		}

		public static Credential ReadCredential(string applicationName)
		{
			if (OperatingSystem.IsWindows())
			{
				return ReadCredentialWindows(applicationName);
			}
			return ReadCredentialUnix(applicationName);
		}

		[SupportedOSPlatform("windows")]
		private static Credential ReadCredentialWindows(string applicationName)
		{
			if (CredRead(applicationName, CredentialType.Generic, 0, out var credentialPtr))
			{
				using (CriticalCredentialHandle criticalCredentialHandle = new CriticalCredentialHandle(credentialPtr))
				{
					return ReadCredential(criticalCredentialHandle.GetCredential());
				}
			}
			return null;
		}

		// 阶段 5：Unix 平台凭据降级到本地文件。
		// 文件路径 ~/.local/share/ForkPlus/credentials/<sha256(applicationName)>.json
		// 仅存明文（无钥匙串加密），仅供开发期使用；生产应接入 libsecret / Keychain。
		private static Credential ReadCredentialUnix(string applicationName)
		{
			try
			{
				string file = GetCredentialFilePath(applicationName);
				if (!File.Exists(file))
				{
					return null;
				}
				string[] lines = File.ReadAllLines(file);
				if (lines.Length < 2)
				{
					return null;
				}
				return new Credential(CredentialType.Generic, applicationName, lines[0], lines[1]);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read credential '" + applicationName + "' from unix store", ex);
				return null;
			}
		}

		private static Credential ReadCredential(CREDENTIAL credential)
		{
			string applicationName = Marshal.PtrToStringUni(credential.TargetName);
			string userName = Marshal.PtrToStringUni(credential.UserName);
			string password = null;
			if (credential.CredentialBlob != IntPtr.Zero)
			{
				password = Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
			}
			return new Credential(credential.Type, applicationName, userName, password);
		}

		public static int WriteCredential(string applicationName, string userName, string secret)
		{
			if (OperatingSystem.IsWindows())
			{
				return WriteCredentialWindows(applicationName, userName, secret);
			}
			return WriteCredentialUnix(applicationName, userName, secret);
		}

		[SupportedOSPlatform("windows")]
		private static int WriteCredentialWindows(string applicationName, string userName, string secret)
		{
			if (Encoding.Unicode.GetBytes(secret).Length > 512)
			{
				throw new ArgumentOutOfRangeException("secret", "The secret message has exceeded 512 bytes.");
			}
			CREDENTIAL userCredential = default(CREDENTIAL);
			userCredential.AttributeCount = 0u;
			userCredential.Attributes = IntPtr.Zero;
			userCredential.Comment = IntPtr.Zero;
			userCredential.TargetAlias = IntPtr.Zero;
			userCredential.Type = CredentialType.Generic;
			userCredential.Persist = 2u;
			userCredential.CredentialBlobSize = (uint)Encoding.Unicode.GetBytes(secret).Length;
			userCredential.TargetName = Marshal.StringToCoTaskMemUni(applicationName);
			userCredential.CredentialBlob = Marshal.StringToCoTaskMemUni(secret);
			userCredential.UserName = Marshal.StringToCoTaskMemUni(userName ?? Environment.UserName);
			bool num = CredWrite(ref userCredential, 0u);
			int lastWin32Error = Marshal.GetLastWin32Error();
			Marshal.FreeCoTaskMem(userCredential.TargetName);
			Marshal.FreeCoTaskMem(userCredential.CredentialBlob);
			Marshal.FreeCoTaskMem(userCredential.UserName);
			if (num)
			{
				return 0;
			}
			throw new Exception($"CredWrite failed with the error code {lastWin32Error}.");
		}

		private static int WriteCredentialUnix(string applicationName, string userName, string secret)
		{
			try
			{
				string file = GetCredentialFilePath(applicationName);
				Directory.CreateDirectory(Path.GetDirectoryName(file));
				// 文件权限 0600：仅 owner 可读写
				File.WriteAllLines(file, new[] { userName ?? Environment.UserName, secret });
				try
				{
					if (!OperatingSystem.IsWindows())
					{
						File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
					}
				}
				catch
				{
					// SetUnixFileMode 在旧 .NET 或不支持平台时忽略
				}
				return 0;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to write credential '" + applicationName + "' to unix store", ex);
				return -1;
			}
		}

		public static bool RemoveCredential(string key)
		{
			if (OperatingSystem.IsWindows())
			{
				return CredDelete(key, CredentialType.Generic, 0);
			}
			try
			{
				string file = GetCredentialFilePath(key);
				if (File.Exists(file))
				{
					File.Delete(file);
					return true;
				}
				return false;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to remove credential '" + key + "' from unix store", ex);
				return false;
			}
		}

		public static IReadOnlyList<Credential> EnumerateCrendentials()
		{
			if (OperatingSystem.IsWindows())
			{
				return EnumerateCredentialsWindows();
			}
			return EnumerateCredentialsUnix();
		}

		[SupportedOSPlatform("windows")]
		[UnconditionalSuppressMessage("AotAnalysis", "IL3050",
			Justification = "Marshal 目标类型为编译期已知的固定布局 struct，非泛型 SizeOf/PtrToStructure 重载在此场景 AOT 安全。")]
		private static IReadOnlyList<Credential> EnumerateCredentialsWindows()
		{
			List<Credential> list = new List<Credential>();
			if (CredEnumerate(null, 0, out var count, out var pCredentials))
			{
				for (int i = 0; i < count; i++)
				{
					IntPtr ptr = Marshal.ReadIntPtr(pCredentials, i * Marshal.SizeOf(typeof(IntPtr)));
					list.Add(ReadCredential((CREDENTIAL)Marshal.PtrToStructure(ptr, typeof(CREDENTIAL))));
				}
				return list;
			}
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}

		private static IReadOnlyList<Credential> EnumerateCredentialsUnix()
		{
			List<Credential> list = new List<Credential>();
			try
			{
				string dir = GetCredentialStoreDirectory();
				if (!Directory.Exists(dir))
				{
					return list;
				}
				foreach (string file in Directory.EnumerateFiles(dir))
				{
					try
					{
						string[] lines = File.ReadAllLines(file);
						if (lines.Length >= 2)
						{
							list.Add(new Credential(CredentialType.Generic, Path.GetFileNameWithoutExtension(file), lines[0], lines[1]));
						}
					}
					catch
					{
						// 跳过损坏文件
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to enumerate credentials from unix store", ex);
			}
			return list;
		}

		private static string GetCredentialStoreDirectory()
		{
			string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			if (string.IsNullOrEmpty(baseDir))
			{
				// Unix 上 LocalApplicationData 通常为 ~/.local/share；fallback 到 ~/.forkplus
				baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".forkplus");
			}
			return Path.Combine(baseDir, "ForkPlus", "credentials");
		}

		private static string GetCredentialFilePath(string applicationName)
		{
			// 用 applicationName 的 SHA-256 hash 作文件名，避免路径非法字符
			using (var sha = System.Security.Cryptography.SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(applicationName));
				StringBuilder sb = new StringBuilder(64);
				foreach (byte b in hash)
				{
					sb.Append(b.ToString("x2"));
				}
				return Path.Combine(GetCredentialStoreDirectory(), sb.ToString() + ".cred");
			}
		}

		[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)]
		[SupportedOSPlatform("windows")]
		private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

		[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)]
		[SupportedOSPlatform("windows")]
		private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

		[DllImport("Advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
		[SupportedOSPlatform("windows")]
		private static extern bool CredEnumerate(string filter, int flag, out int count, out IntPtr pCredentials);

		[DllImport("Advapi32.dll", SetLastError = true)]
		[SupportedOSPlatform("windows")]
		private static extern bool CredFree([In] IntPtr cred);

		[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)]
		[SupportedOSPlatform("windows")]
		private static extern bool CredDelete(string target, CredentialType type, int reservedFlag);
	}
}
