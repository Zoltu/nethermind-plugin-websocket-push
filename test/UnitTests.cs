using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Evm;

namespace Zoltu.Nethermind.Plugin.WebSocket.Push.Test
{
	public class UnitTests
	{
		[Xunit.Fact]
		public async ValueTask Test()
		{
			var filters = ImmutableArray<WebSocketPush.PendingWebSocketClient.FilteredExecutionRequest>.Empty
				.Add(new WebSocketPush.PendingWebSocketClient.FilteredExecutionRequest(new Address("0xcafebeefcafebeefcafebeefcafebeefcafebeef"), 0x022c0d9f, 40000));
			var tracer = new WebSocketPush.PendingWebSocketClient.Tracer(filters);
			tracer.ReportAction(123456, 0, new Address("0xdeadbabedeadbabedeadbabedeadbabedeadbabe"), new Address("0xcafebeefcafebeefcafebeefcafebeefcafebeef"), new Byte[] { 0x02, 0x2c, 0x0d, 0x9f }, ExecutionType.Call, false);
			Xunit.Assert.NotEmpty(tracer.FilterMatches);
			await ValueTask.CompletedTask;
		}
	}
}
