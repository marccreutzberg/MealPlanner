using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MealPlanner.Api.Common.Tjek;

/// <summary>
/// The only class that knows the (unofficial) Tjek API. See spec 12.1.
/// </summary>
public sealed class TjekClient(HttpClient http)
{
    public const string BaseAddress = "https://squid-api.tjek.com/v2/";
    private const int PageSize = 100; // API maximum

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new TjekDateConverter() },
    };

    /// <summary>All offers (current and next catalogue) from one chain within the radius.</summary>
    public async Task<List<TjekOffer>> GetOffersAsync(string dealerId, double latitude, double longitude,
        int radiusMeters, CancellationToken ct)
    {
        var offers = new List<TjekOffer>();
        for (var offset = 0; ; offset += PageSize)
        {
            var url = string.Create(CultureInfo.InvariantCulture,
                $"offers?dealer_ids={Uri.EscapeDataString(dealerId)}&r_lat={latitude}&r_lng={longitude}&r_radius={radiusMeters}&limit={PageSize}&offset={offset}");
            var page = await http.GetFromJsonAsync<List<TjekOffer>>(url, Json, ct) ?? [];
            offers.AddRange(page);
            if (page.Count < PageSize) return offers;
        }
    }

    /// <summary>Tjek writes offsets as "+0000", which System.Text.Json does not accept.</summary>
    private sealed class TjekDateConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.GetString()!;
            // "2026-10-30T23:00:00+0000" -> "2026-10-30T23:00:00+00:00"
            if (text.Length > 5 && text[^5] is '+' or '-' && text[^3] != ':')
                text = text.Insert(text.Length - 2, ":");
            return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }
}

public sealed record TjekOffer(
    string Id,
    string Heading,
    string? Description,
    TjekPricing Pricing,
    [property: JsonPropertyName("run_from")] DateTimeOffset RunFrom,
    [property: JsonPropertyName("run_till")] DateTimeOffset RunTill,
    [property: JsonPropertyName("dealer_id")] string DealerId);

public sealed record TjekPricing(
    decimal Price,
    [property: JsonPropertyName("pre_price")] decimal? PrePrice);
