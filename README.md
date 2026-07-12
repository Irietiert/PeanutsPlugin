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
`/peanutsres` -> Resets the whole overlay to 0. Requires confirmation: `/peanutsres confirm`.

Meowdy ♥

## About this project / AI disclosure

This plugin is a hobby project, not the work of a professional developer. It was
written with heavy AI assistance (Claude, by Anthropic).

Using the levels from Dalamud's [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy),
this project is **Copilot**: the AI did most of the writing, while the design
decisions, the game-domain knowledge, and all building and testing were done by
a human. Every line was compiled and played before release; several AI mistakes
(a non-existent field name, an ImGui pointer/value mix-up, a wrong assumption
about how the chocobo saddlebag works) were caught that way.

The plugin icon is currently **AI-generated**. A hand-made replacement is planned.

## How items are detected

Every tracked item carries its own resolved `ItemId`, German/English name,
NQ/HQ sell price, and max stack size, fetched once at startup via the Lumina
`Item` sheet (`ItemIdResolver.cs`). Resolution primarily looks items up **by
their already-known ItemId** (robust across renames/patches); a name-based
search against the German sheet is only used the very first time a new item
is added.

`InventoryScanner.cs` scans both the four main inventory bags (always 140
slots) **and** the chocobo saddlebag (always 70 slots) for these IDs -
independent of client language and tooltip timing. HQ is read from the item's
quality flag in game memory (not from the ItemId), so HQ and NQ of the same
item are counted separately; anything not flagged HQ (including collectables)
is counted as NQ. The saddlebag can only be read once it's unlocked (later in
the MSQ) and has been opened at least once this session; until then its column
shows "?" rather than a misleading empty value.

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
  Below the tree: a **Growth since first measurement** table (first value,
  current value, growth, trend sparkline - grouped by character, world, data
  center or player), plus a switchable Gil distribution area (by world, data
  center or player) offering total, growth since last save/export, a revenue
  trend line across all saved snapshots, growth per save, an item-share donut,
  and up to four dated history snapshots; plus a stacked bar chart of each
  world's Gil composition by item, and an item heatmap showing who collected
  the most of what. Expanding a character shows a resizable table with
  separate NQ/HQ/Stacks columns per item (both qualities combined into a
  single row).
- **History tab**: pick a world and character, browse Gil history as a graph
  and table (deduplicated to one entry per day, filterable to 1 week / 3
  months / 6 months / 1 year), restore a previous complete overlay snapshot,
  and see "Battle of Datacenter" - a monthly ranking of all characters in the
  same data center by Gil growth. Automatically jumps to whichever character
  just finished a scan.
- **Edit tab**: manual language override (Auto/German/English), a behavior
  toggle to start the tool automatically after login (on by default),
  CSV/Excel export settings, a **Character** section to search all scanned
  characters and independently toggle their visibility in the Tool tab,
  revenue history, ranking, and export - plus a reversible "delete"
  (Ctrl+click) that moves a character to the **Field of Honor** at the bottom
  of the tab, where "Pulse of Life!" brings them back at any time, and only
  "Aetherial Sea" (with its own confirmation) actually, permanently removes
  them. A "Factory reset" button in the danger zone resets the whole overlay
  structure (history is kept). A **Share & import** section lets you export
  your own stock and import other players' data (see below).
- **Item tab**: search the full FFXIV item catalog and add anything you want
  to track (already-added items appear grayed out in search results to
  prevent duplicates), enable/disable tracking per item without losing
  existing data, or delete an item entirely (Ctrl+click). Every change here
  saves and re-scans immediately - no separate save step needed.

## Sharing & importing (compare with friends / FC stock)

Peanuts can exchange stock data between players. This uses a dedicated
**share file** (`Peanuts Share.json`), *not* the CSV/Excel exports - those are
localized, append-only reports without item IDs and are unusable as an import
source. The share file identifies items by **ID**, so it works across client
languages, and carries the sender's name.

- **Export my data** writes the share file into your export folder. It contains
  **only your own characters** - imported data from others is never passed on.
- **Import share file** reads a share file from that same folder. Imported
  characters are tagged with their owner, shown as `Name [Owner]`, and are
  **never scanned and never overwrite your own characters**. A character is
  identified by world + name (unique in FFXIV), so re-importing simply updates
  it - no duplicates, no accumulation.
- The first time a file from a new sender arrives, Peanuts asks **who it belongs
  to** and you give them a nickname. Every later import from that sender is
  applied automatically under that nickname - even when new characters are
  included. If the sender's name changes (e.g. they exported from a different
  alt), Peanuts matches the characters against existing imports and suggests the
  right player.
