using HarmonyLib;

namespace VacationPlanner.Patches;

/// <summary>
/// Debug patch to dump MapBaker data and understand biome structure.
/// </summary>
[HarmonyPatch(typeof(MapBaker))]
public static class MapBakerDebugPatch
{
    private static bool _hasDumped = false;

    /// <summary>
    /// Dumps all levels and biome IDs when GetLevel is first called.
    /// </summary>
    [HarmonyPatch(nameof(MapBaker.GetLevel))]
    [HarmonyPostfix]
    public static void GetLevel_Postfix(MapBaker __instance, int levelIndex, string __result)
    {
        if (!Plugin.DebugLoggingEnabled)
            return;

        if (!_hasDumped)
        {
            _hasDumped = true;
            DumpMapBakerData(__instance);
        }
        
        Plugin.Log.LogInfo($"[GetLevel] levelIndex={levelIndex} => result=\"{__result}\"");
    }

    /// <summary>
    /// Logs every call to GetBiomeID.
    /// </summary>
    [HarmonyPatch(nameof(MapBaker.GetBiomeID))]
    [HarmonyPostfix]
    public static void GetBiomeID_Postfix(MapBaker __instance, int levelIndex, string __result)
    {
        if (!Plugin.DebugLoggingEnabled)
            return;

        var actualIndex = levelIndex % __instance.BiomeIDs.Count;
        Plugin.Log.LogInfo($"[GetBiomeID] levelIndex={levelIndex} (actual={actualIndex}) => biomeID=\"{__result}\"");
    }

    private static void DumpMapBakerData(MapBaker baker)
    {
        Plugin.Log.LogInfo("=== MapBaker Data Dump ===");
        
        // Dump ScenePaths array
        Plugin.Log.LogInfo($"ScenePaths.Length = {baker.ScenePaths?.Length ?? 0}");
        if (baker.ScenePaths != null)
        {
            for (int i = 0; i < baker.ScenePaths.Length; i++)
            {
                Plugin.Log.LogInfo($"  ScenePaths[{i}] = \"{baker.ScenePaths[i]}\"");
            }
        }

        Plugin.Log.LogInfo("");
        
        // Dump BiomeIDs list
        Plugin.Log.LogInfo($"BiomeIDs.Count = {baker.BiomeIDs?.Count ?? 0}");
        if (baker.BiomeIDs != null)
        {
            for (int i = 0; i < baker.BiomeIDs.Count; i++)
            {
                var biomeId = baker.BiomeIDs[i];
                var decoded = DecodeBiomeID(biomeId);
                Plugin.Log.LogInfo($"  BiomeIDs[{i}] = \"{biomeId}\" ({decoded})");
            }
        }

        Plugin.Log.LogInfo("");
        
        // Dump selectedBiomes list (now carries biomeTypes + variantNames separately)
        Plugin.Log.LogInfo($"selectedBiomes.Count = {baker.selectedBiomes?.Count ?? 0}");
        if (baker.selectedBiomes != null)
        {
            for (int i = 0; i < baker.selectedBiomes.Count; i++)
            {
                var result = baker.selectedBiomes[i];
                var biomes = string.Join(", ", result.biomeTypes);
                var variants = string.Join(", ", result.variantNames ?? new System.Collections.Generic.List<string>());
                Plugin.Log.LogInfo($"  selectedBiomes[{i}] biomeTypes=[{biomes}] variantNames=[{variants}] => \"{result}\"");
            }
        }

        Plugin.Log.LogInfo("");
        
        // Check relationship between ScenePaths and BiomeIDs
        Plugin.Log.LogInfo("=== Level/Biome Correlation ===");
        var levelCount = baker.ScenePaths?.Length ?? 0;
        var biomeCount = baker.BiomeIDs?.Count ?? 0;
        Plugin.Log.LogInfo($"ScenePaths has {levelCount} entries, BiomeIDs has {biomeCount} entries");
        
        if (levelCount > 0 && biomeCount > 0)
        {
            Plugin.Log.LogInfo($"Ratio: {(float)biomeCount / levelCount:F2} biomes per level");
            Plugin.Log.LogInfo($"Are they 1:1? {(levelCount == biomeCount ? "YES" : "NO")}");
        }
        
        Plugin.Log.LogInfo("=== End MapBaker Data Dump ===");
    }

    private static string DecodeBiomeID(string biomeId)
    {
        if (string.IsNullOrEmpty(biomeId))
            return "empty";
            
        var parts = new System.Collections.Generic.List<string>();
        for (int i = 0; i < biomeId.Length; i++)
        {
            char c = biomeId[i];
            bool isFirst = i == 0;
            bool isLast = i == biomeId.Length - 1;

            string name;
            if (isFirst && c == 'S')
            {
                // Slot 0 is always Shore
                name = "Shore";
            }
            else if (isLast && c == 'S')
            {
                // Last slot 'S' means Swamp (Gloom + The Citadel), distinct from Shore
                name = "Swamp/Gloom+Citadel";
            }
            else if (isLast && c == 'V')
            {
                // Last slot 'V' means Volcano (Caldera + The Kiln)
                name = "Volcano/Caldera+Kiln";
            }
            else
            {
                name = c switch
                {
                    'T' => "Tropics",
                    'R' => "Roots",
                    'A' => "Alpine",
                    'M' => "Mesa",
                    'V' => "Volcano/Caldera",
                    'K' => "Kiln",
                    'P' => "Peak",
                    _ => $"UNRECOGNIZED_CHAR({c})"
                };
            }
            parts.Add(name);
        }
        return string.Join(" + ", parts);
    }
}
