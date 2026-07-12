using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PeanutsPlugin.Data;

namespace PeanutsPlugin;

/// <summary>
/// Austauschformat zum Teilen von Beständen zwischen Spielern ("Share-Datei").
///
/// Bewusst NICHT die CSV/Excel-Exporte: die sind lokalisierte, fortlaufend
/// angehängte Berichte ohne ItemIds und ohne Besitzer - als Importquelle
/// unbrauchbar. Diese Datei dagegen ist:
///   - sprachneutral (Items werden über ihre ItemId identifiziert, nicht über
///     den übersetzten Namen),
///   - eindeutig zuordenbar (Spielername als Besitzer),
///   - ein Momentaufnahme-Stand (kein Anhänge-Log).
///
/// Importierte Charaktere werden mit gesetztem Owner angelegt, nie gescannt und
/// überschreiben niemals eigene Charaktere.
/// </summary>
public static class ShareFile
{
    public const int CurrentFormat = 1;
    public const string FileName = "Peanuts Share.json";

    public class ShareData
    {
        public int Format { get; set; } = CurrentFormat;
        public string Player { get; set; } = string.Empty;
        public DateTime ExportedAt { get; set; }
        public List<ShareCharacter> Characters { get; set; } = new();
    }

    public class ShareCharacter
    {
        public string World { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<ShareCount> Counts { get; set; } = new();
    }

    public class ShareCount
    {
        public uint ItemId { get; set; }
        public bool Hq { get; set; }
        public int Quantity { get; set; }
    }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// Schreibt die eigenen (nicht importierten, nicht ausgeblendeten) Charaktere
    /// als Share-Datei. Mengen werden je Item/Qualität über ItemId abgelegt.
    /// </summary>
    public static void Export(Configuration config, string playerName, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var data = new ShareData
        {
            Player = playerName,
            ExportedAt = DateTime.Now,
        };

        foreach (var world in config.Worlds.Values)
        {
            foreach (var character in world.Characters)
            {
                // Nur eigene Charaktere teilen - importierte Fremddaten nicht weiterreichen.
                if (character.IsImported || character.IsArchived || character.HiddenFromExport)
                    continue;

                var shareChar = new ShareCharacter { World = world.Name, Name = character.Name };

                foreach (var item in TrackedItems.All)
                {
                    if (item.ItemId == 0)
                        continue;

                    var nq = character.GetTotalCount(item.Key);
                    if (nq > 0)
                        shareChar.Counts.Add(new ShareCount { ItemId = item.ItemId, Hq = false, Quantity = nq });

                    if (item.CanBeHq)
                    {
                        var hq = character.GetTotalCount(item.HqKey);
                        if (hq > 0)
                            shareChar.Counts.Add(new ShareCount { ItemId = item.ItemId, Hq = true, Quantity = hq });
                    }
                }

                data.Characters.Add(shareChar);
            }
        }

        File.WriteAllText(filePath, JsonSerializer.Serialize(data, Options));
    }

    public class ImportResult
    {
        public string Player { get; set; } = string.Empty;
        public int CharactersImported { get; set; }
        public int UnknownItems { get; set; }

        // Charaktere aus der Datei, die bei uns bereits als EIGENE existieren -
        // die werden bewusst übersprungen, nicht überschrieben.
        public int OwnCharactersSkipped { get; set; }
    }

    /// <summary>
    /// Liest eine Share-Datei ein, OHNE sie anzuwenden. Damit kann der Aufrufer
    /// erst den Besitzer klären (Spitzname zuordnen) und danach anwenden.
    /// Wirft bei ungültiger Datei; der Aufrufer fängt das ab.
    /// </summary>
    public static ShareData Read(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<ShareData>(json)
                   ?? throw new InvalidDataException("Share-Datei konnte nicht gelesen werden.");

        if (data.Format > CurrentFormat)
            throw new InvalidDataException($"Share-Datei hat ein neueres Format ({data.Format}) als dieses Plugin unterstützt ({CurrentFormat}).");

        if (string.IsNullOrWhiteSpace(data.Player))
            throw new InvalidDataException("Share-Datei enthält keinen Spielernamen.");

        return data;
    }

