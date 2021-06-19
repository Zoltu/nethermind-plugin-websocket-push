using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Evm;
using static Zoltu.Nethermind.Plugin.WebSocketPush.PendingWebSocketClient.FilteredExecutionRequest;

namespace Zoltu.Nethermind.Plugin.WebSocket.Push.Test
{
	public class UnitTests
	{
		[Xunit.Fact]
		public async ValueTask Test()
		{
			var filteredExecutionRequest = new WebSocketPush.PendingWebSocketClient.FilteredExecutionRequest(new Address("0xcafebeefcafebeefcafebeefcafebeefcafebeef"), 0x022c0d9f, 40000, Array.Empty<CalldataFilter>());
			var filters = ImmutableArray<WebSocketPush.PendingWebSocketClient.FilteredExecutionRequest>.Empty.Add(filteredExecutionRequest);
			var tracer = new WebSocketPush.PendingWebSocketClient.MyTxTracer(filters);
			tracer.ReportAction(123456, 0, new Address("0xdeadbabedeadbabedeadbabedeadbabedeadbabe"), new Address("0xcafebeefcafebeefcafebeefcafebeefcafebeef"), new Byte[] { 0x02, 0x2c, 0x0d, 0x9f }, ExecutionType.Call, false);
			Xunit.Assert.NotEmpty(tracer.FilterMatches);
			await ValueTask.CompletedTask;
		}
	}
}
