# Peanuts Plugin (Dalamud)

Reads the quantity of tracked items directly from the inventory and the
chocobo saddlebag of the currently logged-in character (no hovering/tooltips
needed) and logs them per character/world, in a layout originally inspired by
an "Umsatzrechner" Excel sheet. What started as a tracker for 8 fixed
"salvaged" jewelry items has grown into a fully customizable tool: you can
add any item from the full FFXIV item catalog, NQ and HQ are recognized and
priced separately, and a whole set of overlay features was built on top -
diagrams, a history with rollback, a monthly cross-character ranking, growing
CSV/Excel exports, per-character visibility controls with a fully reversible
archive, and a language system that follows the client or can be set manually.

## Installation

1. Install FFXIVQuickLauncher and enable Dalamud in the settings.
2. Open the Dalamud settings via the Dalamud icon in the top-left corner
   after launching, or by typing `/xlsettings` into the in-game chat.
3. Switch to the "Experimental" tab.
4. Locate the "Custom Plugin Repositories" section, agree to the listed
   terms if prompted, and paste the following link into the text input field:
   ```
   https://raw.githubusercontent.com/Irietiert/PeanutsPlugin/main/pluginmaster.json
   ```
5. Click the "+" button next to it.
6. Click the "Save" button.

Peanuts should now be available to you in the plugin installer ♥

## Commands

`/peanuts` -> Opens/closes the Peanuts overlay.
`/peanutson` -> Starts the Peanuts tool.
`/peanutsoff` -> Stops the Peanuts tool.
`/peanutstot` -> Prints the grand total (Gil/stacks/characters) directly to chat.
`/peanutsex` -> Exports Tataru's Note (CSV/Excel, depending on the Edit tab settings).
`/peanutsres` -> Resets the whole overlay (all worlds/characters/history).

Meowdy ♥

## About this project / LLM disclosure

This plugin was built by a hobbyist, not a professional developer - created
with the support of an LLM. Claude (Anthropic)

## How items are detected

Every tracked item carries its own resolved `ItemId`, German/English name,
NQ/HQ sell price, and max stack size, fetched once at startup via the Lumina
`Item` sheet (`ItemIdResolver.cs`). Resolution primarily looks items up **by
their already-known ItemId** (robust across renames/patches); a name-based
search against the German sheet is only used the very first time a new item
is added.

