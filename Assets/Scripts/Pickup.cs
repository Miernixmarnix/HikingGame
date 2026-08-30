using UnityEngine;

// Attach this to any object you want the player to be able to pick up.
// It doesn't do anything on its own — PlayerInteraction.cs finds this component with a
// raycast and calls CollectItem() on it when the player looks at it and presses E.
//
// Requires a Collider on the same GameObject (any shape) so the raycast can hit it.
public class Pickup : MonoBehaviour
{
    // A human-readable name for this item. Not used yet, but handy once you build an
    // inventory/hotbar and need something to display or store.
    public string itemName = "Item";

    // Called by PlayerInteraction when the player collects this object.
    public void CollectItem()
    {
        // --- INVENTORY INTEGRATION POINT ---
        // When you build an inventory/hotbar system, this is where you'd add the item to it
        // BEFORE the object disappears, e.g.:
        //   Inventory.Instance.AddItem(itemName);
        // For now, we just remove the object from the scene.

        Destroy(gameObject);
    }
}
