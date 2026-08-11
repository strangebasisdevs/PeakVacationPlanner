using Photon.Pun;
using System.Collections.Generic;
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
    private GUIStyle? _tooltipStyle;
    private Texture2D? _borderTexture;
    private Texture2D? _xTexture;

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
        LevelOverridePatch.EnsureBiomeMapBuilt();
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
                    _selectedRow = Mathf.Min(3, _selectedRow + 1); // 4 rows
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
        string effB2 = GetEffectiveBiome2(inRoom);
        string effB3 = GetEffectiveBiome3(inRoom);
        string effB4 = GetEffectiveBiome4(inRoom);

        switch (_selectedRow)
        {
            case 0: // Jungle region
                if (_selectedCol == 0) // Tropics
                {
                    if (!LevelOverridePatch.IsBiome2BranchAvailable("T", effB3, effB4)) break;
                    if (inRoom) NetworkSync.VoteForBiome2("T");
                    else 
                    {
                        BiomeController.SelectedBiome2 = "T";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                else // Roots
                {
                    if (!LevelOverridePatch.IsBiome2BranchAvailable("R", effB3, effB4)) break;
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
                    if (!LevelOverridePatch.IsBiome3BranchAvailable("A", effB2, effB4)) break;
                    if (inRoom) NetworkSync.VoteForBiome3("A");
                    else 
                    {
                        BiomeController.SelectedBiome3 = "A";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                else // Mesa
                {
                    if (!LevelOverridePatch.IsBiome3BranchAvailable("M", effB2, effB4)) break;
                    if (inRoom) NetworkSync.VoteForBiome3("M");
                    else 
                    {
                        BiomeController.SelectedBiome3 = "M";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                break;
            case 2: // Final ascent region
                if (_selectedCol == 0) // Caldera + Kiln
                {
                    if (!LevelOverridePatch.IsBiome4BranchAvailable("V", effB2, effB3)) break;
                    if (inRoom) NetworkSync.VoteForBiome4("V");
                    else 
                    {
                        BiomeController.SelectedBiome4 = "V";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                else // Gloom + Citadel
                {
                    if (!LevelOverridePatch.IsBiome4BranchAvailable("S", effB2, effB3)) break;
                    if (inRoom) NetworkSync.VoteForBiome4("S");
                    else 
                    {
                        BiomeController.SelectedBiome4 = "S";
                        BiomeController.UpdateWorldSpaceOverlay();
                    }
                }
                break;
            case 3: // Bottom row
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

    /// <summary>
    /// The branch currently winning the biome2 vote (or the active solo selection), falling back
    /// to today's default on a tie/no vote. Never null - used to cross-check availability of the
    /// other columns against what's actually leading right now.
    /// </summary>
    private static string GetEffectiveBiome2(bool inRoom)
    {
        if (inRoom)
        {
            int vT = NetworkSync.VotesTropics;
            int vR = NetworkSync.VotesRoots;
            if (vT > vR) return "T";
            if (vR > vT) return "R";
            return BiomeController.GetDefaultBiome2();
        }
        return BiomeController.SelectedBiome2 ?? BiomeController.GetDefaultBiome2();
    }

    private static string GetEffectiveBiome3(bool inRoom)
    {
        if (inRoom)
        {
            int vA = NetworkSync.VotesAlpine;
            int vM = NetworkSync.VotesMesa;
            if (vA > vM) return "A";
            if (vM > vA) return "M";
            return BiomeController.GetDefaultBiome3();
        }
        return BiomeController.SelectedBiome3 ?? BiomeController.GetDefaultBiome3();
    }

    private static string GetEffectiveBiome4(bool inRoom)
    {
        if (inRoom)
        {
            int vV = NetworkSync.VotesVolcano;
            int vS = NetworkSync.VotesSwamp;
            if (vV > vS) return "V";
            if (vS > vV) return "S";
            return BiomeController.GetDefaultBiome4();
        }
        return BiomeController.SelectedBiome4 ?? BiomeController.GetDefaultBiome4();
    }

    private static string FriendlyBiome2(string v) => v == "T" ? "Tropics" : "Roots";
    private static string FriendlyBiome3(string v) => v == "A" ? "Alpine" : "Mesa";
    private static string FriendlyBiome4(string v) => v == "V" ? "Volcano" : "Swamp";

    /// <summary>
    /// Builds a plain-language explanation of why a branch can't be voted for right now, for use
    /// as a button hover tooltip. Distinguishes "not baked into this build at all" from "not baked
    /// together with what's currently winning the other column(s)".
    /// </summary>
    private static string BuildUnavailableMessage(string friendlyName, LevelOverridePatch.BranchAvailability info, string otherALabel, string otherBLabel)
    {
        if (info.IsGloballyUnavailable)
            return $"{friendlyName} is not available at all right now";

        var blockers = new List<string>();
        if (info.ChangingOtherAWouldHelp) blockers.Add(otherALabel);
        if (info.ChangingOtherBWouldHelp) blockers.Add(otherBLabel);

        if (blockers.Count == 0)
            return $"{friendlyName} cannot be voted on with the current {otherALabel} + {otherBLabel} combination";

        return $"{friendlyName} cannot be voted on until at least one of the following results changes: {string.Join(" and/or ", blockers)}";
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

        // Unity does not clear GUI.tooltip automatically when the mouse moves off every control -
        // it only gets overwritten by a control that's actually hovered this event. Reset it here so
        // a stale tooltip from a previous frame doesn't linger once the mouse leaves the button.
        GUI.tooltip = string.Empty;

        bool inRoom = PhotonNetwork.InRoom;

        // Center the box on screen
        float boxWidth = 400;
        float boxHeight = inRoom ? 465 : 345;
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

        // Current leader of each slot (recomputed every frame from live vote/selection state), used
        // to gray out options in one column that aren't baked together with what's currently winning
        // in the other columns - not just options that are unbaked in every combo.
        string effB2 = GetEffectiveBiome2(inRoom);
        string effB3 = GetEffectiveBiome3(inRoom);
        string effB4 = GetEffectiveBiome4(inRoom);

        // Jungle region
        GUI.Label(new Rect(contentX, contentY, contentWidth, 20), "JUNGLE REGION:", _labelStyle);
        contentY += 25;

        string? myB2 = inRoom ? NetworkSync.MyVoteBiome2 : BiomeController.SelectedBiome2;
        bool isTropicsSelected = myB2 == "T";
        bool isRootsSelected = myB2 == "R";
        var tropicsInfo = LevelOverridePatch.EvaluateBiome2Branch("T", effB3, effB4);
        var rootsInfo = LevelOverridePatch.EvaluateBiome2Branch("R", effB3, effB4);
        bool tropicsAvailable = tropicsInfo.IsAvailable;
        bool rootsAvailable = rootsInfo.IsAvailable;

        Rect tropicsRect = new Rect(contentX, contentY, 150, 35);
        Rect rootsRect = new Rect(contentX + 160, contentY, 150, 35);

        bool tropicsHover = tropicsRect.Contains(Event.current.mousePosition);
        bool rootsHover = rootsRect.Contains(Event.current.mousePosition);

        string tropicsTooltip = tropicsAvailable ? "" : BuildUnavailableMessage("Tropics", tropicsInfo, FriendlyBiome3(effB3), FriendlyBiome4(effB4));
        GUIContent tropicsContent = new GUIContent("TROPICS", tropicsTooltip);
if (GUI.Button(tropicsRect, tropicsContent, GetButtonStyle(isTropicsSelected)))
        {
            if (tropicsAvailable)
            {
                if (inRoom) NetworkSync.VoteForBiome2("T");
                else 
                {
                    BiomeController.SelectedBiome2 = "T";
                    BiomeController.UpdateWorldSpaceOverlay();
                }
            }
        }
        if (!tropicsAvailable) GUI.DrawTexture(tropicsRect, _xTexture);

        bool tropicsControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 0 && _selectedCol == 0;
        if (tropicsControllerSelected || (tropicsHover && !isTropicsSelected)) GUI.DrawTexture(tropicsRect, _borderTexture);

        string rootsTooltip = rootsAvailable ? "" : BuildUnavailableMessage("Roots", rootsInfo, FriendlyBiome3(effB3), FriendlyBiome4(effB4));
        GUIContent rootsContent = new GUIContent("ROOTS", rootsTooltip);
        if (GUI.Button(rootsRect, rootsContent, GetButtonStyle(isRootsSelected)))
        {
            if (rootsAvailable)
            {
                if (inRoom) NetworkSync.VoteForBiome2("R");
                else 
                {
                    BiomeController.SelectedBiome2 = "R";
                    BiomeController.UpdateWorldSpaceOverlay();
                }
            }
        }
        if (!rootsAvailable) GUI.DrawTexture(rootsRect, _xTexture);

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
        var alpineInfo = LevelOverridePatch.EvaluateBiome3Branch("A", effB2, effB4);
        var mesaInfo = LevelOverridePatch.EvaluateBiome3Branch("M", effB2, effB4);
        bool alpineAvailable = alpineInfo.IsAvailable;
        bool mesaAvailable = mesaInfo.IsAvailable;

        Rect alpineRect = new Rect(contentX, contentY, 150, 35);
        Rect mesaRect = new Rect(contentX + 160, contentY, 150, 35);

        bool alpineHover = alpineRect.Contains(Event.current.mousePosition);
        bool mesaHover = mesaRect.Contains(Event.current.mousePosition);

        string alpineTooltip = alpineAvailable ? "" : BuildUnavailableMessage("Alpine", alpineInfo, FriendlyBiome2(effB2), FriendlyBiome4(effB4));
        GUIContent alpineContent = new GUIContent("ALPINE", alpineTooltip);
if (GUI.Button(alpineRect, alpineContent, GetButtonStyle(isAlpineSelected)))
        {
            if (alpineAvailable)
            {
                if (inRoom) NetworkSync.VoteForBiome3("A");
                else 
                {
                    BiomeController.SelectedBiome3 = "A";
                    BiomeController.UpdateWorldSpaceOverlay();
                }
            }
        }
        if (!alpineAvailable) GUI.DrawTexture(alpineRect, _xTexture);

        bool alpineControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 1 && _selectedCol == 0;
        if (alpineControllerSelected || (alpineHover && !isAlpineSelected)) GUI.DrawTexture(alpineRect, _borderTexture);

        string mesaTooltip = mesaAvailable ? "" : BuildUnavailableMessage("Mesa", mesaInfo, FriendlyBiome2(effB2), FriendlyBiome4(effB4));
        GUIContent mesaContent = new GUIContent("MESA", mesaTooltip);
        if (GUI.Button(mesaRect, mesaContent, GetButtonStyle(isMesaSelected)))
        {
            if (mesaAvailable)
            {
                if (inRoom) NetworkSync.VoteForBiome3("M");
                else 
                {
                    BiomeController.SelectedBiome3 = "M";
                    BiomeController.UpdateWorldSpaceOverlay();
                }
            }
        }
        if (!mesaAvailable) GUI.DrawTexture(mesaRect, _xTexture);

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

        // Final ascent region
        GUI.Label(new Rect(contentX, contentY, contentWidth, 20), "FINAL ASCENT:", _labelStyle);
        contentY += 25;

        string? myB4 = inRoom ? NetworkSync.MyVoteBiome4 : BiomeController.SelectedBiome4;
        bool isVolcanoSelected = myB4 == "V";
        bool isSwampSelected = myB4 == "S";
        var volcanoInfo = LevelOverridePatch.EvaluateBiome4Branch("V", effB2, effB3);
        var swampInfo = LevelOverridePatch.EvaluateBiome4Branch("S", effB2, effB3);
        bool volcanoAvailable = volcanoInfo.IsAvailable;
        bool swampAvailable = swampInfo.IsAvailable;

        Rect volcanoRect = new Rect(contentX, contentY, 150, 35);
        Rect swampRect = new Rect(contentX + 160, contentY, 150, 35);

        bool volcanoHover = volcanoRect.Contains(Event.current.mousePosition);
        bool swampHover = swampRect.Contains(Event.current.mousePosition);

        string volcanoTooltip = volcanoAvailable ? "" : BuildUnavailableMessage("Volcano", volcanoInfo, FriendlyBiome2(effB2), FriendlyBiome3(effB3));
        GUIContent volcanoContent = new GUIContent("VOLCANO", volcanoTooltip);
        if (GUI.Button(volcanoRect, volcanoContent, GetButtonStyle(isVolcanoSelected)))
        {
            if (volcanoAvailable)
            {
                if (inRoom) NetworkSync.VoteForBiome4("V");
                else 
                {
                    BiomeController.SelectedBiome4 = "V";
                    BiomeController.UpdateWorldSpaceOverlay();
                }
            }
        }
        if (!volcanoAvailable) GUI.DrawTexture(volcanoRect, _xTexture);

        bool volcanoControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 2 && _selectedCol == 0;
        if (volcanoControllerSelected || (volcanoHover && !isVolcanoSelected)) GUI.DrawTexture(volcanoRect, _borderTexture);

        string swampTooltip = swampAvailable ? "" : BuildUnavailableMessage("Swamp", swampInfo, FriendlyBiome2(effB2), FriendlyBiome3(effB3));
        GUIContent swampContent = new GUIContent("SWAMP", swampTooltip);
        if (GUI.Button(swampRect, swampContent, GetButtonStyle(isSwampSelected)))
        {
            if (swampAvailable)
            {
                if (inRoom) NetworkSync.VoteForBiome4("S");
                else 
                {
                    BiomeController.SelectedBiome4 = "S";
                    BiomeController.UpdateWorldSpaceOverlay();
                }
            }
        }

        if (!swampAvailable) GUI.DrawTexture(swampRect, _xTexture);

        bool swampControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 2 && _selectedCol == 1;
        if (swampControllerSelected || (swampHover && !isSwampSelected)) GUI.DrawTexture(swampRect, _borderTexture);

        contentY += 40;

        if (inRoom)
        {
            int vV = NetworkSync.VotesVolcano;
            int vS = NetworkSync.VotesSwamp;
            string defB4 = BiomeController.GetDefaultBiome4();
            bool vWin = vV > vS || (vV == vS && defB4 == "V");
            string winnerB4 = vWin ? "Volcano" : "Swamp";
            GUI.Label(new Rect(contentX, contentY, contentWidth, 20), $"  Votes: Volcano {vV} | Swamp {vS}  →  Winner: {winnerB4}", vWin && vV > 0 || !vWin && vS > 0 ? _winnerStyle : _voteStyle);
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

        bool clearControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 3 && _selectedCol == 0;
        if (clearControllerSelected || (clearHover && true)) GUI.DrawTexture(clearRect, _borderTexture);

        if (GUI.Button(closeRect, "CLOSE", GetButtonStyle(false)))
        {
            Close();
        }

        bool closeControllerSelected = _currentInputScheme == InputScheme.Gamepad && _selectedRow == 3 && _selectedCol == 1;
        if (closeControllerSelected || (closeHover && true)) GUI.DrawTexture(closeRect, _borderTexture);

        contentY += 35;

        GUI.Label(new Rect(contentX, contentY, contentWidth, 20), "Press ESC to close", _voteStyle);

        // Floating tooltip explaining why a grayed-out/X'd option can't be voted on right now.
        // GUI.tooltip is populated automatically by IMGUI whenever the mouse hovers a control
        // that was drawn with a GUIContent carrying a non-empty tooltip string.
        if (!string.IsNullOrEmpty(GUI.tooltip))
        {
            Vector2 mousePos = Event.current.mousePosition;
            GUIContent tooltipContent = new GUIContent(GUI.tooltip);
            float tooltipWidth = 240f;
            float tooltipHeight = _tooltipStyle!.CalcHeight(tooltipContent, tooltipWidth);

            float tooltipX = mousePos.x + 16;
            float tooltipY = mousePos.y + 16;
            if (tooltipX + tooltipWidth > Screen.width) tooltipX = Screen.width - tooltipWidth - 4;
            if (tooltipY + tooltipHeight > Screen.height) tooltipY = Screen.height - tooltipHeight - 4;

            GUI.Label(new Rect(tooltipX, tooltipY, tooltipWidth, tooltipHeight), tooltipContent, _tooltipStyle);
        }
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

        _xTexture = MakeXTex(32, new Color(0.9f, 0.15f, 0.15f, 0.95f), 3);

        _tooltipStyle = new GUIStyle(_labelStyle);
        _tooltipStyle.fontSize = 13;
        _tooltipStyle.fontStyle = FontStyle.Normal;
        _tooltipStyle.normal.textColor = Color.white;
        _tooltipStyle.wordWrap = true;
        _tooltipStyle.padding = new RectOffset(6, 6, 4, 4);
        _tooltipStyle.normal.background = MakeTex(2, 2, new Color(0.05f, 0.05f, 0.05f, 0.97f));
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

    /// <summary>
    /// Draws a diagonal "X" (transparent everywhere else) - used to overlay disabled buttons so
    /// unavailability reads visually at a glance, not just via a text suffix.
    /// </summary>
    private static Texture2D MakeXTex(int size, Color col, int thickness)
    {
        Color[] pix = new Color[size * size];
        for (int i = 0; i < pix.Length; i++) pix[i] = Color.clear;

        for (int x = 0; x < size; x++)
        {
            for (int t = -thickness; t <= thickness; t++)
            {
                int y1 = x + t;
                if (y1 >= 0 && y1 < size) pix[y1 * size + x] = col;

                int y2 = (size - 1 - x) + t;
                if (y2 >= 0 && y2 < size) pix[y2 * size + x] = col;
            }
        }

        Texture2D result = new Texture2D(size, size);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
