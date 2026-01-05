using Photon.Pun;
using UnityEngine;
using VacationPlanner.Patches;
using Zorro.Core;
using Zorro.ControllerSupport;

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
    private Texture2D? _borderTexture;

    // Controller navigation
    private int _selectedRow = 0;
    private int _selectedCol = 0;
    private float _navigationCooldown = 0f;
    private const float NavigationDelay = 0.15f;
    private InputScheme _currentInputScheme = InputScheme.KeyboardMouse;

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
        _currentInputScheme = InputHandler.GetCurrentUsedInputScheme();
        var inputHandler = RetrievableResourceSingleton<InputHandler>.Instance;
        inputHandler.InputSchemeChanged += OnInputSchemeChanged;
    }

    public override void OnOpen()
    {
        Plugin.Log.LogInfo("BiomeVotingMenu OnOpen - cursor should show now");
        _selectedRow = 0;
        _selectedCol = 0;
        _navigationCooldown = 0f;
    }

    public override void OnClose()
    {
        Plugin.Log.LogInfo("BiomeVotingMenu OnClose");
    }

    private new void Update()
    {
        base.Update(); // Call base Update first
        if (!isOpen || !inputActive) return;

        HandleControllerInput();
    }

    private void HandleControllerInput()
    {
        var uiHandler = Singleton<UIInputHandler>.Instance;
        if (uiHandler == null) return;

        // Handle navigation
        Vector2 nav = uiHandler.wheelNavigationVector;
        _navigationCooldown -= Time.deltaTime;

        if (_navigationCooldown <= 0f && nav != Vector2.zero)
        {
            if (Mathf.Abs(nav.x) > Mathf.Abs(nav.y))
            {
                // Horizontal movement
                if (nav.x > 0.5f)
                {
                    _selectedCol = (_selectedCol + 1) % 2; // Move right
                    _navigationCooldown = NavigationDelay;
                }
                else if (nav.x < -0.5f)
                {
                    _selectedCol = (_selectedCol - 1 + 2) % 2; // Move left, wrap around
                    _navigationCooldown = NavigationDelay;
                }
            }
            else
            {
                // Vertical movement
                if (nav.y > 0.5f)
                {
                    _selectedRow = Mathf.Max(0, _selectedRow - 1);
                    _navigationCooldown = NavigationDelay;
                }
                else if (nav.y < -0.5f)
                {
                    _selectedRow = Mathf.Min(2, _selectedRow + 1); // 3 rows
                    _navigationCooldown = NavigationDelay;
                }
            }
        }

        // Handle confirm
        if (uiHandler.confirmWasPressed)
        {
            ExecuteSelectedButton();
            uiHandler.confirmWasPressed = false; // Consume the input
        }
    }

    private void ExecuteSelectedButton()
    {
        bool inRoom = PhotonNetwork.InRoom;

        switch (_selectedRow)
        {
            case 0: // Jungle region
                if (_selectedCol == 0) // Tropics
                {
                    if (inRoom) NetworkSync.VoteForBiome2("T");
                    else 
                    {
                        BiomeController.SelectedBiome2 = "T";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                else // Roots
                {
                    if (inRoom) NetworkSync.VoteForBiome2("R");
                    else 
                    {
                        BiomeController.SelectedBiome2 = "R";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                break;
            case 1: // Mountain region
                if (_selectedCol == 0) // Alpine
                {
                    if (inRoom) NetworkSync.VoteForBiome3("A");
                    else 
                    {
                        BiomeController.SelectedBiome3 = "A";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                else // Mesa
                {
                    if (inRoom) NetworkSync.VoteForBiome3("M");
                    else 
                    {
                        BiomeController.SelectedBiome3 = "M";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                break;
            case 2: // Bottom row
                if (_selectedCol == 0) // Clear
                {
                    if (inRoom) NetworkSync.ClearMyVotes();
                    else 
                    {
                        LevelOverridePatch.ClearOverrides();
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                else // Close
                {
                    Close();
                }
                break;
        }
    }

    private GUIStyle GetButtonStyle(bool isVoted)
    {
        return isVoted ? _selectedButtonStyle! : _buttonStyle!;
    }

    private void OnInputSchemeChanged(InputScheme scheme)
    {
        _currentInputScheme = scheme;
    }

    private new void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
        {
            Instance = null;
        }
        var inputHandler = RetrievableResourceSingleton<InputHandler>.Instance;
        inputHandler.InputSchemeChanged -= OnInputSchemeChanged;
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

        // Content area
        float contentX = x + 15;
        float contentY = y + 15;
        float contentWidth = boxWidth - 30;

        // Header
        GUI.Label(new Rect(contentX, contentY, contentWidth, 30), "DESTINATION SELECTION", _headerStyle);
        contentY += 40;

        // Default biome
        string defaultFriendly = BiomeController.BiomeIdToFriendly(BiomeController.DefaultBiomeId);
        GUI.Label(new Rect(contentX, contentY, contentWidth, 20), $"Today's Default: {defaultFriendly}", _labelStyle);
        contentY += 35;

        // Jungle region
        GUI.Label(new Rect(contentX, contentY, contentWidth, 20), "JUNGLE REGION:", _labelStyle);
        contentY += 25;

        string? myB2 = inRoom ? NetworkSync.MyVoteBiome2 : BiomeController.SelectedBiome2;
        bool isTropicsSelected = myB2 == "T";
        bool isRootsSelected = myB2 == "R";

        Rect tropicsRect = new Rect(contentX, contentY, 150, 35);
        Rect rootsRect = new Rect(contentX + 160, contentY, 150, 35);

        bool tropicsHover = tropicsRect.Contains(Event.current.mousePosition);
        bool rootsHover = rootsRect.Contains(Event.current.mousePosition);

if (GUI.Button(tropicsRect, "TROPICS", GetButtonStyle(isTropicsSelected)))
        {
            if (inRoom) NetworkSync.VoteForBiome2("T");
            else 
            {
                BiomeController.SelectedBiome2 = "T";
                BiomeController.UpdateWorldSpaceOverlay();
            }
        }

        bool tropicsControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 0 && _selectedCol == 0;
        if (tropicsControllerSelected || (tropicsHover && !isTropicsSelected)) GUI.DrawTexture(tropicsRect, _borderTexture);

        if (GUI.Button(rootsRect, "ROOTS", GetButtonStyle(isRootsSelected)))
        {
            if (inRoom) NetworkSync.VoteForBiome2("R");
            else 
            {
                BiomeController.SelectedBiome2 = "R";
                BiomeController.UpdateWorldSpaceOverlay();
            }
        }

        bool rootsControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 0 && _selectedCol == 1;
        if (rootsControllerSelected || (rootsHover && !isRootsSelected)) GUI.DrawTexture(rootsRect, _borderTexture);

        contentY += 40;

        if (inRoom)
        {
            int vT = NetworkSync.VotesTropics;
            int vR = NetworkSync.VotesRoots;
            string defB2 = BiomeController.GetDefaultBiome2();
            bool tWin = vT > vR || (vT == vR && defB2 == "T");
            string winnerB2 = tWin ? "Tropics" : "Roots";
            GUI.Label(new Rect(contentX, contentY, contentWidth, 20), $"  Votes: Tropics {vT} | Roots {vR}  →  Winner: {winnerB2}", tWin && vT > 0 || !tWin && vR > 0 ? _winnerStyle : _voteStyle);
            contentY += 25;
        }
        else
        {
            contentY += 15;
        }

        // Mountain region
        GUI.Label(new Rect(contentX, contentY, contentWidth, 20), "MOUNTAIN REGION:", _labelStyle);
        contentY += 25;

        string? myB3 = inRoom ? NetworkSync.MyVoteBiome3 : BiomeController.SelectedBiome3;
        bool isAlpineSelected = myB3 == "A";
        bool isMesaSelected = myB3 == "M";

        Rect alpineRect = new Rect(contentX, contentY, 150, 35);
        Rect mesaRect = new Rect(contentX + 160, contentY, 150, 35);

        bool alpineHover = alpineRect.Contains(Event.current.mousePosition);
        bool mesaHover = mesaRect.Contains(Event.current.mousePosition);

if (GUI.Button(alpineRect, "ALPINE", GetButtonStyle(isAlpineSelected)))
        {
            if (inRoom) NetworkSync.VoteForBiome3("A");
            else 
            {
                BiomeController.SelectedBiome3 = "A";
                BiomeController.UpdateWorldSpaceOverlay();
            }
        }

        bool alpineControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 1 && _selectedCol == 0;
        if (alpineControllerSelected || (alpineHover && !isAlpineSelected)) GUI.DrawTexture(alpineRect, _borderTexture);

        if (GUI.Button(mesaRect, "MESA", GetButtonStyle(isMesaSelected)))
        {
            if (inRoom) NetworkSync.VoteForBiome3("M");
            else 
            {
                BiomeController.SelectedBiome3 = "M";
                BiomeController.UpdateWorldSpaceOverlay();
            }
        }

        bool mesaControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 1 && _selectedCol == 1;
        if (mesaControllerSelected || (mesaHover && !isMesaSelected)) GUI.DrawTexture(mesaRect, _borderTexture);

        contentY += 40;

        if (inRoom)
        {
            int vA = NetworkSync.VotesAlpine;
            int vM = NetworkSync.VotesMesa;
            string defB3 = BiomeController.GetDefaultBiome3();
            bool aWin = vA > vM || (vA == vM && defB3 == "A");
            string winnerB3 = aWin ? "Alpine" : "Mesa";
            GUI.Label(new Rect(contentX, contentY, contentWidth, 20), $"  Votes: Alpine {vA} | Mesa {vM}  →  Winner: {winnerB3}", aWin && vA > 0 || !aWin && vM > 0 ? _winnerStyle : _voteStyle);
            contentY += 25;
        }
        else
        {
            contentY += 15;
        }

        // Bottom buttons
        Rect clearRect = new Rect(contentX, contentY, 150, 30);
        Rect closeRect = new Rect(contentX + 160, contentY, 150, 30);

        bool clearHover = clearRect.Contains(Event.current.mousePosition);
        bool closeHover = closeRect.Contains(Event.current.mousePosition);

if (GUI.Button(clearRect, "CLEAR", GetButtonStyle(false)))
        {
            if (inRoom) NetworkSync.ClearMyVotes();
            else 
            {
                LevelOverridePatch.ClearOverrides();
                BiomeController.UpdateWorldSpaceOverlay();
            }
        }

        bool clearControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 2 && _selectedCol == 0;
        if (clearControllerSelected || (clearHover && true)) GUI.DrawTexture(clearRect, _borderTexture);

        if (GUI.Button(closeRect, "CLOSE", GetButtonStyle(false)))
        {
            Close();
        }

        bool closeControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 2 && _selectedCol == 1;
        if (closeControllerSelected || (closeHover && true)) GUI.DrawTexture(closeRect, _borderTexture);

        contentY += 35;

        GUI.Label(new Rect(contentX, contentY, contentWidth, 20), "Press ESC to close", _voteStyle);
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
        _buttonStyle.hover.background = _buttonStyle.normal.background;
        _buttonStyle.active.background = _buttonStyle.normal.background;

        _selectedButtonStyle = new GUIStyle(_buttonStyle);
        _selectedButtonStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.5f, 0.2f, 1f));
        _selectedButtonStyle.hover.background = _selectedButtonStyle.normal.background;
        _selectedButtonStyle.active.background = _selectedButtonStyle.normal.background;
        _selectedButtonStyle.normal.textColor = Color.yellow;

        _borderTexture = MakeBorderTex(4, 4, new Color(0.5f, 0.8f, 1f, 0.6f));

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

    private static Texture2D MakeBorderTex(int width, int height, Color borderCol)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            int x = i % width;
            int y = i / width;
            bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
            pix[i] = isBorder ? borderCol : Color.clear;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
