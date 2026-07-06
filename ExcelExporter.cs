using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using PeanutsPlugin.Data;

namespace PeanutsPlugin;

/// <summary>
/// Exportiert den aktuellen Stand aller Welten/Charaktere als Excel-Datei
/// ("Tataru's Note.xlsx").
///
/// Anders als beim alten Exporter wird die Datei nicht mehr überschrieben:
/// jeder Export hängt einen neuen, datierten Block an das Arbeitsblatt des
/// aktuellen Kalendermonats an. Wechselt der Monat, wird automatisch ein
/// neues Arbeitsblatt (Tabellenblatt) für den neuen Monat angelegt - die
/// alten Monate bleiben als eigene Blätter in derselben Datei erhalten.
/// </summary>
public static class ExcelExporter
{
    public static void Export(Configuration config, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var workbook = File.Exists(filePath) ? new XLWorkbook(filePath) : new XLWorkbook();

        var sheetName = DateTime.Now.ToString("yyyy-MM");
        var sheet = workbook.Worksheets.Contains(sheetName)
            ? workbook.Worksheet(sheetName)
            : workbook.Worksheets.Add(sheetName);

        // Nächste freie Zeile ermitteln, damit jeder Export unten angehängt
        // wird, statt frühere Exporte zu überschreiben.
        var lastUsedRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var row = lastUsedRow > 0 ? lastUsedRow + 2 : 1;

        sheet.Cell(row, 1).Value = Loc.Get($"Export vom {DateTime.Now:dd.MM.yyyy HH:mm}", $"Export from {DateTime.Now:MM/dd/yyyy HH:mm}");
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        var headerRow = row;
        var col = 1;
        sheet.Cell(headerRow, col++).Value = Loc.Get("Welt", "World");
        sheet.Cell(headerRow, col++).Value = Loc.Get("Charakter", "Character");
        foreach (var item in TrackedItems.All)
        {
            foreach (var (key, _) in item.Variants())
                sheet.Cell(headerRow, col++).Value = key == item.Key ? item.Name : $"{item.Name} (HQ)";
        }
        sheet.Cell(headerRow, col++).Value = Loc.Get("Gesamt Stacks", "Total Stacks");
        sheet.Cell(headerRow, col).Value = Loc.Get("Gesamt Gil", "Total Gil");
        sheet.Range(headerRow, 1, headerRow, col).Style.Font.Bold = true;
        row++;

        foreach (var world in config.Worlds.Values)
        {
            foreach (var character in world.Characters.Where(c => !c.HiddenFromExport))
            {
                col = 1;
                sheet.Cell(row, col++).Value = world.Name;
                sheet.Cell(row, col++).Value = character.Name;

                foreach (var item in TrackedItems.All)
                {
                    foreach (var (key, _) in item.Variants())
                    {
                        character.ItemCounts.TryGetValue(key, out var qty);
                        sheet.Cell(row, col++).Value = qty;
                    }
                }

                sheet.Cell(row, col++).Value = character.TotalStacks();
                sheet.Cell(row, col).Value = character.TotalGil();
                row++;
            }
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
    }
}
