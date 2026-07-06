# Peanuts Plugin (Dalamud)

Originally built to read the quantity of 8 "salvaged" jewelry items directly
from your inventory (no hovering/tooltips needed) and log them per
character/world in a table matching the layout of an "Umsatzrechner" Excel
sheet. Since then the plugin has grown well beyond that starting point: you
can now add any item from the full FFXIV item catalog to the tracking list
(not just the original 8), NQ and HQ are recognized and counted separately
with their own sell prices, and a whole set of overlay features was built on
top - a searchable overview tree grouped by data center/world/character,
Gil/Stacks/Slots at a glance (with progress bars and mini trend graphs), a
history tab with restore/rollback, a monthly cross-character ranking
("Battle of Datacenter"), CSV/Excel export that grows over time instead of
being overwritten, per-character visibility controls, and a language system
that follows your client's language or can be set manually.

Ursprünglich gebaut, um die Stückzahl von 8 "geborgenen" Schmuck-Items direkt
aus dem Inventar auszulesen (kein Hover/Tooltip nötig) und sie pro
Charakter/Welt in einer Tabelle im Layout einer "Umsatzrechner"-Excel-Mappe
festzuhalten. Seitdem ist das Plugin weit über diesen Ausgangspunkt
hinausgewachsen: Du kannst inzwischen beliebige Items aus dem kompletten
FFXIV-Itemkatalog zur Tracking-Liste hinzufügen (nicht mehr nur die
ursprünglichen 8), NQ und HQ werden getrennt erkannt und mit ihrem jeweils
eigenen Verkaufspreis gezählt, und obendrauf ist ein ganzes Bündel an
Overlay-Funktionen entstanden - eine durchsuchbare Übersicht gruppiert nach
Datenzentrum/Welt/Charakter, Gil/Stacks/Slots auf einen Blick (mit
Fortschrittsbalken und Mini-Trendgraphen), ein Verlauf-Tab mit
Wiederherstellung, ein monatliches Charakter-Ranking ("Battle of
Datacenter"), CSV/Excel-Export, der mit der Zeit wächst statt überschrieben
zu werden, Sichtbarkeits-Schalter pro Charakter, und ein Sprachsystem, das
der Client-Sprache folgt oder manuell eingestellt werden kann.

## Commands

`/peanuts` -> Opens/closes the Peanuts overlay.
`/peanutson` -> Starts the Peanuts tool.
`/peanutsoff` -> Stops the Peanuts tool.
`/peanutstot` -> Prints the grand total (Gil/stacks/characters) directly to chat.
`/peanutsex` -> Exports Tataru's Note (CSV/Excel, depending on the Edit tab settings).
`/peanutsres` -> Resets the whole overlay (all worlds/characters/history).

Meowdy ♥

„Beschreibung": „Kratze die vorhandenen Schmuckstücke/Items zusammen und notiere eifrig deren Anzahl sowie den aktuellen Besitzer in Tatarus kleinem Notizbuch."

`/peanuts` -> Öffnet/schließt das Peanuts-Overlay.
`/peanutson` -> Startet das Peanuts-Tool.
`/peanutsoff` -> Stoppt das Peanuts-Tool.
`/peanutstot` -> Druckt die Gesamtsumme (Gil/Stacks/Charaktere) direkt in den Chat.
`/peanutsex` -> Exportiert Tataru's Note (CSV/Excel, je nach Edit-Einstellung).
`/peanutsres` -> Setzt das komplette Overlay zurück (alle Welten/Charaktere/Verlauf).

## Credits

- Astraea - Crazy Catlady ♥
- Celles - the best plasters in the world
- Eve - For there is always room for improvement
- Ray - Time is priceless

And thanks to all the helping hands, supporters, and volunteers. You are wonderful ♥

## Setup / Compiling

The project uses the official **`Dalamud.NET.Sdk`** (see the first line of the
`.csproj`). This SDK automatically pulls the references matching your
installed Dalamud version (Dalamud, ImGui bindings, Lumina,
FFXIVClientStructs) - you don't need to point to any DLLs manually.

1. Start [XIVLauncher](https://goatcorp.github.io/) at least once so that
   `%AppData%\XIVLauncher\addon\Hooks\dev\` is populated.
2. Install a current **.NET SDK** (newer Dalamud versions need the .NET 10
   SDK - check with `dotnet --version`; if the number is lower than 10,
   update via the [.NET SDK](https://dotnet.microsoft.com/download) page).
3. Open a terminal in the project folder and build:
   ```
   dotnet build -c Debug
   ```
   Visual Studio 2022/2026 works the same way via "Build" in the menu.
4. In-game, go to `/xlsettings` → `Experimental` → `Dev Plugin Locations` and
   add the folder containing the built `PeanutsPlugin.dll` (under
   `bin\Debug\<TargetFramework>\`, the SDK picks the target framework to
   match your Dalamud version automatically).
5. `/xlplugins` → "Dev Tools" tab → load Peanuts.

> If `dotnet build` fails with an error about "Dalamud.NET.Sdk", it's almost
> always NuGet: run `dotnet nuget locals all --clear` and build again. The
> SDK is downloaded from nuget.org on the first build, so an internet
> connection is required.

## Setup zum Kompilieren

Das Projekt nutzt das offizielle **`Dalamud.NET.Sdk`** (siehe erste Zeile der
`.csproj`). Dieses SDK lädt automatisch die zu deiner installierten
Dalamud-Version passenden Referenzen (Dalamud, ImGui-Bindings, Lumina,
FFXIVClientStructs) - du musst also keine DLL-Pfade manuell angeben.

1. [XIVLauncher](https://goatcorp.github.io/) mindestens einmal gestartet
   haben, damit `%AppData%\XIVLauncher\addon\Hooks\dev\` gefüllt ist.
2. Aktuelles **.NET SDK** installieren (für neuere Dalamud-Versionen wird das
   .NET 10 SDK benötigt - prüfe mit `dotnet --version`; falls die Zahl
   niedriger als 10 ist, das [.NET SDK](https://dotnet.microsoft.com/download)
   aktualisieren).
3. Terminal im Projektordner öffnen und bauen:
   ```
   dotnet build -c Debug
   ```
   Visual Studio 2022/2026 funktioniert genauso über "Build" im Menü.
4. Im Spiel via `/xlsettings` → `Experimental` → `Dev Plugin Locations` den
   Ordner mit der gebauten `PeanutsPlugin.dll` eintragen (liegt unter
   `bin\Debug\<TargetFramework>\`, das SDK legt das Zielframework automatisch
   passend zur Dalamud-Version fest).
5. `/xlplugins` → Reiter "Dev Tools" → Peanuts laden.

> Falls `dotnet build` mit einem Fehler zu "Dalamud.NET.Sdk" abbricht, liegt
> es fast immer an NuGet: `dotnet nuget locals all --clear` und dann erneut
> `dotnet build` ausführen. Das SDK wird beim ersten Build automatisch von
> nuget.org heruntergeladen - dafür ist eine Internetverbindung nötig.

## How items are detected

Every tracked item now carries its own resolved `ItemId`, German/English
name, NQ/HQ sell price, and max stack size, fetched once at startup via the
Lumina `Item` sheet (`ItemIdResolver.cs`). Resolution primarily looks items
up **by their already-known ItemId** (robust across renames/patches); a
name-based search against the German sheet is only used the very first time
a new item is added. `InventoryScanner.cs` then scans the four main bags via
`FFXIVClientStructs.InventoryManager` for those IDs - independent of client
language and tooltip timing.

NQ and HQ instances of the same item are recognized separately: in game
memory, an HQ item's ID always equals the base ItemId + 1,000,000. They are
counted, stacked, and priced independently and are never combined into one
number.

You're no longer limited to the original 8 items - the **Item** tab lets you
search the full FFXIV item catalog and add anything you want to track,
enable/disable tracking per item without losing existing data, or delete an
item entirely (Ctrl+click required, to prevent accidental clicks).

## Wie die Items erkannt werden

Jedes getrackte Item trägt jetzt seine eigene aufgelöste `ItemId`, den
deutschen/englischen Namen, NQ/HQ-Verkaufspreis und maximale Stapelgröße,
einmalig beim Start über das Lumina-`Item`-Sheet aufgelöst
(`ItemIdResolver.cs`). Die Auflösung schlägt primär **per bereits bekannter
ItemId** nach (robust gegenüber Umbenennungen/Patches); eine namensbasierte
Suche im deutschen Sheet wird nur beim allerersten Hinzufügen eines neuen
Items verwendet. `InventoryScanner.cs` durchsucht danach die vier
Haupttaschen über `FFXIVClientStructs.InventoryManager` nach diesen IDs -
unabhängig von Client-Sprache und Tooltip-Timing.

NQ- und HQ-Exemplare desselben Items werden getrennt erkannt: im
Spielspeicher hat eine HQ-Variante immer die Basis-ItemId + 1.000.000. Sie
werden unabhängig voneinander gezählt, gestapelt und bepreist und nie zu
einer Zahl zusammengefasst.

Du bist nicht mehr auf die ursprünglichen 8 Items beschränkt - im **Item**-Tab
kannst du den kompletten FFXIV-Itemkatalog durchsuchen und beliebige Items
zum Tracking hinzufügen, das Tracking pro Item an-/abschalten ohne bestehende
Daten zu verlieren, oder ein Item komplett löschen (STRG-Klick nötig, um
versehentliches Löschen zu verhindern).

## Overlay features

- **Tool tab**: live search across names/worlds, an expandable tree grouped
  by data center → world → character, with Gil, Stacks, and free Slots
  (colored progress bar) shown at every level, plus small trend sparklines
  next to Gil values. Start/Stop, Reset (zeroes values, keeps structure,
  auto-saves first), Save, Export, and "Copy summary" buttons. Below the
  tree: a Gil distribution donut chart (by world or data center), a stacked
  bar chart showing each world's Gil composition by item, and an item
  heatmap showing which character collected the most of each item.
- **History tab**: pick a world and character, browse their Gil history as a
  graph and table (deduplicated to one entry per day), restore a previous
  complete overlay snapshot, and see "Battle of Datacenter" - a monthly
  ranking of all characters in the same data center by Gil growth (only
  characters with their Ranking switch enabled in Edit → Character are
  shown). A period selector (1 week / 3 months / 6 months / 1 year) filters
  the graph and table retroactively from the most recent entry.
- **Edit tab**: manual language override (Auto/German/English), CSV/Excel
  export format toggles and export folder setting, a **Character** section
  to search all scanned characters and toggle their visibility individually
  in the Tool tab, revenue history, ranking, and export (each independent,
  data is never deleted by these toggles) plus a Ctrl+click delete with
  confirmation, and a danger zone with "Broken" to wipe the whole overlay
  structure (history is kept).
- **Item tab**: see above.

## Overlay-Funktionen

- **Tool-Tab**: Live-Suche nach Name/Welt, eine aufklappbare Baumansicht
  gruppiert nach Datenzentrum → Welt → Charakter, mit Gil, Stacks und freien
  Slots (farbiger Fortschrittsbalken) auf jeder Ebene, dazu kleine
  Trend-Sparklines neben den Gil-Werten. Start/Stop, Reset (setzt Werte auf
  0, Struktur bleibt, sichert vorher automatisch), Save, Export und
  "Zusammenfassung kopieren". Darunter: ein Donut-Diagramm zur
  Gil-Verteilung (nach Welt oder Datenzentrum), ein gestapeltes
  Balkendiagramm zur Item-Zusammensetzung je Welt, und eine Item-Heatmap, die
  zeigt, welcher Charakter bei welchem Item am meisten gesammelt hat.
- **History-Tab**: Welt und Charakter wählen, deren Gil-Verlauf als Graph und
  Tabelle ansehen (pro Tag dedupliziert), einen früheren kompletten
  Overlay-Zustand wiederherstellen, und "Battle of Datacenter" - ein
  monatliches Ranking aller Charaktere desselben Datenzentrums nach
  Gil-Zuwachs (nur Charaktere mit aktivem Ranking-Schalter in
  Edit → Charakter werden gezeigt). Ein Zeitraum-Umschalter (1 Woche / 3
  Monate / 6 Monate / 1 Jahr) filtert Graph und Tabelle rückwirkend vom
  letzten Eintrag aus.
- **Edit-Tab**: manuelle Sprachüberschreibung (Automatisch/Deutsch/English),
  CSV/Excel-Exportformat-Schalter und Export-Ordner-Einstellung, eine
  **Charakter**-Sektion zum Durchsuchen aller gescannten Charaktere und
  einzelnen Umschalten ihrer Sichtbarkeit im Tool-Tab, im Umsatzverlauf, im
  Ranking und beim Export (jeweils unabhängig, Daten werden dabei nie
  gelöscht), plus Löschen per STRG-Klick mit Bestätigung, und eine
  Gefahrenzone mit "Broken" zum Zurücksetzen der kompletten Overlay-Struktur
  (Verlauf bleibt erhalten).
- **Item-Tab**: siehe oben.

## Adjusting sale prices

NQ sell prices come straight from the game's `Item` sheet (`PriceLow`) and
stay correct automatically across patches. HQ prices aren't stored in the
game data as a separate field, so they're estimated with a +10% bonus
(`ItemDefinition.EstimateHqPrice`) - if that turns out to be off for a
specific item compared to its real in-game sell price, that factor can be
adjusted centrally in the code. `Configuration.PriceOverrides` still exists
as a manual override dictionary (key = item key, e.g. "Ring"), but there is
currently no in-UI editor for it - it has to be edited in the saved
configuration file directly.

## Verkaufspreise anpassen

NQ-Verkaufspreise kommen direkt aus dem `Item`-Sheet des Spiels (`PriceLow`)
und bleiben dadurch automatisch über Patches hinweg korrekt. HQ-Preise sind
in den Spieldaten nicht als eigenes Feld hinterlegt und werden daher mit
einem +10%-Aufschlag geschätzt (`ItemDefinition.EstimateHqPrice`) - falls das
bei einem konkreten Item vom echten Verkaufspreis abweicht, lässt sich dieser
Faktor zentral im Code anpassen. `Configuration.PriceOverrides` existiert
weiterhin als manuelles Override-Dictionary (Key = Item-Key, z. B. "Ring"),
dafür gibt es aktuell aber keinen Editor im UI - Änderungen müssen direkt in
der gespeicherten Konfigurationsdatei vorgenommen werden.

## Storage

All data lives as JSON in the standard Dalamud plugin configuration
(`Configuration.cs`), saved automatically after every scan and every
Save/Export/toggle. History snapshots, per-character visibility flags, and
the tracked item list are all part of this same file - deleting or hiding a
character/item never touches the underlying stored data unless you
explicitly use "Delete" or "Broken".

Export ("Tataru's Note") no longer overwrites itself: CSV
(`CsvExporter.cs`, semicolon-separated for the German Excel locale) appends a
new, dated block on every export, inserting a `### Month: yyyy-MM ###`
marker whenever the calendar month changes. Excel (`ExcelExporter.cs`, via
ClosedXML) similarly appends new rows, but creates an actual new worksheet
per calendar month. The export folder can be set in the Edit tab; it
defaults to the plugin's configuration folder.

## Speicherung

Alle Daten liegen als JSON in der normalen Dalamud-Plugin-Konfiguration
(`Configuration.cs`), automatisch gespeichert nach jedem Scan und jedem
Save/Export/Schalter. Verlaufs-Snapshots, die Sichtbarkeits-Schalter pro
Charakter und die getrackte Item-Liste sind alle Teil derselben Datei -
Löschen oder Ausblenden eines Charakters/Items rührt die zugrunde liegenden
gespeicherten Daten nie an, außer du nutzt explizit "Löschen" oder "Broken".

Der Export ("Tataru's Note") überschreibt sich nicht mehr selbst: CSV
(`CsvExporter.cs`, Semikolon-getrennt für die deutsche Excel-Locale) hängt
bei jedem Export einen neuen, datierten Block an und fügt bei Monatswechsel
einen `### Monat: yyyy-MM ###`-Marker ein. Excel (`ExcelExporter.cs`, via
ClosedXML) hängt ebenfalls neue Zeilen an, legt aber pro Kalendermonat ein
eigenes Arbeitsblatt an. Der Export-Ordner lässt sich im Edit-Tab festlegen;
Standard ist der Plugin-Konfigurationsordner.

## Still open

- Highlighting a value in a different color right when it changes since the
  last scan (currently there's a "since last save/export" delta summary, but
  no per-cell flash/highlight on individual value changes).
- An in-UI editor for `Configuration.PriceOverrides` (currently config-file-only).
- Google Sheets isn't directly supported (requires a Google account
  sign-in); the CSV file can be imported into Google Sheets manually via
  File → Import.
- Retainer inventory isn't included yet - only the character's own inventory
  is scanned.
- Item names are only localized for German/English so far; French/Japanese
  clients fall back to English.

## Noch offen

- Farbliche Hervorhebung eines Werts direkt bei einer Änderung seit dem
  letzten Scan (aktuell gibt es eine "seit letztem Speichern/Export"-Delta-
  Zusammenfassung, aber keine Einzelzellen-Hervorhebung bei Änderungen).
- Ein Editor im UI für `Configuration.PriceOverrides` (aktuell nur über die
  Konfigurationsdatei möglich).
- Google Sheets wird nicht direkt unterstützt (erfordert eine
  Google-Account-Anmeldung); die CSV-Datei lässt sich manuell über Datei →
  Importieren in Google Sheets laden.
- Retainer-Inventar ist noch nicht enthalten - es wird nur das
  Charakter-eigene Inventar gescannt.
- Item-Namen sind bisher nur für Deutsch/Englisch lokalisiert;
  Französisch/Japanisch-Clients fallen auf Englisch zurück.
