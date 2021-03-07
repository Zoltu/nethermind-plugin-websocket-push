using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Zoltu.Nethermind.Plugin.WebSocketPush.JsonConverters
{
	public class UInt256Converter : JsonConverter<UInt256>
	{
		public override UInt256 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.String) throw new JsonException($"Expected a string, but found a {reader.TokenType}");
			var valueAsString = reader.GetString();
			if (valueAsString == null) throw new JsonException($"Expected a string, but found a null.");
			var result = UInt256.TryParse(valueAsString, out var parsed);
			if (!result) throw new JsonException($"Expected a hex encoded number, but found {valueAsString}");
			return parsed;
		}
		public override void Write(Utf8JsonWriter writer, UInt256 value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToHexString(true));
	}
}
