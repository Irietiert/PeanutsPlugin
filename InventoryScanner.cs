using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using PeanutsPlugin.Data;

namespace PeanutsPlugin;

/// <summary>
/// Liest die Anzahl der acht gesuchten Items direkt aus dem Inventar des Clients,
/// statt auf das Hovern mit der Maus über Tooltips angewiesen zu sein.
/// Das ist sprachunabhängig (arbeitet über ItemId), schneller und robuster.
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

    private readonly IPluginLog log;

    public InventoryScanner(IPluginLog log)
    {
        this.log = log;
    }

    /// <summary>
    /// Durchsucht das Inventar einmalig und liefert Key -> Stückzahl für alle
    /// bekannten TrackedItems, deren ItemId bereits aufgelöst wurde.
    /// </summary>
    /// <summary>
    /// Durchsucht das Inventar einmalig und liefert Key -> Stückzahl für alle
    /// bekannten TrackedItems, deren ItemId bereits aufgelöst wurde. NQ und
    /// HQ werden getrennt gezählt: im Spielspeicher hat die HQ-Variante eines
    /// Items immer die Basis-ItemId + 1.000.000 (feste FFXIV-Konvention).
    /// </summary>
    public Dictionary<string, int> ScanInventory()
    {
        var counts = new Dictionary<string, int>();
        foreach (var item in TrackedItems.All)
        {
            if (!item.Enabled || item.ItemId == 0)
                continue;

            counts[item.Key] = 0;
            if (item.CanBeHq)
                counts[item.HqKey] = 0;
        }

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            log.Warning("[Peanuts] InventoryManager nicht verfügbar - ist der Charakter eingeloggt?");
            return counts;
        }

        foreach (var bag in BagsToScan)
        {
            var container = inventoryManager->GetInventoryContainer(bag);
            if (container == null)
                continue;

            for (var i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot == null || slot->ItemId == 0)
                    continue;

                foreach (var item in TrackedItems.All)
                {
                    if (!item.Enabled)
                        continue;

                    if (slot->ItemId == item.ItemId)
                    {
                        counts.TryGetValue(item.Key, out var current);
                        counts[item.Key] = current + slot->Quantity;
                    }
                    else if (item.CanBeHq && slot->ItemId == item.ItemId + 1_000_000)
                    {
                        counts.TryGetValue(item.HqKey, out var current);
                        counts[item.HqKey] = current + slot->Quantity;
                    }
                }
            }
        }

        return counts;
    }

    /// <summary>
    /// Zählt die belegten Plätze im normalen Inventar (Bag 1-4, insgesamt
    /// 140 Slots) des aktuell eingeloggten Charakters - unabhängig von den
    /// 8 getrackten Items, also das exakte Gegenstück zur "129/140"-Anzeige
    /// im Inventarfenster des Spiels.
    /// </summary>
    public int GetUsedInventorySlots()
    {
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
            return 0;

        var used = 0;
        foreach (var bag in BagsToScan)
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
