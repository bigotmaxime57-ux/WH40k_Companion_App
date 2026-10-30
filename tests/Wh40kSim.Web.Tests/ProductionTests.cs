using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wh40kSim.Web.Tests;

/// <summary>Réglages de production : point de santé, adresse du visiteur derrière le proxy de l'hébergeur.</summary>
public class ProductionTests
{
    [Fact]
    public async Task The_health_endpoint_answers()
    {
        await using var factory = Api.Create();

        using var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Behind_the_proxy_each_visitor_has_its_own_limit()
    {
        await using var factory = Api.Create(launchesPerMinute: 1).WithWebHostBuilder(b => b.UseSetting("ForwardedHeaders:Enabled", "true"));
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await LaunchAsync(client, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await LaunchAsync(client, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await LaunchAsync(client, "203.0.113.2"));   // autre visiteur
    }

    [Fact]
    public async Task Without_the_setting_the_forwarded_address_is_ignored()
    {
        await using var factory = Api.Create(launchesPerMinute: 1);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await LaunchAsync(client, "203.0.113.1"));
        // L'en-tête pourrait être falsifié par n'importe qui : sans le réglage, il ne change pas d'adresse
        Assert.Equal(HttpStatusCode.TooManyRequests, await LaunchAsync(client, "203.0.113.2"));
    }

    [Fact]
    public async Task Only_the_address_added_by_the_proxy_counts()
    {
        await using var factory = Api.Create(launchesPerMinute: 1).WithWebHostBuilder(b => b.UseSetting("ForwardedHeaders:Enabled", "true"));
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, await LaunchAsync(client, "198.51.100.7, 203.0.113.1"));
        // Première adresse choisie par le visiteur, dernière ajoutée par le proxy : c'est la même personne
        Assert.Equal(HttpStatusCode.TooManyRequests, await LaunchAsync(client, "198.51.100.8, 203.0.113.1"));
    }

    private static async Task<HttpStatusCode> LaunchAsync(HttpClient client, string forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulations")
        {
            Content = new StringContent(Api.Body(Api.Scenario(), 1_000), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        return response.StatusCode;
    }
}
