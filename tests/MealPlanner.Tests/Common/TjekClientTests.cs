using System.Net;
using System.Text;
using MealPlanner.Api.Common.Tjek;

namespace MealPlanner.Tests.Common;

public class TjekClientTests
{
    private const string OfferJson = """
        {"id":"kjuNephx","heading":"Egelykke danske skrabeæg","description":"10 stk.\nGælder kun med Netto+ appen",
         "pricing":{"price":18,"pre_price":null,"currency":"DKK"},
         "run_from":"2026-10-30T23:00:00+0000","run_till":"2026-11-06T22:59:59+0000","dealer_id":"9ba51"}
        """;

    [Fact]
    public async Task Parses_offer_with_tjek_date_format()
    {
        var handler = new PagedHandler(pageSizes: [1]);
        var client = new TjekClient(new HttpClient(handler) { BaseAddress = new Uri(TjekClient.BaseAddress) });

        var offers = await client.GetOffersAsync("9ba51", 55.9727, 10.1526, 20_000, TestContext.Current.CancellationToken);

        var offer = Assert.Single(offers);
        Assert.Equal("Egelykke danske skrabeæg", offer.Heading);
        Assert.Equal(18m, offer.Pricing.Price);
        Assert.Null(offer.Pricing.PrePrice);
        Assert.Equal(new DateTimeOffset(2026, 10, 30, 23, 0, 0, TimeSpan.Zero), offer.RunFrom);
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 22, 59, 59, TimeSpan.Zero), offer.RunTill);
        Assert.Contains("r_lat=55.9727&r_lng=10.1526&r_radius=20000", handler.Requests[0]);
    }

    [Fact]
    public async Task Pages_until_a_short_page()
    {
        var handler = new PagedHandler(pageSizes: [100, 100, 37]);
        var client = new TjekClient(new HttpClient(handler) { BaseAddress = new Uri(TjekClient.BaseAddress) });

        var offers = await client.GetOffersAsync("11deC", 55.9727, 10.1526, 20_000, TestContext.Current.CancellationToken);

        Assert.Equal(237, offers.Count);
        Assert.Equal(["offset=0", "offset=100", "offset=200"], handler.Requests.Select(r => r[r.IndexOf("offset=")..]));
    }

    private sealed class PagedHandler(int[] pageSizes) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.ToString());
            var size = pageSizes[Requests.Count - 1];
            var body = "[" + string.Join(",", Enumerable.Repeat(OfferJson, size)) + "]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
