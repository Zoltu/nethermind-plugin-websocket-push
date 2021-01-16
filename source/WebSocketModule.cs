using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.WebSockets;

namespace Zoltu.Nethermind.Plugin.PushPending
{
	public sealed class WebSocketModule : DisposeAsyncOnce, IWebSocketsModule
	{
		public String Name => "pending";

		private readonly ILogger _logger;
		private readonly IJsonSerializer _jsonSerializer;
		private readonly IPushPendingConfig _config;
		private UInt32 _lastId;
		private readonly ConcurrentDictionary<String, WebSocketClient> _clients = new();

		public WebSocketModule(ILogger logger, IJsonSerializer jsonSerializer, IPushPendingConfig config)
		{
			_logger = logger;
			_jsonSerializer = jsonSerializer;
			_config = config;
		}

		public Boolean TryInit(HttpRequest request) => true;

		public IWebSocketsClient CreateClient(WebSocket webSocket, String clientName)
		{
			_logger.Info($"Instantiating pending push client: '{clientName}'.");
			var id = Interlocked.Increment(ref _lastId).ToString(CultureInfo.InvariantCulture);
			var client = new WebSocketClient(_logger, _jsonSerializer, _config, webSocket, id, clientName);
			_ = _clients.TryAdd(client.Id, client);
			return client;
		}

		public void RemoveClient(String clientId) => _clients.TryRemove(clientId, out _);

		public Task SendAsync(WebSocketsMessage message) => Task.WhenAll(_clients.Values.Select(client => client.SendAsync(message)));

		public Task SendRawAsync(String message) => Task.WhenAll(_clients.Values.Select(client => client.SendRawAsync(message)));

		public Task Send(Transaction transaction) => Task.WhenAll(_clients.Values.Select(client => client.Send(transaction)));

		protected override async ValueTask DisposeOnce() => await Task.WhenAll(_clients.Select(client => client.Value.DisposeAsync().AsTask()));
	}
}
