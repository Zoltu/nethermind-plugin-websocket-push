using System;
using System.Threading;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public abstract class DisposeSyncOnce : IDisposable
	{
		private UInt32 disposed;

		protected abstract void DisposeOnce();

		public void Dispose()
		{
			if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
			this.DisposeOnce();
			GC.SuppressFinalize(this);
		}
	}
}
