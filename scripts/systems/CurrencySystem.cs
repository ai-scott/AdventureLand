using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Gem wallet. Mirrors the C3 CurrencySystem pattern (gems as single currency)
/// but lives as a static facade over SaveData, not an autoload — same model
/// as QuestSystem so callers don't need to carry a singleton reference.
///
/// All mutations go through SaveManager.CurrentData.Gems so saves/loads
/// persist automatically. No separate snapshot step needed.
/// </summary>
public static class CurrencySystem
{
    private const int MaxGems = 9999;

    [Signal] public delegate void GemsChangedEventHandler(int newAmount);

    private static SaveManager Mgr => SaveManager.Instance;

    public static int GetGems()
    {
        return Mgr?.CurrentData?.Gems ?? 0;
    }

    public static bool CanAfford(int cost)
    {
        return cost <= 0 || GetGems() >= cost;
    }

    /// <summary>Grants gems (positive amount). Clamped to MaxGems. Returns
    /// the amount actually added after clamp.</summary>
    public static int AddGems(int amount)
    {
        if (amount <= 0) return 0;
        var data = Mgr?.CurrentData;
        if (data == null) return 0;

        int before = data.Gems;
        data.Gems = System.Math.Min(MaxGems, before + amount);
        int added = data.Gems - before;
        if (added > 0) GD.Print($"[Currency] +{added} gems (→ {data.Gems})");
        return added;
    }

    /// <summary>Spends gems (positive amount). Returns true if the player
    /// had enough; false otherwise and nothing is deducted.</summary>
    public static bool RemoveGems(int amount)
    {
        if (amount <= 0) return true;
        var data = Mgr?.CurrentData;
        if (data == null || data.Gems < amount) return false;

        data.Gems -= amount;
        GD.Print($"[Currency] -{amount} gems (→ {data.Gems})");
        return true;
    }
}
