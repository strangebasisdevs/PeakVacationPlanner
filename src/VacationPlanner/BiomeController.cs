using System.Collections;
using VacationPlanner.Patches;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;

namespace VacationPlanner;

/// <summary>
/// Handles runtime biome voting via an interactive Kiosk in the Airport.
/// Spawns the BiomeVotingKiosk and BiomeVotingPass UI when the Airport scene loads.
/// </summary>
public class BiomeController : MonoBehaviour
{
    // Cached default biome info
    public static string? DefaultBiomeId => _defaultBiomeId;
    private static string? _defaultBiomeId;
    private static int _defaultLevelIndex = -1;
    
    // Reference to spawned kiosk
    private static GameObject? _spawnedKiosk;

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        UpdateDefaultBiomeInfo();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Airport")
        {
            StartCoroutine(SpawnKioskAndUICoroutine());
        }
    }

    private IEnumerator SpawnKioskAndUICoroutine()
    {
        // Wait for scene to fully initialize
        yield return new WaitForSeconds(0.5f);

        // Create the BiomeVotingMenu (MenuWindow for proper cursor/input handling)
        BiomeVotingMenu.Create();

        // Find kiosks for reference
        var checkInKiosk = Object.FindAnyObjectByType<AirportCheckInKiosk>();
        var inviteKiosk = Object.FindAnyObjectByType<AirportInviteFriendsKiosk>();
        
        if (checkInKiosk != null && inviteKiosk != null)
        {
            // Clone the invite friends kiosk (simple, not networked)
            var kioskGO = Instantiate(inviteKiosk.gameObject);
            kioskGO.name = "BiomeVotingKiosk";
            
            // Position it near the check-in kiosk
            kioskGO.transform.position = checkInKiosk.transform.position + checkInKiosk.transform.right * 3.0f;
            kioskGO.transform.rotation = checkInKiosk.transform.rotation;
            
            // Scale up by 2x
            kioskGO.transform.localScale = inviteKiosk.transform.localScale * 2.5f;
            
            // Remove the invite friends script from the clone
            var oldScript = kioskGO.GetComponent<AirportInviteFriendsKiosk>();
            if (oldScript != null)
            {
                Destroy(oldScript);
            }
            
            // Add our kiosk script
            kioskGO.AddComponent<BiomeVotingKiosk>();
            
            _spawnedKiosk = kioskGO;
            Plugin.Log.LogInfo("BiomeVotingKiosk spawned in Airport (scaled 2.5x)!");
        }
        else if (checkInKiosk != null)
        {
            // Fallback: create primitive if invite kiosk not found
            var kioskGO = new GameObject("BiomeVotingKiosk");
            kioskGO.transform.position = checkInKiosk.transform.position + checkInKiosk.transform.right * 3.0f;
            kioskGO.transform.rotation = checkInKiosk.transform.rotation;
            
            var meshGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshGO.name = "KioskMesh";
            meshGO.transform.SetParent(kioskGO.transform, false);
            meshGO.transform.localScale = new Vector3(2f, 4f, 1f);  // Scaled up
            meshGO.transform.localPosition = new Vector3(0, 2f, 0);
            
            // Add collider
            var box = kioskGO.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 2f, 0);
            box.size = new Vector3(3f, 5f, 2f);
            
            kioskGO.AddComponent<BiomeVotingKiosk>();
            _spawnedKiosk = kioskGO;
            Plugin.Log.LogInfo("BiomeVotingKiosk spawned (fallback cube, scaled 2x)!");
        }
        else
        {
            Plugin.Log.LogError("Could not find kiosks for positioning reference!");
        }
    }

    private void UpdateDefaultBiomeInfo()
    {
        // Only update if we don't have it yet
        if (_defaultBiomeId != null) return;
        
        try
        {
            var mapBaker = SingletonAsset<MapBaker>.Instance;
            var nextLevelService = GameHandler.GetService<NextLevelService>();
            
            if (mapBaker != null && nextLevelService != null && nextLevelService.Data.IsSome)
            {
                _defaultLevelIndex = nextLevelService.Data.Value.CurrentLevelIndex + NextLevelService.debugLevelIndexOffset;
                _defaultBiomeId = mapBaker.GetBiomeID(_defaultLevelIndex);
                Plugin.Log.LogInfo($"Default biome for today: {_defaultBiomeId} (Level {_defaultLevelIndex})");
            }
        }
        catch
        {
            // Silently ignore - services not ready yet
        }
    }

    // --- Public helper methods for BiomeVotingPass ---

    public static string BiomeIdToFriendly(string? biomeId)
    {
        if (string.IsNullOrEmpty(biomeId)) return "Loading...";
        
        // Only show biome 2 (Tropics/Roots) and biome 3 (Alpine/Mesa)
        string biome2 = biomeId.Contains('T') ? "Tropics" : "Roots";
        string biome3 = biomeId.Contains('A') ? "Alpine" : "Mesa";
        
        return $"{biome2} + {biome3}";
    }

    public static string GetDefaultBiome2()
    {
        if (_defaultBiomeId == null) return "T";
        return _defaultBiomeId.Contains('T') ? "T" : "R";
    }

    public static string GetDefaultBiome3()
    {
        if (_defaultBiomeId == null) return "M";
        return _defaultBiomeId.Contains('A') ? "A" : "M";
    }
    
    /// <summary>
    /// Resets cached default biome info (call when returning to airport).
    /// </summary>
    public static void ResetDefaultCache()
    {
        _defaultBiomeId = null;
        _defaultLevelIndex = -1;
    }
}