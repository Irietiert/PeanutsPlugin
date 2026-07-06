using Dalamud.Game;

namespace PeanutsPlugin;

/// <summary>
/// Kleiner Lokalisierungs-Helfer für die Overlay-Texte. Die Sprache wird
/// einmalig beim Plugin-Start aus der Client-Spracheinstellung übernommen
/// (Systemkonfiguration -&gt; Andere -&gt; Sprache/Audio). Deutsch bleibt Deutsch,
/// alles andere (Englisch, Französisch, Japanisch) fällt auf Englisch zurück,
/// da wir aktuell nur DE/EN-Texte gepflegt haben.
///
/// Verwendung an jeder Textstelle direkt: Loc.Get("Deutscher Text", "English text").
/// Das hält Übersetzung und Originaltext an derselben Stelle im Code
/// zusammen, statt sie über eine separate Ressourcendatei/Schlüssel zu
/// verwalten - bei der überschaubaren Größe dieses Plugins weniger
/// fehleranfällig als ein Key-Value-System mit potenziell fehlenden Keys.
/// </summary>
public static class Loc
{
    public static ClientLanguage CurrentLanguage { get; set; } = ClientLanguage.German;

    private static bool IsGerman => CurrentLanguage == ClientLanguage.German;

    /// <summary>Liefert den deutschen oder englischen Text, je nach aktueller Client-Sprache.</summary>
    public static string Get(string de, string en) => IsGerman ? de : en;
}
