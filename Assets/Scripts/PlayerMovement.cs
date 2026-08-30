using UnityEngine;
// The new Input System lives in this namespace. You need the com.unity.inputsystem
// package installed (Package Manager → Input System) for this line to compile.
using UnityEngine.InputSystem;

// Attach this script to your player GameObject.
// Also make sure the GameObject has a Rigidbody component added to it.
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    // Normal walking speed, in meters per second.
    public float walkSpeed = 5f;

    // Speed while sprinting (Left Shift held), in meters per second.
    public float sprintSpeed = 9f;

    [Header("Jump")]
    // How much upward speed is applied the instant the player jumps.
    // Bigger number = higher jump.
    public float jumpForce = 5f;

    // Extra distance (in meters) the ground ray checks BELOW the collider's actual bottom.
    // Kept small — it's just a safety margin, not the full ray length. The full ray length
    // is calculated from the player's real collider size every frame (see FixedUpdate), so
    // resizing/changing the collider (e.g. swapping a Box Collider for a Capsule Collider,
    // or changing player height) can't silently break the ground check the way a hardcoded
    // ray length can.
    public float groundCheckBuffer = 0.15f;

    [Header("Stamina")]
    // The maximum amount of stamina the player can have.
    public float maxStamina = 100f;

    // How much stamina drains per second while actively sprinting.
    public float staminaDrainRate = 25f;

    // How much stamina regenerates per second while NOT sprinting.
    public float staminaRegenRate = 15f;

    // Once stamina hits 0, sprinting is locked out until it regenerates back above
    // this amount — this stops the player from "flickering" between sprint/walk
    // the instant stamina ticks up from exactly 0.
    public float staminaResumeThreshold = 10f;

    // Current stamina, readable by other scripts (e.g. StaminaUI) but only changeable from here.
    public float CurrentStamina { get; private set; }

    // True on any physics step where the player is actually moving at sprint speed.
    // CameraController reads this to intensify head bob while sprinting.
    public bool IsSprinting { get; private set; }

    // True once stamina has been fully drained, until it regenerates past staminaResumeThreshold.
    private bool isExhausted;

    // True when the ground raycast hit something this physics step.
    // Read this from other scripts (e.g. CameraController) if you need to know if the player is grounded.
    public bool IsGrounded { get; private set; }

    // We cache the Rigidbody reference here so we don't look it up every frame.
    private Rigidbody rb;

    // Cached reference to whatever Collider is on this GameObject (Box, Capsule, etc.).
    // Used to measure the player's actual height for the ground check — see FixedUpdate.
    private Collider col;

    // Set to true the moment Space is pressed, then consumed (and reset) in FixedUpdate.
    // We detect the press in Update because Input System "was pressed this frame" checks
    // can be missed if we only ever look for them inside FixedUpdate.
    private bool jumpRequested;

    // Awake is called once when the script first loads, before the game starts.
    // It's the right place to grab references to other components on the same GameObject.
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Warn the developer if no Rigidbody was found, since movement won't work without it.
        if (rb == null)
        {
            Debug.LogError("PlayerMovement requires a Rigidbody component on the same GameObject.");
        }

        // Warn the developer if no Collider was found, since the ground check needs one to
        // measure the player's height.
        if (col == null)
        {
            Debug.LogError("PlayerMovement requires a Collider (Box, Capsule, etc.) on the same GameObject.");
        }

        // Start at full stamina.
        CurrentStamina = maxStamina;
    }

    // Update runs once per rendered frame. We only use it to catch the jump key press
    // reliably (see the comment on jumpRequested above).
    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame)
        {
            jumpRequested = true;
        }
    }

    // FixedUpdate runs on a fixed physics timestep (default: 50 times per second).
    // Always move Rigidbodies here — not in Update — so physics stays stable.
    private void FixedUpdate()
    {
        // --- Ground check ---
        // Fire a ray straight down from the player's position. The ray needs to travel at
        // least as far as the distance from the player's center to the BOTTOM of their
        // collider, plus a small buffer, or it will never reach the ground.
        //
        // We read col.bounds.extents.y (half the collider's current world-space height)
        // every step instead of using a fixed number, so this keeps working correctly if the
        // collider is ever resized or swapped for a different shape.
        float distanceToColliderBottom = col != null ? col.bounds.extents.y : 0.5f;
        float groundCheckDistance = distanceToColliderBottom + groundCheckBuffer;
        IsGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);

        // --- Read movement input ---
        // Keyboard.current gives you the currently active keyboard device.
        // It returns null if no keyboard is connected, so we bail out early to avoid a crash.
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Each key on Keyboard.current exposes a .isPressed bool — true as long as the key is held.
        // We convert each held key into a +1 or -1 contribution using the ternary-like if statements.
        //
        // Horizontal axis: D or RightArrow = +1 (strafe right), A or LeftArrow = -1 (strafe left)
        float horizontal = 0f;
        if (kb.dKey.isPressed        || kb.rightArrowKey.isPressed) horizontal += 1f;
        if (kb.aKey.isPressed        || kb.leftArrowKey.isPressed)  horizontal -= 1f;

        // Vertical axis: W or UpArrow = +1 (move forward), S or DownArrow = -1 (move backward)
        float vertical = 0f;
        if (kb.wKey.isPressed        || kb.upArrowKey.isPressed)    vertical += 1f;
        if (kb.sKey.isPressed        || kb.downArrowKey.isPressed)  vertical -= 1f;

        // --- Build movement relative to where the player is facing ---
        // transform.forward / transform.right point along the player's own local axes (in world space),
        // so "forward" always means "the way the player is looking", not world +Z.
        Vector3 moveInput = transform.right * horizontal + transform.forward * vertical;

        // Flatten out any Y component that could sneak in if the player is tilted, then clamp
        // the magnitude to 1 so diagonal movement (e.g. W+D) isn't faster than axis-aligned movement.
        moveInput.y = 0f;
        moveInput = Vector3.ClampMagnitude(moveInput, 1f);

        // True as long as there's any movement input at all, regardless of speed.
        bool isMoving = moveInput.sqrMagnitude > 0.0001f;

        // --- Sprint + stamina ---
        // Left Shift held, actually moving, and not currently locked out from exhaustion.
        bool shiftHeld = kb.leftShiftKey.isPressed;
        bool wantsToSprint = shiftHeld && isMoving && !isExhausted;
        IsSprinting = wantsToSprint;

        if (wantsToSprint)
        {
            // Only drain stamina while genuinely sprinting (moving + shift + not exhausted).
            CurrentStamina -= staminaDrainRate * Time.fixedDeltaTime;

            if (CurrentStamina <= 0f)
            {
                // Ran out mid-step: clamp to zero, lock out sprinting, and fall back to walk speed
                // immediately rather than waiting a frame.
                CurrentStamina = 0f;
                isExhausted = true;
                IsSprinting = false;
            }
        }
        else
        {
            // Regenerate whenever we're not actively sprinting — includes standing still,
            // walking, or being exhausted.
            CurrentStamina += staminaRegenRate * Time.fixedDeltaTime;
            if (CurrentStamina > maxStamina) CurrentStamina = maxStamina;
        }

        // Lift the exhaustion lockout once stamina has recovered past the threshold.
        if (isExhausted && CurrentStamina >= staminaResumeThreshold)
        {
            isExhausted = false;
        }

        // --- Apply movement ---
        float currentSpeed = IsSprinting ? sprintSpeed : walkSpeed;
        Vector3 velocity = moveInput * currentSpeed;

        // Apply movement by setting the Rigidbody's velocity directly.
        // We preserve the current Y velocity so gravity (and jumping) still work normally.
        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);

        // --- Jump ---
        // Only jump if a press was recorded since the last physics step AND the player is grounded.
        if (jumpRequested && IsGrounded)
        {
            // ForceMode.Impulse applies an instant change in velocity, ignoring mass-over-time —
            // exactly what you want for a snappy jump.
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        // Always clear the request after this physics step, whether or not the jump happened,
        // so holding Space doesn't cause repeated jumps every step you're grounded.
        jumpRequested = false;
    }
}