- Set a **stable share name** so that exporting from a different alt doesn't
  appear as a second player on the recipient's side.
- Imported players don't count towards your totals by default - they're there to
  compare against. Turn on **"Include imported in totals"** to get a shared
  overall stock (e.g. for an FC); CSV/Excel exports follow the same setting.
- Imported players can be hidden in bulk (one toggle per player), or removed
  entirely including their history entries.
- Slots, bag and stacks are deliberately **not** transferred; imported characters
  show "?" there.

Where to see imported players: the **"By Player"** donut in the Gil distribution,
the **"Player"** scope in *Growth since first measurement*, and the character
picker in the **History** tab.

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
(`Configuration.cs`), saved whenever a scan actually changes something and on
every Save/Export. Visibility toggles only persist the setting - they no
longer write export files or history snapshots. History snapshots,
per-character visibility flags (including the Field of Honor archive), and the
tracked item list are all part of this same file - hiding, archiving, or
disabling something never touches the underlying stored data unless you
explicitly use "Aetherial Sea". History entries older than ~400 days are
pruned automatically so the file doesn't grow without bound.

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
`/peanutsres` -> Setzt das komplette Overlay auf 0 zurück. Erfordert Bestätigung: `/peanutsres confirm`.

„Beschreibung": „Kratze die vorhandenen Schmuckstücke/Items zusammen und notiere eifrig deren Anzahl sowie den aktuellen Besitzer in Tatarus kleinem Notizbuch."

## Über dieses Projekt / KI-Hinweis

Dieses Plugin ist ein Hobbyprojekt, nicht die Arbeit eines professionellen
Entwicklers. Es wurde mit starker KI-Unterstützung geschrieben (Claude, von
Anthropic).

Nach den Stufen aus Dalamuds [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy)
entspricht das Projekt der Stufe **Copilot**: Die KI hat den Großteil des Codes
geschrieben, während die Design-Entscheidungen, das Spielwissen sowie sämtliches
Bauen und Testen von einem Menschen kamen. Jede Zeile wurde vor der
Veröffentlichung kompiliert und im Spiel geprüft; mehrere KI-Fehler (ein nicht
existierender Feldname, eine Zeiger/Wert-Verwechslung in ImGui, eine falsche
Annahme über die Chocobo-Satteltasche) wurden dabei gefunden.

Das Plugin-Icon ist derzeit **KI-generiert**. Ein selbst erstelltes Icon ist geplant.

## Wie die Items erkannt werden

Jedes getrackte Item trägt seine eigene aufgelöste `ItemId`, den
deutschen/englischen Namen, NQ/HQ-Verkaufspreis und maximale Stapelgröße,
einmalig beim Start über das Lumina-`Item`-Sheet aufgelöst
(`ItemIdResolver.cs`). Die Auflösung schlägt primär **per bereits bekannter
ItemId** nach (robust gegenüber Umbenennungen/Patches); eine namensbasierte
Suche im deutschen Sheet wird nur beim allerersten Hinzufügen eines neuen
Items verwendet.

`InventoryScanner.cs` durchsucht sowohl die vier Haupttaschen (immer 140
Plätze) **als auch** die Chocobo-Satteltasche (immer 70 Plätze) nach diesen
IDs - unabhängig von Client-Sprache und Tooltip-Timing. HQ wird über das
Qualitäts-Flag im Spielspeicher erkannt (nicht über die ItemId), daher werden
HQ und NQ desselben Items getrennt gezählt; alles ohne HQ-Flag (auch
Sammlerstücke) zählt als NQ. Die Satteltasche kann erst ausgelesen werden,
wenn sie freigeschaltet (später in der MSQ) und in dieser Session mindestens
einmal geöffnet wurde; bis dahin zeigt ihre Spalte "?" statt eines
irreführend leeren Werts.

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
  "Zusammenfassung kopieren". Darunter: eine Tabelle **Entwicklung seit der
  ersten Messung** (erster Wert, aktueller Wert, Zuwachs, Verlaufs-Sparkline -
  gruppiert nach Charakter, Welt, Datenzentrum oder Spieler), dazu ein
  umschaltbarer Diagramm-Bereich zur Gil-Verteilung (nach Welt, Datenzentrum
  oder Spieler) mit Gesamtstand, Zuwachs seit dem letzten Speichern/Export,
  Umsatz-Verlaufslinie über alle Snapshots, Zuwachs je Speicherung,
  Item-Anteil-Donut und bis zu vier datierten Snapshots, ein gestapeltes
  Balkendiagramm zur Item-Zusammensetzung je Welt, und eine Item-Heatmap. Beim Aufklappen
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
  entfernt. Ein "Werkseinstellung"-Button in der Gefahrenzone setzt die
  komplette Overlay-Struktur zurück (Verlauf bleibt erhalten). Eine Sektion
  **Teilen & Importieren** erlaubt den Austausch mit anderen Spielern (siehe
  unten). Außerdem ein Schalter, ob das Tool nach dem Login automatisch startet.
