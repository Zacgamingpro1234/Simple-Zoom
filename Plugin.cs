using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace SimpleZoom;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    float DefaultFOV = 75;
    float CurrentFOV = 75;
    ConfigEntry<float> ZoomAmount;
    ConfigEntry<KeyCode> Keybind;
    internal static new ManualLogSource Logger;
        
    private void Awake()
    {
        // Plugin startup logic
        ZoomAmount = Config.Bind("General", "Zoom Amount", 20f, "The Amount To Zoom In");
        Keybind = Config.Bind("General", "Keybind", KeyCode.C, "The Keybind To Press To Zoom (Must Be A Valid Keycode)");
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
    
private Camera fpsCamera; // To store the Camera reference

    private void Update()
    {
        // Step 1: Search for the FPSCamera object dynamically
        if (fpsCamera == null){
            GameObject cameraObject = GameObject.Find("FPSCamera");
            if (cameraObject != null)
            {
                fpsCamera = cameraObject.GetComponent<Camera>();
                if (fpsCamera != null)
                {
                    Logger.LogInfo("FPSCamera and its Camera component successfully located!");
                    Logger.LogInfo($"Default FOV: {fpsCamera.fieldOfView}");
                    DefaultFOV = fpsCamera.fieldOfView;
                    CurrentFOV = fpsCamera.fieldOfView;
                }
                else
                {
                    Logger.LogWarning("FPSCamera found, but no Camera component attached.");
                }
            }
        }else{
            if (Input.GetKey(Keybind.Value)){
                if(CurrentFOV == DefaultFOV){
                CurrentFOV = ZoomAmount.Value;
                fpsCamera.fieldOfView = CurrentFOV;
                }else{
                    if(CurrentFOV == ZoomAmount.Value){
                    CurrentFOV = fpsCamera.fieldOfView;
                    }
                    if(CurrentFOV != ZoomAmount.Value){
                    DefaultFOV = fpsCamera.fieldOfView;
                    }
                }
            }else{
                if(CurrentFOV != DefaultFOV){
                fpsCamera.fieldOfView = DefaultFOV;
                CurrentFOV = DefaultFOV;
                }else{
                    if(CurrentFOV != fpsCamera.fieldOfView){
                    DefaultFOV = fpsCamera.fieldOfView;
                    }
                }
            }
        }
    }
}                