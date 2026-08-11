using HarmonyLib;
using UnityEngine;
using Zorro.Core;

namespace VacationPlanner.Patches;

/// <summary>
/// Diagnostic-only patch (no gameplay changes). Dumps MapHandler's segment/variant structure
/// so we can determine whether "off" biome variants (e.g. Volcano vs Swamp for slot 4, the way
/// Roots vs Tropics already appears to work for slot 2) are still physically present-but-inactive
/// in a loaded level, or fully absent from this build's baked scenes. Run an actual expedition
/// (not just the airport) and check the log for the dump.
/// </summary>
[HarmonyPatch(typeof(MapHandler))]
public static class MapHandlerDebugPatch
{
    [HarmonyPatch(nameof(MapHandler.InitializeMap))]
    [HarmonyPostfix]
    public static void InitializeMap_Postfix()
    {
        if (!Plugin.DebugLoggingEnabled)
            return;

        if (MapHandler.Exists)
        {
            DumpMapHandlerData(Singleton<MapHandler>.Instance);
        }
    }

    // DetectBiomes is internal, so it must be patched by string name rather than nameof().
    [HarmonyPatch("DetectBiomes")]
    [HarmonyPostfix]
    public static void DetectBiomes_Postfix(MapHandler __instance)
    {
        if (!Plugin.DebugLoggingEnabled)
            return;

        Plugin.Log.LogInfo("[MapHandlerDebug] DetectBiomes() was called.");
        DumpMapHandlerData(__instance);
    }

    private static void DumpMapHandlerData(MapHandler handler)
    {
        Plugin.Log.LogInfo("=== MapHandler Segment Dump ===");

        MapHandler.MapSegment[] segments = handler.segments;
        MapHandler.MapSegment[] variantSegments = handler.variantSegments;

        Plugin.Log.LogInfo($"segments.Length = {segments?.Length ?? 0}");
        if (segments != null)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                DumpSegment($"segments[{i}]", segments[i]);
            }
        }

        Plugin.Log.LogInfo($"variantSegments.Length = {variantSegments?.Length ?? 0}");
        if (variantSegments != null)
        {
            for (int i = 0; i < variantSegments.Length; i++)
            {
                DumpSegment($"variantSegments[{i}]", variantSegments[i]);
            }
        }

        Plugin.Log.LogInfo($"MapHandler.biomes (currently detected/active) = [{string.Join(", ", handler.biomes)}]");
        Plugin.Log.LogInfo("=== End MapHandler Segment Dump ===");
    }

    private static void DumpSegment(string label, MapHandler.MapSegment segment)
    {
        if (segment == null)
        {
            Plugin.Log.LogInfo($"  {label} = null");
            return;
        }

        // _biome / _segmentParent / _segmentCampfire are private [SerializeField]s, so we need
        // reflection (via Harmony's Traverse) to see the *raw* pre-variant-resolution values -
        // that's what tells us whether an inactive alternate is sitting right next to the active one.
        Traverse traverse = Traverse.Create(segment);
        Biome.BiomeType rawBiome = traverse.Field("_biome").GetValue<Biome.BiomeType>();
        GameObject rawParent = traverse.Field("_segmentParent").GetValue<GameObject>();
        GameObject rawCampfire = traverse.Field("_segmentCampfire").GetValue<GameObject>();

        string parentName = rawParent != null ? rawParent.name : "null";
        bool parentActiveSelf = rawParent != null && rawParent.activeSelf;
        bool parentActiveInHierarchy = rawParent != null && rawParent.activeInHierarchy;

        string effectiveBiome;
        try
        {
            effectiveBiome = segment.biome.ToString();
        }
        catch
        {
            effectiveBiome = "ERROR";
        }

        Plugin.Log.LogInfo(
            $"  {label}: rawBiome={rawBiome}, effectiveBiome={effectiveBiome}, " +
            $"hasVariant={segment.hasVariant}, variantBiomeIndex={segment.variantBiomeIndex}, isVariant={segment.isVariant}, " +
            $"segmentParent=\"{parentName}\" (activeSelf={parentActiveSelf}, activeInHierarchy={parentActiveInHierarchy}), " +
            $"campfire=\"{(rawCampfire != null ? rawCampfire.name : "null")}\"");
    }
}
