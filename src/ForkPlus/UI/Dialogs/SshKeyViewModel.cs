using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using ForkPlus.Shell;

namespace ForkPlus.UI.Dialogs
{
	public class SshKeyViewModel : INotifyPropertyChanged
	{
		public bool _isActive;

		public string KeyPath { get; }

		public string KeyFileName { get; }

		public string Sha256 { get; }

		public string PublicKey { get; }

		public bool IsActive
		{
			get
			{
				return _isActive;
			}
			set
			{
				if (_isActive != value)
				{
					_isActive = value;
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsActive"));
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public SshKeyViewModel(SshKey sshKey, bool isActive = false)
			: this(sshKey.FilePath, sshKey.Title, GenerateFingerprint(sshKey), sshKey.RawPublicKey, isActive)
		{
		}

		private SshKeyViewModel(string keyPath, string name, string fingerprint, string publicKey, bool isActive)
		{
			KeyPath = keyPath;
			KeyFileName = name;
			Sha256 = fingerprint;
			PublicKey = publicKey;
			IsActive = isActive;
		}

		private static string GenerateFingerprint(SshKey sshKey)
		{
			try
			{
				string s = DistilledPublicKey(sshKey);
				using SHA256 sHA = SHA256.Create();
				Encoding.ASCII.GetBytes(s);
				byte[] buffer = Convert.FromBase64String(s);
				return Convert.ToBase64String(sHA.ComputeHash(buffer));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to generate fingerprint for '" + sshKey.FilePath + "'", ex);
				return "Cannot calculate fingerprint";
			}
		}

		private static string DistilledPublicKey(SshKey sshKey)
		{
			string rawPublicKey = sshKey.RawPublicKey;
			int num = 0;
			if (rawPublicKey.StartsWith("ssh-rsa "))
			{
				num = "ssh-rsa ".Length;
			}
			else if (rawPublicKey.StartsWith("ssh-ed25519 "))
			{
				num = "ssh-ed25519 ".Length;
			}
			int num2 = rawPublicKey.Length;
			int num3 = rawPublicKey.LastIndexOf("==");
			if (num3 != -1)
			{
				num2 = num3 + "==".Length;
			}
			else
			{
				int num4 = rawPublicKey.LastIndexOf(" ");
				if (num4 != -1)
				{
					num2 = num4;
				}
			}
			return rawPublicKey.Substring(num, num2 - num);
		}
	}
}
