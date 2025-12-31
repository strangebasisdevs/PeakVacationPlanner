using HarmonyLib;

namespace ChoiceEnhanced.Patches;

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
        var actualIndex = levelIndex % __instance.BiomeIDs.Count;
        Plugin.Log.LogInfo($"[GetBiomeID] levelIndex={levelIndex} (actual={actualIndex}) => biomeID=\"{__result}\"");
    }

    private static void DumpMapBakerData(MapBaker baker)
    {
        Plugin.Log.LogInfo("=== MapBaker Data Dump ===");
        
        // Dump AllLevels array
        Plugin.Log.LogInfo($"AllLevels.Length = {baker.AllLevels?.Length ?? 0}");
        if (baker.AllLevels != null)
        {
            for (int i = 0; i < baker.AllLevels.Length; i++)
            {
                Plugin.Log.LogInfo($"  AllLevels[{i}] = \"{baker.AllLevels[i]}\"");
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
        
        // Dump selectedBiomes list
        Plugin.Log.LogInfo($"selectedBiomes.Count = {baker.selectedBiomes?.Count ?? 0}");
        if (baker.selectedBiomes != null)
        {
            for (int i = 0; i < baker.selectedBiomes.Count; i++)
            {
                var result = baker.selectedBiomes[i];
                var biomes = string.Join(", ", result.selectedBiomes);
                Plugin.Log.LogInfo($"  selectedBiomes[{i}] = [{biomes}] => \"{result}\"");
            }
        }

        Plugin.Log.LogInfo("");
        
        // Dump biomeSelection configuration
        Plugin.Log.LogInfo($"biomeSelection.Count = {baker.biomeSelection?.Count ?? 0}");
        if (baker.biomeSelection != null)
        {
            for (int i = 0; i < baker.biomeSelection.Count; i++)
            {
                var selection = baker.biomeSelection[i];
                Plugin.Log.LogInfo($"  biomeSelection[{i}] options:");
                foreach (var option in selection.biomeOptions)
                {
                    Plugin.Log.LogInfo($"    - {option.biome} (weight={option.weight}, maxInRow={option.preventMoreThanXInARow})");
                }
            }
        }
        
        Plugin.Log.LogInfo("");
        
        // Check relationship between AllLevels and BiomeIDs
        Plugin.Log.LogInfo("=== Level/Biome Correlation ===");
        var levelCount = baker.AllLevels?.Length ?? 0;
        var biomeCount = baker.BiomeIDs?.Count ?? 0;
        Plugin.Log.LogInfo($"AllLevels has {levelCount} entries, BiomeIDs has {biomeCount} entries");
        
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
        foreach (char c in biomeId)
        {
            var name = c switch
            {
                'S' => "Shore",
                'T' => "Tropics",
                'R' => "Roots",
                'A' => "Alpine",
                'M' => "Mesa",
                'V' or 'K' => "Kiln/Volcano",
                'P' => "Peak",
                _ => $"Unknown({c})"
            };
            parts.Add(name);
        }
        return string.Join(" + ", parts);
    }
}
