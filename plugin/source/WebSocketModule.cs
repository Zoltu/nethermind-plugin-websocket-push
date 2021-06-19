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

		private readonly ILogger logger;
		private readonly IJsonSerializer jsonSerializer;
		private readonly IWebSocketPushConfig config;
		private UInt32 lastId;
		protected readonly ConcurrentDictionary<String, T> clients = new();

		public WebSocketModule(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config)
		{
			this.logger = logger;
			this.jsonSerializer = jsonSerializer;
			this.config = config;
		}

		public Boolean TryInit(HttpRequest request) => true;

		public IWebSocketsClient CreateClient(WebSocket webSocket, String clientName)
		{
			this.logger.Info($"Instantiating pending push client: '{clientName}'.");
			var id = Interlocked.Increment(ref this.lastId).ToString(CultureInfo.InvariantCulture);
			var client = this.Create(this.logger, this.jsonSerializer, this.config, webSocket, id, clientName);
			_ = this.clients.TryAdd(client.Id, client);
			return client;
		}

		protected abstract T Create(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client);

		public void RemoveClient(String clientId) => this.clients.TryRemove(clientId, out _);

		public Task SendAsync(WebSocketsMessage message) => Task.WhenAll(this.clients.Values.Select(client => client.SendAsync(message)));

		public Task SendRawAsync(String message) => Task.WhenAll(this.clients.Values.Select(client => client.SendRawAsync(message)));

		protected override async ValueTask DisposeOnce() => await Task.WhenAll(this.clients.Select(client => client.Value.DisposeAsync().AsTask()));
	}
}
