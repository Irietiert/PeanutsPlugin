using System;
using System.Collections.Generic;
using System.Linq;

namespace PeanutsPlugin.Data;

public class CharacterData
{
    public string Name { get; set; } = string.Empty;

    // Key = ItemDefinition.Key ("Ring", ...) für NQ, ItemDefinition.HqKey ("Ring:HQ") für HQ.
    // NQ und HQ zählen NIE zusammen - jede Variante hat ihren eigenen Eintrag.
    public Dictionary<string, int> ItemCounts { get; set; } = new();

    public const int MaxInventorySlots = 140;

    // Belegte Slots im normalen Inventar (Bag 1-4), live erfasst beim Scan.
    // -1 = noch nie live erfasst (z.B. Charakter nur aus altem Verlauf wiederhergestellt).
    public int UsedSlots { get; set; } = -1;

    // Zeitpunkt der letzten tatsächlichen Erfassung (jeder Scan-Tick, nicht
    // nur bei Vollständigkeit) - für die "Zuletzt gescannt"-Spalte im
    // Edit-Tab -> Charakter.
    public DateTime? LastScannedAt { get; set; }

    // Sichtbarkeits-Schalter, verwaltet im Edit-Tab -> "Charakter". Löschen
    // NICHT den Charakter oder seine Daten - blenden ihn nur an bestimmten
    // Stellen aus, die Daten werden weiterhin ganz normal erfasst/gespeichert.
    public bool HiddenFromTool { get; set; }
    public bool HiddenFromRevenueHistory { get; set; }
    public bool HiddenFromRanking { get; set; }
    public bool HiddenFromExport { get; set; }

    public bool HasKnownSlots => UsedSlots >= 0;

    /// <summary>Freie Inventarplätze. -1, solange UsedSlots unbekannt ist.</summary>
    public int FreeSlots => HasKnownSlots ? MaxInventorySlots - UsedSlots : -1;

    /// <summary>
    /// True, wenn der Charakter mind. eines der getrackten Items (NQ oder HQ)
    /// im Inventar hat - wird gebraucht, um bei der unaufgeklappten
    /// Weltenansicht nur "farmende" Charaktere in die Slot-Summe einzurechnen.
    /// </summary>
    public bool HasAnyJewelry() => ItemCounts.Values.Any(v => v > 0);

    public long TotalGil()
    {
        long total = 0;
        foreach (var item in TrackedItems.All)
        {
            foreach (var (key, price) in item.Variants())
            {
                if (ItemCounts.TryGetValue(key, out var count))
                    total += (long)count * price;
            }
        }
        return total;
    }

    public int TotalQuantity() => ItemCounts.Values.Sum();

    /// <summary>
    /// Summe der Stacks je Item-Variante (NQ und HQ getrennt, jeweils einzeln
    /// durch die item-eigene Stapelgröße geteilt und aufgerundet).
    /// Beispiel: 195 Geborgener Ring (Stapelgröße 99) = 2 Stacks.
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

    public bool IsComplete()
    {
        foreach (var item in TrackedItems.All)
        {
            if (!item.Enabled)
                continue; // Deaktivierte Items blockieren die Erfassung nicht

            // Nur die NQ-Variante muss erfasst sein, damit "vollständig" gilt -
            // nicht jeder Charakter wird zwangsläufig je eine HQ-Version besitzen.
            if (!ItemCounts.ContainsKey(item.Key))
                return false;
        }
        return true;
    }
}

public class WorldData
{
    public string Name { get; set; } = string.Empty;

    // Max 8 Charaktere pro Welt (enforced in CharacterManager)
    public List<CharacterData> Characters { get; set; } = new();

    /// <summary>
    /// Charaktere, die im "Tool"-Tab (Übersicht/Diagramme/Heatmap) angezeigt
    /// werden sollen - schließt per Edit-Tab -&gt; "Charakter" ausgeblendete
    /// Charaktere aus. Die Rohdaten in Characters bleiben davon unberührt,
    /// es handelt sich nur um eine Anzeige-/Aggregationsfilterung.
    /// </summary>
    public IEnumerable<CharacterData> VisibleCharacters => Characters.Where(c => !c.HiddenFromTool);

    public long TotalGil()
    {
        long total = 0;
        foreach (var c in VisibleCharacters)
            total += c.TotalGil();
        return total;
    }

    public int TotalQuantity() => VisibleCharacters.Sum(c => c.TotalQuantity());

    /// <summary>
    /// Stacks dieser Welt: für jede Item-Variante (NQ/HQ getrennt) wird die
    /// Stückzahl über alle SICHTBAREN Charaktere dieser Welt aufsummiert und
    /// erst DANACH durch die item-eigene Stapelgröße geteilt und aufgerundet.
    /// </summary>
    public int TotalStacks()
    {
        var total = 0;
        var visible = VisibleCharacters.ToList();
        foreach (var item in TrackedItems.All)
        {
            foreach (var (key, _) in item.Variants())
            {
                var itemTotal = visible.Sum(c => c.ItemCounts.TryGetValue(key, out var count) ? count : 0);
                total += StackMath.CeilDiv(itemTotal, (int)item.MaxStackSize);
            }
        }
        return total;
    }

    /// <summary>
    /// Aggregierte Slot-Anzeige dieser Welt (unaufgeklappte Ansicht):
    /// nur sichtbare Charaktere, die mindestens eines der getrackten Items im
    /// Inventar haben UND bereits live gescannt wurden (Slot-Daten bekannt),
    /// fließen ein. Freie Slots und Maximum (Anzahl solcher Charaktere x 140)
    /// werden je Charakter aufsummiert.
    /// </summary>
    public (int Free, int Max) AggregatedSlots()
    {
        var free = 0;
        var max = 0;
        foreach (var c in VisibleCharacters)
        {
            if (!c.HasAnyJewelry() || !c.HasKnownSlots)
                continue;

            free += c.FreeSlots;
            max += CharacterData.MaxInventorySlots;
        }
        return (free, max);
    }
}
