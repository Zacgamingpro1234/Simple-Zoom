using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using System.Reflection;
using System.Diagnostics;

namespace SimpleZoom;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    private ConfigEntry<float> ZoomAmount;
    private ConfigEntry<KeyCode> Keybind;
    private ConfigEntry<bool> ToggleMode; // Config entry for toggle/hold mode
    private ConfigEntry<float> ZoomSpeed; // Config entry for zoom animation speed
    private ConfigEntry<bool> EnableAnimation; // Config entry to enable/disable animation
    private ConfigEntry<float> ScrollSensitivity; // Config entry for scroll wheel sensitivity
    private ConfigEntry<float> zoomMinCap;
    private ConfigEntry<float> zoomMaxCap;
    internal static new ManualLogSource Logger;

    private Camera fpsCamera; // To store the Camera reference
    private bool isZoomed = false;
    private bool isInitialized = false; // Track whether the plugin is fully initialized
    private float currentFOV = 75f; // Store the current FOV dynamically
    private float targetFOV; // Target FOV for smooth zoom animation
    private float zoomVelocity; // Velocity for SmoothDamp
    private Coroutine fovMonitorCoroutine; // Reference to the FOV monitoring coroutine
    private bool isGamePaused = false; // Track whether the game is paused

    private void Awake()
    {
        // Plugin startup logic
        ZoomAmount = Config.Bind("General", "Zoom Amount", 20f, "The Amount To Zoom In");
        Keybind = Config.Bind("General", "Keybind", KeyCode.C, "The Keybind To Press To Zoom (Must Be A Valid Keycode)");
        ToggleMode = Config.Bind("General", "Toggle Mode", false, "Whether zoom should be toggled (true) or held (false)");
        ZoomSpeed = Config.Bind("General", "Zoom Speed", .08f, "The speed of the zoom animation (In Seconds)");
        EnableAnimation = Config.Bind("General", "Enable Animation", true, "Whether zoom animation should be enabled (true) or instant (false)");
        ScrollSensitivity = Config.Bind("General", "Scroll Sensitivity", 5f, "The sensitivity of the scroll wheel zoom (higher values mean faster zoom)");
        zoomMinCap = Config.Bind("General", "Minimum FOV Zoom Cap", .2f, "The minimum FOV to zoom in");
        zoomMaxCap = Config.Bind("General", "Maximum FOV Zoom Cap", 130f, "The maximum FOV to zoom out");
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Initialize the FOV monitoring system
        InitializeFOVMonitoring();
    }

    private void InitializeFOVMonitoring()
    {
        // Start monitoring the game's pause state
        StartCoroutine(MonitorPauseState());
    }

    private System.Collections.IEnumerator MonitorPauseState()
    {
        while (true)
        {
            // Check if the game is paused (e.g., by checking Time.timeScale)
            bool newPauseState = Time.timeScale == 0;

            // If the pause state has changed
            if (newPauseState != isGamePaused)
            {
                isGamePaused = newPauseState;

                if (isGamePaused)
                {
                    // Start the FOV monitoring coroutine when the game is paused
                    fovMonitorCoroutine = StartCoroutine(MonitorFOVFromSettings());
                }
                else
                {
                    // Stop the FOV monitoring coroutine when the game is unpaused
                    if (fovMonitorCoroutine != null)
                    {
                        StopCoroutine(fovMonitorCoroutine);
                        fovMonitorCoroutine = null;
                    }
                }
            }

            // Wait for the next frame
            yield return null;
        }
    }

    private System.Collections.IEnumerator MonitorFOVFromSettings()
    {
        string lastFOVText = ""; // Store the last retrieved FOV text
        Stopwatch stopwatch = new Stopwatch(); // Use a Stopwatch for precise timing
        stopwatch.Start();

        while (true)
        {
            // Check the FOV value every 0.5 seconds (500 milliseconds)
            if (stopwatch.ElapsedMilliseconds >= 500)
            {
                stopwatch.Reset(); // Reset the timer
                stopwatch.Start(); // Start it again immediately

                GameObject fovLabelObject = GameObject.Find("FOVValueLabel");
                if (fovLabelObject != null)
                {
                    // Use reflection to get the TextMeshProUGUI component
                    Component textMeshProComponent = fovLabelObject.GetComponent("TextMeshProUGUI");
                    if (textMeshProComponent != null)
                    {
                        // Use reflection to get the m_text field
                        FieldInfo textField = textMeshProComponent.GetType().GetField("m_text", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (textField != null)
                        {
                            string fovText = (string)textField.GetValue(textMeshProComponent);

                            // Check if the FOV text has changed
                            if (fovText != lastFOVText && float.TryParse(fovText, out float settingsFOV))
                            {
                                // Update the current FOV value
                                currentFOV = settingsFOV;
                                lastFOVText = fovText; // Update the last retrieved FOV text
                                Logger.LogInfo($"FOV updated from settings: {currentFOV}");

                                // If the camera is already found, update its FOV
                                if (fpsCamera != null)
                                {
                                    targetFOV = currentFOV; // Update the target FOV for smooth animation
                                    if (!EnableAnimation.Value)
                                    {
                                        fpsCamera.fieldOfView = targetFOV; // Instant update if animation is disabled
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Yield until the next frame
            yield return null;
        }
    }

    private void Update()
    {
        // Find the camera if not already found
        if (fpsCamera == null)
        {
            isInitialized = false; // Ensure the plugin is not initialized if the camera is not found
            GameObject cameraObject = GameObject.Find("FPSCamera");
            if (cameraObject != null)
            {
                fpsCamera = cameraObject.GetComponent<Camera>();
                if (fpsCamera != null)
                {
                    Logger.LogInfo("FPSCamera and its Camera component successfully located!");

                    // Initialize currentFOV to the camera's current FOV if it hasn't been set yet
                    if (currentFOV == 75f) // 75f is the fallback default value
                    {
                        currentFOV = fpsCamera.fieldOfView;
                        Logger.LogInfo($"Initialized currentFOV to camera's FOV: {currentFOV}");
                    }

                    // Set the camera's FOV to currentFOV
                    fpsCamera.fieldOfView = currentFOV;
                    targetFOV = currentFOV; // Initialize targetFOV
                    isInitialized = true; // Mark the plugin as initialized
                }
                else
                {
                    Logger.LogWarning("FPSCamera found, but no Camera component attached.");
                }
            }
            return;
        }

        // Ensure the plugin is initialized before proceeding
        if (!isInitialized)
            return;

        // Handle zoom logic based on toggle/hold mode
        if (ToggleMode.Value)
        {
            // Toggle mode: Press the key to zoom in, press again to zoom out
            if (Input.GetKeyDown(Keybind.Value))
            {
                if (!isZoomed)
                {
                    targetFOV = ZoomAmount.Value; // Set target FOV for zoom in
                    isZoomed = true;
                }
                else
                {
                    targetFOV = currentFOV; // Set target FOV for zoom out
                    isZoomed = false;
                }
            }
        }
        else
        {
            // Hold mode: Hold the key to zoom in, release to zoom out
            if (Input.GetKey(Keybind.Value))
            {
                if (!isZoomed)
                {
                    targetFOV = ZoomAmount.Value; // Set target FOV for zoom in
                    isZoomed = true;
                }
            }
            else
            {
                if (isZoomed)
                {
                    targetFOV = currentFOV; // Set target FOV for zoom out
                    isZoomed = false;
                }
            }
        }


        if (Input.GetKey(Keybind.Value))
        {
            // Handle scroll wheel zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                float fovChange = scroll * ScrollSensitivity.Value * targetFOV * 0.4f;
                targetFOV -= fovChange;
            }
        }

        targetFOV = Mathf.Clamp(targetFOV, zoomMinCap.Value, zoomMaxCap.Value);
        // Handle FOV updates based on animation setting
        if (EnableAnimation.Value)
        {
            // Smoothly interpolate the camera's FOV towards the target FOV using SmoothDamp
            fpsCamera.fieldOfView = Mathf.SmoothDamp(fpsCamera.fieldOfView, targetFOV, ref zoomVelocity, ZoomSpeed.Value);
        }
        else
        {
            // Instant update if animation is disabled
            fpsCamera.fieldOfView = targetFOV;
        }
    }
}