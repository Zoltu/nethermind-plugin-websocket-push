using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Nethermind.Logging;
using Nethermind.Serialization.Json;
using Nethermind.WebSockets;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public abstract class WebSocketModule<T> : DisposeAsyncOnce, IWebSocketsModule where T : IWebSocketsClient, IAsyncDisposable
	{
		public abstract String Name { get; }

		private readonly ILogger _logger;
		private readonly IJsonSerializer _jsonSerializer;
		private readonly IWebSocketPushConfig _config;
		private UInt32 _lastId;
		protected readonly ConcurrentDictionary<String, T> _clients = new();

		public WebSocketModule(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config)
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
			var client = Create(_logger, _jsonSerializer, _config, webSocket, id, clientName);
			_ = _clients.TryAdd(client.Id, client);
			return client;
		}

		protected abstract T Create(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client);

		public void RemoveClient(String clientId) => _clients.TryRemove(clientId, out _);

		public Task SendAsync(WebSocketsMessage message) => Task.WhenAll(_clients.Values.Select(client => client.SendAsync(message)));

		public Task SendRawAsync(String message) => Task.WhenAll(_clients.Values.Select(client => client.SendRawAsync(message)));

		protected override async ValueTask DisposeOnce() => await Task.WhenAll(_clients.Select(client => client.Value.DisposeAsync().AsTask()));
	}
}
