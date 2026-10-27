using Microsoft.AspNetCore.Components;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Ouverture d'un écran avec une barre de chargement : la première construction d'un écran peut prendre un moment
/// (compilation à la volée du code des composants). La barre, affichée par AppShell, est dessinée avant la construction
/// de l'écran : celle-ci occupe .NET, mais la vue web continue de l'animer. Service « scoped ».
/// </summary>
public sealed class ScreenNavigator(NavigationManager navigation)
{
    /// <summary>Temps laissé à la vue web pour dessiner la barre avant que l'écran soit construit.</summary>
    public static readonly TimeSpan PaintDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Un écran est en cours d'ouverture : la barre est affichée.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Prévient AppShell que la barre doit apparaître ou disparaître.</summary>
    public event Action? LoadingChanged;

    /// <summary>Affiche la barre, lui laisse le temps d'être dessinée, puis ouvre l'écran (adresse de <c>AppRoutes</c>).</summary>
    public async Task OpenAsync(string route)
    {
        SetLoading(true);
        await Task.Delay(PaintDelay);
        navigation.NavigateTo(route);
    }

    /// <summary>Appelé par AppShell une fois le nouvel écran construit.</summary>
    public void Loaded() => SetLoading(false);

    private void SetLoading(bool loading)
    {
        if (IsLoading != loading)
        {
            IsLoading = loading;
            LoadingChanged?.Invoke();
        }
    }
}
