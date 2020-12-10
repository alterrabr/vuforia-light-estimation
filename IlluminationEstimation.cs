/* TODO: Make light estimation asynchronous.
 * Need some testing on mobile and code refactor.
 * Planing to add color temperature estimation https://pdfs.semanticscholar.org/c079/6dd6c790bf6905414d0f265c3d950eed0c2b.pdf */

using UnityEngine;
using Vuforia;

public class IlluminationEstimation : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Light component to change intensity")]
    private Light ambientLight;

    [SerializeField]
    [Tooltip("Max percentage of light change according to scene light")]
    [Range(0f, 1f)] 
    private float ambientLightWeight = 1f;

    [SerializeField]
    [Tooltip("Inverse speed of light changing. Bigger - more smooth, but slower.")]
    [Range(0f, 1f)] 
    private float damping = .6f;

    [SerializeField]
    [Tooltip("Number of frames to skip between lights estimations.")]
    private int skipFrames = 5;

    // Default pixel format, guessing its raw image
    private PIXEL_FORMAT pixelFormat = PIXEL_FORMAT.UNKNOWN_FORMAT;

    private bool canGetCameraImage = false;
    private bool pixelFormatSetted = false;

    // Store image pixels
    private byte[] pixels;

    private float initialIllumination;
    private float countedIllumination;
    private double totalIllumination;

    private int frameCount = 0;

    private void Start()
    {
        // Get Light intensity
        initialIllumination = ambientLight.intensity;
        countedIllumination = initialIllumination;

        #if UNITY_EDITOR
            pixelFormat = PIXEL_FORMAT.GRAYSCALE;
        #else
            pixelFormat = PIXEL_FORMAT.RGB888;
        #endif

        // Register Vuforia callbacks
        VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStarted);
        VuforiaARController.Instance.RegisterTrackablesUpdatedCallback(OnTrackablesUpdated);
        VuforiaARController.Instance.RegisterOnPauseCallback(OnPause);
    }

    /// <summary>
    /// Called after Vuforia camera and trackers init.
    /// </summary>
    private void OnVuforiaStarted()
    {
        // Try register camera image format
        if (CameraDevice.Instance.SetFrameFormat(pixelFormat, true))
        {
            pixelFormatSetted = true;

            if (CameraDevice.Instance.GetCameraImage(pixelFormat) != null)
            {
                canGetCameraImage = true;
            }
        }
        else
        {
            pixelFormatSetted = false;
        }
    }

    /// <summary>
    /// Called each time the Vuforia state is updated.
    /// </summary>
    void OnTrackablesUpdated()
    { 
        if ((pixelFormatSetted) && (canGetCameraImage))
        {
            if (frameCount <= 0)
            {
                frameCount = skipFrames;

                Vuforia.Image image = CameraDevice.Instance.GetCameraImage(pixelFormat);

                if (image != null)
                {
                    pixels = image.Pixels;
                    totalIllumination = 0.0;

                    if (pixels != null && pixels.Length > 0)
                    {
                        for (int p = 0; p < pixels.Length; p += 64)
                        {
                            // Get pixel's Y (luminance) component of YIQ color scheme and add it to total image illumination counter
                            // More about conversion here https://en.wikipedia.org/wiki/YIQ
                            totalIllumination += pixels[p] * 0.299 + pixels[p + 1] * 0.587 + pixels[p + 2] * 0.114;
                        }

                        totalIllumination /= pixels.Length / 16;
                        totalIllumination /= 255.0;
                        countedIllumination = (float)((8 * totalIllumination) * ambientLightWeight) + initialIllumination * (1 - ambientLightWeight);

                        if (countedIllumination <= 0f)
                        {
                            countedIllumination = 0f;
                        }
                    }
                }
            }

            countedIllumination = ambientLight.intensity * damping + countedIllumination * (1 - damping);
            ambientLight.intensity = countedIllumination;

            frameCount--;
        }
    }

    // Called when app is paused / resumed
    void OnPause(bool paused)
    {
        if (paused)
        {
            UnregisterFormat();
        }
        else
        {
            RegisterFormat();
        }
    }

    // Register the camera pixel format
    void RegisterFormat()
    {
        if (CameraDevice.Instance.SetFrameFormat(pixelFormat, true))
        {
            pixelFormatSetted = true;
        }
        else
        {
            pixelFormatSetted = false;
        }
    }

    // Unregister the camera pixel format (e.g. call this when app is paused)
    void UnregisterFormat()
    {
        CameraDevice.Instance.SetFrameFormat(pixelFormat, false);
        pixelFormatSetted = false;
    }
}
