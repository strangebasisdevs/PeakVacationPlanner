using HarmonyLib;
using Photon.Pun;

namespace ChoiceEnhanced.Patches;

/// <summary>
/// Patches game start to log the state of biome selection.
/// </summary>
[HarmonyPatch]
public static class GameStartPatch
{
    /// <summary>
    /// Patch AirportCheckInKiosk.LoadIslandMaster - called on master client when starting game.
    /// </summary>
    [HarmonyPatch(typeof(AirportCheckInKiosk), "LoadIslandMaster")]
    [HarmonyPrefix]
    public static void LoadIslandMaster_Prefix()
    {
        Plugin.Log.LogInfo("[GameStartPatch] LoadIslandMaster starting...");
        
        // Just log the current state. The overrides should already be set by NetworkSync.
        Plugin.Log.LogInfo($"[GameStartPatch] Overrides active: B2={LevelOverridePatch.DesiredBiome2 ?? "Default"}, B3={LevelOverridePatch.DesiredBiome3 ?? "Default"}");
    }
}
