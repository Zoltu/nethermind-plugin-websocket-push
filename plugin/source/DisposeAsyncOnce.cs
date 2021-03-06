using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public abstract class DisposeAsyncOnce : IAsyncDisposable
	{
		private UInt32 disposed;

		protected abstract ValueTask DisposeOnce();
		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
			await this.DisposeOnce();
		}
	}
}
