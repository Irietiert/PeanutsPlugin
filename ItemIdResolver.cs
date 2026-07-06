using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using PeanutsPlugin.Data;

namespace PeanutsPlugin;

/// <summary>
/// Löst für alle Tracked Items ItemId, Name, NQ/HQ-Fähigkeit, NPC-Preise und
/// Stapelgröße über die Lumina Item-Sheets auf.
///
/// WICHTIG - Reparatur-Strategie: Ist bei einem Item bereits eine ItemId
/// bekannt (auch aus einem alten oder durch ein Update beschädigten
/// Speicherstand), wird IMMER direkt per RowId nachgeschlagen statt per
/// Namenstext. Das repariert Name/Preis/Stapelgröße automatisch, selbst wenn
/// diese Felder zwischenzeitlich leer/0 geworden sind (z.B. weil sich ein
/// Feldname im Code geändert hat und die gespeicherte JSON-Datei den alten
/// Namen noch enthielt). Nur wenn noch GAR keine ItemId bekannt ist
/// (Erstauflösung eines neuen Items), wird per deutschem Namen gesucht.
///
/// Das deutsche Sheet wird IMMER explizit angefordert - unabhängig von der
/// tatsächlichen Client-Sprache - weil NameDe die stabile Suchgrundlage ist.
/// Das Spiel liefert die Textdaten aller Sprachen unabhängig von der
/// UI-Sprache mit, weshalb das gezielte Anfordern von "German" bzw.
/// "English" hier problemlos funktioniert.
/// </summary>
public static class ItemIdResolver
{
    public static void ResolveAll(IDataManager dataManager, IPluginLog log)
    {
        var germanSheet = dataManager.GetExcelSheet<Item>(ClientLanguage.German);
        var englishSheet = dataManager.GetExcelSheet<Item>(ClientLanguage.English);
        if (germanSheet == null)
        {
            log.Error("[Peanuts] Item-Sheet konnte nicht geladen werden.");
            return;
        }

        foreach (var tracked in TrackedItems.All)
        {
            Item? resolvedRow = null;

            // Bevorzugt: per bereits bekannter ItemId nachschlagen - robust,
            // auch wenn Name/Preis durch einen alten Speicherstand beschädigt sind.
            if (tracked.ItemId != 0)
                resolvedRow = germanSheet.GetRowOrDefault(tracked.ItemId);

            // Fallback: Erstauflösung eines neuen Items (ItemId noch 0) oder
            // die ItemId existiert im Sheet nicht mehr -> per Namen suchen.
            if (resolvedRow == null && !string.IsNullOrEmpty(tracked.NameDe))
            {
                foreach (var candidate in germanSheet)
                {
                    if (candidate.Name.ExtractText() == tracked.NameDe)
                    {
                        resolvedRow = candidate;
                        break;
                    }
                }
            }

            if (resolvedRow == null)
            {
                log.Warning($"[Peanuts] Item \"{(string.IsNullOrEmpty(tracked.NameDe) ? tracked.Key : tracked.NameDe)}\" " +
                            "konnte weder per ItemId noch per Namen im (deutschen) Item-Sheet gefunden werden.");
                continue;
            }

            var row = resolvedRow.Value;
            tracked.ItemId = row.RowId;
            tracked.NameDe = row.Name.ExtractText();
            tracked.CanBeHq = row.CanBeHq;
            tracked.NpcSalePriceNq = (uint)row.PriceLow;
            tracked.NpcSalePriceHq = tracked.CanBeHq ? ItemDefinition.EstimateHqPrice(tracked.NpcSalePriceNq) : 0;
            tracked.MaxStackSize = row.StackSize > 0 ? (uint)row.StackSize : 99u;

            // Englischen Namen zur selben RowId nachladen, damit die
            // Sprachumschaltung im Overlay auch bei diesem Item greift.
            if (englishSheet != null)
            {
                var englishRow = englishSheet.GetRowOrDefault(row.RowId);
                if (englishRow != null)
                    tracked.NameEn = englishRow.Value.Name.ExtractText();
            }
        }
    }
}
