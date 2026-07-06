using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PeanutsPlugin.Data;

namespace PeanutsPlugin.Windows;

public class MainWindow : Window
{
    private static readonly Vector4 BlueText = new(0.45f, 0.75f, 1f, 1f);
    private static readonly Vector4 GoldText = new(1f, 0.85f, 0.3f, 1f);
    private static readonly Vector4 GreenText = new(0.3f, 1f, 0.3f, 1f);
    private static readonly Vector4 RedText = new(0.8f, 0.3f, 0.3f, 1f);
    private static readonly Vector4 GraySlot = new(0.6f, 0.6f, 0.6f, 1f);

    /// <summary>
    /// Farbverlauf für die "Slots"-Spalte: sattes Grün bei &gt;= greenAt freien
    /// Slots, alarmierendes Rot bei &lt;= redAt, dazwischen interpoliert.
    /// Entartet greenAt&lt;=redAt (z.B. eine Welt mit nur einem Charakter),
    /// wird auf die charakterbezogene Skala (20-140) ausgewichen.
    /// </summary>
    private static Vector4 GetSlotColor(int free, int redAt, int greenAt)
    {
        if (greenAt <= redAt)
        {
            redAt = 20;
            greenAt = 140;
        }

        if (free <= redAt)
            return RedText;
        if (free >= greenAt)
            return GreenText;

        var t = (float)(free - redAt) / (greenAt - redAt);
        return Vector4.Lerp(RedText, GreenText, t);
    }

    private readonly Plugin plugin;
    private string? selectedHistoryWorld;
    private string? selectedHistoryCharacter;
    private string exportFolderBuffer = string.Empty;
    private bool exportFolderBufferInitialized;
    private bool historyTabWasVisible;
    private string? lastAutoJumpedWorld;
    private string? lastAutoJumpedCharacter;
    private DateTime? selectedRestoreTimestamp;
    private string searchFilter = string.Empty;
    private int distributionGroupMode; // 0 = nach Welt, 1 = nach Datenzentrum
    private int historyPeriodIndex = 3; // 0=1 Woche, 1=3 Monate, 2=6 Monate, 3=1 Jahr (Default: größter Zeitraum)
    private string characterTabSearch = string.Empty;

    // --- Item-Tab ---
    private List<ItemDefinition>? itemTabDraft;
    private bool itemTabDirty;
    private string newItemSearchQuery = string.Empty;
    private string lastSearchedItemQuery = string.Empty;
    private List<(uint ItemId, string NameDe, string? NameEn, bool CanBeHq, uint PriceNq, uint PriceHq, uint StackSize)> itemSearchResults = new();
    private uint? selectedNewItemId;

