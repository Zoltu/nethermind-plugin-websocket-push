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

		internal readonly ILogger _logger;
		private readonly IWebSocketPushConfig _config;
		private readonly WebSocket _webSocket;

		public WebSocketClient(ILogger logger, IWebSocketPushConfig config, WebSocket webSocket, String id, String client)
		{
			_logger = logger;
			_config = config;
			_webSocket = webSocket;
			Id = id;
			Client = client;
		}

		public virtual async Task ReceiveAsync(Memory<Byte> data) => await SendRawAsync("WebSocket message received, but this endpoint is not configured to handle any incoming messages.");

		public Task SendAsync(WebSocketsMessage message) => throw new NotImplementedException();

		public async Task SendRawAsync(String message)
		{
			var messageAsBytes = System.Text.Encoding.UTF8.GetBytes(message);
			await _webSocket.SendAsync(messageAsBytes, WebSocketMessageType.Text, true, CancellationToken.None);
		}

		protected override async ValueTask DisposeOnce()
		{
			try
			{
				if (_webSocket.State != WebSocketState.Closed)
				{
					var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_config.ShutdownTimeout));
					await _webSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "shutting down", timeout.Token);
				}
			}
			catch (Exception exception)
			{
				_logger.Error("Exception thrown while trying to cleanly close Push websocket.", exception);
			}

			_webSocket.Abort();
			// disposal of the WebSocket is handled by AspNet
		}
	}
}
