using VacationPlanner.Patches;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;

namespace VacationPlanner;

/// <summary>
/// Handles runtime biome voting via numpad keys.
/// Each player gets one vote per biome slot. Ties broken randomly.
/// </summary>
public class BiomeController : MonoBehaviour
{
    // Cached default biome info
    private static string? _defaultBiomeId;
    private static int _defaultLevelIndex = -1;
    
    // GUI styling
    private GUIStyle? _boxStyle;
    private GUIStyle? _labelStyle;
    private GUIStyle? _headerStyle;
    private GUIStyle? _voteStyle;
    private GUIStyle? _myVoteStyle;
    private GUIStyle? _winnerStyle;

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
        UpdateDefaultBiomeInfo();
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

    private void HandleKeyboardInput()
    {
        // In multiplayer, use voting system
        bool inRoom = PhotonNetwork.InRoom;
        
        // Numpad 1 or F5: Vote/Force Tropics (Jungle)
        if (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.F5))
        {
            if (inRoom)
            {
                NetworkSync.VoteForBiome2("T");
                Plugin.Log.LogInfo("Voted for: TROPICS (Jungle)");
            }
            else
            {
                SelectedBiome2 = "T";
                Plugin.Log.LogInfo("Biome 2 set to: TROPICS (Jungle)");
            }
        }