`InventoryScanner.cs` scans both the four main inventory bags **and** the
chocobo saddlebag (70 slots) for these IDs - independent of client language
and tooltip timing. NQ, HQ, and collectable variants of the same item are
recognized separately in game memory (HQ = base ItemId + 1,000,000,
collectable = base ItemId + 500,000; collectables are counted as NQ, since
they can't be sold to NPCs).

If a tracked item is found in **both** the main inventory and the saddlebag
at the same time, that's flagged as a "duplicate find": both quantities are
still added together for all totals, but the item name and the affected
quantity/Bag column are highlighted in pink so you notice it, and the "scan
complete" chat message gets a red "- - Doppelfund!!! - -" (duplicate find)
warning appended.

## Overlay features

- **Tool tab**: live search across names/worlds, an expandable tree grouped
  by data center → world → character, with Gil, Stacks, free inventory Slots,
  and the chocobo saddlebag ("Bag") shown per character (colored progress
  bars, green = plenty of room, red = nearly full), plus trend sparklines
  next to Gil values. Start/Stop, Reset, Save, Export, and "Copy summary".
  Below the tree: a Gil distribution donut chart (by world or data center)
  shown side by side with a second donut showing Gil growth since the last
  save/export, a stacked bar chart of each world's Gil composition by item,
  and an item heatmap showing who collected the most of what. Expanding a
  character shows a resizable table with separate NQ/HQ/Stacks columns per
  item (both qualities combined into a single row).
- **History tab**: pick a world and character, browse Gil history as a graph
  and table (deduplicated to one entry per day, filterable to 1 week / 3
  months / 6 months / 1 year), restore a previous complete overlay snapshot,
  and see "Battle of Datacenter" - a monthly ranking of all characters in the
  same data center by Gil growth. Automatically jumps to whichever character
  just finished a scan.
- **Edit tab**: manual language override (Auto/German/English), CSV/Excel
  export settings, a **Character** section to search all scanned characters
  and independently toggle their visibility in the Tool tab, revenue
  history, ranking, and export - plus a reversible "delete" (Ctrl+click)
  that moves a character to the **Field of Honor** at the bottom of the tab,
  where "Pulse of Life!" brings them back at any time, and only "Aetherial
  Sea" (with its own confirmation) actually, permanently removes them. A
  "Broken" button in the danger zone resets the whole overlay structure
  (history is kept).
- **Item tab**: search the full FFXIV item catalog and add anything you want
  to track (already-added items appear grayed out in search results to
  prevent duplicates), enable/disable tracking per item without losing
  existing data, or delete an item entirely (Ctrl+click). Every change here
  saves and re-scans immediately - no separate save step needed.

## Adjusting sale prices

NQ sell prices come straight from the game's `Item` sheet (`PriceLow`) and
stay correct automatically across patches. HQ prices aren't stored in the
game data as a separate field, so they're estimated with a +10% bonus
(`ItemDefinition.EstimateHqPrice`) - if that's off for a specific item
compared to its real in-game sell price, that factor can be adjusted
centrally in the code. `Configuration.PriceOverrides` still exists as a
manual override dictionary, but there is currently no in-UI editor for it.

## Storage

All data lives as JSON in the standard Dalamud plugin configuration
(`Configuration.cs`), saved automatically after every scan and every
Save/Export/toggle. History snapshots, per-character visibility flags
(including the Field of Honor archive), and the tracked item list are all
part of this same file - hiding, archiving, or disabling something never
touches the underlying stored data unless you explicitly use "Aetherial Sea".

Export ("Tataru's Note") doesn't overwrite itself: CSV (`CsvExporter.cs`,
semicolon-separated) appends a new, dated block on every export, marking
calendar month changes. Excel (`ExcelExporter.cs`, via ClosedXML) creates a
new worksheet per calendar month. The export folder can be set in the Edit tab.

## Still open

- Highlighting a value in a different color right when it changes since the
  last scan (there's a "since last save/export" delta summary, but no
  per-cell flash on individual changes).
- An in-UI editor for `Configuration.PriceOverrides`.
- Google Sheets isn't directly supported; the CSV file can be imported
  manually via File → Import.
- Retainer inventory isn't scanned - only the character's own inventory and
  saddlebag.
- Item names are only localized for German/English so far; French/Japanese
  clients fall back to English.

---

# Peanuts Plugin (Dalamud) - Deutsch

Liest die Stückzahl getrackter Items direkt aus dem Inventar und der
Chocobo-Satteltasche des eingeloggten Charakters aus (kein Hover/Tooltip
nötig) und trägt sie pro Charakter/Welt ein, ursprünglich im Layout einer
"Umsatzrechner"-Excel-Mappe angelehnt. Was als Tracker für 8 feste
"geborgene" Schmuck-Items begann, ist zu einem komplett anpassbaren Tool
geworden: Du kannst beliebige Items aus dem kompletten FFXIV-Itemkatalog
hinzufügen, NQ und HQ werden getrennt erkannt und bepreist, und obendrauf ist
ein ganzes Bündel an Overlay-Funktionen entstanden - Diagramme, ein Verlauf
mit Wiederherstellung, ein monatliches Charakter-Ranking, wachsende
CSV/Excel-Exporte, Sichtbarkeits-Schalter pro Charakter mit einem komplett
reversiblen Archiv, und ein Sprachsystem, das der Client-Sprache folgt oder
manuell eingestellt werden kann.

## Installation

1. FFXIVQuickLauncher installieren und Dalamud in den Einstellungen aktivieren.
2. Die Dalamud-Einstellungen öffnen - über das Dalamud-Symbol oben links nach
   dem Start, oder per `/xlsettings` im Spielchat.
3. Zum Reiter "Experimentell" wechseln.
4. Den Abschnitt "Custom Plugin Repositories" finden, den Hinweistext ggf.
   bestätigen, und folgenden Link ins Textfeld einfügen:
   ```
   https://raw.githubusercontent.com/Irietiert/PeanutsPlugin/main/pluginmaster.json
   ```
5. Auf den "+"-Button daneben klicken.
6. Auf "Speichern" klicken.

