using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.Sockets;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public abstract class WebSocketClient : SocketClient, IAsyncDisposable
	{
		internal readonly ILogger logger;
		private readonly IWebSocketPushConfig config;
		private readonly WebSocket webSocket;

		public WebSocketClient(ILogManager logManager, IJsonSerializer jsonSerializer, ILogger logger, IWebSocketPushConfig config, WebSocket webSocket, String id, String clientName): base(clientName, new WebSocketHandler(webSocket, logManager), jsonSerializer)
		{
			this.logger = logger;
			this.config = config;
			this.webSocket = webSocket;
		}

		public override async Task ProcessAsync(ArraySegment<Byte> data) => await this.SendRawAsync("WebSocket message received, but this endpoint is not configured to handle any incoming messages.");

		public virtual async Task SendRawAsync(String message)
		{
			var messageAsBytes = System.Text.Encoding.UTF8.GetBytes(message);
			await this.webSocket.SendAsync(messageAsBytes, WebSocketMessageType.Text, true, CancellationToken.None);
		}

		private UInt32 disposed;
		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
			await this.DisposeOnce();
		}
		protected async ValueTask DisposeOnce()
		{
			try
			{
				if (this.webSocket.State != WebSocketState.Closed)
				{
					var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(this.config.ShutdownTimeout));
					await this.webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "shutting down", timeout.Token);
				}
			}
			catch (Exception exception)
			{
				this.logger.Error("Exception thrown while trying to cleanly close Push websocket.", exception);
			}

			this.webSocket.Abort();
			this.Dispose();
			// disposal of the WebSocket is handled by AspNet
		}
	}
}
