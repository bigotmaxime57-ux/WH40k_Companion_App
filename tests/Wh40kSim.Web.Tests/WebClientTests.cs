using System.Net;
using System.Text.Json.Nodes;

namespace Wh40kSim.Web.Tests;

/// <summary>Page du client web servie par le serveur, et limites annoncées au client.</summary>
public class WebClientTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/quick-sim")]
    [InlineData("/full-sim")]
    [InlineData("/full-sim/results")]
    public async Task Every_screen_address_serves_the_page_that_loads_the_client(string path)
    {
        await using var factory = Api.Create();

        using var response = await factory.CreateClient().GetAsync(path);
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("_framework/blazor.web.js", html);
        Assert.Contains("Wh40kSim.Web.Client.Routes", html);   // composant racine rendu par le client (WebAssembly)
        Assert.Contains("id=\"wh-boot\"", html);               // barre de chargement avant le démarrage du client
        Assert.Contains("wh40ksim.theme", html);               // thème mémorisé lu pour la barre de chargement
    }

    [Fact]
    public async Task The_limits_follow_the_configuration()
    {
        await using var factory = Api.Create().WithWebHostBuilder(builder => builder.UseSetting("Simulation:MaxIterations", "250000"));

        var limits = JsonNode.Parse(await factory.CreateClient().GetStringAsync("/api/simulations/limits"))!;

        Assert.Equal(250_000, limits["maxIterations"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_default_limit_is_one_million()
    {
        await using var factory = Api.Create();

        var limits = JsonNode.Parse(await factory.CreateClient().GetStringAsync("/api/simulations/limits"))!;

        Assert.Equal(1_000_000, limits["maxIterations"]!.GetValue<int>());
    }
}
