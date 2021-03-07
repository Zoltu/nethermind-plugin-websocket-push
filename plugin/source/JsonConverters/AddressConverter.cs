using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethermind.Core;
using Nethermind.Core.Extensions;

namespace Zoltu.Nethermind.Plugin.WebSocketPush.JsonConverters
{
	public class AddressConverter : JsonConverter<Address>
	{
		public override Address Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.String) throw new JsonException($"Expected a string, but found a {reader.TokenType}");
			var valueAsString = reader.GetString();
			if (valueAsString == null) throw new JsonException($"Expected a string, but found a null.");
			if (String.IsNullOrWhiteSpace(valueAsString)) throw new JsonException($"Expectede a hex encoded string, but found '{valueAsString}'");
			return new Address(valueAsString);
		}

		public override void Write(Utf8JsonWriter writer, Address value, JsonSerializerOptions options) => writer.WriteStringValue(Bytes.ByteArrayToHexViaLookup32Safe(value.Bytes, true));
	}
}
