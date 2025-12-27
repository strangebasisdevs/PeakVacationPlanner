using System;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace ChoiceEnhanced;

/// <summary>
/// Handles runtime biome selection via numpad keys.
/// Hooks into PEAKChoice's configuration to force specific biomes.
/// </summary>
public class BiomeController : MonoBehaviour
{
    // Cache reflection references to PEAKChoice fields
    private static Type? _peakChoiceType;
    private static FieldInfo? _forcedBiomesField;   // Likely used by Additions.ForcedContains()
    private static FieldInfo? _layoutOrderField;
    private static FieldInfo? _forceLayoutOrderField;
    
    private static bool _initialized;

    public static string SelectedBiome2 { get; private set; } = ""; // Tropics vs Roots
    public static string SelectedBiome3 { get; private set; } = ""; // Alpine vs Mesa

    private void Awake()
    {
        InitializeReflection();
    }

    private static void InitializeReflection()
    {
        if (_initialized) return;

        try
        {
            // Find the PEAKChoice main class
            _peakChoiceType = Type.GetType("PEAKChoice.PEAKChoice, off_grid.PEAKChoice");
            
            if (_peakChoiceType == null)
            {
                // Try searching all assemblies
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    _peakChoiceType = asm.GetType("PEAKChoice.PEAKChoice");
                    if (_peakChoiceType != null) break;
                }
            }

            if (_peakChoiceType != null)
            {
                // Get the fields we need to modify
                _layoutOrderField = _peakChoiceType.GetField("LayoutOrder", 
                    BindingFlags.Public | BindingFlags.Static);
                _forceLayoutOrderField = _peakChoiceType.GetField("ForceLayoutOrder", 
                    BindingFlags.Public | BindingFlags.Static);
                
                // Look for a field that Additions.ForcedContains might check
                // This could be a List<string>, HashSet<string>, or string field
                _forcedBiomesField = _peakChoiceType.GetField("ForcedBiomes", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                Plugin.Log.LogInfo($"PEAKChoice type found. LayoutOrder field: {_layoutOrderField != null}");
                _initialized = true;
            }
            else
            {
                Plugin.Log.LogWarning("Could not find PEAKChoice type!");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to initialize reflection: {ex}");
        }
    }

    private void Update()
    {
        // Numpad 1: Force Tropics (Jungle) instead of Roots
        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            SetBiomeChoice("Tropics", 2);
            Plugin.Log.LogInfo("Biome 2 set to: TROPICS (Jungle)");
        }
        
        // Numpad 2: Force Roots instead of Tropics
        if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            SetBiomeChoice("Roots", 2);
            Plugin.Log.LogInfo("Biome 2 set to: ROOTS");
        }

        // Numpad 4: Force Alpine (Snow) instead of Mesa
        if (Input.GetKeyDown(KeyCode.Keypad4))
        {
            SetBiomeChoice("Alpine", 3);
            Plugin.Log.LogInfo("Biome 3 set to: ALPINE (Snow)");
        }
        
        // Numpad 5: Force Mesa (Desert) instead of Alpine
        if (Input.GetKeyDown(KeyCode.Keypad5))
        {
            SetBiomeChoice("Mesa", 3);
            Plugin.Log.LogInfo("Biome 3 set to: MESA (Desert)");
        }

        // Numpad 0: Clear all forced selections
        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            SelectedBiome2 = "";
            SelectedBiome3 = "";
            Plugin.Log.LogInfo("Biome selections CLEARED");
        }

        // Numpad Enter: Log current selections
        if (Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Plugin.Log.LogInfo($"Current selections - Biome2: {SelectedBiome2 ?? "default"}, Biome3: {SelectedBiome3 ?? "default"}");
        }
    }

    private static void SetBiomeChoice(string biome, int slot)
    {
        if (slot == 2) SelectedBiome2 = biome;
        else if (slot == 3) SelectedBiome3 = biome;
        TryApplyToPEAKChoice();
    }

    /// <summary>
    /// Attempts to apply our selection to PEAKChoice's internal state
    /// </summary>
    private static void TryApplyToPEAKChoice()
    {
        if (!_initialized || _peakChoiceType == null) return;

        try
        {
            // If PEAKChoice exposes ForcedBiomes as a collection, add to it
            if (_forcedBiomesField != null)
            {
                var value = _forcedBiomesField.GetValue(null);
                // Handle different collection types
                if (value is System.Collections.Generic.List<string> list)
                {
                    list.Clear();
                    if (!string.IsNullOrEmpty(SelectedBiome2)) list.Add(SelectedBiome2);
                    if (!string.IsNullOrEmpty(SelectedBiome3)) list.Add(SelectedBiome3);
                }
                else if (value is System.Collections.Generic.HashSet<string> hashSet)
                {
                    hashSet.Clear();
                    if (!string.IsNullOrEmpty(SelectedBiome2)) hashSet.Add(SelectedBiome2);
                    if (!string.IsNullOrEmpty(SelectedBiome3)) hashSet.Add(SelectedBiome3);
                }
            }

            Plugin.Log.LogInfo("Applied biome selection to PEAKChoice");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to apply to PEAKChoice: {ex}");
        }
    }
}