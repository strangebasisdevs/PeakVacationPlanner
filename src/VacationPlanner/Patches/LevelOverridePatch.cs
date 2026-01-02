using System.Collections.Generic;
using HarmonyLib;
using Zorro.Core;

namespace VacationPlanner.Patches;

/// <summary>
/// Overrides level selection to get desired biome combinations.
/// Since each level is pre-baked with specific biomes, we redirect to a level with the desired biomes.
/// 
/// Biome IDs:
/// - STMV = Shore + Tropics + Mesa + Volcano
/// - STAV = Shore + Tropics + Alpine + Volcano  
/// - SRMV = Shore + Roots + Mesa + Volcano
/// - SRAV = Shore + Roots + Alpine + Volcano
/// </summary>
[HarmonyPatch(typeof(MapBaker))]
public static class LevelOverridePatch
{
    // Biome combination to level indices mapping (from debug dump)
    private static readonly Dictionary<string, int[]> BiomeToLevels = new()
    {
        { "STMV", new[] { 0, 3, 5, 8, 11, 14, 17 } },  // Tropics + Mesa
        { "STAV", new[] { 1, 9, 13, 16, 20 } },        // Tropics + Alpine
        { "SRMV", new[] { 2, 7, 10, 18 } },            // Roots + Mesa
        { "SRAV", new[] { 4, 6, 12, 15, 19 } },        // Roots + Alpine
    };

    // Set these to override - null means use default
    // "T" for Tropics, "R" for Roots
    public static string? DesiredBiome2 { get; set; } = null;
    // "A" for Alpine, "M" for Mesa
    public static string? DesiredBiome3 { get; set; } = null;

    /// <summary>
    /// Clears all biome overrides.
    /// </summary>
    public static void ClearOverrides()
    {
        DesiredBiome2 = null;
        DesiredBiome3 = null;
        Plugin.Log.LogInfo("Biome overrides cleared");
    }

    /// <summary>
    /// Gets a human-readable summary of current selections.
    /// </summary>
    public static string GetSelectionSummary()
    {
        string b2 = DesiredBiome2 switch
        {
            "T" => "Tropics",
            "R" => "Roots",
            _ => "Default"
        };
        string b3 = DesiredBiome3 switch
        {
            "A" => "Alpine",
            "M" => "Mesa",
            _ => "Default"
        };
        return $"Biome2: {b2}, Biome3: {b3}";
    }

    [HarmonyPatch(nameof(MapBaker.GetLevel))]
    [HarmonyPrefix]
    public static void GetLevel_Prefix(MapBaker __instance, ref int levelIndex)
    {
        if (DesiredBiome2 == null && DesiredBiome3 == null)
            return; // No override requested

        int originalIndex = levelIndex;

        // Get the current biome ID for this level
        string currentBiomeId = GetBiomeIdForLevel(__instance, levelIndex);
        
        // Determine target biome characters
        string biome2 = DesiredBiome2?.ToUpper() ?? (currentBiomeId.Contains("T") ? "T" : "R");
        string biome3 = DesiredBiome3?.ToUpper() ?? (currentBiomeId.Contains("A") ? "A" : "M");
        string targetBiomeId = $"S{biome2}{biome3}V";

        // If already the correct biome, no need to redirect
        if (currentBiomeId == targetBiomeId)
        {
            Plugin.Log.LogInfo($"[LevelOverride] Level {levelIndex} already has {targetBiomeId}");
            return;
        }

        if (BiomeToLevels.TryGetValue(targetBiomeId, out int[] validLevels))
        {
            // Pick a level with matching biomes
            // Use original index modulo to get variety while being deterministic
            int newIndex = validLevels[originalIndex % validLevels.Length];
            Plugin.Log.LogInfo($"[LevelOverride] Redirecting level {originalIndex} ({currentBiomeId}) -> {newIndex} ({targetBiomeId})");
            levelIndex = newIndex;
        }
        else
        {
            Plugin.Log.LogWarning($"[LevelOverride] Unknown biome combo: {targetBiomeId}");
        }
    }

    [HarmonyPatch(nameof(MapBaker.GetBiomeID))]
    [HarmonyPostfix]
    public static void GetBiomeID_Postfix(MapBaker __instance, int levelIndex, ref string __result)
    {
        // If we're overriding, we need to return the biome ID that matches our override
        // This ensures UI and other systems show the correct biomes
        if (DesiredBiome2 == null && DesiredBiome3 == null)
            return;

        string biome2 = DesiredBiome2?.ToUpper() ?? (__result.Contains("T") ? "T" : "R");
        string biome3 = DesiredBiome3?.ToUpper() ?? (__result.Contains("A") ? "A" : "M");
        string newBiomeId = $"S{biome2}{biome3}V";

        if (__result != newBiomeId)
        {
            Plugin.Log.LogInfo($"[LevelOverride] BiomeID override: {__result} -> {newBiomeId}");
            __result = newBiomeId;
        }
    }

    private static string GetBiomeIdForLevel(MapBaker baker, int levelIndex)
    {
        if (baker.BiomeIDs == null || baker.BiomeIDs.Count == 0)
            return "STMV"; // Fallback
            
        return baker.BiomeIDs[levelIndex % baker.BiomeIDs.Count];
    }
}
