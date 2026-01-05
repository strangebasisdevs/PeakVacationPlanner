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
    
    // Cached material and texture for kiosk
    private static Material? _cachedSignMaterial = null;
    private static bool _materialSearchComplete = false;
    private static Texture2D? _cachedCustomTexture = null;
    
    // Stored placement information from numpad0 logging
    private static Vector3 _lastLoggedHitPoint = Vector3.zero;
    private static Vector3 _lastLoggedHitNormal = Vector3.up;
    private static bool _hasLoggedPosition = false;

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
        
        // // Uncomment the following block to enable overlay adjustment controls and object logging.
        // // Specify which overlay to adjust (change this string to adjust different overlays)
        // string currentOverlayName = "VotingInfoOverlay";
        
        // // Press Numpad0 to log screen position for overlay placement
        // if (Input.GetKeyDown(KeyCode.Keypad0))
        // {
        //     LogScreenPositionForOverlay();
        // }
        
        // // Press Numpad1 to place overlay at last logged position
        // if (Input.GetKeyDown(KeyCode.Keypad1))
        // {
        //     PlaceOverlayAtLoggedPosition(currentOverlayName);
        // }
        
        // // Adjust overlay size with numpad keys
        // if (Input.GetKeyDown(KeyCode.KeypadPlus))
        // {
        //     AdjustOverlayScale(currentOverlayName, 0.05f, 0f); // Wider
        // }
        // if (Input.GetKeyDown(KeyCode.KeypadMinus))
        // {
        //     AdjustOverlayScale(currentOverlayName, -0.05f, 0f); // Narrower
        // }
        // if (Input.GetKeyDown(KeyCode.KeypadMultiply))
        // {
        //     AdjustOverlayScale(currentOverlayName, 0f, 0.05f); // Taller
        // }
        // if (Input.GetKeyDown(KeyCode.KeypadDivide))
        // {
        //     AdjustOverlayScale(currentOverlayName, 0f, -0.05f); // Shorter
        // }
        
        // // Adjust overlay position with arrow keys and page up/down
        // float moveSpeed = 0.01f; // Adjust this value to change movement speed (in world units per key press)
        
        // if (Input.GetKeyDown(KeyCode.LeftArrow))
        // {
        //     AdjustOverlayPosition(currentOverlayName, -moveSpeed, 0f, 0f); // Move left (negative X)
        // }
        // if (Input.GetKeyDown(KeyCode.RightArrow))
        // {
        //     AdjustOverlayPosition(currentOverlayName, moveSpeed, 0f, 0f); // Move right (positive X)
        // }
        // if (Input.GetKeyDown(KeyCode.UpArrow))
        // {
        //     AdjustOverlayPosition(currentOverlayName, 0f, 0f, moveSpeed); // Move forward (positive Z)
        // }
        // if (Input.GetKeyDown(KeyCode.DownArrow))
        // {
        //     AdjustOverlayPosition(currentOverlayName, 0f, 0f, -moveSpeed); // Move backward (negative Z)
        // }
        // if (Input.GetKeyDown(KeyCode.PageUp))
        // {
        //     AdjustOverlayPosition(currentOverlayName, 0f, moveSpeed, 0f); // Move up (positive Y)
        // }
        // if (Input.GetKeyDown(KeyCode.PageDown))
        // {
        //     AdjustOverlayPosition(currentOverlayName, 0f, -moveSpeed, 0f); // Move down (negative Y)
        // }
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
            kioskGO.transform.position = checkInKiosk.transform.position + checkInKiosk.transform.up * 2.262f;
            kioskGO.transform.rotation = checkInKiosk.transform.rotation;
            
            // Scale up by 2.3x
            kioskGO.transform.localScale = inviteKiosk.transform.localScale * 2.2f;
            
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
            
            // Display world-space overlay covering the airport sign
            DisplayWorldSpaceOverlay();
        }
        else if (checkInKiosk != null)
        {
            // Fallback: create primitive if invite kiosk not found
            var kioskGO = new GameObject("BiomeVotingKiosk");
            kioskGO.transform.position = checkInKiosk.transform.position - checkInKiosk.transform.forward * 3.0f;
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
    /// Updates the world space overlay texture to reflect current biome selections.
    /// Call this when biome selections change.
    /// </summary>
    public static void UpdateWorldSpaceOverlay()
    {
        // Find the overlay and update its texture
        GameObject overlayGO = GameObject.Find("BiomeWorldOverlay");
        if (overlayGO != null)
        {
            var renderer = overlayGO.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Texture2D? overlayTexture = LoadOverlayTexture();
                if (overlayTexture != null)
                {
                    renderer.material.mainTexture = overlayTexture;
                    renderer.material.SetTexture("_BaseMap", overlayTexture);
                    Plugin.Log.LogInfo("Updated overlay texture to reflect new biome selection");
                }
            }
        }
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
        
        // Load from BepInEx/plugins/strangebasisdevs-VacationPlanner/kiosk_texture.png
        string filePath = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "strangebasisdevs-VacationPlanner", "kiosk_texture.png");
        
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
        var allRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        
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
    
    // --- Screen Overlay Methods ---
    
    private void LogScreenPositionForOverlay()
    {
        // Get mouse position in screen coordinates
        Vector3 mousePos = Input.mousePosition;
        
        // Convert to viewport coordinates (0-1 range)
        Vector3 viewportPos = Camera.main.ScreenToViewportPoint(mousePos);
        
        Plugin.Log.LogInfo($"=== SCREEN POSITION LOG ===");
        Plugin.Log.LogInfo($"Mouse Screen Position: {mousePos.x:F0}, {mousePos.y:F0}");
        Plugin.Log.LogInfo($"Viewport Position: {viewportPos.x:F3}, {viewportPos.y:F3}");
        Plugin.Log.LogInfo($"Screen Resolution: {Screen.width}x{Screen.height}");
        Plugin.Log.LogInfo($"For overlay rectangle, use viewport coordinates:");
        Plugin.Log.LogInfo($"  new Rect({viewportPos.x:F3}f, {viewportPos.y:F3}f, width, height)");
        Plugin.Log.LogInfo($"===========================");
        
        // Also do a world raycast to get 3D placement information
        LogWorldPlacementInfo();
    }
    
    private void LogWorldPlacementInfo()
    {
        // Cast a ray from camera through mouse position
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // Store the hit information for later use
            _lastLoggedHitPoint = hit.point;
            _lastLoggedHitNormal = hit.normal;
            _hasLoggedPosition = true;
            
            Plugin.Log.LogInfo($"=== WORLD PLACEMENT INFO ===");
            Plugin.Log.LogInfo($"Hit Point: ({hit.point.x:F3}, {hit.point.y:F3}, {hit.point.z:F3})");
            Plugin.Log.LogInfo($"Hit Normal: ({hit.normal.x:F3}, {hit.normal.y:F3}, {hit.normal.z:F3})");
            Plugin.Log.LogInfo($"Hit Distance: {hit.distance:F3}");
            Plugin.Log.LogInfo($"Hit GameObject: {GetFullPath(hit.collider.gameObject)}");
            Plugin.Log.LogInfo($"Hit Material: {hit.collider?.GetComponent<Renderer>()?.material?.name ?? "N/A"}");
            
            // Calculate rotation to face the normal
            Quaternion surfaceRotation = Quaternion.LookRotation(-hit.normal);
            Plugin.Log.LogInfo($"Suggested Rotation (faces surface): ({surfaceRotation.eulerAngles.x:F1}, {surfaceRotation.eulerAngles.y:F1}, {surfaceRotation.eulerAngles.z:F1})");
            
            // For sign placement, we might want it to face the camera instead
            Vector3 toCamera = Camera.main.transform.position - hit.point;
            Quaternion cameraFacingRotation = Quaternion.LookRotation(-toCamera.normalized);
            Plugin.Log.LogInfo($"Suggested Rotation (faces camera): ({cameraFacingRotation.eulerAngles.x:F1}, {cameraFacingRotation.eulerAngles.y:F1}, {cameraFacingRotation.eulerAngles.z:F1})");
            
            // Suggested transform code
            Plugin.Log.LogInfo($"=== SUGGESTED CODE ===");
            Plugin.Log.LogInfo($"transform.position = new Vector3({hit.point.x:F3}f, {hit.point.y:F3}f, {hit.point.z:F3}f);");
            Plugin.Log.LogInfo($"transform.rotation = Quaternion.Euler({surfaceRotation.eulerAngles.x:F1}f, {surfaceRotation.eulerAngles.y:F1}f, {surfaceRotation.eulerAngles.z:F1}f);");
            Plugin.Log.LogInfo($"// Or for camera-facing:");
            Plugin.Log.LogInfo($"transform.rotation = Quaternion.Euler({cameraFacingRotation.eulerAngles.x:F1}f, {cameraFacingRotation.eulerAngles.y:F1}f, {cameraFacingRotation.eulerAngles.z:F1}f);");
            Plugin.Log.LogInfo($"Press NUMPAD1 to place overlay at this position!");
            Plugin.Log.LogInfo($"===========================");
        }
        else
        {
            Plugin.Log.LogInfo("No world hit detected - raycast didn't hit anything");
        }
    }
    
    // Call this method to display a 3D world-space overlay covering the airport sign
    private void DisplayWorldSpaceOverlay()
    {
        // Create or find the overlay quad
        GameObject overlayGO = GameObject.Find("BiomeWorldOverlay");
        bool wasExisting = overlayGO != null;
        if (overlayGO == null)
        {
            overlayGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlayGO.name = "BiomeWorldOverlay";
            
            // Remove the default collider since we don't need interaction
            var collider = overlayGO.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
        
        // Position the overlay to cover the sign
        // overlayGO.transform.position = originalSign.transform.position + originalSign.transform.forward * 0.01f; // Slightly in front
        // overlayGO.transform.rotation = originalSign.transform.rotation;
        // overlayGO.transform.localScale = originalSign.transform.localScale * 1.1f; // Slightly larger to cover completely
        overlayGO.transform.position = new Vector3(1.807f, 5.136f, 103.626f);
        overlayGO.transform.rotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
        overlayGO.transform.localScale = new Vector3(5.700f, 0.750f, 1.000f);
        
        // Apply texture to the quad
        var renderer = overlayGO.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Texture2D? overlayTexture = LoadOverlayTexture();
            if (overlayTexture != null)
            {
                // If overlay already exists, just update the texture
                if (wasExisting)
                {
                    renderer.material.mainTexture = overlayTexture;
                    renderer.material.SetTexture("_BaseMap", overlayTexture);
                    Plugin.Log.LogInfo("Updated existing overlay texture");
                }
                else
                {
                    // Create a material with the texture
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.mainTexture = overlayTexture;
                    material.SetTexture("_BaseMap", overlayTexture);
                    
                    // Make it unlit and semi-transparent if desired
                    material.SetFloat("_Surface", 0); // Opaque
                    material.SetFloat("_Blend", 0); // Alpha blend
                    
                    renderer.material = material;
                }
            }
            else
            {
                // Fallback: colored material
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(1f, 0.5f, 0f, 0.9f); // Orange semi-transparent
                renderer.material = material;
            }
        }
        
        Plugin.Log.LogInfo($"World space overlay created covering airport sign at {overlayGO.transform.position}");
    }
    
    private static Texture2D? LoadOverlayTexture()
    {
        // Determine current biome selections
        string biome2 = LevelOverridePatch.DesiredBiome2?.ToUpper() ?? GetDefaultBiome2();
        string biome3 = LevelOverridePatch.DesiredBiome3?.ToUpper() ?? GetDefaultBiome3();
        
        // Build filename: tropics_alpine_sign.png, etc.
        string region1 = biome2 == "T" ? "tropics" : "roots";
        string region2 = biome3 == "A" ? "alpine" : "mesa";
        string fileName = $"{region1}_{region2}_sign.png";
        
        // Load from BepInEx/plugins/strangebasisdevs-VacationPlanner/
        string filePath = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "strangebasisdevs-VacationPlanner", fileName);
        
        if (System.IO.File.Exists(filePath))
        {
            byte[] fileData = System.IO.File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            Plugin.Log.LogInfo($"Overlay texture loaded from {filePath}");
            return texture;
        }
        
        Plugin.Log.LogWarning($"Could not find overlay texture at {filePath}");
        return null;
    }
    
    private void PlaceOverlayAtLoggedPosition(string overlayName)
    {
        if (!_hasLoggedPosition)
        {
            Plugin.Log.LogWarning("No logged position available. Press NUMPAD0 first to log a position.");
            return;
        }
        
        // Create or find the overlay quad
        GameObject overlayGO = GameObject.Find(overlayName);
        if (overlayGO == null)
        {
            overlayGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlayGO.name = overlayName;
            
            // Remove the default collider since we don't need interaction
            var collider = overlayGO.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }
        
        // Position at the logged hit point with a small offset along the normal
        overlayGO.transform.position = _lastLoggedHitPoint + _lastLoggedHitNormal * 0.01f;
        
        // Rotate to face the surface normal
        overlayGO.transform.rotation = Quaternion.LookRotation(-_lastLoggedHitNormal);
        
        // Scale it appropriately (you can adjust this)
        overlayGO.transform.localScale = new Vector3(1f, 1f, 1f);
        
        // Apply texture to the quad
        var renderer = overlayGO.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Texture2D? overlayTexture = LoadOverlayTexture();
            if (overlayTexture != null)
            {
                // Create a material with the texture
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.mainTexture = overlayTexture;
                material.SetTexture("_BaseMap", overlayTexture);
                
                // Make it unlit and semi-transparent if desired
                material.SetFloat("_Surface", 0); // Opaque
                material.SetFloat("_Blend", 0); // Alpha blend
                
                renderer.material = material;
            }
            else
            {
                // Fallback: colored material
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(1f, 0.5f, 0f, 0.9f); // Orange semi-transparent
                renderer.material = material;
            }
        }
        
        Plugin.Log.LogInfo($"Overlay '{overlayName}' placed at logged position: {_lastLoggedHitPoint}, normal: {_lastLoggedHitNormal}");
        
        // Log the initial transform
        LogCurrentOverlayTransform(overlayGO.transform);
    }
    
    private void AdjustOverlayScale(string overlayName, float widthDelta, float heightDelta)
    {
        GameObject overlayGO = GameObject.Find(overlayName);
        if (overlayGO == null)
        {
            Plugin.Log.LogWarning($"No overlay '{overlayName}' to adjust. Place one first with NUMPAD1.");
            return;
        }
        
        // Adjust the scale
        Vector3 currentScale = overlayGO.transform.localScale;
        Vector3 newScale = new Vector3(
            Mathf.Max(0.1f, currentScale.x + widthDelta),  // Minimum width 0.1
            Mathf.Max(0.1f, currentScale.y + heightDelta),  // Minimum height 0.1
            currentScale.z  // Keep depth the same
        );
        
        overlayGO.transform.localScale = newScale;
        
        // Log the current transform data
        LogCurrentOverlayTransform(overlayGO.transform);
    }
    
    private void AdjustOverlayPosition(string overlayName, float deltaX, float deltaY, float deltaZ)
    {
        GameObject overlayGO = GameObject.Find(overlayName);
        if (overlayGO == null)
        {
            Plugin.Log.LogWarning($"No overlay '{overlayName}' to adjust. Place one first with NUMPAD1.");
            return;
        }
        
        // Adjust the position
        Vector3 currentPosition = overlayGO.transform.position;
        Vector3 newPosition = currentPosition + new Vector3(deltaX, deltaY, deltaZ);
        
        overlayGO.transform.position = newPosition;
        
        // Log the current transform data
        LogCurrentOverlayTransform(overlayGO.transform);
    }
    
    private void LogCurrentOverlayTransform(Transform transform)
    {
        Plugin.Log.LogInfo($"=== OVERLAY TRANSFORM ===");
        Plugin.Log.LogInfo($"Position: ({transform.position.x:F3}, {transform.position.y:F3}, {transform.position.z:F3})");
        Plugin.Log.LogInfo($"Rotation: ({transform.rotation.eulerAngles.x:F1}, {transform.rotation.eulerAngles.y:F1}, {transform.rotation.eulerAngles.z:F1})");
        Plugin.Log.LogInfo($"Scale: ({transform.localScale.x:F3}, {transform.localScale.y:F3}, {transform.localScale.z:F3})");
        Plugin.Log.LogInfo($"=== READY-TO-USE CODE ===");
        Plugin.Log.LogInfo($"overlayGO.transform.position = new Vector3({transform.position.x:F3}f, {transform.position.y:F3}f, {transform.position.z:F3}f);");
        Plugin.Log.LogInfo($"overlayGO.transform.rotation = Quaternion.Euler({transform.rotation.eulerAngles.x:F1}f, {transform.rotation.eulerAngles.y:F1}f, {transform.rotation.eulerAngles.z:F1}f);");
        Plugin.Log.LogInfo($"overlayGO.transform.localScale = new Vector3({transform.localScale.x:F3}f, {transform.localScale.y:F3}f, {transform.localScale.z:F3}f);");
        Plugin.Log.LogInfo($"===========================");
    }
}