Peanuts sollte jetzt im Plugin-Installer auftauchen ♥

## Commands

`/peanuts` -> Öffnet/schließt das Peanuts-Overlay.
`/peanutson` -> Startet das Peanuts-Tool.
`/peanutsoff` -> Stoppt das Peanuts-Tool.
`/peanutstot` -> Druckt die Gesamtsumme (Gil/Stacks/Charaktere) direkt in den Chat.
`/peanutsex` -> Exportiert Tataru's Note (CSV/Excel, je nach Edit-Einstellung).
`/peanutsres` -> Setzt das komplette Overlay zurück (alle Welten/Charaktere/Verlauf).

„Beschreibung": „Kratze die vorhandenen Schmuckstücke/Items zusammen und notiere eifrig deren Anzahl sowie den aktuellen Besitzer in Tatarus kleinem Notizbuch."

## Über dieses Projekt / LLM-Hinweis

Dieses Plugin wurde von einem Hobbyisten gebaut, nicht von einem
professionellen Entwickler - mit Unterstützung eines LLM erstellt.
Claude (Anthropic)

## Wie die Items erkannt werden

Jedes getrackte Item trägt seine eigene aufgelöste `ItemId`, den
deutschen/englischen Namen, NQ/HQ-Verkaufspreis und maximale Stapelgröße,
einmalig beim Start über das Lumina-`Item`-Sheet aufgelöst
(`ItemIdResolver.cs`). Die Auflösung schlägt primär **per bereits bekannter
ItemId** nach (robust gegenüber Umbenennungen/Patches); eine namensbasierte
Suche im deutschen Sheet wird nur beim allerersten Hinzufügen eines neuen
Items verwendet.

`InventoryScanner.cs` durchsucht sowohl die vier Haupttaschen **als auch**
die Chocobo-Satteltasche (70 Plätze) nach diesen IDs - unabhängig von
Client-Sprache und Tooltip-Timing. NQ-, HQ- und Sammlerstück-Varianten
desselben Items werden im Spielspeicher getrennt erkannt (HQ = Basis-ItemId +
1.000.000, Sammlerstück = Basis-ItemId + 500.000; Sammlerstücke zählen als
NQ, da sie nicht an NPCs verkauft werden können).

Wird ein getracktes Item **gleichzeitig** im Hauptinventar und in der
Satteltasche gefunden, gilt das als "Doppelfund": beide Mengen werden für
alle Summen trotzdem zusammengezählt, aber Item-Name und betroffene
Stückzahl-/Bag-Spalte werden pink hervorgehoben, damit es auffällt - und die
"Erfassung abgeschlossen"-Meldung bekommt einen rot hervorgehobenen Zusatz
"- - Doppelfund!!! - -".

## Overlay-Funktionen

- **Tool-Tab**: Live-Suche nach Name/Welt, eine aufklappbare Baumansicht
  gruppiert nach Datenzentrum → Welt → Charakter, mit Gil, Stacks, freien
  Inventarplätzen und der Chocobo-Satteltasche ("Bag") pro Charakter
  (farbige Fortschrittsbalken, grün = viel Platz, rot = fast voll), dazu
  Trend-Sparklines neben den Gil-Werten. Start/Stop, Reset, Save, Export und
  "Zusammenfassung kopieren". Darunter: ein Donut-Diagramm zur
  Gil-Verteilung (nach Welt oder Datenzentrum), daneben ein zweites für den
  Zuwachs seit dem letzten Speichern/Export, ein gestapeltes Balkendiagramm
  zur Item-Zusammensetzung je Welt, und eine Item-Heatmap. Beim Aufklappen
  eines Charakters erscheint eine größenverstellbare Tabelle mit getrennten
  NQ-/HQ-/Stacks-Spalten je Item (beide Qualitäten in einer Zeile kombiniert).
- **History-Tab**: Welt und Charakter wählen, Gil-Verlauf als Graph und
  Tabelle ansehen (pro Tag dedupliziert, filterbar auf 1 Woche / 3 Monate /
  6 Monate / 1 Jahr), einen früheren Overlay-Zustand wiederherstellen, und
  "Battle of Datacenter" - ein monatliches Ranking aller Charaktere
  desselben Datenzentrums nach Gil-Zuwachs. Springt automatisch auf den
  Charakter, dessen Erfassung gerade abgeschlossen wurde.
