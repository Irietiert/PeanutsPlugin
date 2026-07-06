namespace PeanutsPlugin.Data;

/// <summary>
/// Angebrochene Stacks zählen als voller Stack (Aufrundung), weil im
/// Inventar trotzdem ein zusätzlicher Slot belegt wird. Die Stapelgröße ist
/// NICHT mehr pauschal 99 - viele Items (v.a. Crafting-Materialien) stapeln
/// bis 999. Die tatsächliche Größe kommt pro Item aus ItemDefinition.MaxStackSize.
/// </summary>
public static class StackMath
{
    public const int DefaultStackSize = 99;

    public static int CeilDiv(int quantity, int stackSize = DefaultStackSize)
    {
        if (quantity <= 0 || stackSize <= 0)
            return 0;
        return (quantity + stackSize - 1) / stackSize;
    }
}
