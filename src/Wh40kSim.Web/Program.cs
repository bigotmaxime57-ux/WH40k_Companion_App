using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Wh40kSim.UI.Components;
using Wh40kSim.Web;
using Wh40kSim.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Page du client web (rendu WebAssembly, sans prérendu : voir Components/App.razor)
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();

builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection(SimulationOptions.Section));
builder.Services.AddSingleton<SimulationQueue>();
builder.Services.AddSingleton<ISimulationRunner, SimulatorRunner>();
builder.Services.AddProblemDetails();
// Point de santé (/health) : interrogé par l'hébergeur pour savoir si le serveur est prêt
builder.Services.AddHealthChecks();

// Derrière le proxy de l'hébergeur (Fly.io), l'adresse du visiteur est dans X-Forwarded-For : seulement si la
// configuration l'active (ForwardedHeaders:Enabled), sinon n'importe qui pourrait choisir son adresse et contourner la
// limite par IP. Seule la dernière adresse (ajoutée par le proxy) est retenue.
bool behindProxy = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Lancements limités par adresse IP (fenêtre fixe d'une minute), refus en 429 avec le délai à attendre
builder.Services.AddRateLimiter(limiter =>
{
    limiter.AddPolicy(SimulationEndpoints.RateLimitPolicy, context =>
    {
        int permits = context.RequestServices.GetRequiredService<IOptions<SimulationOptions>>().Value.LaunchesPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "inconnue",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
    limiter.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        string wait = "dans un instant";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            int seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            response.Headers.RetryAfter = seconds.ToString();
            wait = $"dans {seconds} s";
        }
        await TypedResults.Problem(
                title: "Trop de lancements",
                statusCode: StatusCodes.Status429TooManyRequests,
                extensions: new Dictionary<string, object?> { ["errors"] = new[] { $"Limite de lancements par minute atteinte : réessayez {wait}." } })
            .ExecuteAsync(context.HttpContext);
    };
});

var app = builder.Build();

if (behindProxy)
{
    app.UseForwardedHeaders(); // avant tout ce qui lit l'adresse du visiteur (limite par IP)
}
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapSimulationEndpoints();
// Adresses des écrans (accueil, Quick sim, Full sim…) : composants routés de Wh40kSim.UI, affichés par le client
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(AppShell).Assembly);

app.Run();

/// <summary>Point d'entrée, rendu public pour les tests d'intégration (<c>WebApplicationFactory</c>).</summary>
public partial class Program;
