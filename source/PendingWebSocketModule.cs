using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class PendingWebSocketModule : WebSocketModule<PendingWebSocketClient>
	{
		public override String Name => "pending";

		private readonly IBlockTree _blockTree;
		private readonly ITransactionProcessor _transactionProcessor;

		public PendingWebSocketModule(ILogger logger, IJsonSerializer jsonSerializer, IBlockTree blockTree, ITransactionProcessor transactionProcessor, IWebSocketPushConfig config) : base(logger, jsonSerializer, config)
		{
			_blockTree = blockTree;
			_transactionProcessor = transactionProcessor;
		}

		protected override PendingWebSocketClient Create(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) => new(logger, jsonSerializer, _blockTree, _transactionProcessor, config, webSocket, id, client);

		public Task Send(Transaction transaction) => Task.WhenAll(_clients.Values.Select(client => client.Send(transaction)));
	}
}
