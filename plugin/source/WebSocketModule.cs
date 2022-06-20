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
using Nethermind.Sockets;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public abstract class WebSocketModule<T> : DisposeAsyncOnce, IWebSocketsModule where T : WebSocketClient
	{
		public abstract String Name { get; }

		protected ILogManager LogManager { get; }
		protected ILogger Logger { get; }
		protected IJsonSerializer JsonSerializer { get; }
		private readonly IWebSocketPushConfig config;
		private UInt32 lastId;
		protected ConcurrentDictionary<String, T> Clients { get; } = new();

		public WebSocketModule(ILogManager logManager, ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config)
		{
			this.LogManager = logManager;
			this.Logger = logger;
			this.JsonSerializer = jsonSerializer;
			this.config = config;
		}

		public Boolean TryInit(HttpRequest request) => true;

		public ISocketsClient CreateClient(WebSocket webSocket, String clientName, int port, bool authenticated = false)
		{
			this.Logger.Info($"Instantiating pending push client: '{clientName}'.");
			var id = Interlocked.Increment(ref this.lastId).ToString(CultureInfo.InvariantCulture);
			var client = this.Create(this.LogManager, this.Logger, this.JsonSerializer, this.config, webSocket, id, clientName);
			_ = this.Clients.TryAdd(client.Id, client);
			return client;
		}

		protected abstract T Create(ILogManager logManager, ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client);

		public void RemoveClient(String clientId) => this.Clients.TryRemove(clientId, out _);

		public Task SendAsync(SocketsMessage message) => Task.WhenAll(this.Clients.Values.Select(client => client.SendAsync(message)));

		public Task SendRawAsync(String message) => Task.WhenAll(this.Clients.Values.Select(client => client.SendRawAsync(message)));

		protected override async ValueTask DisposeOnce() => await Task.WhenAll(this.Clients.Select(client => client.Value.DisposeAsync().AsTask()));
	}
}
