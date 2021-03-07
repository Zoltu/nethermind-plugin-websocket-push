using System;
using Nethermind.Config;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public interface IWebSocketPushConfig : IConfig
	{
		[ConfigItem(Description = "If 'true' then the websocket endpoint for receiving new pending transactions will be enabled.  Accessible at `wshost:wsport/pending`.", DefaultValue = "false")]
		public Boolean PendingEnabled { get; set; }
		[ConfigItem(Description = "If 'true' then the websocket endpoint for receiving new blocks will be enabled.  Accessible at `wshost:wsport/block`.", DefaultValue = "false")]
		public Boolean BlockEnabled { get; set; }
		[ConfigItem(Description = "Number of seconds to wait for the socket to be cleanly closed on shutdown.", DefaultValue = "10")]
		public UInt32 ShutdownTimeout { get; set; }
	}
}