        // Numpad 2 or F6: Vote/Force Roots
        if (Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.F6))
        {
            if (inRoom)
            {
                NetworkSync.VoteForBiome2("R");
                Plugin.Log.LogInfo("Voted for: ROOTS");
            }
            else
            {
                SelectedBiome2 = "R";
                Plugin.Log.LogInfo("Biome 2 set to: ROOTS");
            }
        }

        // Numpad 4 or F7: Vote/Force Alpine (Snow)
        if (Input.GetKeyDown(KeyCode.Keypad4) || Input.GetKeyDown(KeyCode.F7))
        {
            if (inRoom)
            {
                NetworkSync.VoteForBiome3("A");
                Plugin.Log.LogInfo("Voted for: ALPINE (Snow)");
            }
            else
            {
                SelectedBiome3 = "A";
                Plugin.Log.LogInfo("Biome 3 set to: ALPINE (Snow)");
            }
        }

        // Numpad 5 or F8: Vote/Force Mesa (Desert)
        if (Input.GetKeyDown(KeyCode.Keypad5) || Input.GetKeyDown(KeyCode.F8))
        {
            if (inRoom)
            {
                NetworkSync.VoteForBiome3("M");
                Plugin.Log.LogInfo("Voted for: MESA (Desert)");
            }
            else
            {
                SelectedBiome3 = "M";
                Plugin.Log.LogInfo("Biome 3 set to: MESA (Desert)");
            }
        }

        // Numpad 0 or F9: Clear votes/selections
        if (Input.GetKeyDown(KeyCode.Keypad0) || Input.GetKeyDown(KeyCode.F9))
        {
            if (inRoom)
            {
                NetworkSync.ClearMyVotes();
                Plugin.Log.LogInfo("Cleared my votes");
            }
            else
            {
                LevelOverridePatch.ClearOverrides();
                Plugin.Log.LogInfo("Cleared biome selections");
            }
        }

        // Numpad Enter: Log current state
        if (Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Plugin.Log.LogInfo($"Current: {LevelOverridePatch.GetSelectionSummary()}");
            if (inRoom)
            {
                Plugin.Log.LogInfo($"Votes - T:{NetworkSync.VotesTropics} R:{NetworkSync.VotesRoots} | A:{NetworkSync.VotesAlpine} M:{NetworkSync.VotesMesa}");
            }
        }
    }

    private void OnGUI()
    {
        // Only show in Airport (lobby)
        if (SceneManager.GetActiveScene().name != "Airport") return;
        
        InitStyles();
        
        bool inRoom = PhotonNetwork.InRoom;
        
        // Position in top-left corner
        float boxWidth = 300;
        float boxHeight = inRoom ? 320 : 140;
        float margin = 10;
        
        Rect boxRect = new Rect(margin, margin, boxWidth, boxHeight);
        
        GUI.Box(boxRect, "", _boxStyle);
        
        GUILayout.BeginArea(new Rect(margin + 10, margin + 10, boxWidth - 20, boxHeight - 20));
        
        GUILayout.Label("BIOME SELECTION", _headerStyle);
        GUILayout.Space(3);
        
        // Default biome track (only show biomes 2 and 3)
        string defaultFriendly = BiomeIdToFriendly(_defaultBiomeId);
        GUILayout.Label($"Today's Default: {defaultFriendly}", _labelStyle);
        
        GUILayout.Space(5);
        
        if (inRoom)
        {
            // Voting UI
            GUILayout.Label("VOTES:", _headerStyle);
            
            // Biome 2 votes
            string? myB2 = NetworkSync.MyVoteBiome2;
            int vT = NetworkSync.VotesTropics;
            int vR = NetworkSync.VotesRoots;
            string defB2 = GetDefaultBiome2();
            bool tWin = vT > vR || (vT == vR && defB2 == "T");
            bool rWin = vR > vT || (vR == vT && defB2 == "R");

            string tMark = (myB2 == "T" ? " ←YOU" : "") + (tWin ? " ★" : "");
            string rMark = (myB2 == "R" ? " ←YOU" : "") + (rWin ? " ★" : "");
            
            GUILayout.Label($"  Tropics: {vT}{tMark}", tWin ? _winnerStyle : (myB2 == "T" ? _myVoteStyle : _voteStyle));
            GUILayout.Label($"  Roots:   {vR}{rMark}", rWin ? _winnerStyle : (myB2 == "R" ? _myVoteStyle : _voteStyle));
            
            GUILayout.Space(3);
            
            // Biome 3 votes
            string? myB3 = NetworkSync.MyVoteBiome3;
            int vA = NetworkSync.VotesAlpine;
            int vM = NetworkSync.VotesMesa;
            string defB3 = GetDefaultBiome3();
            bool aWin = vA > vM || (vA == vM && defB3 == "A");
            bool mWin = vM > vA || (vM == vA && defB3 == "M");

            string aMark = (myB3 == "A" ? " ←YOU" : "") + (aWin ? " ★" : "");
            string mMark = (myB3 == "M" ? " ←YOU" : "") + (mWin ? " ★" : "");
            
            GUILayout.Label($"  Alpine:  {vA}{aMark}", aWin ? _winnerStyle : (myB3 == "A" ? _myVoteStyle : _voteStyle));
            GUILayout.Label($"  Mesa:    {vM}{mMark}", mWin ? _winnerStyle : (myB3 == "M" ? _myVoteStyle : _voteStyle));
            
            GUILayout.Space(3);
            GUILayout.Label("(Ties use default)", _labelStyle);
        }
        else
        {
            // Solo mode - direct selection
            string currentBiome2 = SelectedBiome2 ?? GetDefaultBiome2();
            string currentBiome3 = SelectedBiome3 ?? GetDefaultBiome3();
            string biome2Name = currentBiome2 == "T" ? "Tropics" : "Roots";
            string biome3Name = currentBiome3 == "A" ? "Alpine" : "Mesa";
            
            bool hasOverride = SelectedBiome2 != null || SelectedBiome3 != null;
            string overrideText = hasOverride ? " [OVERRIDE]" : "";
            
            GUILayout.Label($"Current: {biome2Name} + {biome3Name}{overrideText}", _labelStyle);
        }
        
        GUILayout.Space(5);
        GUILayout.Label("Tropics: Num1 / F5   Roots: Num2 / F6", _labelStyle);
        GUILayout.Label("Alpine:  Num4 / F7   Mesa:  Num5 / F8", _labelStyle);
        GUILayout.Label("Clear:   Num0 / F9", _labelStyle);
        
        GUILayout.EndArea();
    }

    private void InitStyles()
    {
        if (_boxStyle != null) return;
        
        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f));
        
        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.normal.textColor = Color.white;
        _labelStyle.fontSize = 14;
        
        _headerStyle = new GUIStyle(_labelStyle);
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.fontSize = 16;
        _headerStyle.normal.textColor = new Color(1f, 0.9f, 0.5f);
        
        _voteStyle = new GUIStyle(_labelStyle);
        _voteStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
        
        _myVoteStyle = new GUIStyle(_labelStyle);
        _myVoteStyle.normal.textColor = new Color(0.5f, 1f, 0.5f);
        _myVoteStyle.fontStyle = FontStyle.Bold;

        _winnerStyle = new GUIStyle(_labelStyle);
        _winnerStyle.normal.textColor = new Color(1f, 0.84f, 0.0f); // Gold
        _winnerStyle.fontStyle = FontStyle.Bold;
        _winnerStyle.fontSize = 16; // Larger font
    }

    private static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private static string BiomeIdToFriendly(string? biomeId)
    {
        if (string.IsNullOrEmpty(biomeId)) return "Loading...";
        
        // Only show biome 2 (Tropics/Roots) and biome 3 (Alpine/Mesa)
        string biome2 = biomeId.Contains('T') ? "Tropics" : "Roots";
        string biome3 = biomeId.Contains('A') ? "Alpine" : "Mesa";
        
        return $"{biome2} + {biome3}";
    }

    private static string GetDefaultBiome2()
    {
        if (_defaultBiomeId == null) return "T";
        return _defaultBiomeId.Contains('T') ? "T" : "R";
    }

    private static string GetDefaultBiome3()
    {
        if (_defaultBiomeId == null) return "M";
        return _defaultBiomeId.Contains('A') ? "A" : "M";
    }

    /// <summary>
    /// Called by NetworkSync when vote results are finalized.
    /// </summary>
    public static void SetFromVoteResult(string? biome2, string? biome3)
    {
        Plugin.Log.LogInfo($"Applying vote result: Biome2={biome2 ?? "null"}, Biome3={biome3 ?? "null"}");
        SelectedBiome2 = string.IsNullOrEmpty(biome2) ? null : biome2;
        SelectedBiome3 = string.IsNullOrEmpty(biome3) ? null : biome3;
    }

    /// <summary>
    /// Called by NetworkSync when receiving biome selection from host (legacy).
    /// </summary>
    public static void SetFromNetwork(string? biome2, string? biome3)
    {
        SetFromVoteResult(biome2, biome3);
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