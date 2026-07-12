# Changelog

All notable changes to Peanuts are documented here.
This project loosely follows [Semantic Versioning](https://semver.org/).

## [1.4.0.0] - 2026-07-12

### Added
- **Unknown items on import are now offered by name.** When a share file contains
  items you don't track yourself, Peanuts lists them by name (in your selected
  language) and asks whether to add them to your tracking list. Say yes and the
  items are created with their correct NQ/HQ price and stack size - and the same
  import is **re-applied automatically**, this time in full. No need to pick the
  file again or re-assign the owner; both are still known.
  Previously these entries were only reported as a count and silently skipped.

## [1.3.0.1] - 2026-07-12

### Changed
- **The saddlebag column now shows its fill level, like the inventory column.**
  A duplicate find used to paint the whole cell pink, which overwrote the
  free-slot colour - so you could no longer see how full the saddlebag was.
  Duplicates are still reported in chat and marked pink in the character's item
  table, where you can also see *which* item is affected.
- The plugin icon is now **hand-drawn**; the previous AI-generated icon is gone.
- Documentation cleanup: trimmed the "Still open" list in the README.

### Added
- **Unknown items in an import are now offered by name.** Previously, items the
  other player tracks but you don't were silently counted and skipped ("3 entries
  skipped"). Now they're resolved to their actual names (in your selected
  language) and listed in the Edit tab with a simple choice:
  - **"Yes, add them"** - the items are added to your tracking list with the
    correct NQ/HQ prices and stack size, and **the import is immediately
    re-applied automatically**, this time complete. You don't need to pick the
    file again, and the owner is already known.
  - **"No, skip"** - the import stays as it is; those items are ignored.

  This matters because a share file only carries item **IDs and quantities**, not
  the other player's item list. If you don't track an item, its numbers have
  nowhere to go. This dialog is how you close that gap without having to guess
  which item was missing.

## [1.3.0.0] - 2026-07-12

### Added
- **Growth since first measurement** section under the Gil total: per-entry first
  value, current value, growth (color-coded) and a trend sparkline. Switchable
  between **Character / World / Data center / Player**.
- **Gil distribution: "By Player"** as a third grouping (next to World and Data
  Center) - compares you ("Me") against imported players. Imported players are
  always included in this view, regardless of the totals toggle.
- **Share & import** (Edit tab): export your own stock as `Peanuts Share.json`
  and import another player's file. Built for comparing with friends and for
  tracking a shared FC stock.
  - Imported characters are tagged with their owner, are **never scanned and
    never overwrite your own characters**, and are excluded from your totals by
    default (toggle "Include imported in totals" to get a combined stock).
  - A character is identified by **world + name** (unique in FFXIV), so a
    re-import updates in place - it never duplicates or accumulates.
  - **Owner assignment on import**: the first time a file from a new sender
    arrives, Peanuts asks who it belongs to and you give them a nickname. Every
    later import from that same sender is applied automatically under that
    nickname - even when new characters are included. If the sender's name in the
    file changes (e.g. they exported from a different alt), Peanuts matches the
    characters against existing imports and suggests the right player.
  - A **stable share name** can be set, so exporting from a different alt doesn't
    show up as a second player on the recipient's side.
  - Importing is itself a measurement: it lands in the history with the *sender's*
    export timestamp, so imported players appear in the revenue history right away.
  - Imported players can be hidden in bulk (one toggle per player) or removed
    entirely, including their history entries.
  - Slots, bag and stack data are deliberately **not** transferred - imported
    characters show "?" there.
- Item search now matches **German and English** names; results are shown in the
  language selected in the Edit tab.

### Fixed
- **CSV/Excel exports could include imported characters** while the overlay's
  totals excluded them, so report and display showed different numbers. Both now
  follow the same rule; when included, imported characters are marked
  `Name [Owner]`. The share file never contains other players' data at all.

### Note
The CSV/Excel exports are localized, append-only reports without item IDs - they
are deliberately **not** used as an import source. The share file is a dedicated,
language-neutral snapshot format (items identified by ID).

## [1.2.0.2] - 2026-07-12

### Fixed
- **Characters without a saddlebag kept showing a stale "70/70".** Two issues:
  the not-loaded case preserved the previously stored (wrong) values instead of
  marking them unknown, and characters that aren't currently logged in never got
  rescanned. The saddlebag is now marked unknown ("?") whenever it isn't loaded,
  and a one-time config migration clears the bad values written by earlier
  versions. Gil totals, item counts and history are untouched.

### Changed
- Item search now matches **German and English** item names, and the result list
  is shown in the language selected in the Edit tab.

## [1.2.0.1] - 2026-07-12

### Fixed
- **The saddlebag was shown for every character, always appearing empty.**
  Availability was detected via the container's `Size`, which is also set for a
  saddlebag that hasn't been unlocked yet (it unlocks later in the MSQ) - so it
  showed up as "70/70 free" instead of unknown. It's now detected via the
  container's `Loaded` flag, and characters without an unlocked (or not yet
  opened) saddlebag correctly show "?" again. Saddlebag capacity is a fixed 70
  slots.

## [1.2.0.0] - 2026-07-10

### Added
- Three new views in the Gil distribution area: a **revenue trend line** across
  all saved snapshots, a **growth-per-save** bar chart, and a global
  **item-share** donut (in addition to total, growth-since-save, and the dated
  snapshots).
- **Auto-start toggle** in the Edit tab: choose whether the tool starts
  automatically after login (on by default).
- Automatic **history pruning** (entries older than ~400 days are dropped) so
  the config file no longer grows without bound.

### Changed
- **Visibility toggles are now display-only.** Toggling a character's
  visibility no longer writes export files or history snapshots - it just
  persists the setting. Snapshots/exports still run via the Save/Export buttons
  and `/peanutsex`.
- The **saddlebag column** now shows "?" until the saddlebag is actually
  unlocked (later in the MSQ) and has been opened once this session, instead of
  a misleading empty "70/70". Saddlebag capacity is treated as a fixed 70.
- The config is now only written when a scan **actually changes something**,
  removing constant idle disk writes.
- `/peanutsres` now requires confirmation: **`/peanutsres confirm`**.
- The donut chart hole uses the current **theme background color** (no more dark
  spot in light themes).
- The danger-zone **"Broken" button was renamed to "Factory reset"**.
- Localized the "copy summary" output, the chart "no data" text, and the unknown
  data center label (DE/EN).

### Fixed
- **HQ items were counted as NQ.** HQ is now read from the item's quality flag
  in game memory instead of from the ItemId, so HQ-capable items are split
  correctly. (Gil totals were always correct; only the NQ/HQ split was off.)
- **Export no longer crashes** when "Tataru's Note" is open in Excel - it now
  reports a friendly message instead of throwing.
- The **"Reset (0)" history marker** now actually appears after a reset (it was
  previously dead code).

### Internal
- `OpenMainUi` handler is now unsubscribed on unload.
- Inventory scan uses a dictionary lookup instead of a per-slot linear search.
- Removed unused code and fixed a few doc comments.

## [1.1.0.0]
- Earlier release (item-level duplicate detection, NQ/HQ breakdown column,
  selectable charts 1-6, and more).
