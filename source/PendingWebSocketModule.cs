using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class PendingWebSocketModule : WebSocketModule<PendingWebSocketClient>
	{
		public override String Name => "pending";

		public PendingWebSocketModule(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config) : base(logger, jsonSerializer, config) { }

		protected override PendingWebSocketClient Create(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) => new(logger, jsonSerializer, config, webSocket, id, client);

		public Task Send(Transaction transaction) => Task.WhenAll(_clients.Values.Select(client => client.Send(transaction)));
	}
}
