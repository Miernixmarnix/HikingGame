using UnityEngine;

// Attach this to the Main Camera.
// Controls how far the camera renders by setting its far clip plane at runtime, and again
// live in the Editor whenever you tweak the value (via OnValidate).
//
// This is meant to work TOGETHER with URP's distance Fog (Window > Rendering > Lighting >
// Environment tab > Other Settings > Fog — see the setup notes for exact steps). Fog fades
// distant objects into the fog/sky color as they approach the fog's End distance; the far
// clip plane then removes them entirely a bit further out, once they're already fully
// hidden by fog. As long as renderDistance is set a little BEYOND the fog's End distance,
// the camera cutoff itself is never visible — everything has already faded to fog color
// before it's clipped.
[RequireComponent(typeof(Camera))]
public class CameraRenderDistance : MonoBehaviour
{
    [Tooltip("How far the camera renders, in meters. Keep this a bit larger than the fog's End distance so objects are fully faded before they're clipped.")]
    public float renderDistance = 120f;

    // Cached reference to the Camera component this script controls.
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        ApplyRenderDistance();
    }

    // OnValidate is called by the Unity Editor whenever a value changes in the Inspector —
    // including while the game isn't running — so dragging the renderDistance slider updates
    // the camera immediately without needing to press Play.
    private void OnValidate()
    {
        // Awake() may not have run yet if this is being edited outside Play mode,
        // so make sure we have the Camera reference before using it.
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        ApplyRenderDistance();
    }

    private void ApplyRenderDistance()
    {
        if (cam != null)
        {
            cam.farClipPlane = renderDistance;
        }
    }
}
