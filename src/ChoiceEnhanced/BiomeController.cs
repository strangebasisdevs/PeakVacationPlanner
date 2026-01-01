using ChoiceEnhanced.Patches;
using Photon.Pun;
using UnityEngine;

namespace ChoiceEnhanced;

/// <summary>
/// Handles runtime biome selection via numpad keys.
/// Controls LevelOverridePatch to redirect to levels with desired biomes.
/// 
/// No longer depends on PEAKChoice - directly patches the game's MapBaker.
/// </summary>
public class BiomeController : MonoBehaviour
{
    /// <summary>
    /// Selected biome for slot 2 (Tropics vs Roots).
    /// "T" for Tropics, "R" for Roots, null for default.
    /// </summary>
    public static string? SelectedBiome2
    {
        get => LevelOverridePatch.DesiredBiome2;
        set => LevelOverridePatch.DesiredBiome2 = value;
    }

    /// <summary>
    /// Selected biome for slot 3 (Alpine vs Mesa).
    /// "A" for Alpine, "M" for Mesa, null for default.
    /// </summary>
    public static string? SelectedBiome3
    {
        get => LevelOverridePatch.DesiredBiome3;
        set => LevelOverridePatch.DesiredBiome3 = value;
    }

    private void Update()
    {
        HandleKeyboardInput();
    }

    private void HandleKeyboardInput()
    {
        // Numpad 1: Force Tropics (Jungle)
        if (Input.GetKeyDown(KeyCode.Keypad1))
        {
            SelectedBiome2 = "T";
            Plugin.Log.LogInfo("Biome 2 set to: TROPICS (Jungle)");
            SyncToRoomIfHost();
        }

        // Numpad 2: Force Roots
        if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            SelectedBiome2 = "R";
            Plugin.Log.LogInfo("Biome 2 set to: ROOTS");
            SyncToRoomIfHost();
        }

        // Numpad 4: Force Alpine (Snow)
        if (Input.GetKeyDown(KeyCode.Keypad4))
        {
            SelectedBiome3 = "A";
            Plugin.Log.LogInfo("Biome 3 set to: ALPINE (Snow)");
            SyncToRoomIfHost();
        }

        // Numpad 5: Force Mesa (Desert)
        if (Input.GetKeyDown(KeyCode.Keypad5))
        {
            SelectedBiome3 = "M";
            Plugin.Log.LogInfo("Biome 3 set to: MESA (Desert)");
            SyncToRoomIfHost();
        }

        // Numpad 0: Clear all forced selections
        if (Input.GetKeyDown(KeyCode.Keypad0))
        {
            LevelOverridePatch.ClearOverrides();
            SyncToRoomIfHost();
        }

        // Numpad Enter: Log current selections
        if (Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Plugin.Log.LogInfo($"Current: {LevelOverridePatch.GetSelectionSummary()}");
        }
    }

    private static void SyncToRoomIfHost()
    {
        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            NetworkSync.SetRoomBiomeSelection();
        }
    }

    /// <summary>
    /// Called by NetworkSync when receiving biome selection from host.
    /// </summary>
    public static void SetFromNetwork(string? biome2, string? biome3)
    {
        Plugin.Log.LogInfo($"Applying from network: Biome2={biome2 ?? "null"}, Biome3={biome3 ?? "null"}");
        SelectedBiome2 = string.IsNullOrEmpty(biome2) ? null : biome2;
        SelectedBiome3 = string.IsNullOrEmpty(biome3) ? null : biome3;
    }
}