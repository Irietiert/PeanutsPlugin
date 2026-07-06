using System;
using System.IO;
using System.Linq;
using System.Text;
using PeanutsPlugin.Data;

namespace PeanutsPlugin;

/// <summary>
/// Exportiert den aktuellen Stand aller Welten/Charaktere als CSV
/// (Semikolon-getrennt, passend für die deutsche Excel-Locale).
///
/// Die Datei ("Tataru's Note.csv") wird NICHT mehr bei jedem Export neu
/// angelegt, sondern mit jedem Export um einen datierten Block erweitert.
/// Wechselt der Kalendermonat, wird ein neuer "### Monat: yyyy-MM ###"-
/// Abschnitt eingefügt (CSV kennt keine echten Arbeitsblätter wie Excel,
/// daher dient dieser Marker als Ersatz für ein neues "Blatt").
/// </summary>
public static class CsvExporter
{
    public static void Export(Configuration config, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var isNewFile = !File.Exists(filePath);
        var monthLabel = DateTime.Now.ToString("yyyy-MM");

        var needsNewMonthSection = isNewFile;
        if (!isNewFile)
        {
            var lastMonthMarker = File.ReadLines(filePath, Encoding.UTF8)
                .LastOrDefault(l => l.StartsWith(Loc.Get("### Monat:", "### Month:")));
            needsNewMonthSection = lastMonthMarker == null || !lastMonthMarker.Contains(monthLabel);
        }

        var sb = new StringBuilder();

        if (needsNewMonthSection)
        {
            if (!isNewFile)
                sb.AppendLine();
            sb.AppendLine(Loc.Get($"### Monat: {monthLabel} ###", $"### Month: {monthLabel} ###"));
        }

        sb.AppendLine();
        sb.AppendLine(Loc.Get($"# Export vom {DateTime.Now:dd.MM.yyyy HH:mm}", $"# Export from {DateTime.Now:MM/dd/yyyy HH:mm}"));
        sb.AppendLine(Loc.Get("Welt;Charakter;Item;Qualitaet;Stueckzahl;NPCPreis;Gilwert", "World;Character;Item;Quality;Quantity;NpcPrice;GilValue"));

        var totalLabel = Loc.Get("GESAMT", "TOTAL");
        var allWorldsLabel = Loc.Get("ALLE WELTEN", "ALL WORLDS");

        foreach (var world in config.Worlds.Values)
        {
            var exportableCharacters = world.Characters.Where(c => !c.HiddenFromExport).ToList();

            foreach (var character in exportableCharacters)
            {
                foreach (var item in TrackedItems.All)
                {
                    foreach (var (key, price) in item.Variants())
                    {
                        var quality = key == item.Key ? "NQ" : "HQ";
                        character.ItemCounts.TryGetValue(key, out var qty);
                        var gil = (long)qty * price;
                        sb.AppendLine($"{Escape(world.Name)};{Escape(character.Name)};{Escape(item.Name)};{quality};{qty};{price};{gil}");
                    }
                }

                sb.AppendLine($"{Escape(world.Name)};{Escape(character.Name)};{totalLabel};;{character.TotalQuantity()};;{character.TotalGil()}");
            }

            var worldQty = exportableCharacters.Sum(c => c.TotalQuantity());
            var worldGil = exportableCharacters.Sum(c => c.TotalGil());
            sb.AppendLine($"{Escape(world.Name)};{totalLabel};;;{worldQty};;{worldGil}");
        }

        var allExportable = config.Worlds.Values.SelectMany(w => w.Characters.Where(c => !c.HiddenFromExport)).ToList();
        sb.AppendLine($"{allWorldsLabel};{totalLabel};;;{allExportable.Sum(c => c.TotalQuantity())};;{allExportable.Sum(c => c.TotalGil())}");

        if (isNewFile)
        {
            // UTF8 mit BOM, damit Excel Umlaute korrekt anzeigt (nur beim allerersten Anlegen nötig).
            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
        }
        else
        {
            File.AppendAllText(filePath, sb.ToString(), new UTF8Encoding(false));
        }
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
