using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Logging;
using Nethermind.WebSockets;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public abstract class WebSocketClient : DisposeAsyncOnce, IWebSocketsClient
	{
		public String Id { get; }

		public String Client { get; }

		internal readonly ILogger logger;
		private readonly IWebSocketPushConfig config;
		private readonly WebSocket webSocket;

		public WebSocketClient(ILogger logger, IWebSocketPushConfig config, WebSocket webSocket, String id, String client)
		{
			this.logger = logger;
			this.config = config;
			this.webSocket = webSocket;
			this.Id = id;
			this.Client = client;
		}

		public virtual async Task ReceiveAsync(Memory<Byte> data) => await this.SendRawAsync("WebSocket message received, but this endpoint is not configured to handle any incoming messages.");

		public Task SendAsync(WebSocketsMessage message) => throw new NotImplementedException();

		public async Task SendRawAsync(String message)
		{
			var messageAsBytes = System.Text.Encoding.UTF8.GetBytes(message);
			await this.webSocket.SendAsync(messageAsBytes, WebSocketMessageType.Text, true, CancellationToken.None);
		}

		protected override async ValueTask DisposeOnce()
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
			// disposal of the WebSocket is handled by AspNet
		}
	}
}
