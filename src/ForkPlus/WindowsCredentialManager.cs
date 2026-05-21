using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ForkPlus
{
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
			if (CredRead(applicationName, CredentialType.Generic, 0, out var credentialPtr))
			{
				using (CriticalCredentialHandle criticalCredentialHandle = new CriticalCredentialHandle(credentialPtr))
				{
					return ReadCredential(criticalCredentialHandle.GetCredential());
				}
			}
			return null;
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

		public static bool RemoveCredential(string key)
		{
			return CredDelete(key, CredentialType.Generic, 0);
		}

		public static IReadOnlyList<Credential> EnumerateCrendentials()
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

		[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredReadW", SetLastError = true)]
		private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

		[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredWriteW", SetLastError = true)]
		private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

		[DllImport("Advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool CredEnumerate(string filter, int flag, out int count, out IntPtr pCredentials);

		[DllImport("Advapi32.dll", SetLastError = true)]
		private static extern bool CredFree([In] IntPtr cred);

		[DllImport("Advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CredDeleteW", SetLastError = true)]
		private static extern bool CredDelete(string target, CredentialType type, int reservedFlag);
	}
}