- **Edit-Tab**: manuelle Sprachüberschreibung (Automatisch/Deutsch/English),
  CSV/Excel-Export-Einstellungen, eine **Charakter**-Sektion zum Durchsuchen
  aller gescannten Charaktere und einzelnen Umschalten ihrer Sichtbarkeit im
  Tool-Tab, Umsatzverlauf, Ranking und Export - plus ein reversibles
  "Löschen" (STRG-Klick), das einen Charakter ins **Feld der Ehre** am Ende
  des Tabs verschiebt, wo "Pulse of Life!" ihn jederzeit zurückholt, und nur
  "Aetherial Sea" (mit eigener Bestätigung) ihn tatsächlich endgültig
  entfernt. Ein "Broken"-Button in der Gefahrenzone (Werkseinstellung) setzt
  die komplette Overlay-Struktur zurück (Verlauf bleibt erhalten).
- **Item-Tab**: den kompletten FFXIV-Itemkatalog durchsuchen und beliebige
  Items zum Tracking hinzufügen (bereits hinzugefügte Items erscheinen
  ausgegraut in der Suche, verhindert Doppelerfassung), Tracking pro Item
  an-/abschalten ohne Datenverlust, oder ein Item komplett löschen
  (STRG-Klick). Jede Änderung hier speichert und scannt sofort neu - kein
  separater Speicherschritt nötig.

## Verkaufspreise anpassen

NQ-Verkaufspreise kommen direkt aus dem `Item`-Sheet des Spiels (`PriceLow`)
und bleiben dadurch automatisch über Patches hinweg korrekt. HQ-Preise sind
in den Spieldaten nicht als eigenes Feld hinterlegt und werden daher mit
einem +10%-Aufschlag geschätzt (`ItemDefinition.EstimateHqPrice`) - falls das
bei einem konkreten Item abweicht, lässt sich dieser Faktor zentral im Code
anpassen. `Configuration.PriceOverrides` existiert weiterhin als manuelles
Override-Dictionary, dafür gibt es aktuell aber keinen Editor im UI.

## Speicherung

Alle Daten liegen als JSON in der normalen Dalamud-Plugin-Konfiguration
(`Configuration.cs`), automatisch gespeichert nach jedem Scan und jedem
Save/Export/Schalter. Verlaufs-Snapshots, die Sichtbarkeits-Schalter pro
Charakter (inklusive des Feld-der-Ehre-Archivs) und die getrackte Item-Liste
sind alle Teil derselben Datei - Ausblenden, Archivieren oder Deaktivieren
rührt die gespeicherten Daten nie an, außer du nutzt explizit "Aetherial Sea".

Der Export ("Tataru's Note") überschreibt sich nicht selbst: CSV
(`CsvExporter.cs`, Semikolon-getrennt) hängt bei jedem Export einen neuen,
datierten Block an und markiert Monatswechsel. Excel (`ExcelExporter.cs`,
via ClosedXML) legt pro Kalendermonat ein eigenes Arbeitsblatt an. Der
Export-Ordner lässt sich im Edit-Tab festlegen.

## Noch offen

- Farbliche Hervorhebung eines Werts direkt bei einer Änderung seit dem
  letzten Scan (aktuell nur eine "seit letztem Speichern/Export"-Delta-
  Zusammenfassung, keine Einzelzellen-Hervorhebung).
- Ein Editor im UI für `Configuration.PriceOverrides`.
- Google Sheets wird nicht direkt unterstützt; die CSV-Datei lässt sich
  manuell über Datei → Importieren laden.
- Retainer-Inventar wird nicht gescannt - nur das Charakter-eigene Inventar
  und die Satteltasche.
- Item-Namen sind bisher nur für Deutsch/Englisch lokalisiert;
  Französisch/Japanisch-Clients fallen auf Englisch zurück.

---

## Credits

- Astraea - Crazy Catlady ♥
- Celles - the best plasters in the world
- Eve - For there is always room for improvement
- Ray - Time is priceless

And thanks to all the helping hands, supporters, and volunteers. You are wonderful ♥
