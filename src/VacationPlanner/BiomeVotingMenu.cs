using Photon.Pun;
using UnityEngine;
using VacationPlanner.Patches;

namespace VacationPlanner;

/// <summary>
/// A MenuWindow-based voting menu that properly integrates with the game's UI system.
/// Uses OnGUI for rendering but MenuWindow for cursor/input management.
/// </summary>
public class BiomeVotingMenu : MenuWindow
{
    public static BiomeVotingMenu? Instance { get; private set; }

    // GUI styling
    private GUIStyle? _boxStyle;
    private GUIStyle? _labelStyle;
    private GUIStyle? _headerStyle;
    private GUIStyle? _buttonStyle;
    private GUIStyle? _selectedButtonStyle;
    private GUIStyle? _voteStyle;
    private GUIStyle? _winnerStyle;

    // MenuWindow overrides - these control cursor and input blocking
    public override bool openOnStart => false;
    public override bool selectOnOpen => false;  // We don't use Unity UI selection
    public override bool closeOnPause => true;
    public override bool closeOnUICancel => true;
    public override bool autoHideOnClose => true;
    public override bool blocksPlayerInput => true;  // Freeze the player
    public override bool showCursorWhileOpen => true;  // Show the cursor!

    /// <summary>
    /// Creates the BiomeVotingMenu. Call once during scene load.
    /// </summary>
    public static BiomeVotingMenu Create()
    {
        if (Instance != null)
        {
            return Instance;
        }

        // Create a simple GameObject for our menu
        var menuGO = new GameObject("BiomeVotingMenu");
        var menu = menuGO.AddComponent<BiomeVotingMenu>();
        Instance = menu;
        
        // Start closed (don't call Open in Start)
        menu.StartClosed();
        
        Plugin.Log.LogInfo("BiomeVotingMenu created");
        return menu;
    }

    private void Awake()
    {
        Instance = this;
    }

    public override void OnOpen()
    {
        Plugin.Log.LogInfo("BiomeVotingMenu OnOpen - cursor should show now");
    }

    public override void OnClose()
    {
        Plugin.Log.LogInfo("BiomeVotingMenu OnClose");
    }

