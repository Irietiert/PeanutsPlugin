using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using PeanutsPlugin.Data;

namespace PeanutsPlugin;

/// <summary>Ergebnis eines einzelnen Scan-Durchlaufs: Hauptinventar und Satteltasche getrennt, damit Doppelfunde erkannt werden können.</summary>
public class ScanResult
{
    public Dictionary<string, int> Inventory { get; } = new();
    public Dictionary<string, int> Saddlebag { get; } = new();
    public int UsedInventorySlots { get; set; }
    public int UsedSaddlebagSlots { get; set; } = -1;

    // True, wenn die Satteltasche beim Scan tatsächlich geladen war (vor dem
    // ersten Öffnen am Rufglöckchen ist sie es oft nicht). Nur dann sind
    // Satteltaschen-Zählung/-Slots/-Kapazität aussagekräftig.
    public bool SaddlebagLoaded { get; set; }

    // Beobachtete Gesamtkapazität der Satteltasche (35 oder 70), -1 = unbekannt.
    public int SaddlebagCapacity { get; set; } = -1;
}

/// <summary>
/// Liest die Anzahl der gesuchten Items direkt aus dem Inventar UND der
/// Chocobo-Satteltasche des Clients, statt auf das Hovern mit der Maus über
/// Tooltips angewiesen zu sein. Das ist sprachunabhängig (arbeitet über
/// ItemId), schneller und robuster.
/// </summary>
public unsafe class InventoryScanner
{
    private static readonly InventoryType[] BagsToScan =
    {
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    };

    // Chocobo-Satteltasche: zwei Seiten à 35 Plätze = 70 Plätze gesamt.
    private static readonly InventoryType[] SaddlebagsToScan =
    {
        InventoryType.SaddleBag1,
        InventoryType.SaddleBag2,
    };

    private readonly IPluginLog log;

    public InventoryScanner(IPluginLog log)
    {
        this.log = log;
    }

    /// <summary>
    /// Durchsucht Hauptinventar UND Satteltasche einmalig und liefert beide
    /// getrennt zurück (für die Doppelfund-Erkennung). NQ und HQ werden
    /// getrennt gezählt: im Spielspeicher hat die HQ-Variante eines Items
    /// immer die Basis-ItemId + 1.000.000 (feste FFXIV-Konvention),
    /// Sammlerstücke die Basis-ItemId + 500.000 (werden der NQ-Zählung
    /// zugeschlagen, da sie nicht an NPCs verkauft werden können).
    /// </summary>
    public ScanResult ScanInventory()
    {
        var result = new ScanResult();
        foreach (var item in TrackedItems.All)
        {
            if (!item.Enabled || item.ItemId == 0)
                continue;

            result.Inventory[item.Key] = 0;
            result.Saddlebag[item.Key] = 0;
            if (item.CanBeHq)
            {
                result.Inventory[item.HqKey] = 0;
                result.Saddlebag[item.HqKey] = 0;
            }
        }

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            log.Warning("[Peanuts] InventoryManager nicht verfügbar - ist der Charakter eingeloggt?");
            return result;
        }

        // Einmalig eine Nachschlagetabelle Basis-ItemId -> Item aufbauen, statt
        // pro Inventar-Slot linear über alle Tracked-Items zu iterieren.
        // Doppelte ItemIds werden im Item-Tab bereits verhindert; sollte doch
        // eine doppelt vorkommen, gewinnt der letzte Eintrag (Indexer).
        var lookup = new Dictionary<uint, ItemDefinition>();
        foreach (var item in TrackedItems.All)
        {
            if (item.Enabled && item.ItemId != 0)
                lookup[item.ItemId] = item;
        }

        ScanContainers(inventoryManager, BagsToScan, result.Inventory, lookup);
        ScanContainers(inventoryManager, SaddlebagsToScan, result.Saddlebag, lookup);

        result.UsedInventorySlots = CountUsedSlots(inventoryManager, BagsToScan);

        // Satteltasche: Kapazität und Ladezustand bestimmen. Vor dem ersten
        // Öffnen der Satteltasche sind ihre Container oft null - dann gilt sie
        // als "nicht geladen" und die belegten Plätze bleiben unbekannt (-1),
        // statt fälschlich 0 (= scheinbar leer) zu melden.
        var saddlebagCapacity = 0;
        var saddlebagLoaded = false;
        foreach (var bag in SaddlebagsToScan)
        {
            var container = inventoryManager->GetInventoryContainer(bag);
            if (container != null && container->Size > 0)
            {
                saddlebagCapacity += container->Size;
                saddlebagLoaded = true;
            }
        }

        result.SaddlebagLoaded = saddlebagLoaded;
        result.SaddlebagCapacity = saddlebagLoaded ? saddlebagCapacity : -1;
        result.UsedSaddlebagSlots = saddlebagLoaded
            ? CountUsedSlots(inventoryManager, SaddlebagsToScan)
            : -1;

        return result;
    }

    private static void ScanContainers(InventoryManager* inventoryManager, InventoryType[] bags, Dictionary<string, int> counts, Dictionary<uint, ItemDefinition> lookup)
    {
        foreach (var bag in bags)
        {
            var container = inventoryManager->GetInventoryContainer(bag);
            if (container == null)
                continue;

            for (var i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot == null || slot->ItemId == 0)
                    continue;

                // HQ-Erkennung: Im echten Inventar steht in slot->ItemId die
                // BASIS-ID; die Qualität steckt im Flags-Feld (HighQuality-Bit).
                // (Der +1.000.000-Offset gilt fürs Marktbrett/andere Kontexte,
                // NICHT für Inventar-Slots - deshalb wurde HQ früher nie erkannt
                // und alles als NQ gezählt.)
                var baseId = slot->ItemId;
                var isHq = (slot->Flags & InventoryItem.ItemFlags.HighQuality) != 0;

                if (!lookup.TryGetValue(baseId, out var item))
                    continue;

                var key = isHq && item.CanBeHq ? item.HqKey : item.Key;
                counts.TryGetValue(key, out var current);
                counts[key] = current + slot->Quantity;
            }
        }
    }

    private static int CountUsedSlots(InventoryManager* inventoryManager, InventoryType[] bags)
    {
        var used = 0;
        foreach (var bag in bags)
        {
            var container = inventoryManager->GetInventoryContainer(bag);
            if (container == null)
                continue;

            for (var i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot != null && slot->ItemId != 0)
                    used++;
            }
        }

        return used;
    }
}
