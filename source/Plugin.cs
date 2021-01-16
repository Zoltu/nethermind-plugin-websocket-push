using System;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.JsonRpc;
using Nethermind.Logging;

namespace Zoltu.Nethermind.Plugin.PushPending
{
	public sealed class Plugin : DisposeSyncOnce, INethermindPlugin
	{
		public String Name => "PushPending";
		public String Description => "Pushes all new pending transactions to any connected WebSocket clients.";
		public String Author => "Micah";
		private WebSocketModule? _webSocketModule;
		private INethermindApi? _nethermindApi;
		private ILogger? _logger;

		public async Task Init(INethermindApi nethermindApi)
		{
			_nethermindApi = nethermindApi;
			_logger = nethermindApi.LogManager.GetClassLogger();
			if (_webSocketModule != null)
			{
				_logger.Warn($"Attempted to initialize {Name} plugin twice, refusing to re-initialize.");
				return;
			}
			var config = nethermindApi.Config<IPushPendingConfig>();
			if (!config.Enabled)
			{
				_logger.Warn($"{Name}.Enabled configuration variable set to false, halting initialization of {Name} plugin.");
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
			_webSocketModule = new WebSocketModule(_logger, jsonSerializer, config);
			nethermindApi.WebSocketsManager.AddModule(_webSocketModule);
			_logger.Info($"Subscribe to pending transactions by connecting to ws://{jsonRpcConfig.Host}:{jsonRpcConfig.WebSocketsPort}/{_webSocketModule.Name}");
			await Task.CompletedTask;
		}

		public async Task InitNetworkProtocol()
		{
			if (_webSocketModule == null) return;
			if (_nethermindApi == null || _logger == null) throw new Exception($"InitNetworkProtocol called on {Name} plugin before Init was called.");
			if (_nethermindApi.TxPool == null) throw new Exception($"TxPool not instantiated, cannot finish initialization of {Name} plugin.");
			_nethermindApi.TxPool.NewPending += (_, eventArgs) => _webSocketModule.Send(eventArgs.Transaction);
			await Task.CompletedTask;
		}

		public Task InitRpcModules() => Task.CompletedTask;

		protected override void DisposeOnce()
		{
			// TODO: figure out a better way to dispose this asynchronously
			if (_webSocketModule != null) { Task.Run(async () => await _webSocketModule.DisposeAsync()).RunSynchronously(); }
		}
	}
}
