using System;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public class WebSocketPushConfig : IWebSocketPushConfig
	{
		public Boolean PendingEnabled { get; set; }
		public Boolean BlockEnabled { get; set; }
		public UInt32 ShutdownTimeout { get; set; } = 10;
	}
}
