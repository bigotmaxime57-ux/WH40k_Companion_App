using Microsoft.JSInterop;
using Wh40kSim.UI.Services;
using Wh40kSim.UI.Theme;
using Wh40kSim.Web.Client.Services;

namespace Wh40kSim.Web.Client.Tests;

/// <summary>Fichiers et préférences dans le navigateur, avec le JavaScript remplacé par un faux.</summary>
public class BrowserServicesTests
{
    [Fact]
    public async Task The_theme_is_remembered_in_the_browser_storage()
    {
        var js = new FakeJSRuntime();
        var preferences = new BrowserPreferenceService(js);
        Assert.Null(await preferences.GetThemeAsync());

        await preferences.SetThemeAsync(ThemeChoice.Neon);

        Assert.Equal("neon", js.Storage[BrowserPreferenceService.ThemeKey]);
        Assert.Equal(ThemeChoice.Neon, await preferences.GetThemeAsync());
    }

    [Fact]
    public async Task An_unknown_stored_theme_is_ignored()
    {
        var js = new FakeJSRuntime();
        js.Storage[BrowserPreferenceService.ThemeKey] = "rose";

        Assert.Null(await new BrowserPreferenceService(js).GetThemeAsync());
    }

    [Fact]
    public async Task An_unavailable_storage_falls_back_to_the_default_theme()
    {
        var js = new FakeJSRuntime { StorageFails = true };
        var preferences = new BrowserPreferenceService(js);

        Assert.Null(await preferences.GetThemeAsync());
        await preferences.SetThemeAsync(ThemeChoice.Steel); // sans erreur
    }

    [Fact]
    public async Task Opening_returns_the_chosen_file_or_null_if_cancelled()
    {
        var js = new FakeJSRuntime { FileToOpen = new OpenedFile("escouade.json", "{ }") };
        var files = new BrowserScenarioFileService(new BrowserScripts(js));

        Assert.Equal(new OpenedFile("escouade.json", "{ }"), await files.OpenAsync());

        js.FileToOpen = null;
        Assert.Null(await files.OpenAsync());
    }

    [Fact]
    public async Task Saving_downloads_the_file_under_the_suggested_name()
    {
        var js = new FakeJSRuntime();
        var files = new BrowserScenarioFileService(new BrowserScripts(js));

        string? written = await files.SaveAsync("{ \"attacker\": {} }", "Escouade.json");

        Assert.Equal("Escouade.json", written);
        Assert.Equal(("Escouade.json", "{ \"attacker\": {} }"), js.Downloaded.Single());
    }

    [Fact]
    public async Task The_module_is_imported_once()
    {
        var js = new FakeJSRuntime();
        var scripts = new BrowserScripts(js);

        await scripts.HideBootAsync();
        await scripts.PrefersDarkAsync();

        Assert.Equal(1, js.Imports);
    }

    /// <summary>Faux JavaScript : stockage du navigateur et module du client en mémoire.</summary>
    private sealed class FakeJSRuntime : IJSRuntime, IJSObjectReference
    {
        public Dictionary<string, string> Storage { get; } = [];

        public bool StorageFails { get; init; }

        public OpenedFile? FileToOpen { get; set; }

        public List<(string FileName, string Content)> Downloaded { get; } = [];

        public int Imports { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier.StartsWith("localStorage.") && StorageFails)
            {
                throw new JSException("Stockage indisponible");
            }
            object? result = identifier switch
            {
                "import" => Import(),
                "localStorage.getItem" => Storage.GetValueOrDefault((string)args![0]!),
                "localStorage.setItem" => Set((string)args![0]!, (string)args[1]!),
                "openFile" => FileToOpen,
                "downloadFile" => Download((string)args![0]!, (string)args[1]!),
                "prefersDark" => false,
                _ => null,
            };
            return ValueTask.FromResult((TValue)result!);
        }

        private object Import()
        {
            Imports++;
            return this;
        }

        private object? Set(string key, string value)
        {
            Storage[key] = value;
            return null;
        }

        private object? Download(string fileName, string content)
        {
            Downloaded.Add((fileName, content));
            return null;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
