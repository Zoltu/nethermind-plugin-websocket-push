using System;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class Plugin : DisposeSyncOnce, INethermindPlugin
	{
		public String Name => "PushPending";
		public String Description => "Pushes all new pending transactions to any connected WebSocket clients.";
		public String Author => "Micah";
		private PendingWebSocketModule? _pendingWebSocketModule;
		private BlockWebSocketModule? _blockWebSocketModule;
		private INethermindApi? _nethermindApi;
		private ILogger? _logger;

		public async Task Init(INethermindApi nethermindApi)
		{
			_nethermindApi = nethermindApi;
			_logger = nethermindApi.LogManager.GetClassLogger();
			if (_pendingWebSocketModule != null)
			{
				_logger.Warn($"Attempted to initialize {Name} plugin twice, refusing to re-initialize.");
				return;
			}
			if (_blockWebSocketModule != null)
			{
				_logger.Warn($"Attempted to initialize {Name} plugin twice, refusing to re-initialize.");
				return;
			}
			var config = nethermindApi.Config<IWebSocketPushConfig>();
			if (!config.PendingEnabled && !config.BlockEnabled)
			{
				_logger.Warn($"{Name}.PendingEnabled or {Name}.BlockEnabled configuration variables set to false, halting initialization of {Name} plugin.");
				return;
			}
			var initConfig = nethermindApi.Config<IInitConfig>();
			if (!initConfig.WebSocketsEnabled)
			{
				_logger.Warn($"Init.WebSocketsEnabled configuration variable set to false, halting initialization of {Name} plugin.");
				return;
			}
			var jsonRpcConfig = nethermindApi.Config<IJsonRpcConfig>();
			if (!jsonRpcConfig.Enabled)
			{
				_logger.Warn($"JsonRpc.Enabled configuration variable set to false, halting initialization of {Name} plugin.");
				return;
			}
			var jsonSerializer = nethermindApi.EthereumJsonSerializer;
			if (config.PendingEnabled)
			{
				_pendingWebSocketModule = new PendingWebSocketModule(_logger, jsonSerializer, config);
				nethermindApi.WebSocketsManager.AddModule(_pendingWebSocketModule);
				_logger.Info($"Subscribe to pending transactions by connecting to ws://{jsonRpcConfig.Host}:{jsonRpcConfig.WebSocketsPort}/{_pendingWebSocketModule.Name}");
			}
			if (config.BlockEnabled)
			{
				_blockWebSocketModule = new BlockWebSocketModule(_logger, jsonSerializer, config);
				nethermindApi.WebSocketsManager.AddModule(_blockWebSocketModule);
				_logger.Info($"Subscribe to new blocks by connecting to ws://{jsonRpcConfig.Host}:{jsonRpcConfig.WebSocketsPort}/{_blockWebSocketModule.Name}");
			}
			await Task.CompletedTask;
		}

		public async Task InitNetworkProtocol()
		{
			if (_pendingWebSocketModule == null && _blockWebSocketModule == null) return;
			if (_nethermindApi == null || _logger == null) throw new Exception($"InitNetworkProtocol called on {Name} plugin before Init was called.");
			if (_pendingWebSocketModule != null)
			{
				if (_nethermindApi.TxPool == null) throw new Exception($"TxPool not instantiated, cannot finish initialization of {Name} plugin.");
				_nethermindApi.TxPool.NewPending += (_, eventArgs) => _pendingWebSocketModule.Send(eventArgs.Transaction);
			}
			if (_blockWebSocketModule != null)
			{
				if (_nethermindApi.BlockTree == null) throw new Exception($"BlockTree not instantiated, cannot finish initialization of {Name} plugin.");
				_nethermindApi.BlockTree.NewHeadBlock += (_, eventArgs) => _blockWebSocketModule.Send(eventArgs.Block);
			}
			await Task.CompletedTask;
		}

		public Task InitRpcModules() => Task.CompletedTask;

		protected override void DisposeOnce()
		{
			// TODO: figure out a better way to dispose this asynchronously
			if (_pendingWebSocketModule != null) { Task.Run(async () => await _pendingWebSocketModule.DisposeAsync()).RunSynchronously(); }
		}
	}
}
