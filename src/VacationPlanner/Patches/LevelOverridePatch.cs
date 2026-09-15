using System;
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
    // "V" for Volcano (Caldera + The Kiln), "S" for Swamp (Gloom + The Citadel)
    public static string? DesiredBiome4 { get; set; } = null;

    /// <summary>
    /// Clears all biome overrides.
    /// </summary>
    public static void ClearOverrides()
    {
        DesiredBiome2 = null;
        DesiredBiome3 = null;
        DesiredBiome4 = null;
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
    /// Ensures the biome-to-level mapping is built even if no override has been requested yet.
    /// Lets the voting menu gate impossible options (and explain why) the moment it's opened,
    /// rather than only after the player has already triggered an override once.
    /// </summary>
    public static void EnsureBiomeMapBuilt()
    {
        if (_biomeToLevels != null)
            return;

        MapBaker? baker = SingletonAsset<MapBaker>.Instance;
        if (baker != null)
        {
            _biomeToLevels = BuildBiomeToLevels(baker);
        }
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
        string b4 = DesiredBiome4 switch
        {
            "V" => "Caldera/Kiln",
            "S" => "Gloom/Citadel",
            _ => "Default"
        };
        return $"Biome2: {b2}, Biome3: {b3}, Biome4: {b4}";
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
        if (DesiredBiome2 == null && DesiredBiome3 == null && DesiredBiome4 == null)
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
        // Detect the current last character dynamically (V, S, etc.) so it survives future renames
        char currentLastChar = currentBiomeId.Length > 0 ? currentBiomeId[currentBiomeId.Length - 1] : 'V';
        string biome4 = DesiredBiome4?.ToUpper() ?? currentLastChar.ToString();

        // Normalize the original index up front - this is also "today's actual island" index,
        // used as the reference point so fallback substitutes stay as close to it as possible
        // (and naturally rotate day-to-day along with it, instead of being pinned to one static pick).
        int actualOriginal = originalIndex % biomeCount;

        // Resolve to a biome ID that actually exists in this build's baked levels.
        // Not every branch (e.g. Volcano vs Swamp) is guaranteed to be baked into every build,
        // so we gracefully degrade rather than aborting the whole redirect.
        string targetBiomeId = ResolveAchievableBiomeId(biome2, biome3, biome4, currentBiomeId, actualOriginal, biomeCount);

        // If already the correct biome, no need to redirect
        if (currentBiomeId == targetBiomeId)
        {
            Plugin.Log.LogInfo($"[LevelOverride] Level {levelIndex} already has {targetBiomeId}");
            return;
        }

        if (_biomeToLevels.TryGetValue(targetBiomeId, out List<int> validLevels) && validLevels.Count > 0)
        {
            // Pick from valid levels for variety, staying as close as possible to today's actual index
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
        if (DesiredBiome2 == null && DesiredBiome3 == null && DesiredBiome4 == null)
            return;

        if (_biomeToLevels == null)
        {
            _biomeToLevels = BuildBiomeToLevels(__instance);
        }

        string biome2 = DesiredBiome2?.ToUpper() ?? (__result.Contains("T") ? "T" : "R");
        string biome3 = DesiredBiome3?.ToUpper() ?? (__result.Contains("A") ? "A" : "M");

        // Detect the current last character dynamically (V, S, etc.)
        char currentLastChar = __result.Length > 0 ? __result[__result.Length - 1] : 'V';
        string biome4 = DesiredBiome4?.ToUpper() ?? currentLastChar.ToString();
        int refIndex = levelIndex % (__instance.BiomeIDs?.Count ?? 1);
        int totalLevels = __instance.BiomeIDs?.Count ?? 1;
        string newBiomeId = ResolveAchievableBiomeId(biome2, biome3, biome4, __result, refIndex, totalLevels);

        if (__result != newBiomeId)
        {
            Plugin.Log.LogInfo($"[LevelOverride] BiomeID override: {__result} -> {newBiomeId}");
            __result = newBiomeId;
        }
    }

    /// <summary>
    /// Resolves the desired biome2/biome3/biome4 selection to a biome ID that actually exists
    /// among this build's baked levels. Not every branch is guaranteed to be baked into every
    /// build (e.g. a build may only ship the Volcano branch or only the Swamp branch for slot 4),
    /// so this degrades gracefully: exact match first, then match biome2+biome3 only (letting
    /// biome4 be whatever's actually available), then match biome2 only, then give up.
    /// </summary>
    private static string ResolveAchievableBiomeId(string biome2, string biome3, string biome4, string fallbackCurrent, int referenceIndex, int totalLevels)
    {
        string targetBiomeId = $"S{biome2}{biome3}{biome4}";

        if (_biomeToLevels == null || _biomeToLevels.Count == 0)
            return targetBiomeId;

        if (_biomeToLevels.ContainsKey(targetBiomeId))
            return targetBiomeId;

        string? tier2 = FindClosestKeyMatching(referenceIndex, totalLevels, key => key.Length >= 3 && key[1] == biome2[0] && key[2] == biome3[0]);
        if (tier2 != null)
        {
            Plugin.Log.LogWarning($"[LevelOverride] {targetBiomeId} isn't baked into this build (biome4 branch '{biome4}' may not be available). Falling back to closest match {tier2}.");
            return tier2;
        }

        string? tier3 = FindClosestKeyMatching(referenceIndex, totalLevels, key => key.Length >= 2 && key[1] == biome2[0]);
        if (tier3 != null)
        {
            Plugin.Log.LogWarning($"[LevelOverride] {targetBiomeId} isn't baked into this build. Falling back to closest biome2-only match {tier3}.");
            return tier3;
        }

        Plugin.Log.LogWarning($"[LevelOverride] No baked level matches even a partial override for {targetBiomeId}. Available: {string.Join(", ", _biomeToLevels.Keys)}");
        return fallbackCurrent;
    }

    /// <summary>
    /// Among all biome IDs whose level indices satisfy the predicate, returns the biome ID whose
    /// nearest matching level index is cyclically closest to <paramref name="referenceIndex"/>
    /// (today's actual, un-overridden index) - wrapping around the end of the list back to the
    /// beginning, so a candidate near index 0 can still be "close" to a reference index near the
    /// end (and vice versa). This keeps the fallback substitute "close to" the island the daily
    /// rotation actually picked, and - since referenceIndex changes every day - means the
    /// substitute rotates along with it instead of always being the same one.
    /// </summary>
    private static string? FindClosestKeyMatching(int referenceIndex, int totalLevels, Func<string, bool> predicate)
    {
        string? best = null;
        int bestDistance = int.MaxValue;

        foreach (var kvp in _biomeToLevels!)
        {
            if (!predicate(kvp.Key))
                continue;

            foreach (int idx in kvp.Value)
            {
                int distance = CyclicDistance(idx, referenceIndex, totalLevels);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = kvp.Key;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Distance between two level indices, wrapping around the end of the list back to the
    /// start (e.g. with 14 levels, index 13 and index 0 are only 1 apart, not 13).
    /// </summary>
    private static int CyclicDistance(int a, int b, int totalLevels)
    {
        if (totalLevels <= 0)
            return Math.Abs(a - b);

        int diff = Math.Abs(a - b) % totalLevels;
        return Math.Min(diff, totalLevels - diff);
    }

    private static char? NormalizeLeader(string? leader) => string.IsNullOrEmpty(leader) ? null : leader!.ToUpper()[0];

    // Sentinel position meaning "match by last character" (biome4), rather than a fixed index,
    // since biome4 is checked by last char so it survives the ID format growing longer.
    private const int PosBiome2 = 1;
    private const int PosBiome3 = 2;
    private const int PosBiome4 = -1;

    private static bool KeyMatchesPosition(string key, int position, char value)
    {
        if (position == PosBiome4)
            return key.Length > 0 && key[key.Length - 1] == value;
        return key.Length > position && key[position] == value;
    }

    private static bool AnyKeyMatches(int targetPos, char targetChar, int constraintPosA, char? constraintValueA, int constraintPosB, char? constraintValueB)
    {
        foreach (string key in _biomeToLevels!.Keys)
        {
            if (!KeyMatchesPosition(key, targetPos, targetChar)) continue;
            if (constraintValueA.HasValue && !KeyMatchesPosition(key, constraintPosA, constraintValueA.Value)) continue;
            if (constraintValueB.HasValue && !KeyMatchesPosition(key, constraintPosB, constraintValueB.Value)) continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Describes whether a branch can currently be voted for, and - if not - why, so the UI can
    /// explain the constraint to players instead of just graying an option out silently.
    /// </summary>
    public struct BranchAvailability
    {
        /// <summary>True if at least one baked level satisfies this branch and both current leaders.</summary>
        public bool IsAvailable { get; set; }
        /// <summary>True if NO baked level has this branch at all, regardless of any other slot's value.</summary>
        public bool IsGloballyUnavailable { get; set; }
        /// <summary>True if changing the "other A" slot away from its current leader (while the "other B" slot stays put) would make this branch achievable.</summary>
        public bool ChangingOtherAWouldHelp { get; set; }
        /// <summary>True if changing the "other B" slot away from its current leader (while the "other A" slot stays put) would make this branch achievable.</summary>
        public bool ChangingOtherBWouldHelp { get; set; }
    }

    private static BranchAvailability Evaluate(int position, string branch, int otherPosA, string? otherLeaderA, int otherPosB, string? otherLeaderB)
    {
        if (_biomeToLevels == null || _biomeToLevels.Count == 0 || string.IsNullOrEmpty(branch))
            return new BranchAvailability { IsAvailable = true };

        char branchChar = branch.ToUpper()[0];
        char? a = NormalizeLeader(otherLeaderA);
        char? b = NormalizeLeader(otherLeaderB);

        bool globallyUnavailable = !AnyKeyMatches(position, branchChar, otherPosA, null, otherPosB, null);
        if (globallyUnavailable)
            return new BranchAvailability { IsAvailable = false, IsGloballyUnavailable = true };

        bool available = AnyKeyMatches(position, branchChar, otherPosA, a, otherPosB, b);
        if (available)
            return new BranchAvailability { IsAvailable = true };

        // Not available with both current leaders held fixed - check whether relaxing just one
        // of them (holding the other fixed) would be enough to find a match.
        return new BranchAvailability
        {
            IsAvailable = false,
            IsGloballyUnavailable = false,
            ChangingOtherAWouldHelp = AnyKeyMatchesSingleConstraint(position, branchChar, otherPosB, b),
            ChangingOtherBWouldHelp = AnyKeyMatchesSingleConstraint(position, branchChar, otherPosA, a)
        };
    }

    private static bool AnyKeyMatchesSingleConstraint(int targetPos, char targetChar, int constraintPos, char? constraintValue)
    {
        foreach (string key in _biomeToLevels!.Keys)
        {
            if (!KeyMatchesPosition(key, targetPos, targetChar)) continue;
            if (constraintValue.HasValue && !KeyMatchesPosition(key, constraintPos, constraintValue.Value)) continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Evaluates the biome2 branch ("T"/"R") against the current biome3/biome4 leaders.
    /// </summary>
    public static BranchAvailability EvaluateBiome2Branch(string branch, string? biome3Leader = null, string? biome4Leader = null)
        => Evaluate(PosBiome2, branch, PosBiome3, biome3Leader, PosBiome4, biome4Leader);

    /// <summary>
    /// Evaluates the biome3 branch ("A"/"M") against the current biome2/biome4 leaders.
    /// </summary>
    public static BranchAvailability EvaluateBiome3Branch(string branch, string? biome2Leader = null, string? biome4Leader = null)
        => Evaluate(PosBiome3, branch, PosBiome2, biome2Leader, PosBiome4, biome4Leader);

    /// <summary>
    /// Evaluates the biome4 branch ("V"/"S") against the current biome2/biome3 leaders.
    /// </summary>
    public static BranchAvailability EvaluateBiome4Branch(string branch, string? biome2Leader = null, string? biome3Leader = null)
        => Evaluate(PosBiome4, branch, PosBiome2, biome2Leader, PosBiome3, biome3Leader);

    /// <summary>
    /// Returns true if at least one baked level in this build has the given biome2 branch
    /// ("T" for Tropics, "R" for Roots). Optionally cross-checked against the branch currently
    /// *leading* the biome3/biome4 vote, so the UI can gray out an option that's individually
    /// fine but not baked together with what's actually winning in the other columns right now.
    /// </summary>
    public static bool IsBiome2BranchAvailable(string branch, string? biome3Leader = null, string? biome4Leader = null)
        => EvaluateBiome2Branch(branch, biome3Leader, biome4Leader).IsAvailable;

    /// <summary>
    /// Returns true if at least one baked level in this build has the given biome3 branch
    /// ("A" for Alpine, "M" for Mesa). Optionally cross-checked against the branch currently
    /// leading the biome2/biome4 vote (see <see cref="IsBiome2BranchAvailable"/>).
    /// </summary>
    public static bool IsBiome3BranchAvailable(string branch, string? biome2Leader = null, string? biome4Leader = null)
        => EvaluateBiome3Branch(branch, biome2Leader, biome4Leader).IsAvailable;

    /// <summary>
    /// Returns true if at least one baked level in this build has the given biome4 branch
    /// ("V" for Volcano/Caldera+Kiln, "S" for Swamp/Gloom+Citadel). Optionally cross-checked
    /// against the branch currently leading the biome2/biome3 vote (see
    /// <see cref="IsBiome2BranchAvailable"/>).
    /// </summary>
    public static bool IsBiome4BranchAvailable(string branch, string? biome2Leader = null, string? biome3Leader = null)
        => EvaluateBiome4Branch(branch, biome2Leader, biome3Leader).IsAvailable;

    private static string GetBiomeIdForLevel(MapBaker baker, int levelIndex)
    {
        if (baker.BiomeIDs == null || baker.BiomeIDs.Count == 0)
            return "STMV"; // Fallback
            
        return baker.BiomeIDs[levelIndex % baker.BiomeIDs.Count];
    }
}
