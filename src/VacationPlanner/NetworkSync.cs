using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using VacationPlanner.Patches;

namespace VacationPlanner;

/// <summary>
/// Handles syncing biome votes from all players using Photon Room Custom Properties.
/// Each player can vote once per biome slot.
/// Updates the LevelOverridePatch immediately based on current vote counts.
/// Ties result in using the default biome (null override).
/// </summary>
public class NetworkSync : MonoBehaviourPunCallbacks
{
    // Room property keys for vote tracking
    // Format: "VP_V2_{actorNumber}" = "T" or "R" for biome 2 votes
    // Format: "VP_V3_{actorNumber}" = "A" or "M" for biome 3 votes
    private const string VOTE_PREFIX_B2 = "VP_V2_";
    private const string VOTE_PREFIX_B3 = "VP_V3_";
    
    private static NetworkSync? _instance;
    private static bool _initialized;

    // Vote counts (updated from room properties)
    public static int VotesTropics { get; private set; }
    public static int VotesRoots { get; private set; }
    public static int VotesAlpine { get; private set; }
    public static int VotesMesa { get; private set; }
    
    // Current player's votes
    public static string? MyVoteBiome2 { get; private set; }
    public static string? MyVoteBiome3 { get; private set; }
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _initialized = true;
        
        // Reset local state on creation
        MyVoteBiome2 = null;
        MyVoteBiome3 = null;
        
        Plugin.Log.LogInfo("NetworkSync initialized (Continuous Voting System)");
    }

    public override void OnEnable()
    {
        base.OnEnable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // When returning to the Airport (lobby), clear votes so we can vote again for the new day
        if (scene.name == "Airport")
        {
            Plugin.Log.LogInfo("[NetworkSync] Returned to Airport - Clearing votes");
            
            // Clear local state
            MyVoteBiome2 = null;
            MyVoteBiome3 = null;
            
            // Clear network state if connected (removes our vote from the room)
            if (PhotonNetwork.InRoom)
            {
                ClearMyVotes();
            }
            
            // Reset overrides
            LevelOverridePatch.ClearOverrides();
        }
    }

    /// <summary>
    /// Submit a vote for biome 2 (Tropics vs Roots).
    /// </summary>
    public static void VoteForBiome2(string vote)
    {
        if (!PhotonNetwork.InRoom) return;
        if (vote != "T" && vote != "R") return;
        
        MyVoteBiome2 = vote;
        string key = VOTE_PREFIX_B2 + PhotonNetwork.LocalPlayer.ActorNumber;
        
        var props = new Hashtable { { key, vote } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        
        Plugin.Log.LogInfo($"[VOTE] Biome2 vote submitted: {vote}");
    }

    /// <summary>
    /// Submit a vote for biome 3 (Alpine vs Mesa).
    /// </summary>
    public static void VoteForBiome3(string vote)
    {
        if (!PhotonNetwork.InRoom) return;
        if (vote != "A" && vote != "M") return;
        
        MyVoteBiome3 = vote;
        string key = VOTE_PREFIX_B3 + PhotonNetwork.LocalPlayer.ActorNumber;
        
        var props = new Hashtable { { key, vote } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        
        Plugin.Log.LogInfo($"[VOTE] Biome3 vote submitted: {vote}");
    }

    /// <summary>
    /// Clear this player's votes.
    /// </summary>
    public static void ClearMyVotes()
    {
        if (!PhotonNetwork.InRoom) return;
        
        MyVoteBiome2 = null;
        MyVoteBiome3 = null;
        
        string keyB2 = VOTE_PREFIX_B2 + PhotonNetwork.LocalPlayer.ActorNumber;
        string keyB3 = VOTE_PREFIX_B3 + PhotonNetwork.LocalPlayer.ActorNumber;
        
        // Set to empty string to clear (Photon doesn't support removing keys easily)
        var props = new Hashtable { { keyB2, "" }, { keyB3, "" } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        
        Plugin.Log.LogInfo("[VOTE] Cleared my votes");
    }

    /// <summary>
    /// Count all votes from room properties and update the LevelOverridePatch immediately.
    /// </summary>
    public static void CountVotesAndUpdateOverride()
    {
        VotesTropics = 0;
        VotesRoots = 0;
        VotesAlpine = 0;
        VotesMesa = 0;
        
        if (!PhotonNetwork.InRoom) return;
        
        var props = PhotonNetwork.CurrentRoom.CustomProperties;
        
        foreach (var kvp in props)
        {
            string key = kvp.Key as string ?? "";
            string value = kvp.Value as string ?? "";
            
            if (string.IsNullOrEmpty(value)) continue;
            
            if (key.StartsWith(VOTE_PREFIX_B2))
            {
                if (value == "T") VotesTropics++;
                else if (value == "R") VotesRoots++;
            }
            else if (key.StartsWith(VOTE_PREFIX_B3))
            {
                if (value == "A") VotesAlpine++;
                else if (value == "M") VotesMesa++;
            }
        }
        
        // Determine winners immediately
        // If tied or zero votes, result is null (default behavior)
        string? winnerB2 = null;
        if (VotesTropics > VotesRoots) winnerB2 = "T";
        else if (VotesRoots > VotesTropics) winnerB2 = "R";
        
        string? winnerB3 = null;
        if (VotesAlpine > VotesMesa) winnerB3 = "A";
        else if (VotesMesa > VotesAlpine) winnerB3 = "M";
        
        // Apply to patch immediately
        LevelOverridePatch.DesiredBiome2 = winnerB2;
        LevelOverridePatch.DesiredBiome3 = winnerB3;
        
        // Update the overlay to reflect the new winner
        BiomeController.UpdateWorldSpaceOverlay();
        
        Plugin.Log.LogInfo($"[VOTE] Tally Updated: T:{VotesTropics} R:{VotesRoots} (Winner: {winnerB2 ?? "Default"}) | A:{VotesAlpine} M:{VotesMesa} (Winner: {winnerB3 ?? "Default"})");
    }

    /// <summary>
    /// Photon callback when room properties change.
    /// </summary>
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        // Recount votes and update override whenever properties change
        CountVotesAndUpdateOverride();
    }

    /// <summary>
    /// Photon callback when joining a room.
    /// </summary>
    public override void OnJoinedRoom()
    {
        Plugin.Log.LogInfo($"[CALLBACK] OnJoinedRoom - IsMasterClient={PhotonNetwork.IsMasterClient}");
        
        // Clear local vote state from previous sessions
        LevelOverridePatch.ClearOverrides();
        
        // Count existing votes
        CountVotesAndUpdateOverride();
        
        // Restore my votes if I had any (in case of rejoin/reconnect logic, though usually properties persist)
        var roomProps = PhotonNetwork.CurrentRoom.CustomProperties;
        string myB2Key = VOTE_PREFIX_B2 + PhotonNetwork.LocalPlayer.ActorNumber;
        string myB3Key = VOTE_PREFIX_B3 + PhotonNetwork.LocalPlayer.ActorNumber;
        
        if (roomProps.TryGetValue(myB2Key, out object? b2) && b2 is string b2Str && !string.IsNullOrEmpty(b2Str))
            MyVoteBiome2 = b2Str;
        if (roomProps.TryGetValue(myB3Key, out object? b3) && b3 is string b3Str && !string.IsNullOrEmpty(b3Str))
            MyVoteBiome3 = b3Str;
    }

    /// <summary>
    /// Check if networking is available.
    /// </summary>
    public static bool IsAvailable => _initialized && PhotonNetwork.InRoom;
    
    public static bool IsMasterClient => PhotonNetwork.IsMasterClient;
}