    /// <summary>
    /// Schlägt einen Besitzer für eine noch unbekannte Share-Datei vor, indem
    /// geprüft wird, ob ihre Charaktere schon als Import eines bestehenden
    /// Spielers vorliegen. Greift z.B. dann, wenn jemand von einem anderen Alt
    /// exportiert und dadurch ein anderer Absender-Name in der Datei steht.
    /// Liefert null, wenn sich kein Bezug herstellen lässt.
    /// </summary>
    public static string? SuggestOwner(Configuration config, ShareData data)
    {
        var known = config.Worlds.Values
            .SelectMany(w => w.Characters)
            .Where(c => c.IsImported)
            .ToDictionary(c => c.Name, c => c.Owner);

        return data.Characters
            .Select(sc => known.GetValueOrDefault(sc.Name))
            .Where(o => !string.IsNullOrEmpty(o))
            .GroupBy(o => o!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    /// <summary>Namen der Charaktere aus der Datei, die bei uns noch NICHT als Import existieren.</summary>
    public static List<string> NewCharacterNames(Configuration config, ShareData data)
    {
        var known = config.Worlds.Values
            .SelectMany(w => w.Characters)
            .Select(c => c.Name)
            .ToHashSet();

        return data.Characters
            .Select(sc => sc.Name)
            .Where(n => !known.Contains(n))
            .ToList();
    }

    /// <summary>
    /// Wendet eine gelesene Share-Datei an: legt ihre Charaktere als importierte
    /// Charaktere unter dem angegebenen Spitznamen an bzw. aktualisiert sie, und
    /// merkt sich die Zuordnung Absender-Name -> Spitzname für künftige Importe.
    /// Items, die lokal nicht getrackt werden, werden übersprungen und gezählt.
    /// </summary>
    public static ImportResult Apply(Configuration config, ShareData data, string ownerNickname)
    {
        var result = new ImportResult { Player = ownerNickname.Trim() };
        if (string.IsNullOrWhiteSpace(result.Player))
            throw new InvalidDataException("Kein Besitzername angegeben.");

        // Absender-Name (aus der Datei) -> Spitzname merken, damit derselbe
        // Absender beim nächsten Mal ohne Nachfrage erkannt wird.
        config.ShareOwnerAliases[data.Player.Trim()] = result.Player;

        // ItemId -> lokale Item-Definition. Items, die der andere Spieler trackt,
        // wir aber nicht, können nicht sinnvoll dargestellt werden.
        var byItemId = new Dictionary<uint, ItemDefinition>();
        foreach (var item in TrackedItems.All)
        {
            if (item.ItemId != 0)
                byItemId[item.ItemId] = item;
        }

        foreach (var shareChar in data.Characters)
        {
            if (string.IsNullOrWhiteSpace(shareChar.World) || string.IsNullOrWhiteSpace(shareChar.Name))
                continue;

            var character = config.GetOrCreateImportedCharacter(shareChar.World, shareChar.Name, result.Player);
            if (character == null)
            {
                // Der Charakter existiert bei uns bereits als EIGENER - unsere
                // Live-Scans sind verlässlicher als fremde Importdaten.
                result.OwnCharactersSkipped++;
                continue;
            }

            // Vollständig ersetzen: die Share-Datei ist eine Momentaufnahme,
            // kein Delta. So verschwinden auch Items, die der andere inzwischen
            // verkauft hat, statt als Karteileiche stehen zu bleiben.
            character.ItemCounts.Clear();
            character.SaddlebagCounts.Clear();
            character.DuplicateFindKeys.Clear();
            character.UsedSlots = -1;
            character.UsedSaddlebagSlots = -1;

            foreach (var count in shareChar.Counts)
            {
                if (!byItemId.TryGetValue(count.ItemId, out var item))
                {
                    result.UnknownItems++;
                    continue;
                }

                var key = count.Hq && item.CanBeHq ? item.HqKey : item.Key;
                character.ItemCounts.TryGetValue(key, out var current);
                character.ItemCounts[key] = current + count.Quantity;
            }

            result.CharactersImported++;

            // Der Import ist selbst ein Messpunkt: er wird direkt als
            // Verlaufseintrag abgelegt (mit dem Zeitstempel, zu dem der andere
            // Spieler exportiert hat - nicht dem Importzeitpunkt). Ein bereits
            // vorhandener Eintrag mit demselben Zeitstempel wird ersetzt, damit
            // ein wiederholter Import derselben Datei keine Dubletten erzeugt.
            var stamp = data.ExportedAt == default ? DateTime.Now : data.ExportedAt;
            config.History.RemoveAll(h =>
                h.Owner == result.Player &&
                h.World == shareChar.World &&
                h.Character == shareChar.Name &&
                h.Timestamp == stamp);

            config.History.Add(new HistoryEntry
            {
                Timestamp = stamp,
                World = shareChar.World,
                Character = shareChar.Name,
                Owner = result.Player,
                ItemCounts = new Dictionary<string, int>(character.ItemCounts),
                TotalGil = character.TotalGil(),
            });
        }

        config.PruneHistory();
        config.Save();
        return result;
    }

    /// <summary>Entfernt alle importierten Charaktere eines Spielers wieder - inklusive seiner Verlaufseinträge.</summary>
    public static int RemoveImportedPlayer(Configuration config, string player)
    {
        var removed = 0;
        foreach (var world in config.Worlds.Values)
            removed += world.Characters.RemoveAll(c => c.Owner == player);

        // Auch aus dem Verlauf entfernen, sonst würde der Spieler in den
        // Zeitreihen-Auswertungen als Karteileiche weiterleben.
        config.History.RemoveAll(h => h.Owner == player);

        // Und die Absender-Zuordnung lösen: sonst würde eine erneute Share-Datei
        // desselben Absenders ihn kommentarlos wieder unter diesem Spitznamen
        // anlegen, obwohl du ihn bewusst entfernt hast.
        foreach (var key in config.ShareOwnerAliases
                     .Where(kv => kv.Value == player)
                     .Select(kv => kv.Key)
                     .ToList())
        {
            config.ShareOwnerAliases.Remove(key);
        }

        if (removed > 0)
            config.Save();

        return removed;
    }

    /// <summary>Liste aller Spieler, von denen aktuell Charaktere importiert sind.</summary>
    public static List<string> ImportedPlayers(Configuration config) =>
        config.Worlds.Values
            .SelectMany(w => w.Characters)
            .Where(c => c.IsImported)
            .Select(c => c.Owner)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
}
