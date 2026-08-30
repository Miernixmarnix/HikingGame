using UnityEngine;
// The new Input System lives in this namespace. You need the com.unity.inputsystem
// package installed (Package Manager → Input System) for this line to compile.
using UnityEngine.InputSystem;

// Attach this script to the Main Camera, once the camera has been made a CHILD of the
// player GameObject (see the setup notes you were given alongside this script).
public class CameraController : MonoBehaviour
{
    [Header("Mouse Look")]
    // How fast the camera/player turn in response to mouse movement.
    // Tune this in the Inspector to taste.
    public float mouseSensitivity = 0.15f;

    // Clamp so the player can't flip the camera all the way over their head or under their feet.
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Head Bob")]
    // How fast the head-bob sine wave cycles. Higher = faster bobbing.
    public float bobFrequency = 8f;

    // How far up/down the camera moves while bobbing, in meters.
    public float bobAmplitude = 0.05f;

    // How quickly the camera eases back to its resting height when you stop moving/land.
    public float bobReturnSpeed = 6f;

    [Header("Sprint Head Bob")]
    // While sprinting, bobFrequency/bobAmplitude are multiplied by these to make
    // running feel more intense than walking.
    public float sprintBobFrequencyMultiplier = 1.4f;
    public float sprintBobAmplitudeMultiplier = 1.6f;

    // How quickly the bob intensity eases between its walk and sprint values.
    // Higher = snappier transition, lower = more gradual.
    public float bobIntensitySmoothSpeed = 4f;

    [Header("Player Reference")]
    // Drag the Player GameObject here in the Inspector. If left empty, this script
    // will assume the camera is a direct child of the player and use transform.parent instead.
    public GameObject player;

    // Cached references so we don't call GetComponent every frame.
    private Transform playerTransform;
    private Rigidbody playerRigidbody;
    private PlayerMovement playerMovement;

    // How far the camera has turned up/down so far, in degrees.
    // We track this separately from the Transform because we need to clamp it.
    private float pitch;

    // The camera's resting local position (before any head-bob offset is added).
    // Set this by positioning the camera at "eye level" in the Inspector before pressing Play.
    private Vector3 baseLocalPosition;

    // Tracks progress through the head-bob sine wave.
    private float bobTimer;

    // The bob frequency/amplitude actually in use this frame, eased toward the walk or
    // sprint target rather than snapping instantly (see HandleHeadBob).
    private float currentBobFrequency;
    private float currentBobAmplitude;

    // Below this speed we treat the player as "not moving" for head-bob purposes,
    // so tiny physics jitter while standing still doesn't cause a phantom bob.
    private const float MoveSpeedThreshold = 0.1f;

    // Tracks whether WE want the cursor locked right now (as opposed to Unity's actual
    // Cursor.lockState, which the OS/editor can silently reset out from under us — e.g. on
    // alt-tab, or when the Game view loses focus). This is the source of truth we restore from.
    private bool wantsCursorLocked = true;

    private void Awake()
    {
        // Resolve the player reference: use the assigned field, or fall back to our parent.
        playerTransform = player != null ? player.transform : transform.parent;

        if (playerTransform == null)
        {
            Debug.LogError("CameraController needs a Player reference (either assign the 'player' field, or parent this camera under the player GameObject).");
            return;
        }

        playerRigidbody = playerTransform.GetComponent<Rigidbody>();
        playerMovement = playerTransform.GetComponent<PlayerMovement>();

        // Remember where the camera starts — this is "eye level", and head bob will
        // oscillate around this point rather than permanently drifting away from it.
        baseLocalPosition = transform.localPosition;

        // Start the eased bob intensity at the walk values so there's no pop on the first frame.
        currentBobFrequency = bobFrequency;
        currentBobAmplitude = bobAmplitude;

        // Hide and lock the cursor to the center of the screen as soon as the game starts,
        // so mouse movement can be used purely for looking around.
        LockCursor();
    }

    private void Update()
    {
        HandleCursorLock();

        // Only rotate the camera from mouse movement while the cursor is actually locked —
        // otherwise, moving the mouse to click a UI element or the editor would also spin the view.
        if (wantsCursorLocked)
        {
            HandleMouseLook();
        }

        HandleHeadBob();
    }

