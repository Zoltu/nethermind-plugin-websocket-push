using System;

namespace Zoltu.Nethermind.Plugin.PushPending
{
	public class PushPendingConfig : IPushPendingConfig
	{
		public Boolean Enabled { get; set; }
		public UInt32 ShutdownTimeout { get; set; } = 10;
	}
}