    private new void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnGUI()
    {
        // Only draw when open
        if (!isOpen) return;

        InitStyles();

        bool inRoom = PhotonNetwork.InRoom;

        // Center the box on screen
        float boxWidth = 400;
        float boxHeight = inRoom ? 380 : 280;
        float x = (Screen.width - boxWidth) / 2;
        float y = (Screen.height - boxHeight) / 2;

        GUI.Box(new Rect(x, y, boxWidth, boxHeight), "", _boxStyle);

        GUILayout.BeginArea(new Rect(x + 15, y + 15, boxWidth - 30, boxHeight - 30));

        GUILayout.Label("DESTINATION SELECTION", _headerStyle);
        GUILayout.Space(10);

        // Default biome info
        string defaultFriendly = BiomeController.BiomeIdToFriendly(BiomeController.DefaultBiomeId);
        GUILayout.Label($"Today's Default: {defaultFriendly}", _labelStyle);
        GUILayout.Space(15);

        // --- Biome 2 Section (Tropics/Roots) ---
        GUILayout.Label("JUNGLE REGION:", _labelStyle);
        GUILayout.BeginHorizontal();
        
        string? myB2 = inRoom ? NetworkSync.MyVoteBiome2 : BiomeController.SelectedBiome2;
        bool isTropicsSelected = myB2 == "T";
        bool isRootsSelected = myB2 == "R";
        
        if (GUILayout.Button("TROPICS", isTropicsSelected ? _selectedButtonStyle : _buttonStyle, GUILayout.Width(150), GUILayout.Height(35)))
        {
            if (inRoom) NetworkSync.VoteForBiome2("T");
            else BiomeController.SelectedBiome2 = "T";
        }
        
        if (GUILayout.Button("ROOTS", isRootsSelected ? _selectedButtonStyle : _buttonStyle, GUILayout.Width(150), GUILayout.Height(35)))
        {
            if (inRoom) NetworkSync.VoteForBiome2("R");
            else BiomeController.SelectedBiome2 = "R";
        }
        
        GUILayout.EndHorizontal();

        if (inRoom)
        {
            int vT = NetworkSync.VotesTropics;
            int vR = NetworkSync.VotesRoots;
            string defB2 = BiomeController.GetDefaultBiome2();
            bool tWin = vT > vR || (vT == vR && defB2 == "T");
            string winnerB2 = tWin ? "Tropics" : "Roots";
            GUILayout.Label($"  Votes: Tropics {vT} | Roots {vR}  →  Winner: {winnerB2}", tWin && vT > 0 || !tWin && vR > 0 ? _winnerStyle : _voteStyle);
        }

        GUILayout.Space(15);

        // --- Biome 3 Section (Alpine/Mesa) ---
        GUILayout.Label("MOUNTAIN REGION:", _labelStyle);
        GUILayout.BeginHorizontal();
        
        string? myB3 = inRoom ? NetworkSync.MyVoteBiome3 : BiomeController.SelectedBiome3;
        bool isAlpineSelected = myB3 == "A";
        bool isMesaSelected = myB3 == "M";
        
        if (GUILayout.Button("ALPINE", isAlpineSelected ? _selectedButtonStyle : _buttonStyle, GUILayout.Width(150), GUILayout.Height(35)))
        {
            if (inRoom) NetworkSync.VoteForBiome3("A");
            else BiomeController.SelectedBiome3 = "A";
        }
        
        if (GUILayout.Button("MESA", isMesaSelected ? _selectedButtonStyle : _buttonStyle, GUILayout.Width(150), GUILayout.Height(35)))
        {
            if (inRoom) NetworkSync.VoteForBiome3("M");
            else BiomeController.SelectedBiome3 = "M";
        }
        
        GUILayout.EndHorizontal();

        if (inRoom)
        {
            int vA = NetworkSync.VotesAlpine;
            int vM = NetworkSync.VotesMesa;
            string defB3 = BiomeController.GetDefaultBiome3();
            bool aWin = vA > vM || (vA == vM && defB3 == "A");
            string winnerB3 = aWin ? "Alpine" : "Mesa";
            GUILayout.Label($"  Votes: Alpine {vA} | Mesa {vM}  →  Winner: {winnerB3}", aWin && vA > 0 || !aWin && vM > 0 ? _winnerStyle : _voteStyle);
        }

        GUILayout.Space(20);

        // Clear and Close buttons
        GUILayout.BeginHorizontal();
        
        if (GUILayout.Button("CLEAR", _buttonStyle, GUILayout.Width(150), GUILayout.Height(30)))
        {
            if (inRoom) NetworkSync.ClearMyVotes();
            else LevelOverridePatch.ClearOverrides();
        }
        
        if (GUILayout.Button("CLOSE", _buttonStyle, GUILayout.Width(150), GUILayout.Height(30)))
        {
            Close();
        }
        
        GUILayout.EndHorizontal();

        GUILayout.Space(10);
        GUILayout.Label("Press ESC to close", _voteStyle);

        GUILayout.EndArea();
    }

    private void InitStyles()
    {
        if (_boxStyle != null) return;

        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = MakeTex(2, 2, new Color(0.08f, 0.06f, 0.04f, 0.95f));

        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.normal.textColor = Color.white;
        _labelStyle.fontSize = 16;

        _headerStyle = new GUIStyle(_labelStyle);
        _headerStyle.fontStyle = FontStyle.Bold;
        _headerStyle.fontSize = 22;
        _headerStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
        _headerStyle.alignment = TextAnchor.MiddleCenter;

        _buttonStyle = new GUIStyle(GUI.skin.button);
        _buttonStyle.fontSize = 16;
        _buttonStyle.fontStyle = FontStyle.Bold;
        _buttonStyle.normal.textColor = Color.white;

        _selectedButtonStyle = new GUIStyle(_buttonStyle);
        _selectedButtonStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.5f, 0.2f, 1f));
        _selectedButtonStyle.normal.textColor = Color.yellow;

        _voteStyle = new GUIStyle(_labelStyle);
        _voteStyle.fontSize = 14;
        _voteStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

        _winnerStyle = new GUIStyle(_labelStyle);
        _winnerStyle.fontSize = 14;
        _winnerStyle.fontStyle = FontStyle.Bold;
        _winnerStyle.normal.textColor = new Color(1f, 0.84f, 0f);
    }

    private static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