- **Item-Tab**: den kompletten FFXIV-Itemkatalog durchsuchen und beliebige
  Items zum Tracking hinzufügen (bereits hinzugefügte Items erscheinen
  ausgegraut in der Suche, verhindert Doppelerfassung), Tracking pro Item
  an-/abschalten ohne Datenverlust, oder ein Item komplett löschen
  (STRG-Klick). Jede Änderung hier speichert und scannt sofort neu - kein
  separater Speicherschritt nötig.

## Teilen & Importieren (Vergleich mit Freunden / FC-Bestand)

Peanuts kann Bestände zwischen Spielern austauschen. Dafür gibt es eine eigene
**Share-Datei** (`Peanuts Share.json`) - *nicht* die CSV/Excel-Exporte: die sind
lokalisierte, fortlaufend angehängte Berichte ohne ItemIds und als Importquelle
unbrauchbar. Die Share-Datei identifiziert Items über die **ID**, funktioniert
also über Client-Sprachen hinweg, und trägt den Namen des Absenders.

- **Eigene Daten exportieren** schreibt die Share-Datei in den Exportordner. Sie
  enthält **nur deine eigenen Charaktere** - importierte Fremddaten werden nie
  weitergereicht.
- **Share-Datei importieren** liest eine Share-Datei aus demselben Ordner.
  Importierte Charaktere sind mit ihrem Besitzer gekennzeichnet (`Name [Owner]`),
  werden **nie gescannt und überschreiben nie deine eigenen Charaktere**. Ein
  Charakter ist über Welt + Name eindeutig (in FFXIV kann es auf einer Welt keine
  zwei gleichnamigen Charaktere geben), ein erneuter Import aktualisiert ihn also
  einfach - keine Dubletten, kein Aufaddieren.
- Kommt eine Datei von einem noch unbekannten Absender, fragt Peanuts, **wem sie
  gehört**, und du vergibst einen Spitznamen. Jeder weitere Import desselben
  Absenders wird automatisch unter diesem Spitznamen übernommen - auch dann, wenn
  neue Charaktere dazugekommen sind. Ändert sich der Absender-Name (z.B. weil von
  einem anderen Alt exportiert wurde), gleicht Peanuts die Charaktere mit den
  bisherigen Importen ab und schlägt den passenden Spieler vor.
- Ein **stabiler Freigabe-Name** sorgt dafür, dass ein Export von einem anderen
  Alt beim Empfänger nicht als zweiter Spieler ankommt.
- Importierte Spieler zählen standardmäßig **nicht** in deine Gesamtsummen - sie
  dienen dem Vergleich. Mit **"Importierte in Gesamtsummen einbeziehen"** ergibt
  sich ein gemeinsamer Gesamtbestand (z.B. für eine FC); die CSV/Excel-Exporte
  folgen derselben Einstellung.
- Importierte Spieler lassen sich sammelweise ausblenden (ein Schalter pro
  Spieler) oder komplett entfernen, inklusive ihrer Verlaufseinträge.
- Slots, Satteltasche und Stacks werden bewusst **nicht** übertragen; importierte
  Charaktere zeigen dort "?".

Wo importierte Spieler sichtbar sind: der Donut **"Nach Spieler"** in der
Gil-Verteilung, der Bereich **"Spieler"** in *Entwicklung seit der ersten
Messung*, und die Charakterauswahl im **Verlauf**-Tab.

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
(`Configuration.cs`), gespeichert immer dann, wenn ein Scan tatsächlich etwas
ändert, sowie bei jedem Save/Export. Sichtbarkeits-Schalter speichern nur die
Einstellung - sie schreiben keine Exportdateien und keine Verlaufs-Snapshots
mehr. Verlaufs-Snapshots, die Sichtbarkeits-Schalter pro Charakter (inklusive
des Feld-der-Ehre-Archivs) und die getrackte Item-Liste sind alle Teil
derselben Datei - Ausblenden, Archivieren oder Deaktivieren rührt die
gespeicherten Daten nie an, außer du nutzt explizit "Aetherial Sea".
Verlaufseinträge, die älter als ~400 Tage sind, werden automatisch entfernt,
damit die Datei nicht unbegrenzt wächst.

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
