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

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"Plugin {Name} is loaded!");

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