    // --- Cursor lock: Escape releases the cursor, clicking back into the game re-locks it ---
    private void HandleCursorLock()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }

        // While unlocked, a left click means "the player clicked back into the game window" —
        // re-lock and re-hide the cursor so play can continue.
        Mouse mouse = Mouse.current;
        if (!wantsCursorLocked && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    // Locks the cursor to the center of the screen and hides it.
    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        wantsCursorLocked = true;
    }

    // Releases the cursor so it can move freely and click on the editor/UI again.
    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        wantsCursorLocked = false;
    }

    // Unity (and the OS) can silently force the cursor to unlock when the game window/Game view
    // loses focus (e.g. alt-tabbing, clicking another editor panel). When we regain focus, if the
    // player hadn't deliberately pressed Escape, re-apply the lock rather than leaving it stuck free.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && wantsCursorLocked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // --- Mouse look: yaw rotates the player, pitch rotates only the camera ---
    private void HandleMouseLook()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || playerTransform == null) return;

        // Mouse.current.delta gives how far the mouse moved since the last frame, in pixels.
        // This is already a raw distance, NOT a speed — so we must NOT multiply it by
        // Time.deltaTime. Doing so would make a fast, short mouse flick (small deltaTime)
        // rotate less than the same physical movement made slowly (large deltaTime), which
        // is exactly the "inconsistent sensitivity" feel. Using delta as-is means a given
        // physical mouse movement always produces the same rotation, regardless of frame rate.
        Vector2 delta = mouse.delta.ReadValue();

        // Yaw (left/right look) rotates the PLAYER around the world Y axis, so movement
        // keys (which are relative to the player's facing) turn together with the camera.
        float yaw = delta.x * mouseSensitivity;
        playerTransform.Rotate(Vector3.up, yaw, Space.World);

        // Pitch (up/down look) rotates only the CAMERA, around its local X axis.
        // We subtract because moving the mouse up (positive Y delta) should look up (negative pitch).
        pitch -= delta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Vector3 localAngles = transform.localEulerAngles;
        localAngles.x = pitch;
        localAngles.z = 0f;
        transform.localEulerAngles = localAngles;
    }

    // --- Head bob: bob the camera while walking on the ground, otherwise ease back to rest ---
    private void HandleHeadBob()
    {
        bool isMoving = false;
        bool isGrounded = false;

        if (playerRigidbody != null)
        {
            // Only look at horizontal speed — vertical (jump/fall) speed shouldn't trigger walking bob.
            Vector3 horizontalVelocity = new Vector3(playerRigidbody.linearVelocity.x, 0f, playerRigidbody.linearVelocity.z);
            isMoving = horizontalVelocity.magnitude > MoveSpeedThreshold;
        }

        bool isSprinting = false;
        if (playerMovement != null)
        {
            isGrounded = playerMovement.IsGrounded;
            isSprinting = playerMovement.IsSprinting;
        }

        // Pick the target frequency/amplitude for this frame — bigger while sprinting —
        // then smoothly ease our current values toward it instead of snapping, so speeding
        // up/slowing down doesn't cause a jarring jump in the bob motion.
        float targetFrequency = isSprinting ? bobFrequency * sprintBobFrequencyMultiplier : bobFrequency;
        float targetAmplitude = isSprinting ? bobAmplitude * sprintBobAmplitudeMultiplier : bobAmplitude;
        currentBobFrequency = Mathf.Lerp(currentBobFrequency, targetFrequency, Time.deltaTime * bobIntensitySmoothSpeed);
        currentBobAmplitude = Mathf.Lerp(currentBobAmplitude, targetAmplitude, Time.deltaTime * bobIntensitySmoothSpeed);

        if (isMoving && isGrounded)
        {
            // Advance the sine wave over time and use it to offset the camera's height.
            bobTimer += Time.deltaTime * currentBobFrequency;
            float bobOffset = Mathf.Sin(bobTimer) * currentBobAmplitude;

            Vector3 bobbedPosition = baseLocalPosition;
            bobbedPosition.y += bobOffset;
            transform.localPosition = bobbedPosition;
        }
        else
        {
            // Reset the wave so the next time you start walking, the bob starts smoothly from zero.
            bobTimer = 0f;

            // Smoothly glide back to the resting eye-level position instead of snapping instantly.
            transform.localPosition = Vector3.Lerp(transform.localPosition, baseLocalPosition, Time.deltaTime * bobReturnSpeed);
        }
    }
}
