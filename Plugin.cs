using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using PeanutsPlugin.Data;
using PeanutsPlugin.Windows;

namespace PeanutsPlugin;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Peanuts";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    public Configuration Configuration { get; }
    private readonly WindowSystem windowSystem = new("Peanuts");
    private readonly MainWindow mainWindow;
    private readonly InventoryScanner scanner;

    public bool ScannerRunning { get; private set; }
    public bool LastScanComplete { get; private set; }

    // Für den History-Tab: Welt/Charakter der zuletzt ABGESCHLOSSENEN
    // Erfassung, damit die Verlaufsansicht automatisch nachzieht - auch wenn
    // der History-Tab beim Scan-Abschluss gar nicht sichtbar war.
    public string? LastCompletedWorld { get; private set; }
    public string? LastCompletedCharacter { get; private set; }
    public string? CurrentCharacterName { get; private set; }
    public string? CurrentWorldName { get; private set; }

    private CharacterData? activeCharacter;
    private int framesSinceLastScan;
    private const int ScanIntervalFramesFast = 30;  // ~0,5s, während der Ersterfassung
    private const int ScanIntervalFramesIdle = 300; // ~5s, danach im Hintergrund (z.B. für U-Boot-Änderungen)
    private int currentScanIntervalFrames = ScanIntervalFramesFast;
    private bool hasAnnouncedComplete;
    private bool hasAnnouncedDuplicate;

    private bool autoStartPending;
    private int autoStartDelayFrames;
    private const int AutoStartDelayFramesInitial = 90; // ~1,5s warten, bis Name/Welt nach Login sicher verfügbar sind

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        // Overlay-Sprache anwenden: manuelle Überschreibung im Edit-Tab hat
        // Vorrang, sonst folgt sie der Client-Spracheinstellung
        // (Systemkonfiguration -> Andere -> Sprache/Audio).
        ApplyLanguageSetting();

        scanner = new InventoryScanner(Log);

        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler("/peanuts", new CommandInfo(OnToggleWindow)
        {
            HelpMessage = Loc.Get(
                "Öffnet/schließt das Peanuts-Overlay.",
                "Opens/closes the Peanuts overlay."),
        });
        CommandManager.AddHandler("/peanutson", new CommandInfo(OnStartCommand)
        {
            HelpMessage = Loc.Get("Startet das Peanuts-Tool.", "Starts the Peanuts tool."),
        });
        CommandManager.AddHandler("/peanutsoff", new CommandInfo(OnStopCommand)
        {
            HelpMessage = Loc.Get("Stoppt das Peanuts-Tool.", "Stops the Peanuts tool."),
        });
        CommandManager.AddHandler("/peanutstot", new CommandInfo(OnTotalCommand)
        {
            HelpMessage = Loc.Get(
                "Druckt die Gesamtsumme (Gil/Stacks/Charaktere) direkt in den Chat.",
                "Prints the grand total (Gil/stacks/characters) directly to chat."),
        });
        CommandManager.AddHandler("/peanutsex", new CommandInfo(OnExportCommand)
        {
            HelpMessage = Loc.Get(
                "Exportiert Tataru's Note (CSV/Excel, je nach Edit-Einstellung).",
                "Exports Tataru's Note (CSV/Excel, depending on the Edit tab settings)."),
        });
        CommandManager.AddHandler("/peanutsres", new CommandInfo(OnResetCommand)
        {
            HelpMessage = Loc.Get(
                "Setzt das komplette Overlay zurück - zur Bestätigung: /peanutsres confirm (alle Welten/Charaktere/Verlauf).",
                "Resets the whole overlay - to confirm: /peanutsres confirm (all worlds/characters/history)."),
        });

        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUi;

        // ItemIds erst auflösen, sobald die Spieldaten sicher geladen sind.
        Framework.RunOnFrameworkThread(() => ItemIdResolver.ResolveAll(DataManager, Log));

        Framework.Update += OnFrameworkUpdate;
        ClientState.Login += OnLogin;

        // Falls das Plugin geladen wird, während bereits ein Charakter eingeloggt ist
        // (z.B. Neustart/Reload des Plugins), ebenfalls automatisch starten.
        if (ClientState.IsLoggedIn)
            OnLogin();
    }

    private void OnOpenMainUi() => mainWindow.IsOpen = true;

    private void OnLogin()
    {
        // Auto-Start kann im Edit-Tab abgeschaltet werden - dann bleibt das
        // Tool nach dem Login gestoppt, bis der Nutzer manuell startet.
        if (!Configuration.AutoStartOnLogin)
            return;

        autoStartPending = true;
        autoStartDelayFrames = AutoStartDelayFramesInitial;
    }

    /// <summary>
    /// Wendet die Overlay-Sprache an: "Auto" folgt der Client-Spracheinstellung
    /// (Deutsch bleibt Deutsch, EN/FR/JP fallen auf Englisch zurück), "German"
    /// bzw. "English" erzwingen die jeweilige Sprache unabhängig vom Client.
    /// Wird beim Start UND sofort nach einer Änderung im Edit-Tab aufgerufen.
    /// </summary>
    public void ApplyLanguageSetting()
    {
        Loc.CurrentLanguage = Configuration.LanguageOverride switch
        {
            "German" => ClientLanguage.German,
            "English" => ClientLanguage.English,
            _ => ClientState.ClientLanguage == ClientLanguage.German ? ClientLanguage.German : ClientLanguage.English,
        };
    }

    /// <summary>
    /// Sucht im (deutschen) Item-Sheet nach Items, deren Name den Suchtext
    /// enthält - für den "+"-Dialog im Item-Tab. Liefert direkt alles, was
    /// zum Anlegen eines vollständigen ItemDefinition nötig ist (NQ/HQ-Preis,
    /// Stapelgröße, englischer Name), damit keine erneute Auflösung nötig ist.
    /// Ergebnisse sind bewusst limitiert, damit die Liste übersichtlich bleibt.
    /// </summary>
    public List<(uint ItemId, string NameDe, string? NameEn, bool CanBeHq, uint PriceNq, uint PriceHq, uint StackSize)> SearchItems(string query, int maxResults = 20)
    {
        var results = new List<(uint, string, string?, bool, uint, uint, uint)>();
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return results;

        var germanSheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(ClientLanguage.German);
        var englishSheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(ClientLanguage.English);
        if (germanSheet == null)
            return results;

        var needle = query.Trim();
        foreach (var row in germanSheet)
        {
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!name.Contains(needle, StringComparison.OrdinalIgnoreCase))
                continue;

            var canBeHq = row.CanBeHq;
            var priceNq = (uint)row.PriceLow;
            var priceHq = canBeHq ? ItemDefinition.EstimateHqPrice(priceNq) : 0;
            var stackSize = row.StackSize > 0 ? (uint)row.StackSize : 99u;

            string? nameEn = null;
            if (englishSheet != null)
            {
                var englishRow = englishSheet.GetRowOrDefault(row.RowId);
                if (englishRow != null)
                    nameEn = englishRow.Value.Name.ExtractText();
            }

            results.Add((row.RowId, name, nameEn, canBeHq, priceNq, priceHq, stackSize));
            if (results.Count >= maxResults)
                break;
        }

        return results;
    }

    /// <summary>
    /// Übernimmt die im "Item"-Tab bearbeitete Item-Liste dauerhaft: ersetzt
    /// den Inhalt der aktiven, geteilten Liste (TrackedItems.All) und
    /// speichert. Wird über den "Änderungen sichern!"-Button ausgelöst.
    /// </summary>
    public void SaveTrackedItems(List<ItemDefinition> updated)
    {
        var live = TrackedItems.All;
        live.Clear();
        live.AddRange(updated.Select(i => i.Clone()));
        Configuration.TrackedItemList = live;
        Configuration.Save();
    }

    /// <summary>Persistiert die aktuelle (bereits live geänderte) Item-Liste - ohne Chat-Meldung.</summary>
    private void PersistTrackedItems()
    {
        Configuration.TrackedItemList = TrackedItems.All;
        Configuration.Save();
    }

    /// <summary>
    /// Fügt ein neu gesuchtes Item sofort zur aktiven Liste hinzu, speichert
    /// direkt und stößt einen Hintergrund-Scan an, damit sich die Änderung
    /// sofort in der Darstellung niederschlägt - kein separater "Änderungen
    /// sichern"-Schritt mehr nötig.
    /// </summary>
    public void AddTrackedItem(ItemDefinition item)
    {
        TrackedItems.All.Add(item);
        PersistTrackedItems();
        if (activeCharacter != null)
            ScanOnce();
    }

    /// <summary>
    /// Entfernt ein Item komplett, speichert direkt, druckt NUR "Name -
    /// Gelöscht" (keine weiteren Meldungen) und stößt einen Hintergrund-Scan
    /// an, damit es sofort aus der Darstellung verschwindet.
    /// </summary>
    public void RemoveTrackedItem(ItemDefinition item)
    {
        TrackedItems.All.Remove(item);
        PersistTrackedItems();
        ChatGui.Print($"{item.Name} - {Loc.Get("Gelöscht", "Deleted")}");
        if (activeCharacter != null)
            ScanOnce();
    }

    /// <summary>Schaltet ein Item aktiv/inaktiv (Checkbox im Item-Tab), speichert sofort und scannt im Hintergrund nach.</summary>
    public void SetTrackedItemEnabled(ItemDefinition item, bool enabled)
    {
        item.Enabled = enabled;
        PersistTrackedItems();
        if (activeCharacter != null)
            ScanOnce();
    }

    private void OnToggleWindow(string command, string args) => mainWindow.Toggle();

    private void OnTotalCommand(string command, string args) => PrintTotalsToChat();

    /// <summary>Schneller Chatbefehl "/peanuts total" - Gesamtsumme direkt in den Chat, ohne das Fenster zu öffnen.</summary>
    private void PrintTotalsToChat()
    {
        var gil = Configuration.GlobalTotalGil();
        var stacks = Configuration.GlobalTotalStacks();
        var chars = Configuration.GlobalCharacterCount();
        ChatGui.Print(Loc.Get(
            $"[Peanuts] Gesamt: {gil:N0} Gil, {stacks} Stacks, {chars} Charakter(e).",
            $"[Peanuts] Total: {gil:N0} Gil, {stacks} stacks, {chars} character(s)."));
    }

    /// <summary>
    /// Baut eine schön formatierte Textübersicht für die Zwischenablage,
    /// z.B. zum Posten in einen FC-Discord.
    /// </summary>
    public string BuildSummaryText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.Get(
            $"📊 Peanuts Übersicht ({DateTime.Now:dd.MM.yyyy HH:mm})",
            $"📊 Peanuts overview ({DateTime.Now:MM/dd/yyyy HH:mm})"));
        sb.AppendLine(Loc.Get(
            $"Umsatz aller Welten: {Configuration.GlobalTotalGil():N0} Gil",
            $"Total across all worlds: {Configuration.GlobalTotalGil():N0} Gil"));
        sb.AppendLine(Loc.Get(
            $"Stacks aller Welten: {Configuration.GlobalTotalStacks()}",
            $"Stacks across all worlds: {Configuration.GlobalTotalStacks()}"));
        sb.AppendLine(Loc.Get(
            $"Gezählte Charaktere: {Configuration.GlobalCharacterCount()}",
            $"Characters counted: {Configuration.GlobalCharacterCount()}"));
        sb.AppendLine();

        foreach (var world in Configuration.Worlds.Values.OrderBy(w => w.Name))
        {
            if (world.Characters.Count == 0)
                continue;

            sb.AppendLine(Loc.Get(
                $"— {world.Name} ({DataCenters.GetDataCenter(world.Name)}): {world.TotalGil():N0} Gil ({world.TotalStacks()} Stacks)",
                $"— {world.Name} ({DataCenters.GetDataCenter(world.Name)}): {world.TotalGil():N0} Gil ({world.TotalStacks()} stacks)"));
        }

        return sb.ToString();
    }

    private void OnStartCommand(string command, string args) => StartScanner();

    private void OnStopCommand(string command, string args) => StopScanner();

    private void OnResetCommand(string command, string args)
    {
        // Im Chat gibt es kein Bestätigungs-Popup wie beim UI-Button, deshalb
        // muss der Reset per Argument bestätigt werden: "/peanutsres confirm".
        if (!string.Equals(args?.Trim(), "confirm", StringComparison.OrdinalIgnoreCase))
        {
            ChatGui.Print(Loc.Get(
                "[Peanuts] Achtung: /peanutsres setzt ALLE Werte auf 0 (der Verlauf wird vorher gesichert). Zum Bestätigen: /peanutsres confirm",
                "[Peanuts] Warning: /peanutsres resets ALL values to 0 (history is saved first). To confirm: /peanutsres confirm"));
            return;
        }

        ResetAll();
    }

    private void OnExportCommand(string command, string args) => ExportData();

    public void StartScanner(bool silent = false)
    {
        var localPlayer = ObjectTable.LocalPlayer;
        if (!ClientState.IsLoggedIn || localPlayer == null)
        {
            if (!silent)
                ChatGui.PrintError(Loc.Get("[Peanuts] Kein Charakter eingeloggt.", "[Peanuts] No character logged in."));
            return;
        }

        CurrentCharacterName = localPlayer.Name.TextValue;
        CurrentWorldName = localPlayer.HomeWorld.Value.Name.ExtractText();

        activeCharacter = Configuration.GetOrCreateCharacter(CurrentWorldName, CurrentCharacterName);
        if (activeCharacter == null)
        {
            ChatGui.PrintError(Loc.Get(
                $"[Peanuts] Welt \"{CurrentWorldName}\" hat bereits die maximale Anzahl von {Configuration.MaxCharactersPerWorld} Charakteren.",
                $"[Peanuts] World \"{CurrentWorldName}\" already has the maximum of {Configuration.MaxCharactersPerWorld} characters."));
            return;
        }

        LastScanComplete = false;
        hasAnnouncedComplete = false;
        hasAnnouncedDuplicate = false;
        currentScanIntervalFrames = ScanIntervalFramesFast;
        ScannerRunning = true;
        framesSinceLastScan = 0;
        if (!silent)
            ChatGui.Print(Loc.Get("[Peanuts] Tool gestartet.", "[Peanuts] Tool started."));

        // Sofortiger, synchroner Scan direkt beim Klick - Items UND Slots
        // werden dadurch garantiert zeitgleich erfasst, statt erst auf den
        // nächsten Timer-Tick (~0,5s) zu warten.
        ScanOnce();
    }

    public void StopScanner()
    {
        ScannerRunning = false;
        ChatGui.Print(Loc.Get("[Peanuts] Tool gestoppt.", "[Peanuts] Tool stopped."));
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (autoStartPending)
        {
            if (autoStartDelayFrames > 0)
            {
                autoStartDelayFrames--;
            }
            else
            {
                autoStartPending = false;
                if (ClientState.IsLoggedIn && ObjectTable.LocalPlayer != null && !ScannerRunning)
                    StartScanner(silent: true);
            }
        }

        if (!ScannerRunning)
            return;

        // Kritischer Fix: sobald der Charakter nicht mehr eingeloggt ist
        // (Ausloggen, Charakterwahl, Disconnect), SOFORT stoppen statt mit
        // ungültigen/leeren Speicherdaten weiterzuscannen - sonst würden die
        // zuletzt korrekt erfassten Werte mit Nullen überschrieben.
        if (!ClientState.IsLoggedIn || ObjectTable.LocalPlayer == null)
        {
            StopScanner();
            return;
        }

        framesSinceLastScan++;
        if (framesSinceLastScan < currentScanIntervalFrames)
            return;

        framesSinceLastScan = 0;
        ScanOnce();
    }

    /// <summary>
    /// Liest das Inventar einmal aus und aktualisiert den aktiven Charakter -
    /// sowohl die 8 getrackten Items als auch die belegten Inventar-Slots.
    /// Läuft danach bewusst im Hintergrund WEITER (nur langsamer), damit
    /// spätere Änderungen (z.B. nach einer U-Boot-Ausfahrt, Handel, Quests)
    /// automatisch übernommen werden, ohne dass ein erneutes Einloggen oder
    /// manuelles Neustarten nötig ist.
    /// </summary>
    public void ScanOnce()
    {
        if (activeCharacter == null)
            return;

        // Zweite Absicherung: niemals scannen/speichern, wenn kein Charakter
        // (mehr) geladen ist - verhindert das Überschreiben echter Werte
        // mit Nullen beim Ausloggen.
        if (!ClientState.IsLoggedIn || ObjectTable.LocalPlayer == null)
            return;

        var result = scanner.ScanInventory();

        // Nur bei einer echten Änderung wird am Ende auf die Platte
        // geschrieben - sonst würde jeder Idle-Tick (~5s) die komplette
        // (mit dem Verlauf wachsende) JSON unnötig neu serialisieren.
        var changed = false;

        // --- Hauptinventar ---
        foreach (var kv in result.Inventory)
        {
            if (activeCharacter.ItemCounts.GetValueOrDefault(kv.Key) != kv.Value)
                changed = true;
            activeCharacter.ItemCounts[kv.Key] = kv.Value;
        }

        if (activeCharacter.UsedSlots != result.UsedInventorySlots)
            changed = true;
        activeCharacter.UsedSlots = result.UsedInventorySlots;

        // --- Satteltasche ---
        // Nur übernehmen, wenn die Satteltasche tatsächlich geladen war.
        // Vor dem ersten Öffnen (z.B. direkt nach dem Login) sind ihre
        // Container leer/nicht vorhanden - würden wir dann blind 0 schreiben,
        // ginge der zuletzt bekannte Stand verloren. Stattdessen behalten wir
        // in diesem Fall die alten Werte und lassen die Anzeige auf "?" stehen.
        if (result.SaddlebagLoaded)
        {
            foreach (var kv in result.Saddlebag)
            {
                if (activeCharacter.SaddlebagCounts.GetValueOrDefault(kv.Key) != kv.Value)
                    changed = true;
                activeCharacter.SaddlebagCounts[kv.Key] = kv.Value;
            }

            if (activeCharacter.UsedSaddlebagSlots != result.UsedSaddlebagSlots)
                changed = true;
            activeCharacter.UsedSaddlebagSlots = result.UsedSaddlebagSlots;

            if (activeCharacter.SaddlebagCapacity != result.SaddlebagCapacity)
                changed = true;
            activeCharacter.SaddlebagCapacity = result.SaddlebagCapacity;

            // Doppelfund auf ITEM-Ebene: ein Item gilt als Doppelfund, sobald
            // IRGENDEINE seiner Varianten (NQ/HQ) im Hauptinventar UND (dieselbe
            // oder eine andere Variante) in der Satteltasche liegt - auch über
            // verschiedene Qualitäten hinweg. Nur sinnvoll, wenn die Satteltasche
            // geladen ist; sonst bleibt der letzte bekannte Doppelfund-Stand stehen.
            var newDuplicateKeys = TrackedItems.All
                .Where(item => item.Enabled
                    && item.Variants().Any(v => result.Inventory.TryGetValue(v.CountKey, out var iq) && iq > 0)
                    && item.Variants().Any(v => result.Saddlebag.TryGetValue(v.CountKey, out var bq) && bq > 0))
                .Select(item => item.Key)
                .ToHashSet();

            if (!newDuplicateKeys.SetEquals(activeCharacter.DuplicateFindKeys))
                changed = true;
            activeCharacter.DuplicateFindKeys = newDuplicateKeys;
        }

        activeCharacter.LastScannedAt = DateTime.Now;

        if (changed)
            Configuration.Save();

        var hasDuplicateNow = activeCharacter.DuplicateFindKeys.Count > 0;

        if (activeCharacter.IsComplete() && !hasAnnouncedComplete)
        {
            LastScanComplete = true;
            hasAnnouncedComplete = true;
            currentScanIntervalFrames = ScanIntervalFramesIdle;

            // Für den History-Tab: sobald ein Charakter fertig gescannt ist,
            // soll er dort automatisch ausgewählt werden - unabhängig davon,
            // ob der Tab gerade sichtbar ist oder nicht.
            LastCompletedWorld = CurrentWorldName;
            LastCompletedCharacter = CurrentCharacterName;

            hasAnnouncedDuplicate = hasDuplicateNow;
            PrintCompletionMessage(TotalSearchableVariantCount(), hasDuplicateNow);
        }
        else if (hasDuplicateNow && !hasAnnouncedDuplicate)
        {
            // Ein Doppelfund kann auch NACH der ersten "Erfassung abgeschlossen"-
            // Meldung auftauchen (z.B. wenn erst später ein Item zusätzlich in
            // die Satteltasche gelegt wird) - dafür braucht es eine eigene,
            // von hasAnnouncedComplete unabhängige Meldung.
            hasAnnouncedDuplicate = true;
            PrintCompletionMessage(TotalSearchableVariantCount(), true);
        }
        else if (!hasDuplicateNow)
        {
            // Erlaubt eine erneute Meldung, falls der Doppelfund behoben
            // (z.B. Item aus der Satteltasche entfernt) und später erneut auftritt.
            hasAnnouncedDuplicate = false;
        }
    }

    /// <summary>
    /// Anzahl aller aktiven Item-VARIANTEN (NQ zählt 1, ein zusätzlich
    /// HQ-fähiges Item zählt 2) - die dynamische Ersetzung für die früher
    /// hart codierte "alle 8 Items"-Meldung, da inzwischen beliebig viele
    /// eigene Items hinzugefügt werden können.
    /// </summary>
    private static int TotalSearchableVariantCount() =>
        TrackedItems.All.Where(i => i.Enabled).Sum(i => i.CanBeHq ? 2 : 1);

    /// <summary>
    /// Druckt die "Erfassung abgeschlossen"-Meldung. Bei einem Doppelfund
    /// (Item sowohl im Hauptinventar als auch in der Satteltasche gefunden)
    /// wird zusätzlich "- - Doppelfund!!! - -" in Rot eingeblendet.
    /// </summary>
    private void PrintCompletionMessage(int targetCount, bool hasDuplicate)
    {
        var prefixDe = $"[Peanuts] Erfassung abgeschlossen - alle {targetCount} erkannt. ";
        var prefixEn = $"[Peanuts] Scan complete - all {targetCount} detected. ";
        var suffixDe = "Tool läuft im Hintergrund weiter und übernimmt spätere Änderungen automatisch.";
        var suffixEn = "The tool keeps running in the background and picks up later changes automatically.";

        if (!hasDuplicate)
        {
            ChatGui.Print(Loc.Get(prefixDe + suffixDe, prefixEn + suffixEn));
            return;
        }

        // Farbschlüssel 17 = Rot im UIColor-Sheet des Spiels (Best-Effort -
        // falls die Farbe bei dir nicht rot erscheint, bitte Rückmeldung
        // geben, dann passen wir den Farbschlüssel hier zentral an).
        const ushort redColorKey = 17;
        var builder = new SeStringBuilder()
            .AddText(Loc.Get(prefixDe, prefixEn))
            .AddUiForeground("- - Doppelfund!!! - -", redColorKey)
            .AddText(" " + Loc.Get(suffixDe, suffixEn));

        ChatGui.Print(new XivChatEntry { Message = builder.Build() });
    }

    /// <summary>
    /// Speichert den KOMPLETTEN aktuellen Stand des Overlays - also alle
    /// Welten mit allen Charakteren - als datierten Snapshot in die History.
    /// Jeder Charakter bekommt dabei seinen eigenen Eintrag, zugeordnet zu
    /// seiner Welt, sodass im "Verlauf"-Tab jeder Charakter einzeln über die
    /// Zeit nachvollzogen werden kann.
    /// </summary>
    /// <param name="silent">
    /// True, wenn dies durch eine Änderung im Edit-Tab -> "Charakter" ausgelöst
    /// wurde (Sichtbarkeits-Schalter/Löschen) - dann keine Chat-Meldung, die
    /// eigentliche Speicherfunktion bleibt aber identisch.
    /// </param>
    public int SaveSnapshot(bool silent = false)
    {
        var now = DateTime.Now;
        var count = 0;

        foreach (var world in Configuration.Worlds.Values)
        {
            foreach (var character in world.Characters)
            {
                Configuration.History.Add(new HistoryEntry
                {
                    Timestamp = now,
                    World = world.Name,
                    Character = character.Name,
                    ItemCounts = character.GetCombinedCounts(),
                    TotalGil = character.TotalGil(),
                });
                count++;
            }
        }

        if (count == 0)
        {
            if (!silent)
                ChatGui.PrintError(Loc.Get(
                    "[Peanuts] Keine Daten zum Speichern vorhanden.",
                    "[Peanuts] No data available to save."));
            return 0;
        }

        Configuration.PruneHistory();
        Configuration.Save();
        if (!silent)
            ChatGui.Print(Loc.Get(
                $"[Peanuts] Snapshot für {count} Charakter(e) gespeichert ({now:dd.MM.yyyy HH:mm}).",
                $"[Peanuts] Snapshot saved for {count} character(s) ({now:MM/dd/yyyy HH:mm})."));

        UpdateCheckpoint();
        return count;
    }

    /// <summary>
    /// Sichert den aktuellen globalen Gil/Stacks-Stand als Referenzpunkt für
    /// die "Seit letztem Speichern/Export"-Delta-Anzeige im Tool-Tab. Wird
    /// sowohl von Save als auch von Export aufgerufen.
    /// </summary>
    private void UpdateCheckpoint()
    {
        Configuration.LastCheckpointGil = Configuration.GlobalTotalGil();
        Configuration.LastCheckpointStacks = Configuration.GlobalTotalStacks();
        Configuration.LastCheckpointTimestamp = DateTime.Now;
        Configuration.Save();
    }

    /// <summary>
    /// Liefert alle im Verlauf vorhandenen "Batches": ein einzelner Aufruf
    /// von Save/Reset erzeugt für ALLE betroffenen Charaktere denselben
    /// Zeitstempel - das ist also ein kompletter, wiederherstellbarer
    /// Overlay-Zustand von damals.
    /// </summary>
    public List<(DateTime Timestamp, int CharacterCount)> GetHistoryBatches()
    {
        return Configuration.History
            .GroupBy(h => h.Timestamp)
            .Select(g => (Timestamp: g.Key, CharacterCount: g.Count()))
            .OrderByDescending(b => b.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Stellt einen kompletten, früheren Overlay-Zustand aus der History
    /// wieder her (z.B. nach einem versehentlichen "Broken" oder um einen
    /// alten Save erneut anzuschauen). Die aktuelle Welten/Charakter-
    /// Struktur wird dabei komplett durch den gewählten Snapshot ersetzt.
    /// Der Verlauf selbst bleibt unverändert - der Zustand VOR der
    /// Wiederherstellung geht aber verloren, falls er nicht vorher separat
    /// gesichert wurde. Slot-Daten waren damals noch nicht Teil der History
    /// und gelten daher nach der Wiederherstellung erstmal als unbekannt,
    /// bis der jeweilige Charakter neu gescannt wird.
    /// </summary>
    public int RestoreFromHistory(DateTime timestamp)
    {
        var entries = Configuration.History.Where(h => h.Timestamp == timestamp).ToList();
        if (entries.Count == 0)
            return 0;

        Configuration.Worlds.Clear();

        foreach (var entry in entries)
        {
            var character = Configuration.GetOrCreateCharacter(entry.World, entry.Character);
            if (character == null)
                continue; // Welt hätte dadurch mehr als die maximale Charakteranzahl - sollte praktisch nie passieren

            character.ItemCounts = new Dictionary<string, int>(entry.ItemCounts);
            character.UsedSlots = -1;
        }

        Configuration.Save();

        ScannerRunning = false;
        LastScanComplete = false;
        activeCharacter = null;
        CurrentCharacterName = null;
        CurrentWorldName = null;

        ChatGui.Print(Loc.Get(
            $"[Peanuts] Overlay-Zustand vom {timestamp:dd.MM.yyyy HH:mm} wiederhergestellt ({entries.Count} Charakter(e)).",
            $"[Peanuts] Overlay state from {timestamp:MM/dd/yyyy HH:mm} restored ({entries.Count} character(s))."));
        return entries.Count;
    }

    /// <summary>
    /// Setzt alle Werte im Scanner-Overlay auf 0 zurück: Welten und
    /// Charaktere BLEIBEN als Struktur bestehen (Baum bleibt sichtbar),
    /// nur ihre 8 Item-Zählungen (und damit Gil/Stacks) werden auf 0
    /// gesetzt. Vorher wird automatisch ein kompletter Snapshot aller
    /// Welten/Charaktere in der History gesichert, sodass nichts verloren
    /// geht - der Verlauf selbst bleibt in jedem Fall erhalten.
    /// </summary>
    public void ResetAll()
    {
        SaveSnapshot();

        foreach (var world in Configuration.Worlds.Values)
        {
            foreach (var character in world.Characters)
            {
                foreach (var item in TrackedItems.All)
                {
                    foreach (var (key, _) in item.Variants())
                    {
                        character.ItemCounts[key] = 0;
                        character.SaddlebagCounts[key] = 0;
                    }
                }
                character.DuplicateFindKeys.Clear();
            }
        }

        // Reset-Markierung: ein Null-Eintrag pro Charakter mit eigenem
        // Zeitstempel (leicht nach dem Vor-Reset-Snapshot). Dadurch zeigt der
        // Umsatzverlauf den Abfall auf 0 sichtbar an (rote "Reset (0)"-Zeile);
        // der Vor-Reset-Stand bleibt über den vorherigen Snapshot erhalten.
        var resetTime = DateTime.Now;
        foreach (var world in Configuration.Worlds.Values)
        {
            foreach (var character in world.Characters)
            {
                Configuration.History.Add(new HistoryEntry
                {
                    Timestamp = resetTime,
                    World = world.Name,
                    Character = character.Name,
                    ItemCounts = new Dictionary<string, int>(),
                    TotalGil = 0,
                    IsReset = true,
                });
            }
        }

        Configuration.PruneHistory();
        Configuration.Save();

        ScannerRunning = false;
        LastScanComplete = false;
        activeCharacter = null;
        CurrentCharacterName = null;
        CurrentWorldName = null;

        ChatGui.Print(Loc.Get(
            "[Peanuts] Alle Werte wurden auf 0 gesetzt (Struktur bleibt erhalten, Verlauf gesichert).",
            "[Peanuts] All values reset to 0 (structure kept, snapshot saved to history)."));
    }

    /// <summary>
    /// "Broken"-Notausstieg im Edit-Tab: löscht die komplette Overlay-
    /// Struktur (alle Welten und Charaktere verschwinden vollständig, nicht
    /// nur ihre Werte). Der Verlauf bleibt davon unberührt.
    /// </summary>
    public void FullWipe()
    {
        Configuration.Worlds.Clear();
        Configuration.Save();

        ScannerRunning = false;
        LastScanComplete = false;
        activeCharacter = null;
        CurrentCharacterName = null;
        CurrentWorldName = null;

        ChatGui.Print(Loc.Get(
            "[Peanuts] Overlay komplett gelöscht (Struktur entfernt, Verlauf bleibt erhalten).",
            "[Peanuts] Overlay completely wiped (structure removed, history kept)."));
    }

    /// <summary>
    /// Löscht einen einzelnen Charakter komplett aus dem Overlay (Edit-Tab
    /// -> "Charakter", per Löschen-Schalter mit STRG-Bestätigung). Der
    /// Verlauf dieses Charakters bleibt unberührt - nur die aktuelle
    /// Überblicksstruktur verliert ihn.
    /// </summary>
    /// <summary>
    /// "Löschen" im Edit-Tab -> "Charakter": verschiebt den Charakter jetzt
    /// nur noch ins "Feld der Ehre" (archiviert) - komplett reversibel über
    /// "Pulse of Life!". Blendet ihn sofort aus Tool/History/Ranking/Export
    /// aus, die zugrunde liegenden Daten bleiben aber vollständig erhalten.
    /// </summary>
    public void ArchiveCharacter(string worldName, string characterName)
    {
        var character = Configuration.FindCharacter(worldName, characterName);
        if (character == null)
            return;

        character.IsArchived = true;
        character.ArchivedAt = DateTime.Now;

        if (CurrentWorldName == worldName && CurrentCharacterName == characterName)
        {
            activeCharacter = null;
            CurrentCharacterName = null;
            CurrentWorldName = null;
            ScannerRunning = false;
        }

        Configuration.Save();
        ChatGui.Print(Loc.Get(
            $"[Peanuts] \"{characterName}\" @ {worldName} ins Feld der Ehre verschoben.",
            $"[Peanuts] \"{characterName}\" @ {worldName} moved to the Field of Honor."));
    }

    /// <summary>"Pulse of Life!" im Feld der Ehre: holt einen archivierten Charakter vollständig zurück.</summary>
    public void RestoreCharacter(string worldName, string characterName)
    {
        var character = Configuration.FindCharacter(worldName, characterName);
        if (character == null)
            return;

        character.IsArchived = false;
        character.ArchivedAt = null;

        Configuration.Save();
        ChatGui.Print(Loc.Get(
            $"[Peanuts] \"{characterName}\" @ {worldName} wiederbelebt.",
            $"[Peanuts] \"{characterName}\" @ {worldName} revived."));
    }

    /// <summary>
    /// "Aetherial Sea" im Feld der Ehre: löst einen archivierten Charakter
    /// TATSÄCHLICH und endgültig auf - im Gegensatz zu ArchiveCharacter kann
    /// das NICHT rückgängig gemacht werden. Der Verlauf bleibt trotzdem erhalten.
    /// </summary>
    public void PermanentlyDeleteCharacter(string worldName, string characterName)
    {
        if (!Configuration.Worlds.TryGetValue(worldName, out var world))
            return;

        var character = world.Characters.FirstOrDefault(c => c.Name == characterName);
        if (character == null)
            return;

        world.Characters.Remove(character);
        Configuration.Save();
        ChatGui.Print(Loc.Get(
            $"[Peanuts] \"{characterName}\" @ {worldName} im Aetherstrom aufgelöst - endgültig.",
            $"[Peanuts] \"{characterName}\" @ {worldName} dissolved into the aetherial sea - permanently."));
    }

    /// <summary>
    /// Wird nach einer Änderung im Edit-Tab -> "Charakter" (Sichtbarkeits-
    /// Schalter, Wiederherstellen) aufgerufen. Eine Sichtbarkeits-Änderung ist
    /// eine reine Anzeige-Einstellung: sie wird nur PERSISTIERT. Sie erzeugt
    /// bewusst KEINEN History-Snapshot und schreibt KEINE Exportdateien mehr -
    /// das hatte früher bei jedem Klick den Verlauf vollgeschrieben und (oft
    /// gesperrte) Exportdateien angefasst. Der Effekt ist trotzdem sofort
    /// sichtbar, weil das Overlay die Aggregation bei jedem Frame neu aus den
    /// Live-Daten berechnet. Snapshots/Exporte laufen weiterhin ganz normal
    /// über die "Save"-/"Export"-Buttons bzw. /peanutsex.
    /// </summary>
    public void RefreshAfterCharacterTabChange()
    {
        Configuration.Save();
    }

    /// <summary>
    /// Liefert den aktuell effektiven Export-ORDNER: entweder den vom
    /// Nutzer im "Edit"-Tab gesetzten Ordner, oder den Standardordner im
    /// Plugin-Konfigurationsordner, falls keiner gesetzt ist.
    /// </summary>
    public string GetEffectiveExportFolder()
    {
        return string.IsNullOrWhiteSpace(Configuration.ExportFolder)
            ? PluginInterface.GetPluginConfigDirectory()
            : Configuration.ExportFolder;
    }

    /// <summary>
    /// Exportiert "Tataru's Note" in den im Edit-Tab per Kästchen
    /// gewählten Formaten (CSV und/oder Excel). Beide Formate hängen mit
    /// jedem Aufruf einen neuen, datierten Block an dieselbe Datei an,
    /// statt sie zu überschreiben; bei Excel entsteht bei Monatswechsel
    /// automatisch ein neues Arbeitsblatt.
    /// </summary>
    /// <param name="silent">
    /// True, wenn dies durch eine Änderung im Edit-Tab -> "Charakter" ausgelöst
    /// wurde - dann keine Chat-Meldungen, die eigentliche Exportfunktion
    /// bleibt aber identisch (Dateien werden ganz normal geschrieben).
    /// </param>
    public void ExportData(bool silent = false)
    {
        if (!Configuration.ExportAsCsv && !Configuration.ExportAsExcel)
        {
            if (!silent)
                ChatGui.PrintError(Loc.Get(
                    "[Peanuts] Kein Exportformat gewählt. Im 'Edit'-Tab CSV und/oder Excel aktivieren.",
                    "[Peanuts] No export format selected. Enable CSV and/or Excel in the 'Edit' tab."));
            return;
        }

        var folder = GetEffectiveExportFolder();
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        if (Configuration.ExportAsCsv)
        {
            var csvPath = Path.Combine(folder, "Tataru's Note.csv");
            try
            {
                CsvExporter.Export(Configuration, csvPath);
                if (!silent)
                    ChatGui.Print(Loc.Get(
                        $"[Peanuts] Tataru's Note (CSV) aktualisiert: {csvPath}",
                        $"[Peanuts] Tataru's Note (CSV) updated: {csvPath}"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Peanuts] CSV-Export fehlgeschlagen.");
                if (!silent)
                    ChatGui.PrintError(Loc.Get(
                        "[Peanuts] CSV-Export fehlgeschlagen - ist \"Tataru's Note.csv\" evtl. in Excel geöffnet? Bitte schließen und erneut exportieren.",
                        "[Peanuts] CSV export failed - is \"Tataru's Note.csv\" perhaps open in Excel? Please close it and export again."));
            }
        }

        if (Configuration.ExportAsExcel)
        {
            var xlsxPath = Path.Combine(folder, "Tataru's Note.xlsx");
            try
            {
                ExcelExporter.Export(Configuration, xlsxPath);
                if (!silent)
                    ChatGui.Print(Loc.Get(
                        $"[Peanuts] Tataru's Note (Excel) aktualisiert: {xlsxPath}",
                        $"[Peanuts] Tataru's Note (Excel) updated: {xlsxPath}"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Peanuts] Excel-Export fehlgeschlagen.");
                if (!silent)
                    ChatGui.PrintError(Loc.Get(
                        "[Peanuts] Excel-Export fehlgeschlagen - ist \"Tataru's Note.xlsx\" evtl. in Excel geöffnet? Bitte schließen und erneut exportieren.",
                        "[Peanuts] Excel export failed - is \"Tataru's Note.xlsx\" perhaps open in Excel? Please close it and export again."));
            }
        }

        // Referenzstand für die "Seit letztem Speichern/Export"-Delta-Anzeige im Tool-Tab sichern.
        UpdateCheckpoint();
    }

    private void DrawUi() => windowSystem.Draw();

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        ClientState.Login -= OnLogin;
        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUi;
        windowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler("/peanuts");
        CommandManager.RemoveHandler("/peanutson");
        CommandManager.RemoveHandler("/peanutsoff");
        CommandManager.RemoveHandler("/peanutstot");
        CommandManager.RemoveHandler("/peanutsres");
        CommandManager.RemoveHandler("/peanutsex");
    }
}
