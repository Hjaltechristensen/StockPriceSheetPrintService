using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StockPriceSheetPrintService.Outbound.MarketStack
{
	public class FlexibleDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
	{
		private static readonly string[] Formats =
		{
			"yyyy-MM-dd'T'HH:mm:sszzz",
			"yyyy-MM-dd'T'HH:mm:sszz"
		};

		public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var raw = reader.GetString()!;
			return DateTimeOffset.ParseExact(raw, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None);
		}

		public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
			=> writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:sszzz"));
	}
}
