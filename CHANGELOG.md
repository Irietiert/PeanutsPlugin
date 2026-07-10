# Changelog

All notable changes to Peanuts are documented here.
This project loosely follows [Semantic Versioning](https://semver.org/).

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
