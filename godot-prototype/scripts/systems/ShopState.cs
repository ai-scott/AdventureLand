using Godot;

namespace AdventureLandPrototype;

/// <summary>
/// Global shop-mode flag. Mirrors C3's ShopMode global var: true when the
/// player is standing in a shop layout (Blacksmith, Adventure Shop, General
/// Store, Penny's House). Item pickups branch on this to decide between a
/// free collect and a purchase prompt.
///
/// Scene lifecycle:
///   - WorldMeta._Ready sets IsActive = IsShop on scene load.
///   - Scene unload clears it automatically (OnSceneLeaving).
///
/// Free-grant mechanic:
///   The `NextItemFree` flag, set by a dialogue action (grantFreeItem), lets
///   the next purchase in a shop be free. Consumed by ItemTrigger on pickup.
///   One-shot, scene-scoped — resets on scene change so authors can't grant
///   a free item "for use in a different shop".
/// </summary>
public static class ShopState
{
    public static bool IsActive { get; private set; }

    /// <summary>True if a dialogue has granted the player a single free
    /// purchase. Consumed by the next successful pickup in a shop.</summary>
    public static bool NextItemFree { get; set; }

    /// <summary>Called by WorldMeta on scene load. `isShop=false` also clears
    /// any pending NextItemFree so the grant doesn't leak between shops.</summary>
    public static void SetActive(bool isShop)
    {
        IsActive = isShop;
        if (!isShop) NextItemFree = false;
    }
}
