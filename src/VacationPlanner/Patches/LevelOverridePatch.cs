using System.Collections.Generic;
using HarmonyLib;
using Zorro.Core;

namespace VacationPlanner.Patches;

/// <summary>
/// Overrides level selection to get desired biome combinations.
/// Since each level is pre-baked with specific biomes, we redirect to a level with the desired biomes.
/// Dynamically builds the biome-to-level mapping from MapBaker data so it survives game updates.
/// </summary>
[HarmonyPatch(typeof(MapBaker))]
public static class LevelOverridePatch
{
    // Dynamically built mapping (populated on first use from MapBaker.BiomeIDs)
    private static Dictionary<string, List<int>>? _biomeToLevels;

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
    /// Invalidate the cached mapping so it gets rebuilt on next use.
    /// Call this if MapBaker data might have changed.
    /// </summary>
    public static void InvalidateCache()
    {
        _biomeToLevels = null;
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

    /// <summary>
    /// Dynamically builds the biome-to-level-indices mapping from the MapBaker's BiomeIDs list.
    /// </summary>
    private static Dictionary<string, List<int>> BuildBiomeToLevels(MapBaker baker)
    {
        var map = new Dictionary<string, List<int>>();

        if (baker.BiomeIDs != null)
        {
            for (int i = 0; i < baker.BiomeIDs.Count; i++)
            {
                string biomeId = baker.BiomeIDs[i];
                if (!map.ContainsKey(biomeId))
                {
                    map[biomeId] = new List<int>();
                }
                map[biomeId].Add(i);
            }

            Plugin.Log.LogInfo($"[LevelOverride] Built dynamic biome mapping with {map.Count} biome types:");
            foreach (var kvp in map)
            {
                Plugin.Log.LogInfo($"  {kvp.Key} -> [{string.Join(", ", kvp.Value)}]");
            }
        }

        return map;
    }

    [HarmonyPatch(nameof(MapBaker.GetLevel))]
    [HarmonyPrefix]
    public static void GetLevel_Prefix(MapBaker __instance, ref int levelIndex)
    {
        if (DesiredBiome2 == null && DesiredBiome3 == null)
            return; // No override requested

        // Build mapping on first use
        if (_biomeToLevels == null)
        {
            _biomeToLevels = BuildBiomeToLevels(__instance);
        }

        int originalIndex = levelIndex;
        int biomeCount = __instance.BiomeIDs?.Count ?? 1;

        // Get the current biome ID for this level (handle large indices)
        string currentBiomeId = GetBiomeIdForLevel(__instance, levelIndex);

        // Determine target biome characters
        string biome2 = DesiredBiome2?.ToUpper() ?? (currentBiomeId.Contains("T") ? "T" : "R");
        string biome3 = DesiredBiome3?.ToUpper() ?? (currentBiomeId.Contains("A") ? "A" : "M");

        // Build the target biome ID using the same format as the game
        // Detect the last character dynamically (V, K, etc.) so it survives future renames
        char lastChar = currentBiomeId.Length > 0 ? currentBiomeId[currentBiomeId.Length - 1] : 'V';
        string targetBiomeId = $"S{biome2}{biome3}{lastChar}";

        // If already the correct biome, no need to redirect
        if (currentBiomeId == targetBiomeId)
        {
            Plugin.Log.LogInfo($"[LevelOverride] Level {levelIndex} already has {targetBiomeId}");
            return;
        }

        if (_biomeToLevels.TryGetValue(targetBiomeId, out List<int> validLevels) && validLevels.Count > 0)
        {
            // Normalize the original index, then pick from valid levels for variety
            int actualOriginal = originalIndex % biomeCount;
            int newIndex = validLevels[actualOriginal % validLevels.Count];
            Plugin.Log.LogInfo($"[LevelOverride] Redirecting level {originalIndex} (actual={actualOriginal}, {currentBiomeId}) -> {newIndex} ({targetBiomeId})");
            levelIndex = newIndex;
        }
        else
        {
            Plugin.Log.LogWarning($"[LevelOverride] Unknown biome combo: {targetBiomeId}. Available: {string.Join(", ", _biomeToLevels.Keys)}");
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

        // Detect the last character dynamically (V, K, etc.)
        char lastChar = __result.Length > 0 ? __result[__result.Length - 1] : 'V';
        string newBiomeId = $"S{biome2}{biome3}{lastChar}";

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
