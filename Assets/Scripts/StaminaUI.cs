using UnityEngine;
using UnityEngine.UI;

// Attach this to any GameObject under your Canvas (an empty "StaminaUI" object works well).
// It reads the player's current stamina every frame and updates a UI Image's fill amount
// to visually represent it as a shrinking/growing bar.
public class StaminaUI : MonoBehaviour
{
    [Header("References")]
    // Drag the Player GameObject here so this script can read PlayerMovement.CurrentStamina.
    // If left empty, Awake() will try to find one automatically and log a warning either way —
    // so a lost/forgotten reference shows up immediately in the Console instead of the bar
    // just silently doing nothing.
    public PlayerMovement player;

    // Drag the stamina bar's FILL Image here — the child Image whose Image Type is set to
    // "Filled" (NOT the background Image). This is the one whose fillAmount we control.
    public Image staminaFillImage;

    private void Awake()
    {
        // Fall back to searching the scene if the reference wasn't wired up in the Inspector.
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerMovement>();
            Debug.LogWarning("StaminaUI: 'player' was not assigned in the Inspector — found one automatically. Drag the Player object into the field to avoid relying on this.", this);
        }

        if (staminaFillImage == null)
        {
            Debug.LogError("StaminaUI: 'staminaFillImage' is not assigned — drag the stamina bar's fill Image into the field in the Inspector.", this);
        }
    }

    private void Update()
    {
        if (player == null || staminaFillImage == null) return;

        // Image.fillAmount expects a 0–1 value. Dividing current stamina by max stamina
        // gives exactly that: 1 = full bar, 0 = empty bar.
        staminaFillImage.fillAmount = player.CurrentStamina / player.maxStamina;
    }
}
