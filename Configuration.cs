using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Plugin;
using PeanutsPlugin.Data;

namespace PeanutsPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public const int MaxCharactersPerWorld = 8;

    // Key = Weltname ("Raiden", "Shiva", ...)
    public Dictionary<string, WorldData> Worlds { get; set; } = new();

    // Optional overrides for the NPC-Verkaufspreise, e.g. after a patch.
    public Dictionary<string, uint> PriceOverrides { get; set; } = new();

    // Datierte Snapshots, angelegt über den "Save"-Button, für den Verlauf-Tab.
    public List<HistoryEntry> History { get; set; } = new();

    // Verlaufseinträge, die älter als so viele Tage sind, werden beim nächsten
    // Snapshot automatisch entfernt. Bewusst großzügig (> 1 Jahr), damit der
    // längste im UI wählbare Zeitraum (1 Jahr) vollständig abgedeckt bleibt.
    public const int HistoryRetentionDays = 400;

    /// <summary>
    /// Entfernt Verlaufseinträge, die älter als <see cref="HistoryRetentionDays"/>
    /// sind, damit die gespeicherte Datei nicht unbegrenzt wächst. Wird nach
    /// dem Anlegen neuer Snapshots aufgerufen.
    /// </summary>
    public void PruneHistory()
    {
        var cutoff = DateTime.Now.AddDays(-HistoryRetentionDays);
        History.RemoveAll(h => h.Timestamp < cutoff);
    }

    // Vom Nutzer im "Edit"-Tab gesetzter Export-ORDNER (nicht mehr der volle Dateiname -
    // die Datei heißt jetzt immer "Tataru's Note.csv" / ".xlsx").
    // Leer = Standardordner im Plugin-Konfigurationsordner wird verwendet.
    public string ExportFolder { get; set; } = string.Empty;

    // Im "Edit"-Tab per Kästchen wählbare Exportformate.
    public bool ExportAsCsv { get; set; } = true;
    public bool ExportAsExcel { get; set; } = false;

    // Globaler Gil/Stacks-Stand zum Zeitpunkt des letzten Save ODER Export,
    // für die "Seit letztem Speichern/Export"-Delta-Anzeige im Tool-Tab.
    public long LastCheckpointGil { get; set; }
    public int LastCheckpointStacks { get; set; }
    public DateTime? LastCheckpointTimestamp { get; set; }

    // Die vom Nutzer im "Item"-Tab verwaltete Liste getrackter Items
    // (Basis: die 8 vorinstallierten, plus ggf. selbst hinzugefügte).
    // Leer bei Erstinstallation -> wird dann mit den 8 Defaults befüllt.
    public List<ItemDefinition> TrackedItemList { get; set; } = new();

    // Manuelle Sprachüberschreibung im "Edit"-Tab, unabhängig von der
    // Client-Sprache: "Auto" (Standard, folgt der Client-Sprache), "German", "English".
    public string LanguageOverride { get; set; } = "Auto";

    // Startet der Scanner nach dem Login automatisch? Im "Edit"-Tab umschaltbar.
    // Standard true (bisheriges Verhalten). Auf false gesetzt bleibt das Tool
    // nach dem Login gestoppt, bis der Nutzer manuell auf "Start" klickt.
    public bool AutoStartOnLogin { get; set; } = true;

    // Zählen importierte Charaktere anderer Spieler in die Gesamtsummen mit?
    // Standard false: "Umsatz aller Welten" bleibt der EIGENE Bestand, fremde
    // Charaktere dienen nur dem Vergleich. Auf true gesetzt ergibt sich ein
    // gemeinsamer Gesamtbestand (z.B. für eine FC).
    public bool IncludeImportedInTotals { get; set; }

    // Name, unter dem die eigenen Daten in der Share-Datei erscheinen (der
    // "Owner" beim Empfänger). MUSS stabil bleiben, sonst legt ein erneuter
    // Export von einem anderen Alt beim Empfänger einen zweiten "Spieler" an.
    // Leer = es wird der Name des aktuell eingeloggten Charakters genutzt.
    public string ShareName { get; set; } = string.Empty;

    // Zuordnung: Absender-Name AUS DER SHARE-DATEI -> von dir vergebener
    // Spitzname. Dadurch wird derselbe Absender beim nächsten Import
    // automatisch wiedererkannt und ohne Nachfrage übernommen - auch dann,
    // wenn neue Charaktere dazugekommen sind.
    public Dictionary<string, string> ShareOwnerAliases { get; set; } = new();

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
    {
        pluginInterface = pi;

        // Erstinstallation: mit den 8 Standard-Items befüllen.
        if (TrackedItemList.Count == 0)
            TrackedItemList = TrackedItems.CreateDefaults();

        // Dieselbe Listen-Instanz teilen, damit Änderungen über den Item-Tab
        // (hinzufügen/deaktivieren/löschen) automatisch überall wirken UND
        // beim nächsten Save() mitgespeichert werden.
        TrackedItems.All = TrackedItemList;

        // Apply saved price overrides on top of the defaults.
        foreach (var item in TrackedItemList)
        {
            if (!PriceOverrides.TryGetValue(item.Key, out var price))
                continue;

            item.NpcSalePriceNq = price;
            if (item.CanBeHq)
                item.NpcSalePriceHq = ItemDefinition.EstimateHqPrice(price);
        }
    }

    public void Save() => pluginInterface?.SavePluginConfig(this);

    public WorldData GetOrCreateWorld(string worldName)
    {
        if (!Worlds.TryGetValue(worldName, out var world))
        {
            world = new WorldData { Name = worldName };
            Worlds[worldName] = world;
        }
        return world;
    }

    /// <summary>
    /// Findet den EIGENEN Charakter in der angegebenen Welt, oder legt ihn neu
    /// an, solange das 8-Charaktere-Limit der Welt nicht erreicht ist. Das Limit
    /// zählt bewusst nur eigene Charaktere - importierte Charaktere anderer
    /// Spieler (Owner gesetzt) sind davon ausgenommen, sonst würde ein Import
    /// verhindern, dass eigene Charaktere noch angelegt werden können.
    /// </summary>
    public CharacterData? GetOrCreateCharacter(string worldName, string characterName)
    {
        var world = GetOrCreateWorld(worldName);

        // Nur eigene Charaktere: ein gleichnamiger IMPORTIERTER Charakter darf
        // hier niemals zurückgegeben (und damit vom Scanner überschrieben) werden.
        var existing = world.Characters.Find(c => c.Name == characterName && !c.IsImported);
        if (existing != null)
            return existing;

        if (world.Characters.Count(c => !c.IsImported) >= MaxCharactersPerWorld)
            return null; // Limit erreicht - Aufrufer muss das im UI anzeigen

        var newChar = new CharacterData { Name = characterName };
        world.Characters.Add(newChar);
        return newChar;
    }

    /// <summary>
    /// Legt einen importierten Charakter an bzw. aktualisiert ihn. Ein Charakter
    /// ist durch Welt + Name EINDEUTIG identifiziert (in FFXIV kann es auf einer
    /// Welt keine zwei gleichnamigen Charaktere geben), der Owner ist deshalb nur
    /// ein Attribut und kein Teil des Schlüssels. Schickt dir jemand einen
    /// Charakter, den du schon von einem anderen Absender hast, wird er
    /// umgehängt statt doppelt angelegt. Unterliegt NICHT dem 8-Charaktere-Limit
    /// (das gilt nur für eigene Charaktere).
    /// </summary>
    public CharacterData? GetOrCreateImportedCharacter(string worldName, string characterName, string owner)
    {
        var world = GetOrCreateWorld(worldName);

        var existing = world.Characters.Find(c => c.Name == characterName);
        if (existing != null)
        {
            // Eigene Charaktere werden NIE von einem Import überschrieben -
            // deine Live-Scans sind immer die verlässlichere Quelle.
            if (!existing.IsImported)
                return null;

            existing.Owner = owner;
            return existing;
        }

        var newChar = new CharacterData { Name = characterName, Owner = owner };
        world.Characters.Add(newChar);
        return newChar;
    }

    // --- Zusammenführung über alle Welten ---

    public long GlobalTotalGil() => Worlds.Values.Sum(w => w.TotalGil());

    /// <summary>
    /// Stacks über alle Welten: für jede Item-Variante (NQ/HQ getrennt) wird
    /// die Stückzahl über ALLE SICHTBAREN Charaktere aller Welten aufsummiert
    /// und erst DANACH durch die item-eigene Stapelgröße geteilt und aufgerundet.
    /// </summary>
    public int GlobalTotalStacks()
    {
        var total = 0;
        foreach (var item in TrackedItems.All)
        {
            foreach (var (key, _) in item.Variants())
            {
                var itemTotal = Worlds.Values.Sum(w => w.CountedCharacters.Sum(c => c.GetTotalCount(key)));
                total += StackMath.CeilDiv(itemTotal, (int)item.MaxStackSize);
            }
        }
        return total;
    }

    public int GlobalCharacterCount() => Worlds.Values.Sum(w => w.CountedCharacters.Count());

    /// <summary>Findet einen Charakter anhand von Welt+Name, oder null falls nicht vorhanden.</summary>
    public CharacterData? FindCharacter(string worldName, string characterName) =>
        Worlds.TryGetValue(worldName, out var world) ? world.Characters.FirstOrDefault(c => c.Name == characterName) : null;
}
