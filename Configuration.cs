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
    /// Findet den Charakter in der angegebenen Welt, oder legt ihn neu an,
    /// solange das 8-Charaktere-Limit der Welt nicht erreicht ist.
    /// </summary>
    public CharacterData? GetOrCreateCharacter(string worldName, string characterName)
    {
        var world = GetOrCreateWorld(worldName);

        var existing = world.Characters.Find(c => c.Name == characterName);
        if (existing != null)
            return existing;

        if (world.Characters.Count >= MaxCharactersPerWorld)
            return null; // Limit erreicht - Aufrufer muss das im UI anzeigen

        var newChar = new CharacterData { Name = characterName };
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
                var itemTotal = Worlds.Values.Sum(w => w.VisibleCharacters.Sum(c => c.GetTotalCount(key)));
                total += StackMath.CeilDiv(itemTotal, (int)item.MaxStackSize);
            }
        }
        return total;
    }

    public int GlobalCharacterCount() => Worlds.Values.Sum(w => w.VisibleCharacters.Count());

    /// <summary>Findet einen Charakter anhand von Welt+Name, oder null falls nicht vorhanden.</summary>
    public CharacterData? FindCharacter(string worldName, string characterName) =>
        Worlds.TryGetValue(worldName, out var world) ? world.Characters.FirstOrDefault(c => c.Name == characterName) : null;
}
