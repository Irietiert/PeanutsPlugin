using System;
using System.Collections.Generic;

namespace PeanutsPlugin.Data;

/// <summary>
/// Ein per "Save" gespeicherter, datierter Schnappschuss der Itemzahlen
/// eines Charakters. Wird im "Verlauf"-Tab grafisch dargestellt.
/// </summary>
public class HistoryEntry
{
    public DateTime Timestamp { get; set; }
    public string World { get; set; } = string.Empty;
    public string Character { get; set; } = string.Empty;

    // Besitzer zum Zeitpunkt des Snapshots. Leer = eigener Charakter.
    // Nicht leer = importierter Charakter eines anderen Spielers; solche
    // Einträge dürfen die eigenen Gesamt-/Verlaufsauswertungen nicht verfälschen.
    public string Owner { get; set; } = string.Empty;

    public bool IsImported => !string.IsNullOrEmpty(Owner);
    public Dictionary<string, int> ItemCounts { get; set; } = new();
    public long TotalGil { get; set; }
    public bool IsReset { get; set; }

    public int TotalQuantity()
    {
        var total = 0;
        foreach (var kv in ItemCounts)
            total += kv.Value;
        return total;
    }

    /// <summary>
    /// Stacks dieses Snapshots: jede Item-Variante (NQ/HQ getrennt) wird
    /// einzeln durch ihre item-eigene Stapelgröße geteilt und aufgerundet,
    /// dann summiert.
    /// </summary>
    public int TotalStacks()
    {
        var total = 0;
        foreach (var item in TrackedItems.All)
        {
            foreach (var (key, _) in item.Variants())
            {
                ItemCounts.TryGetValue(key, out var count);
                total += StackMath.CeilDiv(count, (int)item.MaxStackSize);
            }
        }
        return total;
    }
}
