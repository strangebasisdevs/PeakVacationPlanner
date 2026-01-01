using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace ChoiceEnhanced;

/// <summary>
/// ChoiceEnhanced - Override biome selection in PEAK.
/// 
/// Controls:
/// - Numpad 1: Force Tropics
/// - Numpad 2: Force Roots
/// - Numpad 4: Force Alpine
/// - Numpad 5: Force Mesa
/// - Numpad 0: Clear all overrides
/// - Numpad Enter: Log current selection
/// </summary>
[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    private readonly HarmonyLib.Harmony _harmony = new(Id);

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");

        // Apply Harmony patches
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        // Create persistent controller for input handling and network sync
        var controllerObj = new GameObject("ChoiceEnhanced_Controller");
        controllerObj.AddComponent<BiomeController>();
        controllerObj.AddComponent<NetworkSync>();
        DontDestroyOnLoad(controllerObj);
        
        Log.LogInfo("BiomeController and NetworkSync initialized");
    }
}
