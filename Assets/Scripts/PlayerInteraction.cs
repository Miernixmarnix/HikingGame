using UnityEngine;
// The new Input System lives in this namespace. You need the com.unity.inputsystem
// package installed (Package Manager → Input System) for this line to compile.
using UnityEngine.InputSystem;

// Attach this to the Main Camera (the same GameObject CameraController is on) — the raycast
// needs to fire from wherever the player is actually looking, which is the camera, not the
// player body.
//
// Handles "look at and pick up" interaction: pressing E fires a ray forward from the camera,
// and if it hits an object with a Pickup component within range, collects it.
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    // How far, in meters, the player can reach to pick something up.
    public float interactionRange = 3f;

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.eKey.wasPressedThisFrame)
        {
            TryPickUp();
        }
    }

    // Fires a ray straight out from the camera. If it hits something with a Pickup
    // component within interactionRange, collects it.
    private void TryPickUp()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        // out RaycastHit hit gives us details about whatever the ray struck (if anything).
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange))
        {
            // GetComponent returns null if the object we hit isn't a Pickup — e.g. it's a wall
            // or the ground — so this safely does nothing in that case.
            Pickup pickup = hit.collider.GetComponent<Pickup>();
            if (pickup != null)
            {
                pickup.CollectItem();
            }
        }
    }
}
