using HarmonyLib;
using System;

namespace ChoiceEnhanced.Patches;

/// <summary>
/// Patches PEAKChoice's Additions.ForcedContains to inject our biome choices
/// </summary>
[HarmonyPatch]
internal static class AdditionsPatch
{
    // Target the Additions.ForcedContains method
    [HarmonyTargetMethod]
    static System.Reflection.MethodBase TargetMethod()
    {
        var additionsType = Type.GetType("PEAKChoice.Features.Additions, off_grid.PEAKChoice");
        if (additionsType == null)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                additionsType = asm.GetType("PEAKChoice.Features.Additions");
                if (additionsType != null) break;
            }
        }
        
        if (additionsType != null)
        {
            var method = additionsType.GetMethod("ForcedContains", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (method != null)
            {
                Plugin.Log.LogInfo("Found Additions.ForcedContains to patch");
                return method;
            }
        }
        
        Plugin.Log.LogWarning("Could not find Additions.ForcedContains");
        return null!;
    }

    /// <summary>
    /// Postfix that overrides the result based on our selections
    /// </summary>
    [HarmonyPostfix]
    static void Postfix(string __0, ref bool __result)
    {
        string biomeToCheck = __0;
        
        // If user selected Tropics and we're checking for Tropics, return true
        if (BiomeController.SelectedBiome2 == "Tropics" && biomeToCheck == "Tropics")
        {
            __result = true;
            return;
        }
        
        // If user selected Roots and we're checking for Roots, return true
        if (BiomeController.SelectedBiome2 == "Roots" && biomeToCheck == "Roots")
        {
            __result = true;
            return;
        }

        // Same for Alpine/Mesa
        if (BiomeController.SelectedBiome3 == "Alpine" && biomeToCheck == "Alpine")
        {
            __result = true;
            return;
        }
        
        if (BiomeController.SelectedBiome3 == "Mesa" && biomeToCheck == "Mesa")
        {
            __result = true;
            return;
        }
    }
}