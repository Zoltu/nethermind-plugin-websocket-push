using System;
using System.Threading;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public abstract class DisposeSyncOnce : IDisposable
	{
		private UInt32 _disposed;

		protected abstract void DisposeOnce();

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
			DisposeOnce();
			GC.SuppressFinalize(this);
		}
	}
}
