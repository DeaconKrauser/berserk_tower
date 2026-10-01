using UnityEngine;
using UnityEngine.Rendering.Universal;

// Main camera: pixel-perfect setup, light screen shake (respects the option), small boss emphasis pan.
public class CameraRig : MonoBehaviour
{
    public static CameraRig I;
    public static bool ShakeEnabled = true;

    Camera cam;
    Vector3 home = new(0, 0, -10), focusOffset;
    float trauma, focusLeft, focusSeconds;
    Vector2 focus;

    public static CameraRig Setup()
    {
        var cam = Camera.main;
        if (!cam)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor = new Color32(11, 10, 13, 255);
        var ppc = cam.GetComponent<PixelPerfectCamera>();
        if (!ppc) ppc = cam.gameObject.AddComponent<PixelPerfectCamera>();
        ppc.assetsPPU = Gfx.PPU;
        ppc.refResolutionX = 960;
        ppc.refResolutionY = 540;
        ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
        ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;
        var rig = cam.GetComponent<CameraRig>();
        if (!rig) rig = cam.gameObject.AddComponent<CameraRig>();
        rig.cam = cam;
        return I = rig;
    }

    public static void Shake(float amount)
    {
        if (I && ShakeEnabled) I.trauma = Mathf.Min(0.6f, I.trauma + amount);
    }

    // Short pan towards a point (boss entrance) that returns by itself; never blocks input.
    public static void Emphasize(Vector2 at, float seconds)
    {
        if (!I) return;
        I.focus = at;
        I.focusLeft = I.focusSeconds = seconds;
    }

    public static void ResetView()
    {
        if (!I) return;
        I.trauma = 0;
        I.focusLeft = 0;
        I.transform.position = I.home;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        trauma = Mathf.Max(0, trauma - dt * 1.4f);
        Vector3 offset = Vector3.zero;
        if (trauma > 0)
        {
            float s = trauma * trauma * 0.35f;
            float t = Time.unscaledTime * 40;
            offset = new Vector3((Mathf.PerlinNoise(t, 1) - 0.5f) * 2 * s, (Mathf.PerlinNoise(1, t) - 0.5f) * 2 * s, 0);
        }
        if (focusLeft > 0)
        {
            focusLeft -= dt;
            float k = Mathf.Sin(Mathf.Clamp01(1 - focusLeft / focusSeconds) * Mathf.PI);   // in and back out
            var target = Vector3.ClampMagnitude((Vector3)(focus * 0.18f), 1.6f);
            focusOffset = target * k;
        }
        else focusOffset = Vector3.zero;
        transform.position = home + offset + focusOffset;
    }
}
