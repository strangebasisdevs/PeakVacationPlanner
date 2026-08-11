using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace VacationPlanner;

/// <summary>
/// VacationPlanner - Override biome selection in PEAK.
/// 
/// Players vote on biomes by interacting with the BiomeVoting Kiosk in the Airport.
/// The winning biome combination is applied when the expedition starts.
/// </summary>
[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    private readonly HarmonyLib.Harmony _harmony = new(Id);

    // Verbose MapBaker/MapHandler diagnostic dumps (MapBakerDebugPatch, MapHandlerDebugPatch).
    // Off by default so normal play isn't bogged down by log spam - flip on only when
    // investigating baked-level/biome structure.
    internal static bool DebugLoggingEnabled => _debugLogging?.Value ?? false;
    private static BepInEx.Configuration.ConfigEntry<bool>? _debugLogging;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");

        _debugLogging = Config.Bind(
            "Debug",
            "EnableVerboseLogging",
            false,
            "Enables verbose MapBaker/MapHandler diagnostic dumps used for development. Leave off for normal play.");

        // Apply Harmony patches
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        // Create persistent controller for input handling and network sync
        var controllerObj = new GameObject("VacationPlanner_Controller");
        controllerObj.AddComponent<BiomeController>();
        controllerObj.AddComponent<NetworkSync>();
        DontDestroyOnLoad(controllerObj);
        
        Log.LogInfo("BiomeController and NetworkSync initialized");
    }
}