    public MainWindow(Plugin plugin) : base("Peanuts Tool###PeanutsMainWindow")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 440),
            MaximumSize = new Vector2(900, 1200),
        };
    }

    public override void Draw()
    {
        AutoJumpHistoryToLastCompletedScan();

        if (!ImGui.BeginTabBar("PeanutsTabs"))
            return;

        if (ImGui.BeginTabItem("Tool"))
        {
            DrawScannerPanel();
            ImGui.Separator();
            DrawWorldTree();
            ImGui.Separator();
            DrawGlobalSummary();
            ImGui.Spacing();
            DrawGilDistributionSection();
            DrawItemCompositionSection();
            DrawHeatmapSection();
            ImGui.EndTabItem();
        }

        var historyTabOpen = ImGui.BeginTabItem("History");
        if (historyTabOpen)
        {
            if (!historyTabWasVisible)
                JumpToCurrentCharacterInHistory();

            DrawHistoryTab();
            ImGui.EndTabItem();
        }
        historyTabWasVisible = historyTabOpen;

        if (ImGui.BeginTabItem("Edit"))
        {
            DrawEditTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem(Loc.Get("Item", "Item")))
        {
            DrawItemTab();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    /// <summary>
    /// Springt bei jedem (Neu-)Öffnen des "History"-Tabs automatisch auf den
    /// aktuell eingeloggten Charakter/Welt, sofern dafür bereits Verlaufsdaten
    /// existieren. Danach kann der Nutzer frei zu anderen Charakteren wechseln,
    /// ohne dass die Auswahl während des offenen Tabs wieder zurückspringt.
    /// </summary>
    /// <summary>
    /// Springt automatisch im History-Tab auf den Charakter, dessen
    /// Erfassung gerade abgeschlossen wurde - unabhängig davon, ob der
    /// History-Tab überhaupt sichtbar ist. Merkt sich, für welche
    /// Vervollständigung schon gesprungen wurde, damit eine anschließende
    /// manuelle Auswahl des Nutzers nicht bei jedem Frame überschrieben wird.
    /// </summary>
    private void AutoJumpHistoryToLastCompletedScan()
    {
        var world = plugin.LastCompletedWorld;
        var character = plugin.LastCompletedCharacter;
        if (world == null || character == null)
            return;

        if (world == lastAutoJumpedWorld && character == lastAutoJumpedCharacter)
            return;

        selectedHistoryWorld = world;
        selectedHistoryCharacter = character;
        lastAutoJumpedWorld = world;
        lastAutoJumpedCharacter = character;
    }

    private void JumpToCurrentCharacterInHistory()
    {
        var cfg = plugin.Configuration;
        if (plugin.CurrentWorldName == null || plugin.CurrentCharacterName == null)
            return;

        var hasHistory = cfg.History.Any(h =>
            h.World == plugin.CurrentWorldName && h.Character == plugin.CurrentCharacterName);

        if (hasHistory)
        {
            selectedHistoryWorld = plugin.CurrentWorldName;
            selectedHistoryCharacter = plugin.CurrentCharacterName;
        }
    }

    private void DrawScannerPanel()
    {
        ImGui.TextUnformatted(Loc.Get("Charakter:", "Character:"));
        ImGui.SameLine();
        ImGui.TextColored(BlueText, plugin.CurrentCharacterName ?? "-");

        ImGui.TextUnformatted(Loc.Get("Welt:", "World:"));
        ImGui.SameLine();
        ImGui.TextColored(BlueText, plugin.CurrentWorldName ?? "-");

        if (plugin.CurrentWorldName != null)
        {
            ImGui.TextUnformatted(Loc.Get("Datenzentrum:", "Data Center:"));
            ImGui.SameLine();
            ImGui.TextColored(BlueText, DataCenters.GetDataCenter(plugin.CurrentWorldName));
        }

        ImGui.TextUnformatted(Loc.Get("Status:", "Status:"));
        ImGui.SameLine();
        if (plugin.ScannerRunning)
            ImGui.TextColored(GreenText, Loc.Get("● Läuft", "● Running"));
        else
            ImGui.TextColored(RedText, Loc.Get("● Gestoppt", "● Stopped"));

        if (plugin.LastScanComplete)
            ImGui.TextColored(GreenText, Loc.Get("Erfassung abgeschlossen.", "Scan complete."));

        if (ImGui.Button(plugin.ScannerRunning ? Loc.Get("Stop", "Stop") : Loc.Get("Start", "Start"), new Vector2(110, 30)))
        {
            if (plugin.ScannerRunning)
                plugin.StopScanner();
            else
                plugin.StartScanner();
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("Reset", "Reset"), new Vector2(110, 30)))
            ImGui.OpenPopup("ConfirmReset");

        if (ImGui.BeginPopup("ConfirmReset"))
        {
            ImGui.TextColored(RedText, Loc.Get("Alle Werte auf 0 setzen?", "Reset all values to 0?"));
            ImGui.TextDisabled(Loc.Get(
                "Der aktuelle Stand wird zuerst als Snapshot im Verlauf gesichert.",
                "The current state is saved as a snapshot to history first."));
            ImGui.TextDisabled(Loc.Get(
                "Welten/Charaktere bleiben danach sichtbar, nur ihre Werte werden 0.",
                "Worlds/characters stay visible afterward, only their values become 0."));
            ImGui.Spacing();
            if (ImGui.Button(Loc.Get("Ja, Werte nullen", "Yes, reset values"), new Vector2(150, 26)))
            {
                plugin.ResetAll();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button(Loc.Get("Abbrechen", "Cancel"), new Vector2(100, 26)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("Save", "Save"), new Vector2(110, 30)))
            plugin.SaveSnapshot();

        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("Export", "Export"), new Vector2(110, 30)))
            plugin.ExportData();

        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("Zusammenfassung kopieren", "Copy summary"), new Vector2(200, 30)))
        {
            ImGui.SetClipboardText(plugin.BuildSummaryText());
            Plugin.ChatGui.Print(Loc.Get(
                "[Peanuts] Zusammenfassung in Zwischenablage kopiert.",
                "[Peanuts] Summary copied to clipboard."));
        }
    }

    /// <summary>Tages-Gil-Verlauf einer Welt (Summe aller Charaktere je Tag) für die Mini-Sparkline.</summary>
    private float[] GetWorldGilTrend(string worldName, int maxPoints = 10)
    {
        return plugin.Configuration.History
            .Where(h => h.World == worldName)
            .GroupBy(h => h.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => (float)g.Sum(h => h.TotalGil))
            .TakeLast(maxPoints)
            .ToArray();
    }

    /// <summary>Tages-Gil-Verlauf eines Charakters (letzte Messung je Tag) für die Mini-Sparkline.</summary>
    private float[] GetCharacterGilTrend(string worldName, string characterName, int maxPoints = 10)
    {
        return plugin.Configuration.History
            .Where(h => h.World == worldName && h.Character == characterName)
            .GroupBy(h => h.Timestamp.Date)
            .OrderBy(g => g.Key)
            .Select(g => (float)g.OrderBy(h => h.Timestamp).Last().TotalGil)
            .TakeLast(maxPoints)
            .ToArray();
    }

    /// <summary>Winzige Sparkline direkt neben einem Gil-Wert; zeigt nichts, wenn zu wenig Verlaufsdaten vorhanden sind.</summary>
    private static void DrawTrendSparkline(float[] values, float width = 50f, float height = 16f)
    {
        if (values.Length < 2)
            return;

        var min = values.Min();
        var max = values.Max();
        ImGui.SameLine();
        ImGui.PlotLines("##trend", values, values.Length, "", min * 0.95f, max * 1.05f,
            new Vector2(width, height), sizeof(float));
        Tooltip(
            "Gil-Trend der letzten gespeicherten Tage (aus dem Verlauf) - kein exakter Wert, nur zur groben Orientierung.",
            "Gil trend over the last saved days (from history) - not an exact value, just a rough indicator.");
    }

    /// <summary>
    /// Übersicht als Tabelle (Datenzentrum / Welt / Charakter | Gil | Stacks |
    /// Slots), damit Gil-unter-Gil, Stacks-unter-Stacks und Slots-unter-Slots
    /// sauber ausgerichtet sind - unabhängig von der Länge der Namen (Text in
    /// ImGui ist proportional, reines Auffüllen mit Leerzeichen richtet
    /// daher NICHT zuverlässig aus; eine echte Tabelle schon).
    /// Alle Spaltenbreiten sind per Drag am Spaltenrand frei verstellbar
    /// (ImGuiTableFlags.Resizable). Ein Suchfeld filtert live nach Name/Welt;
    /// nur Datenzentren/Welten mit bereits gescannten Charakteren tauchen
    /// überhaupt auf (Configuration.Worlds enthält nur besuchte Welten).
    /// </summary>
    /// <summary>
    /// Stacks über mehrere Welten hinweg (z.B. ein Datenzentrum): für jedes
    /// Item wird die Stückzahl über ALLE Charaktere ALLER übergebenen Welten
    /// aufsummiert und erst DANACH durch 99 geteilt und aufgerundet. NICHT
    /// einfach die Summe der bereits pro Welt gerundeten Stacks nehmen - das
    /// würde durch mehrfaches Aufrunden zu hoch ausfallen (Rundungsfehler
    /// akkumulieren sich sonst pro Welt statt einmal für die Gruppe).
    /// </summary>
    private static int AggregatedStacksAcrossWorlds(IEnumerable<WorldData> worlds)
    {
        var worldList = worlds.ToList();
        var total = 0;
        foreach (var item in TrackedItems.All)
        {
            foreach (var (key, _) in item.Variants())
            {
                var itemTotal = worldList.Sum(w => w.VisibleCharacters.Sum(c => c.ItemCounts.TryGetValue(key, out var v) ? v : 0));
                total += StackMath.CeilDiv(itemTotal, (int)item.MaxStackSize);
            }
        }
        return total;
    }

    private void DrawWorldTree()
    {
        ImGui.SetNextItemWidth(300);
        ImGui.InputTextWithHint("##search", Loc.Get("Suche nach Name oder Welt...", "Search by name or world..."),
            ref searchFilter, 100);
        ImGui.Spacing();

        var filter = searchFilter.Trim();
        var hasFilter = filter.Length > 0;

        if (!ImGui.BeginTable("overview_table", 4,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV |
                ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn(Loc.Get("Datenzentrum / Welt / Charakter", "Data Center / World / Character"), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Gil", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Stacks", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn(Loc.Get("Slots", "Slots"), ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableHeadersRow();

        var groups = plugin.Configuration.Worlds.Values
            .OrderBy(w => w.Name)
            .GroupBy(w => DataCenters.GetDataCenter(w.Name))
            .OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            // Pro Welt in dieser Gruppe die (ggf. gefilterten) Charaktere bestimmen.
            var worldsInGroup = new List<(WorldData World, List<CharacterData> Characters)>();

            foreach (var world in group)
            {
                List<CharacterData> characters;
                if (!hasFilter)
                {
                    characters = world.VisibleCharacters.ToList();
                }
                else
                {
                    var worldMatches = world.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
                    characters = worldMatches
                        ? world.VisibleCharacters.ToList()
                        : world.VisibleCharacters
                            .Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                    if (characters.Count == 0)
                        continue; // Diese Welt hat keinen Treffer -> ausblenden
                }

                worldsInGroup.Add((world, characters));
            }

            if (worldsInGroup.Count == 0)
                continue; // Ganzes Datenzentrum hat keinen Treffer -> ausblenden

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            if (hasFilter)
                ImGui.SetNextItemOpen(true, ImGuiCond.Always); // Treffer beim Suchen sofort sichtbar, ohne Extra-Klick

            var dcExpanded = ImGui.TreeNodeEx($"{group.Key}###dc_{group.Key}", ImGuiTreeNodeFlags.SpanFullWidth);

            ImGui.TableNextColumn();
            ImGui.TextColored(GoldText, $"{worldsInGroup.Sum(w => w.World.TotalGil()):N0}");
            Tooltip(
                "Gesamtwert (Gil) aller sichtbaren Charaktere dieses Datenzentrums: Stückzahl × NPC-Verkaufspreis, über alle getrackten Items summiert.",
                "Total value (Gil) of all visible characters in this data center: quantity × NPC sell price, summed across all tracked items.");
            ImGui.TableNextColumn();
            ImGui.TextColored(BlueText, AggregatedStacksAcrossWorlds(worldsInGroup.Select(w => w.World)).ToString());
            Tooltip(
                "Benötigte Inventar-Stacks: je Item (NQ/HQ getrennt) Stückzahl ÷ maximale Stapelgröße, aufgerundet, dann summiert.",
                "Required inventory stacks: per item (NQ/HQ separate) quantity ÷ max stack size, rounded up, then summed.");
            ImGui.TableNextColumn();

            var dcFree = 0;
            var dcMax = 0;
            foreach (var (w, _) in worldsInGroup)
            {
                var (f, m) = w.AggregatedSlots();
                dcFree += f;
                dcMax += m;
            }

            if (dcMax == 0)
                ImGui.TextColored(GraySlot, "-");
            else
                ChartHelpers.DrawSlotBar(dcFree, dcMax, GetSlotColor(dcFree, 140, dcMax));
            Tooltip(
                "Freie Inventarplätze, summiert über alle Charaktere dieses Datenzentrums, die mind. ein Item besitzen und bereits gescannt wurden. Grün = viel Platz, Rot = fast voll.",
                "Free inventory slots, summed across all characters in this data center that own at least one item and have been scanned. Green = plenty of room, red = nearly full.");

            if (!dcExpanded)
                continue;

            foreach (var (world, characters) in worldsInGroup)
                DrawWorldRow(world, characters, hasFilter);

            ImGui.TreePop();
        }

        ImGui.EndTable();
    }

    private void DrawWorldRow(WorldData world, List<CharacterData> charactersToShow, bool forceOpen)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Indent();

        if (forceOpen)
            ImGui.SetNextItemOpen(true, ImGuiCond.Always);

        var worldExpanded = ImGui.TreeNodeEx($"{world.Name}###world_{world.Name}", ImGuiTreeNodeFlags.SpanFullWidth);
        ImGui.Unindent();
        ImGui.TableNextColumn();
        ImGui.TextColored(GoldText, $"{world.TotalGil():N0}");
        Tooltip(
            "Gesamtwert (Gil) aller sichtbaren Charaktere dieser Welt: Stückzahl × NPC-Verkaufspreis, über alle getrackten Items summiert.",
            "Total value (Gil) of all visible characters on this world: quantity × NPC sell price, summed across all tracked items.");
        DrawTrendSparkline(GetWorldGilTrend(world.Name));
        ImGui.TableNextColumn();
        ImGui.TextColored(BlueText, world.TotalStacks().ToString());
        Tooltip(
            "Benötigte Inventar-Stacks: je Item (NQ/HQ getrennt) Stückzahl ÷ maximale Stapelgröße, aufgerundet, dann summiert.",
            "Required inventory stacks: per item (NQ/HQ separate) quantity ÷ max stack size, rounded up, then summed.");
        ImGui.TableNextColumn();

        var (free, max) = world.AggregatedSlots();
        if (max == 0)
            ImGui.TextColored(GraySlot, "-");
        else
            ChartHelpers.DrawSlotBar(free, max, GetSlotColor(free, 140, max));
        Tooltip(
            "Freie Inventarplätze, summiert über alle Charaktere dieser Welt, die mind. ein Item besitzen und bereits gescannt wurden. Grün = viel Platz, Rot = fast voll.",
            "Free inventory slots, summed across all characters on this world that own at least one item and have been scanned. Green = plenty of room, red = nearly full.");

        if (!worldExpanded)
            return;

        foreach (var character in charactersToShow)
            DrawCharacterRow(world, character);

        ImGui.TreePop();
    }

    private void DrawCharacterRow(WorldData world, CharacterData character)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Indent();
        var charExpanded = ImGui.TreeNodeEx(
            $"{character.Name}###char_{world.Name}_{character.Name}", ImGuiTreeNodeFlags.SpanFullWidth);
        ImGui.Unindent();
        ImGui.TableNextColumn();
        ImGui.TextColored(GoldText, $"{character.TotalGil():N0}");
        Tooltip(
            "Gesamtwert (Gil) dieses Charakters: Stückzahl × NPC-Verkaufspreis, über alle getrackten Items (NQ/HQ getrennt) summiert.",
            "This character's total value (Gil): quantity × NPC sell price, summed across all tracked items (NQ/HQ separate).");
        DrawTrendSparkline(GetCharacterGilTrend(world.Name, character.Name));
        ImGui.TableNextColumn();
        ImGui.TextColored(BlueText, character.TotalStacks().ToString());
        Tooltip(
            "Benötigte Inventar-Stacks: je Item (NQ/HQ getrennt) Stückzahl ÷ maximale Stapelgröße, aufgerundet, dann summiert.",
            "Required inventory stacks: per item (NQ/HQ separate) quantity ÷ max stack size, rounded up, then summed.");
        ImGui.TableNextColumn();

        if (character.HasKnownSlots)
            ChartHelpers.DrawSlotBar(character.FreeSlots, CharacterData.MaxInventorySlots, GetSlotColor(character.FreeSlots, 20, 140));
        else
            ImGui.TextColored(GraySlot, "?");
        Tooltip(
            character.HasKnownSlots
                ? "Freie Plätze im normalen Inventar (von 140). Grün = viel Platz, Rot = fast voll. Wird bei jedem Scan aktualisiert."
                : "Noch nie live gescannt - unbekannt, wie viele Inventarplätze frei sind. Einmal einloggen und scannen, um das zu erfassen.",
            character.HasKnownSlots
                ? "Free slots in the regular inventory (out of 140). Green = plenty of room, red = nearly full. Updated on every scan."
                : "Never scanned live yet - unknown how many inventory slots are free. Log in and scan once to capture this.");

        if (!charExpanded)
            return;

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.Indent();
        DrawItemBreakdownTable(world, character);
        ImGui.Unindent();
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        ImGui.TableNextColumn();

        ImGui.TreePop();
    }

    /// <summary>
    /// Item-Aufschlüsselung eines Charakters, ebenfalls als Tabelle
    /// (Item | Stück | Stacks). NQ und HQ eines Items erscheinen als eigene
    /// Zeile ("Item" bzw. "Item (HQ)"), da sie nie zusammen als ein Stack
    /// zählen. Die "Stück"-Zelle ist als Mini-Heatmap eingefärbt (kräftig =
    /// viel, relativ zur stärksten Zeile), darunter ein Balkendiagramm für
    /// den direkten optischen Vergleich.
    /// </summary>
    private void DrawItemBreakdownTable(WorldData world, CharacterData character)
    {
        var rows = TrackedItems.All
            .SelectMany(item => item.Variants().Select(v => (
                Label: v.CountKey == item.Key ? item.Name : Loc.Get($"{item.Name} (HQ)", $"{item.Name} (HQ)"),
                Count: character.ItemCounts.TryGetValue(v.CountKey, out var c) ? c : 0,
                StackSize: (int)item.MaxStackSize)))
            .ToList();

        var ownMax = rows.Count > 0 ? rows.Max(r => r.Count) : 0;

        if (!ImGui.BeginTable($"items_{world.Name}_{character.Name}", 3,
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn(Loc.Get("Item", "Item"), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Loc.Get("Stueck", "Qty"), ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Stacks", ImGuiTableColumnFlags.WidthFixed, 70);

        foreach (var row in rows)
        {
            var stacks = StackMath.CeilDiv(row.Count, row.StackSize);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Label);
            ImGui.TableNextColumn();

            var intensity = ownMax > 0 ? row.Count / (float)ownMax : 0f;
            var bg = Vector4.Lerp(new Vector4(0.16f, 0.16f, 0.16f, 1f), new Vector4(0.85f, 0.45f, 0.1f, 1f), intensity);
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.ColorConvertFloat4ToU32(bg));
            ImGui.TextUnformatted(row.Count.ToString());
            Tooltip(
                "Besessene Stückzahl dieses Items/dieser Qualität. Zellfarbe zeigt die relative Stärke im Vergleich zum eigenen stärksten Item.",
                "Owned quantity of this item/quality. Cell color shows relative strength compared to this character's own strongest item.");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(stacks.ToString());
            Tooltip(
                $"Stückzahl ÷ maximale Stapelgröße ({row.StackSize}), aufgerundet - so viele Inventar-Stacks belegt dieses Item.",
                $"Quantity ÷ max stack size ({row.StackSize}), rounded up - this many inventory stacks this item occupies.");
        }

        ImGui.EndTable();

        var barValues = rows.Select(r => (float)r.Count).ToArray();
        var barMax = barValues.Length > 0 ? barValues.Max() : 0f;

        ImGui.Spacing();
        ImGui.PlotHistogram("##itembars", barValues, barValues.Length, "", 0f,
            barMax * 1.1f, new Vector2(260, 60), sizeof(float));

        ImGui.Spacing();
        ImGui.TextColored(GoldText, Loc.Get($"Gesamt: {character.TotalGil():N0} Gil", $"Total: {character.TotalGil():N0} Gil"));
        ImGui.Spacing();
    }

    private void DrawGlobalSummary()
    {
        var cfg = plugin.Configuration;
        ImGui.Spacing();
        ImGui.TextColored(GoldText, Loc.Get(
            $"Umsatz aller Welten: {cfg.GlobalTotalGil():N0} Gil",
            $"Total across all worlds: {cfg.GlobalTotalGil():N0} Gil"));
        Tooltip(
            "Summe des Gil-Werts aller sichtbaren Charaktere über alle Welten/Datenzentren hinweg.",
            "Sum of the Gil value of all visible characters across all worlds/data centers.");
        ImGui.TextColored(BlueText, Loc.Get(
            $"Stacks aller Welten: {cfg.GlobalTotalStacks()}",
            $"Stacks across all worlds: {cfg.GlobalTotalStacks()}"));
        Tooltip(
            "Summe der benötigten Inventar-Stacks (je Item/Qualität einzeln aufgerundet) über alle sichtbaren Charaktere.",
            "Sum of required inventory stacks (each item/quality rounded up individually) across all visible characters.");
        ImGui.TextUnformatted(Loc.Get(
            $"Gezählte Charaktere: {cfg.GlobalCharacterCount()}",
            $"Characters counted: {cfg.GlobalCharacterCount()}"));
        Tooltip(
            "Anzahl der Charaktere, die aktuell im Tool-Tab sichtbar sind (im Edit-Tab -> Charakter ausgeblendete zählen nicht mit).",
            "Number of characters currently visible in the Tool tab (ones hidden via Edit tab -> Character don't count).");

        if (cfg.LastCheckpointTimestamp.HasValue)
        {
            var deltaGil = cfg.GlobalTotalGil() - cfg.LastCheckpointGil;
            var deltaStacks = cfg.GlobalTotalStacks() - cfg.LastCheckpointStacks;
            var color = deltaGil >= 0 ? GreenText : RedText;
            var gilSign = deltaGil >= 0 ? "+" : "";
            var stacksSign = deltaStacks >= 0 ? "+" : "";
            ImGui.TextColored(color, Loc.Get(
                $"Seit letztem Speichern/Export ({cfg.LastCheckpointTimestamp:dd.MM.yyyy HH:mm}): {gilSign}{deltaGil:N0} Gil, {stacksSign}{deltaStacks} Stacks",
                $"Since last save/export ({cfg.LastCheckpointTimestamp:MM/dd/yyyy HH:mm}): {gilSign}{deltaGil:N0} Gil, {stacksSign}{deltaStacks} stacks"));
            Tooltip(
                "Veränderung von Gil/Stacks seit dem letzten Klick auf 'Save' oder 'Export' - aktualisiert sich bei jedem weiteren Save/Export.",
                "Change in Gil/stacks since the last time 'Save' or 'Export' was clicked - updates with every further save/export.");
        }
        else
        {
            ImGui.TextDisabled(Loc.Get(
                "Noch nicht gespeichert/exportiert - keine Delta-Anzeige möglich.",
                "Not saved/exported yet - no delta display available."));
        }
    }

    /// <summary>Donut-Diagramm für den Gil-Anteil pro Welt oder Datenzentrum am Gesamtumsatz.</summary>
    private void DrawGilDistributionSection()
    {
        if (!ImGui.CollapsingHeader(Loc.Get("📊 Gil-Verteilung", "📊 Gil Distribution")))
            return;

        ImGui.RadioButton(Loc.Get("Nach Welt", "By World"), ref distributionGroupMode, 0);
        ImGui.SameLine();
        ImGui.RadioButton(Loc.Get("Nach Datenzentrum", "By Data Center"), ref distributionGroupMode, 1);
        ImGui.Spacing();

        var cfg = plugin.Configuration;
        List<ChartHelpers.Segment> segments;

        if (distributionGroupMode == 0)
        {
            segments = cfg.Worlds.Values
                .Where(w => w.TotalGil() > 0)
                .OrderByDescending(w => w.TotalGil())
                .Select((w, i) => new ChartHelpers.Segment(w.Name, w.TotalGil(), ChartHelpers.Palette[i % ChartHelpers.Palette.Length]))
                .ToList();
        }
        else
        {
            segments = cfg.Worlds.Values
                .GroupBy(w => DataCenters.GetDataCenter(w.Name))
                .Select(g => (Label: g.Key, Value: g.Sum(w => w.TotalGil())))
                .Where(x => x.Value > 0)
                .OrderByDescending(x => x.Value)
                .Select((x, i) => new ChartHelpers.Segment(x.Label, x.Value, ChartHelpers.Palette[i % ChartHelpers.Palette.Length]))
                .ToList();
        }

        ChartHelpers.DrawDonutChart(segments, 90f, $"{cfg.GlobalTotalGil():N0}\nGil");
    }

    /// <summary>Gestapeltes Balkendiagramm je Welt: Aufteilung des Welt-Gilwerts auf die 8 Items.</summary>
    private void DrawItemCompositionSection()
    {
        if (!ImGui.CollapsingHeader(Loc.Get("🧩 Item-Zusammensetzung je Welt", "🧩 Item Composition per World")))
            return;

        foreach (var world in plugin.Configuration.Worlds.Values.OrderByDescending(w => w.TotalGil()))
        {
            if (world.TotalGil() <= 0)
                continue;

            ImGui.TextUnformatted(world.Name);

            var segments = TrackedItems.All
                .Select((item, i) =>
                {
                    long gil = 0;
                    foreach (var (key, price) in item.Variants())
                    {
                        var qty = world.VisibleCharacters.Sum(c => c.ItemCounts.TryGetValue(key, out var v) ? v : 0);
                        gil += (long)qty * price;
                    }
                    return new ChartHelpers.Segment(item.Name, gil, ChartHelpers.Palette[i % ChartHelpers.Palette.Length]);
                })
                .ToList();

            ChartHelpers.DrawStackedBar(segments, 400f, 24f);
            ImGui.Spacing();
        }
    }

    /// <summary>
    /// Heatmap-Tabelle je Welt: Zeilen = Charaktere, Spalten = die 8 Items,
    /// Zellfarbe je Item-Spalte relativ zum stärksten Charakter dieser Welt
    /// eingefärbt - zeigt auf einen Blick, wer bei welchem Item am meisten hat.
    /// </summary>
    private void DrawHeatmapSection()
    {
        if (!ImGui.CollapsingHeader(Loc.Get("🔥 Item-Heatmap je Welt", "🔥 Item Heatmap per World")))
            return;

        ImGui.TextDisabled(Loc.Get(
            "Zeigt, welcher Charakter bei welchem Item am stärksten eingesammelt hat.",
            "Shows which character has collected the most of each item."));
        ImGui.Spacing();

        foreach (var world in plugin.Configuration.Worlds.Values.OrderBy(w => w.Name))
        {
            if (!world.VisibleCharacters.Any())
                continue;

            if (!ImGui.TreeNodeEx($"{world.Name}###heatmap_{world.Name}"))
                continue;

            DrawWorldHeatmapTable(world);
            ImGui.TreePop();
        }
    }

    /// <summary>Kombinierte NQ+HQ-Stückzahl eines Items bei einem Charakter (für die Heatmap - grobe Übersicht, kein Stack-Wert).</summary>
    private static int CombinedCount(CharacterData character, ItemDefinition item) =>
        item.Variants().Sum(v => character.ItemCounts.TryGetValue(v.CountKey, out var c) ? c : 0);

    private void DrawWorldHeatmapTable(WorldData world)
    {
        var visible = world.VisibleCharacters.ToList();
        var maxPerItem = TrackedItems.All.ToDictionary(
            item => item.Key,
            item => visible.Max(c => CombinedCount(c, item)));

        if (!ImGui.BeginTable($"heatmap_table_{world.Name}", TrackedItems.All.Count + 1,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn(Loc.Get("Charakter", "Character"), ImGuiTableColumnFlags.WidthStretch);
        foreach (var item in TrackedItems.All)
            ImGui.TableSetupColumn(item.Name, ImGuiTableColumnFlags.WidthFixed, 65);
        ImGui.TableHeadersRow();

        foreach (var character in visible)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(character.Name);

            foreach (var item in TrackedItems.All)
            {
                ImGui.TableNextColumn();
                var value = CombinedCount(character, item);
                var max = maxPerItem[item.Key];
                var intensity = max > 0 ? value / (float)max : 0f;

                var bg = Vector4.Lerp(new Vector4(0.16f, 0.16f, 0.16f, 1f), new Vector4(0.85f, 0.45f, 0.1f, 1f), intensity);
                ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.ColorConvertFloat4ToU32(bg));
                ImGui.TextUnformatted(value.ToString());
            }
        }

        ImGui.EndTable();
    }

    private void DrawEditTab()
    {
        var cfg = plugin.Configuration;

        ImGui.TextUnformatted(Loc.Get("Sprache", "Language"));
        ImGui.TextDisabled(Loc.Get(
            "Legt die Overlay-Sprache manuell fest, unabhängig von der Client-Sprache.",
            "Manually sets the overlay language, independent of the client language."));
        ImGui.Spacing();

        var currentLang = cfg.LanguageOverride;
        if (ImGui.RadioButton(Loc.Get("Automatisch (Client-Sprache)", "Automatic (client language)"), currentLang == "Auto"))
        {
            cfg.LanguageOverride = "Auto";
            cfg.Save();
            plugin.ApplyLanguageSetting();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("Deutsch", currentLang == "German"))
        {
            cfg.LanguageOverride = "German";
            cfg.Save();
            plugin.ApplyLanguageSetting();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("English", currentLang == "English"))
        {
            cfg.LanguageOverride = "English";
            cfg.Save();
            plugin.ApplyLanguageSetting();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (!exportFolderBufferInitialized)
        {
            exportFolderBuffer = cfg.ExportFolder;
            exportFolderBufferInitialized = true;
        }

        ImGui.TextUnformatted(Loc.Get("Exportformate für \"Tataru's Note\"", "Export formats for \"Tataru's Note\""));
        ImGui.TextDisabled(Loc.Get(
            "Beide können gleichzeitig aktiv sein. Jeder Export hängt einen neuen,",
            "Both can be active at once. Every export appends a new,"));
        ImGui.TextDisabled(Loc.Get(
            "datierten Block an - bestehende Daten werden nicht überschrieben.",
            "dated block - existing data is never overwritten."));
        ImGui.Spacing();

        var exportCsv = cfg.ExportAsCsv;
        if (ImGui.Checkbox("CSV (Tataru's Note.csv)", ref exportCsv))
        {
            cfg.ExportAsCsv = exportCsv;
            cfg.Save();
        }

        var exportExcel = cfg.ExportAsExcel;
        if (ImGui.Checkbox("Excel (Tataru's Note.xlsx)", ref exportExcel))
        {
            cfg.ExportAsExcel = exportExcel;
            cfg.Save();
        }

        ImGui.Spacing();
        ImGui.TextDisabled(Loc.Get(
            "Google Sheets wird nicht direkt unterstützt.",
            "Google Sheets isn't directly supported."));
        ImGui.TextDisabled(Loc.Get(
            "Alternative: die CSV-Datei in Google Sheets über Datei > Importieren laden.",
            "Alternative: load the CSV file into Google Sheets via File > Import."));

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted(Loc.Get("Export-Ordner", "Export folder"));
        ImGui.TextDisabled(Loc.Get(
            "Nur der Ordner, z.B. C:\\Users\\Name\\Documents\\Peanuts",
            "Just the folder, e.g. C:\\Users\\Name\\Documents\\Peanuts"));
        ImGui.TextDisabled(Loc.Get(
            "Leer lassen, um den Standardordner im Plugin-Ordner zu verwenden.",
            "Leave empty to use the default folder inside the plugin's config directory."));
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##exportfolder", ref exportFolderBuffer, 512);

        ImGui.Spacing();
        if (ImGui.Button(Loc.Get("Speichern", "Save"), new Vector2(120, 28)))
        {
            cfg.ExportFolder = exportFolderBuffer.Trim();
            cfg.Save();
            Plugin.ChatGui.Print(Loc.Get("[Peanuts] Export-Ordner gespeichert.", "[Peanuts] Export folder saved."));
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("Auf Standard zurücksetzen", "Reset to default"), new Vector2(200, 28)))
        {
            exportFolderBuffer = string.Empty;
            cfg.ExportFolder = string.Empty;
            cfg.Save();
        }

        ImGui.Spacing();
        ImGui.TextUnformatted(Loc.Get("Aktuell wirksamer Ordner:", "Currently effective folder:"));
        ImGui.TextColored(BlueText, plugin.GetEffectiveExportFolder());

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawCharacterTabSection();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(RedText, Loc.Get("Gefahrenzone", "Danger zone"));
        ImGui.TextDisabled(Loc.Get(
            "Löscht die komplette Overlay-Struktur (alle Welten & Charaktere",
            "Deletes the complete overlay structure (all worlds & characters"));
        ImGui.TextDisabled(Loc.Get(
            "verschwinden komplett, nicht nur ihre Werte). Der Verlauf bleibt erhalten.",
            "disappear entirely, not just their values). History is kept."));
        ImGui.Spacing();

        if (ImGui.Button(Loc.Get("Broken", "Broken"), new Vector2(120, 30)))
            ImGui.OpenPopup("ConfirmBroken");

        if (ImGui.BeginPopup("ConfirmBroken"))
        {
            ImGui.TextColored(RedText, Loc.Get(
                "Wirklich das komplette Overlay löschen?",
                "Really delete the entire overlay?"));
            ImGui.TextDisabled(Loc.Get(
                "Alle Welten und Charaktere werden vollständig entfernt.",
                "All worlds and characters will be removed entirely."));
            ImGui.TextDisabled(Loc.Get(
                "Dies kann nicht rückgängig gemacht werden.",
                "This cannot be undone."));
            ImGui.Spacing();
            if (ImGui.Button(Loc.Get("Ja, komplett löschen", "Yes, delete everything"), new Vector2(170, 26)))
            {
                plugin.FullWipe();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button(Loc.Get("Abbrechen", "Cancel"), new Vector2(100, 26)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    /// <summary>
    /// Kleiner An/Aus-Schalter mit derselben Grün(an)/Rot(aus)-Farbmechanik,
    /// die überall im Overlay für Ampel-artige Zustände genutzt wird.
    /// Gibt true zurück, wenn der Nutzer geklickt hat (Wert muss dann vom
    /// Aufrufer umgeschaltet werden).
    /// </summary>
    /// <summary>Zeigt einen Erklärungs-Tooltip, wenn das zuletzt gezeichnete Element gehovert wird.</summary>
    private static void Tooltip(string de, string en)
    {
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.Get(de, en));
    }

    private bool DrawToggleSwitch(string id, bool isOn)
    {
        var label = isOn ? Loc.Get("An", "On") : Loc.Get("Aus", "Off");
        ImGui.PushStyleColor(ImGuiCol.Text, isOn ? GreenText : RedText);
        var clicked = ImGui.Button($"{label}##{id}", new Vector2(45, 0));
        ImGui.PopStyleColor();
        return clicked;
    }

    /// <summary>
    /// Verwaltung einzelner Charaktere: Sichtbarkeit im Tool-Tab, im
    /// Umsatzverlauf, im Datencenter-Ranking und beim Export separat
    /// schaltbar, plus Löschen (nur mit gehaltener STRG-Taste). Jede
    /// Änderung löst automatisch Save+Export+Scan aus, damit sie sofort
    /// überall sichtbar wird. Die zugrunde liegenden Daten (Stückzahlen,
    /// Verlauf) bleiben von den Sichtbarkeits-Schaltern unberührt - nur
    /// "Löschen" entfernt den Charakter tatsächlich.
    /// </summary>
    private void DrawCharacterTabSection()
    {
        if (!ImGui.CollapsingHeader(Loc.Get("Charakter", "Character")))
            return;

        ImGui.SetNextItemWidth(300);
        ImGui.InputTextWithHint("##chartabsearch",
            Loc.Get("Suche nach Name oder Welt...", "Search by name or world..."), ref characterTabSearch, 100);
        ImGui.Spacing();

        var filter = characterTabSearch.Trim();
        var now = DateTime.Now;

        (string World, CharacterData? Delete) pendingDelete = (string.Empty, null);

        if (ImGui.BeginTable("character_tab_table", 9,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY,
                new Vector2(0, 400)))
        {
            ImGui.TableSetupColumn(Loc.Get("Datenzentrum", "Data Center"), ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn(Loc.Get("Welt", "World"), ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableSetupColumn(Loc.Get("Charakter", "Character"), ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(Loc.Get("Zuletzt gescannt", "Last scanned"), ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn(Loc.Get("Tool", "Tool"), ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn(Loc.Get("Verlauf", "History"), ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn(Loc.Get("Ranking", "Ranking"), ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn(Loc.Get("Export", "Export"), ImGuiTableColumnFlags.WidthFixed, 55);
            ImGui.TableSetupColumn(Loc.Get("Löschen", "Delete"), ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableHeadersRow();

            foreach (var world in plugin.Configuration.Worlds.Values.OrderBy(w => w.Name))
            {
                foreach (var character in world.Characters)
                {
                    if (filter.Length > 0 &&
                        !world.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                        !character.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var isStale = character.LastScannedAt == null || (now - character.LastScannedAt.Value).TotalDays > 90;

                    ImGui.TableNextRow();
                    if (isStale)
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.ColorConvertFloat4ToU32(new Vector4(0.55f, 0.5f, 0.1f, 0.5f)));

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(DataCenters.GetDataCenter(world.Name));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(world.Name);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(character.Name);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(character.LastScannedAt?.ToString("dd.MM.yyyy") ?? Loc.Get("nie", "never"));

                    ImGui.TableNextColumn();
                    if (DrawToggleSwitch($"tool_{world.Name}_{character.Name}", !character.HiddenFromTool))
                    {
                        character.HiddenFromTool = !character.HiddenFromTool;
                        plugin.RefreshAfterCharacterTabChange();
                    }

                    ImGui.TableNextColumn();
                    if (DrawToggleSwitch($"hist_{world.Name}_{character.Name}", !character.HiddenFromRevenueHistory))
                    {
                        character.HiddenFromRevenueHistory = !character.HiddenFromRevenueHistory;
                        plugin.RefreshAfterCharacterTabChange();
                    }

                    ImGui.TableNextColumn();
                    if (DrawToggleSwitch($"rank_{world.Name}_{character.Name}", !character.HiddenFromRanking))
                    {
                        character.HiddenFromRanking = !character.HiddenFromRanking;
                        plugin.RefreshAfterCharacterTabChange();
                    }

                    ImGui.TableNextColumn();
                    if (DrawToggleSwitch($"exp_{world.Name}_{character.Name}", !character.HiddenFromExport))
                    {
                        character.HiddenFromExport = !character.HiddenFromExport;
                        plugin.RefreshAfterCharacterTabChange();
                    }

                    ImGui.TableNextColumn();
                    if (ImGui.Button(Loc.Get("Löschen", "Delete") + $"##del_{world.Name}_{character.Name}"))
                    {
                        if (ImGui.GetIO().KeyCtrl)
                            ImGui.OpenPopup($"ConfirmDeleteChar_{world.Name}_{character.Name}");
                    }

                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(Loc.Get(
                            "STRG (CTRL) gedrückt halten und klicken, um wirklich zu löschen.",
                            "Hold CTRL and click to actually delete."));

                    if (ImGui.BeginPopup($"ConfirmDeleteChar_{world.Name}_{character.Name}"))
                    {
                        ImGui.TextColored(RedText, Loc.Get(
                            $"\"{character.Name}\" aus \"{world.Name}\" wirklich löschen?",
                            $"Really delete \"{character.Name}\" from \"{world.Name}\"?"));
                        ImGui.TextDisabled(Loc.Get(
                            "Der Verlauf dieses Charakters bleibt erhalten.",
                            "This character's history is kept."));
                        ImGui.Spacing();
                        if (ImGui.Button(Loc.Get("Ja, löschen", "Yes, delete"), new Vector2(120, 26)))
                        {
                            pendingDelete = (world.Name, character);
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button(Loc.Get("Abbrechen", "Cancel"), new Vector2(100, 26)))
                            ImGui.CloseCurrentPopup();
                        ImGui.EndPopup();
                    }
                }
            }

            ImGui.EndTable();
        }

        if (pendingDelete.Delete != null)
        {
            plugin.DeleteCharacter(pendingDelete.World, pendingDelete.Delete.Name);
            plugin.RefreshAfterCharacterTabChange();
        }
    }

    /// <summary>
    /// Liste aller früheren Overlay-Zustände (Save/Reset-Batches) mit der
    /// Möglichkeit, einen davon komplett wiederherzustellen - ersetzt dabei
    /// die aktuelle Welten/Charakter-Struktur im Tool-Tab.
    /// </summary>
    private void DrawRestoreSection()
    {
        ImGui.TextUnformatted(Loc.Get("Altes Overlay wiederherstellen", "Restore a previous overlay"));
        ImGui.TextDisabled(Loc.Get(
            "Ersetzt die aktuelle Übersicht im Tool-Tab komplett durch einen früheren Stand.",
            "Replaces the current overview in the Tool tab entirely with a previous state."));

        var batches = plugin.GetHistoryBatches();
        selectedRestoreTimestamp ??= batches.First().Timestamp;
        if (!batches.Any(b => b.Timestamp == selectedRestoreTimestamp))
            selectedRestoreTimestamp = batches.First().Timestamp;

        var characterWord = Loc.Get("Charakter(e)", "character(s)");
        var selectedLabel = batches
            .Where(b => b.Timestamp == selectedRestoreTimestamp)
            .Select(b => $"{b.Timestamp:dd.MM.yyyy HH:mm} ({b.CharacterCount} {characterWord})")
            .FirstOrDefault() ?? "-";

        ImGui.SetNextItemWidth(320);
        if (ImGui.BeginCombo("##restorebatch", selectedLabel))
        {
            foreach (var batch in batches)
            {
                var label = $"{batch.Timestamp:dd.MM.yyyy HH:mm} ({batch.CharacterCount} {characterWord})";
                if (ImGui.Selectable(label, batch.Timestamp == selectedRestoreTimestamp))
                    selectedRestoreTimestamp = batch.Timestamp;
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.Get("Wiederherstellen", "Restore"), new Vector2(150, 26)))
            ImGui.OpenPopup("ConfirmRestore");

        if (ImGui.BeginPopup("ConfirmRestore"))
        {
            ImGui.TextColored(RedText, Loc.Get(
                "Aktuelles Overlay wirklich ersetzen?",
                "Really replace the current overlay?"));
            ImGui.TextDisabled(Loc.Get(
                "Die jetzige Welten/Charakter-Struktur im Tool-Tab wird komplett durch",
                "The current worlds/characters structure in the Tool tab will be fully"));
            ImGui.TextDisabled(Loc.Get(
                $"den Stand vom {selectedRestoreTimestamp:dd.MM.yyyy HH:mm} ersetzt.",
                $"replaced by the state from {selectedRestoreTimestamp:MM/dd/yyyy HH:mm}."));
            ImGui.TextDisabled(Loc.Get(
                "Slot-Daten gelten danach als unbekannt, bis neu gescannt wird.",
                "Slot data counts as unknown afterward, until re-scanned."));
            ImGui.Spacing();
            if (ImGui.Button(Loc.Get("Ja, wiederherstellen", "Yes, restore"), new Vector2(160, 26)) && selectedRestoreTimestamp.HasValue)
            {
                plugin.RestoreFromHistory(selectedRestoreTimestamp.Value);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button(Loc.Get("Abbrechen", "Cancel"), new Vector2(100, 26)))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private void DrawHistoryTab()
    {
        var cfg = plugin.Configuration;
        if (cfg.History.Count == 0)
        {
            ImGui.TextDisabled(Loc.Get(
                "Noch keine gespeicherten Snapshots. Im Tool-Tab auf 'Save' klicken.",
                "No saved snapshots yet. Click 'Save' in the Tool tab."));
            return;
        }

        DrawRestoreSection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // --- Schritt 1: Welt auswählen ---
        var worlds = cfg.History
            .Select(h => h.World)
            .Distinct()
            .OrderBy(w => w)
            .ToList();

        selectedHistoryWorld ??= worlds.First();
        if (!worlds.Contains(selectedHistoryWorld))
            selectedHistoryWorld = worlds.First();

        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo(Loc.Get("Welt", "World"), selectedHistoryWorld))
        {
            foreach (var world in worlds)
            {
                if (ImGui.Selectable(world, world == selectedHistoryWorld))
                {
                    selectedHistoryWorld = world;
                    selectedHistoryCharacter = null; // Charakterauswahl bei Weltwechsel zurücksetzen
                }
            }

            ImGui.EndCombo();
        }

        // --- Schritt 2: Charakter innerhalb der gewählten Welt auswählen ---
        // Charaktere, die im Edit-Tab -> Charakter für den Umsatzverlauf
        // ausgeblendet wurden, tauchen hier nicht auf - ihre Daten werden
        // aber weiterhin ganz normal erfasst.
        var characters = cfg.History
            .Where(h => h.World == selectedHistoryWorld)
            .Select(h => h.Character)
            .Distinct()
            .Where(name => cfg.FindCharacter(selectedHistoryWorld, name)?.HiddenFromRevenueHistory != true)
            .OrderBy(c => c)
            .ToList();

        if (characters.Count == 0)
        {
            ImGui.TextDisabled(Loc.Get(
                "Alle Charaktere dieser Welt sind im Umsatzverlauf ausgeblendet (Edit-Tab -> Charakter).",
                "All characters of this world are hidden from the revenue history (Edit tab -> Character)."));
            return;
        }

        selectedHistoryCharacter ??= characters.First();
        if (!characters.Contains(selectedHistoryCharacter))
            selectedHistoryCharacter = characters.First();

        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo(Loc.Get("Charakter", "Character"), selectedHistoryCharacter))
        {
            foreach (var character in characters)
            {
                if (ImGui.Selectable(character, character == selectedHistoryCharacter))
                    selectedHistoryCharacter = character;
            }

            ImGui.EndCombo();
        }

        // Bei mehreren Messungen am selben Tag reicht eine Anzeige pro Tag -
        // es wird jeweils die letzte (aktuellste) Messung des Tages genommen.
        var entries = cfg.History
            .Where(h => h.World == selectedHistoryWorld && h.Character == selectedHistoryCharacter)
            .GroupBy(h => h.Timestamp.Date)
            .Select(g => g.OrderBy(h => h.Timestamp).Last())
            .OrderBy(h => h.Timestamp)
            .ToList();

        if (entries.Count == 0)
            return;

        // --- Zeitraum-Auswahl: filtert rückwirkend vom letzten Eintrag aus ---
        ImGui.Spacing();
        ImGui.RadioButton(Loc.Get("1 Woche", "1 week"), ref historyPeriodIndex, 0);
        ImGui.SameLine();
        ImGui.RadioButton(Loc.Get("3 Monate", "3 months"), ref historyPeriodIndex, 1);
        ImGui.SameLine();
        ImGui.RadioButton(Loc.Get("6 Monate", "6 months"), ref historyPeriodIndex, 2);
        ImGui.SameLine();
        ImGui.RadioButton(Loc.Get("1 Jahr", "1 year"), ref historyPeriodIndex, 3);

        var periodDays = historyPeriodIndex switch
        {
            0 => 7,
            1 => 90,
            2 => 180,
            _ => 365,
        };
        var cutoff = entries.Last().Timestamp.AddDays(-periodDays);
        entries = entries.Where(e => e.Timestamp >= cutoff).ToList();

        if (entries.Count == 0)
        {
            ImGui.TextDisabled(Loc.Get(
                "Keine Daten im gewählten Zeitraum.",
                "No data in the selected period."));
            return;
        }

        var values = entries.Select(e => (float)e.TotalGil).ToArray();
        var maxValue = values.Length > 0 ? values.Max() : 0f;

        ImGui.Spacing();
        ImGui.TextUnformatted(Loc.Get(
            $"Umsatzverlauf: {entries.First().Timestamp:dd.MM.yyyy} - {entries.Last().Timestamp:dd.MM.yyyy}",
            $"Revenue history: {entries.First().Timestamp:MM/dd/yyyy} - {entries.Last().Timestamp:MM/dd/yyyy}"));
        ImGui.PlotLines(
            "##gilplot",
            values,
            values.Length,
            "",
            0f,
            maxValue * 1.1f,
            new Vector2(0, 150),
            sizeof(float));

        ImGui.Spacing();
        if (ImGui.BeginTable("history_table", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn(Loc.Get("Datum", "Date"));
            ImGui.TableSetupColumn(Loc.Get("Gesamt Gil", "Total Gil"));
            ImGui.TableSetupColumn("Stacks");
            ImGui.TableHeadersRow();

            foreach (var entry in Enumerable.Reverse(entries))
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.Timestamp.ToString("dd.MM.yyyy HH:mm"));
                ImGui.TableNextColumn();
                if (entry.IsReset)
                    ImGui.TextColored(RedText, Loc.Get("Reset (0)", "Reset (0)"));
                else
                    ImGui.TextUnformatted($"{entry.TotalGil:N0}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(entry.TotalStacks().ToString());
            }

            ImGui.EndTable();
        }

        DrawDataCenterComparisonSection(entries);
    }

    /// <summary>
    /// Vergleicht alle Charaktere desselben Datenzentrums anhand ihres
    /// Gil-Zuwachses im AKTUELLEN Kalendermonat (Monatsanfang bis heute).
    /// Der Vergleich setzt sich dadurch automatisch mit jedem neuen Monat
    /// zurück, ohne dass etwas manuell zurückgesetzt werden muss. Namen
    /// werden direkt angezeigt (kein Hover nötig), der aktuell eingeloggte
    /// Charakter ist farblich hervorgehoben. Ein Zuwachs kann nicht unter 0
    /// fallen (z.B. nach einem Reset) - niedrigster Wert ist immer 0.
    /// </summary>
    private void DrawDataCenterComparisonSection(List<HistoryEntry> entries)
    {
        if (selectedHistoryWorld == null)
            return;

        var dataCenter = DataCenters.GetDataCenter(selectedHistoryWorld);
        var now = DateTime.Now;
        var start = new DateTime(now.Year, now.Month, 1);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted(Loc.Get(
            $"Schlacht der Datencenter \"{dataCenter}\" (Monat {now:MM.yyyy})",
            $"Battle of datacenter \"{dataCenter}\" (month {now:MM/yyyy})"));
        ImGui.TextDisabled(Loc.Get(
            "Zuwachs im laufenden Kalendermonat - setzt sich automatisch mit jedem neuen Monat zurück.",
            "Growth within the current calendar month - resets automatically every new month."));
        ImGui.TextDisabled(Loc.Get(
            "Zeigt nur Charaktere, die im Edit-Tab -> Charakter für das Ranking sichtbar geschaltet sind.",
            "Only shows characters set visible for the ranking in the Edit tab -> Character."));
        ImGui.Spacing();

        var cfg = plugin.Configuration;
        var rows = cfg.History
            .Where(h => DataCenters.GetDataCenter(h.World) == dataCenter && h.Timestamp >= start && h.Timestamp <= now)
            .Where(h => cfg.FindCharacter(h.World, h.Character)?.HiddenFromRanking != true)
            .GroupBy(h => (h.World, h.Character))
            .Select(g =>
            {
                var ordered = g.OrderBy(h => h.Timestamp).ToList();
                var rawDelta = ordered[^1].TotalGil - ordered[0].TotalGil;
                return (
                    World: g.Key.World,
                    Character: g.Key.Character,
                    Delta: Math.Max(0, rawDelta), // Zuwachs kann nie negativ sein, niedrigster Wert ist 0
                    LatestStacks: ordered[^1].TotalStacks()
                );
            })
            .OrderByDescending(r => r.Delta)
            .ToList();

        if (rows.Count == 0)
        {
            ImGui.TextDisabled(Loc.Get(
                "Keine Vergleichsdaten für dieses Datenzentrum in diesem Monat.",
                "No comparison data for this data center this month."));
            return;
        }

        if (!ImGui.BeginTable("dc_comparison_table", 4,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            return;

        ImGui.TableSetupColumn(Loc.Get("Rang", "Rank"), ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(Loc.Get("Charakter", "Character"), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Loc.Get("Gil (Δ diesen Monat)", "Gil (Δ this month)"), ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn(Loc.Get("Stacks (aktuell)", "Stacks (current)"), ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableHeadersRow();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var isSelf = row.World == plugin.CurrentWorldName && row.Character == plugin.CurrentCharacterName;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"#{i + 1}");
            ImGui.TableNextColumn();
            ImGui.TextColored(isSelf ? GoldText : Vector4.One, $"{row.Character} @ {row.World}");
            ImGui.TableNextColumn();
            ImGui.TextColored(row.Delta > 0 ? GreenText : GraySlot, $"+{row.Delta:N0}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.LatestStacks.ToString());
        }

        ImGui.EndTable();
    }

    /// <summary>
    /// Verwaltung der getrackten Items: aktivieren/deaktivieren per Checkbox
    /// (stoppt nur das Scannen, Werte bleiben in Statistiken erhalten),
    /// Löschen nur mit gehaltener STRG-Taste (entfernt das Item komplett),
    /// neue Items per Suche gegen den FFXIV-Item-Katalog hinzufügen. Alle
    /// Änderungen sind ein Entwurf und werden erst mit "Änderungen sichern!"
    /// tatsächlich übernommen und gespeichert. Der Nutzer wählt nur EIN Item
    /// aus - ob NQ, HQ oder beides vorkommt, erkennt das Plugin beim Scan
    /// selbst und zählt/rechnet beide Varianten von da an getrennt.
    /// </summary>
    private void DrawItemTab()
    {
        itemTabDraft ??= TrackedItems.CloneList(TrackedItems.All);

        ImGui.TextUnformatted(Loc.Get("Getrackte Items", "Tracked items"));
        ImGui.TextDisabled(Loc.Get(
            "Häkchen = wird aktiv gescannt. Deaktivieren stoppt nur das Scannen -",
            "Checkbox = actively scanned. Disabling only stops scanning -"));
        ImGui.TextDisabled(Loc.Get(
            "bisherige Werte bleiben in allen Tabellen/Statistiken sichtbar.",
            "existing values remain visible in all tables/statistics."));
        ImGui.TextDisabled(Loc.Get(
            "NPC-Preis zeigt \"NQ / HQ\", falls das Item als HQ vorkommen kann -",
            "NPC price shows \"NQ / HQ\" if the item can exist as HQ -"));
        ImGui.TextDisabled(Loc.Get(
            "NQ und HQ werden immer als eigene Stückzahl/Stack gezählt.",
            "NQ and HQ are always counted as separate quantities/stacks."));
        ImGui.Spacing();

        ItemDefinition? toDelete = null;

        if (ImGui.BeginTable("item_tab_table", 4,
                ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("##enabled", ImGuiTableColumnFlags.WidthFixed, 30);
            ImGui.TableSetupColumn(Loc.Get("Item", "Item"), ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn(Loc.Get("NPC-Preis (NQ / HQ)", "NPC Price (NQ / HQ)"), ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("##delete", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();

            foreach (var item in itemTabDraft)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var enabled = item.Enabled;
                if (ImGui.Checkbox($"##enabled_{item.Key}", ref enabled))
                {
                    item.Enabled = enabled;
                    itemTabDirty = true;
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(item.Name);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(item.CanBeHq
                    ? $"{item.NpcSalePriceNq:N0} / {item.NpcSalePriceHq:N0}"
                    : $"{item.NpcSalePriceNq:N0}");
                ImGui.TableNextColumn();

                if (ImGui.Button(Loc.Get("Löschen", "Delete") + $"##{item.Key}"))
                {
                    if (ImGui.GetIO().KeyCtrl)
                        toDelete = item;
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Loc.Get(
                        "STRG (CTRL) gedrückt halten und klicken, um wirklich zu löschen.",
                        "Hold CTRL and click to actually delete."));
            }

            ImGui.EndTable();
        }

        if (toDelete != null)
        {
            itemTabDraft.Remove(toDelete);
            itemTabDirty = true;
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted(Loc.Get("Neues Item hinzufügen", "Add new item"));
        ImGui.SetNextItemWidth(300);
        ImGui.InputTextWithHint("##itemsearch",
            Loc.Get("Item-Name suchen...", "Search item name..."), ref newItemSearchQuery, 100);

        var trimmedQuery = newItemSearchQuery.Trim();
        if (trimmedQuery != lastSearchedItemQuery)
        {
            lastSearchedItemQuery = trimmedQuery;
            itemSearchResults = plugin.SearchItems(lastSearchedItemQuery);
            selectedNewItemId = null;
        }

        if (itemSearchResults.Count > 0)
        {
            ImGui.SetNextItemWidth(340);
            var selectedLabel = itemSearchResults
                .Where(r => r.ItemId == selectedNewItemId)
                .Select(r => r.NameDe)
                .FirstOrDefault() ?? Loc.Get("Treffer wählen...", "Select a match...");

            if (ImGui.BeginCombo("##itemsearchresults", selectedLabel))
            {
                foreach (var result in itemSearchResults)
                {
                    var priceLabel = result.CanBeHq
                        ? $"{result.PriceNq:N0} / {result.PriceHq:N0} Gil"
                        : $"{result.PriceNq:N0} Gil";
                    var label = $"{result.NameDe} ({priceLabel})";
                    if (ImGui.Selectable(label, result.ItemId == selectedNewItemId))
                        selectedNewItemId = result.ItemId;
                }

                ImGui.EndCombo();
            }

            ImGui.SameLine();
            var alreadyAdded = selectedNewItemId.HasValue && itemTabDraft.Any(i => i.ItemId == selectedNewItemId.Value);
            var canAdd = selectedNewItemId.HasValue && !alreadyAdded;

            if (!canAdd)
                ImGui.BeginDisabled();

            if (ImGui.Button("+", new Vector2(30, 0)))
            {
                var picked = itemSearchResults.First(r => r.ItemId == selectedNewItemId!.Value);
                itemTabDraft.Add(new ItemDefinition
                {
                    Key = picked.ItemId.ToString(),
                    NameDe = picked.NameDe,
                    NameEn = picked.NameEn,
                    ItemId = picked.ItemId,
                    CanBeHq = picked.CanBeHq,
                    NpcSalePriceNq = picked.PriceNq,
                    NpcSalePriceHq = picked.PriceHq,
                    MaxStackSize = picked.StackSize,
                    Enabled = true,
                });
                itemTabDirty = true;
                newItemSearchQuery = string.Empty;
                lastSearchedItemQuery = string.Empty;
                itemSearchResults = new List<(uint, string, string?, bool, uint, uint, uint)>();
                selectedNewItemId = null;
            }

            if (!canAdd)
                ImGui.EndDisabled();

            if (alreadyAdded)
                ImGui.TextDisabled(Loc.Get("Dieses Item ist bereits in der Liste.", "This item is already in the list."));
        }
        else if (lastSearchedItemQuery.Length > 0)
        {
            ImGui.TextDisabled(Loc.Get("Keine Treffer.", "No matches."));
        }

        if (itemTabDirty)
        {
            ImGui.Spacing();
            if (ImGui.Button(Loc.Get("Änderungen sichern!", "Save changes!"), new Vector2(180, 30)))
            {
                plugin.SaveTrackedItems(itemTabDraft);
                itemTabDirty = false;
            }
        }
    }
}
