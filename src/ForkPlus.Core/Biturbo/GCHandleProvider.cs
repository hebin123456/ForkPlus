using System;
using System.Runtime.InteropServices;

namespace ForkPlus.Biturbo
{
	public class GCHandleProvider : IDisposable
	{
		private bool _disposed;

		public IntPtr Pointer => GCHandle.ToIntPtr(Handle);

		public GCHandle Handle { get; }

		public GCHandleProvider(object target)
		{
			Handle = GCHandle.Alloc(target);
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				ReleaseUnmanagedResources();
				GC.SuppressFinalize(this);
				_disposed = true;
			}
		}

		~GCHandleProvider()
		{
			ReleaseUnmanagedResources();
		}

		private void ReleaseUnmanagedResources()
		{
			if (Handle.IsAllocated)
			{
				Handle.Free();
			}
		}
	}
}
