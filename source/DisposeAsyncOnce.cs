using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zoltu.Nethermind.Plugin.PushPending
{
	public abstract class DisposeAsyncOnce : IAsyncDisposable
	{
		private UInt32 _disposed;

		protected abstract ValueTask DisposeOnce();
		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
			await DisposeOnce();
		}
	}
}
