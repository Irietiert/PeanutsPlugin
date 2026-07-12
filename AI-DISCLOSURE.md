# AI Usage Disclosure (for the DalamudPluginsD17 pull request)

> Copy the section below into the **pull request description** when submitting
> Peanuts to `goatcorp/DalamudPluginsD17`. Per the
> [AI Usage Policy](https://dalamud.dev/plugin-publishing/ai-policy), disclosure
> belongs in the PR description, not in the plugin itself.
>
> Note: the current icon IS AI-generated and is disclosed as such below. The
> policy explicitly prefers a hand-made icon (even a crude one), and the approval
> team may ask you to replace it before merging. Consider replacing it *before*
> submitting to save a review round-trip.

---

## AI usage: Copilot

Per the AI Usage Policy levels, this plugin is **Copilot**: the AI (Claude, by
Anthropic) wrote most of the code, while I planned the features, made the design
decisions, and built, ran and tested every change in-game before release.

**What I did myself:**

- All building (`dotnet build -c Release`) and all in-game testing. The AI had no
  access to a compiler or to the game at any point.
- All design decisions and the game-domain knowledge behind them.
- Caught and corrected several AI mistakes, including:
  - a guessed `InventoryContainer.Loaded` field that doesn't exist (the real one
    is `IsLoaded`),
  - `ImGui.GetStyleColorVec4` being treated as a value when this binding returns
    a pointer,
  - a saddlebag detection routine based on container `Size`, which wrongly
    reported a locked saddlebag as "present and empty" — the saddlebag unlocks
    later in the MSQ and always holds exactly 70 slots,
  - HQ detection based on an ItemId offset instead of the item's quality flag.

**I can explain any part of this plugin and why it is implemented the way it is.**

**Assets:** `images/icon.png` is **AI-generated**. I'm aware the policy prefers a
hand-made icon and I intend to replace it with my own; I'm happy to do so before
merge if you'd like.

**Translations:** The DE/EN strings are written by a native German speaker; the
English strings are AI-assisted and human-reviewed.
