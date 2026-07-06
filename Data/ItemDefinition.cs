using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;

namespace PeanutsPlugin.Data;

/// <summary>
/// Ein vom Plugin getracktes Item. Die ersten 8 sind vorinstalliert (deutsche
/// Namen, ItemId/Preis/Stapelgröße werden beim Start via Lumina aufgelöst).
/// Über den "Item"-Tab können weitere Items hinzugefügt, deaktiviert oder
/// gelöscht werden - dafür werden ItemId/Preis/Stapelgröße direkt beim
/// Hinzufügen aus dem Suchtreffer übernommen.
///
/// NQ und HQ werden als eigenständige, getrennt gezählte Varianten behandelt
/// (unterschiedliche ItemId im Speicher: HQ = ItemId + 1.000.000, siehe
/// InventoryScanner) - sie teilen sich zwar denselben "Key" als Basis, landen
/// aber unter zwei verschiedenen Dictionary-Keys in ItemCounts (Key bzw.
/// HqKey) und haben unterschiedliche Verkaufspreise.
/// </summary>
public class ItemDefinition
{
    // Stabiler interner Key (JSON, UI, ItemCounts-Dictionary), sprachunabhängig.
    public required string Key { get; init; }

    public required string NameDe { get; set; }
    public string? NameEn { get; set; }

    /// <summary>Name in der aktuell eingestellten Overlay-Sprache (fällt auf Deutsch zurück, falls Englisch unbekannt).</summary>
    public string Name => Loc.CurrentLanguage == ClientLanguage.German ? NameDe : (NameEn ?? NameDe);

    public uint ItemId { get; set; }

    public bool CanBeHq { get; set; }
    public uint NpcSalePriceNq { get; set; }
    public uint NpcSalePriceHq { get; set; }

    /// <summary>Maximale Stapelgröße laut Spieldaten (meist 99, viele Materialien 999).</summary>
    public uint MaxStackSize { get; set; } = 99;

    // Steuert nur, ob AKTIV danach gescannt wird - deaktivierte Items
    // bleiben in allen Statistiken/Tabellen mit ihrem letzten Stand sichtbar.
    public bool Enabled { get; set; } = true;

    /// <summary>Dictionary-Key für die HQ-Variante in ItemCounts.</summary>
    public string HqKey => Key + ":HQ";

    /// <summary>
    /// Alle in ItemCounts tatsächlich gezählten Varianten dieses Items:
    /// immer NQ (Key), zusätzlich HQ (HqKey), falls das Item HQ-fähig ist.
    /// NQ und HQ zählen NIE zusammen als ein Stack - jede Variante hat ihre
    /// eigene Stückzahl und wird für sich durch MaxStackSize geteilt.
    /// </summary>
    public IEnumerable<(string CountKey, uint Price)> Variants()
    {
        yield return (Key, NpcSalePriceNq);
        if (CanBeHq)
            yield return (HqKey, NpcSalePriceHq);
    }

    public ItemDefinition Clone() => new()
    {
        Key = Key,
        NameDe = NameDe,
        NameEn = NameEn,
        ItemId = ItemId,
        CanBeHq = CanBeHq,
        NpcSalePriceNq = NpcSalePriceNq,
        NpcSalePriceHq = NpcSalePriceHq,
        MaxStackSize = MaxStackSize,
        Enabled = Enabled,
    };

    /// <summary>
    /// Es gibt keinen separaten "HQ-Verkaufspreis"-Wert in den Spieldaten -
    /// der NPC-Verkaufsbonus für HQ wird vom Spiel intern berechnet. Wir
    /// nähern das mit dem in der Community gängigen +10%-Aufschlag
    /// (aufgerundet) an. Falls das bei einem konkreten Item nicht mit dem
    /// tatsächlichen Verkaufspreis übereinstimmt, bitte Rückmeldung geben -
    /// der Faktor lässt sich hier zentral anpassen.
    /// </summary>
    public static uint EstimateHqPrice(uint nqPrice) =>
        nqPrice + (uint)Math.Ceiling(nqPrice * 0.10);
}

public static class TrackedItems
{
    /// <summary>
    /// Die aktuell aktive, veränderliche Item-Liste. Wird beim Plugin-Start
    /// mit der persistierten Liste aus der Configuration verknüpft (dieselbe
    /// Listen-Instanz), damit Änderungen über den Item-Tab automatisch auch
    /// gespeichert werden.
    /// </summary>
    public static List<ItemDefinition> All = CreateDefaults();

    /// <summary>Die 8 vorinstallierten Items - Ausgangszustand bei Erstinstallation.</summary>
    public static List<ItemDefinition> CreateDefaults() => new()
    {
        new ItemDefinition { Key = "Ring",       NameDe = "Geborgener Ring",                   NpcSalePriceNq = 8000 },
        new ItemDefinition { Key = "Armreif",     NameDe = "Geborgener Armreif",                NpcSalePriceNq = 9000 },
        new ItemDefinition { Key = "Ohrring",     NameDe = "Geborgener Ohrring",                NpcSalePriceNq = 10000 },
        new ItemDefinition { Key = "Halsband",    NameDe = "Geborgenes Halsband",               NpcSalePriceNq = 13000 },
        new ItemDefinition { Key = "ExRing",      NameDe = "Extravaganter geborgener Ring",     NpcSalePriceNq = 27000 },
        new ItemDefinition { Key = "ExArmreif",   NameDe = "Extravaganter geborgener Armreif",  NpcSalePriceNq = 28500 },
        new ItemDefinition { Key = "ExOhrring",   NameDe = "Extravaganter geborgener Ohrring",  NpcSalePriceNq = 30000 },
        new ItemDefinition { Key = "ExHalsband",  NameDe = "Extravagantes geborgenes Halsband", NpcSalePriceNq = 34500 },
    };

    public static List<ItemDefinition> CloneList(IEnumerable<ItemDefinition> source) =>
        source.Select(i => i.Clone()).ToList();
}
