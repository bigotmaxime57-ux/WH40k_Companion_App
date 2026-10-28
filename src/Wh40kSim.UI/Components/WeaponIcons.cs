using Wh40kSim.Core.Model;

namespace Wh40kSim.UI.Components;

/// <summary>
/// Pictogrammes du type d'arme, au format des icônes MudBlazor (contenu SVG d'un repère 24 × 24) : viseur pour le tir,
/// épées croisées pour la mêlée. Dessins repris de Lucide (« crosshair », « swords » ; licence ISC), en traits de la
/// couleur du texte (currentColor).
/// </summary>
public static class WeaponIcons
{
    private const string Stroke = "fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";

    public const string Ranged =
        $"<g {Stroke}><circle cx=\"12\" cy=\"12\" r=\"10\"/><line x1=\"22\" x2=\"18\" y1=\"12\" y2=\"12\"/>" +
        "<line x1=\"6\" x2=\"2\" y1=\"12\" y2=\"12\"/><line x1=\"12\" x2=\"12\" y1=\"6\" y2=\"2\"/>" +
        "<line x1=\"12\" x2=\"12\" y1=\"22\" y2=\"18\"/></g>";

    public const string Melee =
        $"<g {Stroke}><polyline points=\"14.5 17.5 3 6 3 3 6 3 17.5 14.5\"/><line x1=\"13\" x2=\"19\" y1=\"19\" y2=\"13\"/>" +
        "<line x1=\"16\" x2=\"20\" y1=\"16\" y2=\"20\"/><line x1=\"19\" x2=\"21\" y1=\"21\" y2=\"19\"/>" +
        "<polyline points=\"14.5 6.5 18 3 21 3 21 6 17.5 9.5\"/><line x1=\"5\" x2=\"9\" y1=\"14\" y2=\"18\"/>" +
        "<line x1=\"7\" x2=\"4\" y1=\"17\" y2=\"20\"/><line x1=\"3\" x2=\"5\" y1=\"19\" y2=\"21\"/></g>";

    public static string For(WeaponType type) => type == WeaponType.Melee ? Melee : Ranged;
}
