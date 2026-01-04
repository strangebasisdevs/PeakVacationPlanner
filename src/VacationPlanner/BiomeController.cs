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
    
    // Cached material and texture for performance
    private static Material? _cachedSignMaterial = null;
    private static bool _materialSearchComplete = false;
    private static Texture2D? _cachedCustomTexture = null;

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
        
        // Press F10 to dump all textures in the scene
        if (Input.GetKeyDown(KeyCode.F10))
        {
            DumpAllSceneTextures();
        }
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
            // Do discovery on the ORIGINAL kiosk (once)
            if (!_materialSearchComplete)
            {
                FindAndCacheSignMaterial(inviteKiosk.gameObject, "Add Player Kiosk@4x");
                _materialSearchComplete = true;
            }
            
            // Clone the invite friends kiosk (simple, not networked)
            var kioskGO = Instantiate(inviteKiosk.gameObject);
            kioskGO.name = "BiomeVotingKiosk";
            
            // Position it near the check-in kiosk
            kioskGO.transform.position = checkInKiosk.transform.position + checkInKiosk.transform.right * 3.0f;
            kioskGO.transform.rotation = checkInKiosk.transform.rotation;
            
            // Scale up by 2.3x
            kioskGO.transform.localScale = inviteKiosk.transform.localScale * 2.3f;
            
            // Replace texture using cached reference (fast!)
            if (_cachedSignMaterial != null)
            {
                ReplaceTextureOnClone(kioskGO);
            }
            
            // Remove the invite friends script from the clone
            var oldScript = kioskGO.GetComponent<AirportInviteFriendsKiosk>();
            if (oldScript != null)
            {
                Destroy(oldScript);
            }
            
            // Add our kiosk script
            kioskGO.AddComponent<BiomeVotingKiosk>();
            
            _spawnedKiosk = kioskGO;
            Plugin.Log.LogInfo("BiomeVotingKiosk spawned in Airport (scaled 2.3x)!");
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
    
    // --- Texture Replacement Methods ---
    
    private void FindAndCacheSignMaterial(GameObject originalKiosk, string targetTextureName)
    {
        Plugin.Log.LogInfo("Searching for sign material (one-time discovery)...");
        
        var renderers = originalKiosk.GetComponentsInChildren<MeshRenderer>();
        
        Plugin.Log.LogInfo($"Found {renderers.Length} renderers in kiosk");
        
        foreach (var renderer in renderers)
        {
            Plugin.Log.LogInfo($"Renderer: {renderer.name} has {renderer.sharedMaterials.Length} materials");
            
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null)
                {
                    Plugin.Log.LogInfo($"  Material: {material.name}, Shader: {material.shader.name}");
                    Plugin.Log.LogInfo($"    mainTexture: {material.mainTexture?.name ?? "null"}");
                    
                    // Check ALL texture properties on the shader
                    var shader = material.shader;
                    int propertyCount = shader.GetPropertyCount();
                    
                    for (int i = 0; i < propertyCount; i++)
                    {
                        var propType = shader.GetPropertyType(i);
                        if (propType == UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            string propName = shader.GetPropertyName(i);
                            Texture tex = material.GetTexture(propName);
                            Plugin.Log.LogInfo($"    [{propName}]: {tex?.name ?? "null"}");
                            
                            // Check if this is our target texture
                            if (tex != null && tex.name == targetTextureName)
                            {
                                _cachedSignMaterial = material;
                                _cachedTexturePropertyName = propName; // Store which property it's on
                                Plugin.Log.LogInfo($"✓ FOUND: Material '{material.name}' property '{propName}' = '{targetTextureName}'");
                                return;
                            }
                        }
                    }
                }
            }
        }
        
        Plugin.Log.LogWarning($"Could not find material with texture '{targetTextureName}'");
    }
    
    private static string? _cachedTexturePropertyName = null;

    private void ReplaceTextureOnClone(GameObject clonedKiosk)
    {
        if (_cachedSignMaterial == null || _cachedTexturePropertyName == null) return;
        
        var renderers = clonedKiosk.GetComponentsInChildren<MeshRenderer>();
        
        foreach (var renderer in renderers)
        {
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                if (renderer.materials[i].shader == _cachedSignMaterial.shader &&
                    renderer.materials[i].name.Contains(_cachedSignMaterial.name.Replace(" (Instance)", "")))
                {
                    Texture2D? customTexture = LoadCustomTexture();
                    
                    if (customTexture != null)
                    {
                        // Use the cached property name instead of mainTexture
                        renderer.materials[i].SetTexture(_cachedTexturePropertyName, customTexture);
                        Plugin.Log.LogInfo($"Texture replaced on clone (property: {_cachedTexturePropertyName})!");
                    }
                    return;
                }
            }
        }
    }
    
    private Texture2D? LoadCustomTexture()
    {
        if (_cachedCustomTexture != null)
            return _cachedCustomTexture;
        
        // Load from BepInEx/plugins/VacationPlanner/custom_sign.png
        string filePath = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "VacationPlanner", "custom_sign.png");
        
        if (System.IO.File.Exists(filePath))
        {
            byte[] fileData = System.IO.File.ReadAllBytes(filePath);
            _cachedCustomTexture = new Texture2D(2, 2);
            _cachedCustomTexture.LoadImage(fileData);
            Plugin.Log.LogInfo($"Custom texture loaded and cached from {filePath}");
            return _cachedCustomTexture;
        }
        
        Plugin.Log.LogError($"Could not find custom texture at {filePath}");
        return null;
    }

    private void DumpAllSceneTextures()
    {
        Plugin.Log.LogInfo("=== DUMPING ALL SCENE TEXTURES ===");
        
        // Find all renderers in the entire scene
        var allRenderers = Object.FindObjectsOfType<MeshRenderer>();
        
        Plugin.Log.LogInfo($"Found {allRenderers.Length} renderers in scene");
        
        foreach (var renderer in allRenderers)
        {
            Plugin.Log.LogInfo($"\nGameObject: {GetFullPath(renderer.gameObject)}");
            
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null)
                {
                    Plugin.Log.LogInfo($"  Material: {material.name}");
                    Plugin.Log.LogInfo($"    Shader: {material.shader.name}");
                    
                    // Check all texture properties
                    var shader = material.shader;
                    for (int i = 0; i < shader.GetPropertyCount(); i++)
                    {
                        if (shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            string propName = shader.GetPropertyName(i);
                            Texture tex = material.GetTexture(propName);
                            if (tex != null)
                            {
                                Plugin.Log.LogInfo($"      [{propName}]: {tex.name} ({tex.width}x{tex.height})");
                           }
                        }
                    }
                }
            }
        }
    
        Plugin.Log.LogInfo("=== DUMP COMPLETE ===");
    }

// Helper to get full path in hierarchy
    private string GetFullPath(GameObject obj)
    {
        string path = obj.name;
        Transform parent = obj.transform.parent;
        
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        
        return path;
    }
}