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
    public int UsedSaddlebagSlots { get; set; }
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

        ScanContainers(inventoryManager, BagsToScan, result.Inventory);
        ScanContainers(inventoryManager, SaddlebagsToScan, result.Saddlebag);

        result.UsedInventorySlots = CountUsedSlots(inventoryManager, BagsToScan);
        result.UsedSaddlebagSlots = CountUsedSlots(inventoryManager, SaddlebagsToScan);

        return result;
    }

    private static void ScanContainers(InventoryManager* inventoryManager, InventoryType[] bags, Dictionary<string, int> counts)
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

                var rawId = slot->ItemId;
                uint baseId;
                var isHq = false;

                if (rawId >= 1_000_000)
                {
                    baseId = rawId - 1_000_000;
                    isHq = true;
                }
                else if (rawId >= 500_000)
                {
                    baseId = rawId - 500_000; // Sammlerstück
                }
                else
                {
                    baseId = rawId;
                }

                foreach (var item in TrackedItems.All)
                {
                    if (!item.Enabled || item.ItemId != baseId)
                        continue;

                    var key = isHq && item.CanBeHq ? item.HqKey : item.Key;
                    counts.TryGetValue(key, out var current);
                    counts[key] = current + slot->Quantity;
                }
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
