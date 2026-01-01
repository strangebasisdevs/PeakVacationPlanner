using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace ChoiceEnhanced;

/// <summary>
/// Handles syncing biome selections from host to clients using Photon Room Custom Properties.
/// Room properties persist and are automatically sent to joining players.
/// 
/// Simplified version - no longer syncs seeds (not needed without PEAKChoice).
/// </summary>
public class NetworkSync : MonoBehaviourPunCallbacks
{
    // Room property keys for biome selection
    private const string PROP_BIOME2 = "CE_Biome2";
    private const string PROP_BIOME3 = "CE_Biome3";

    private static NetworkSync? _instance;
    private static bool _initialized;

    /// <summary>
    /// True if this client has received/read the host's biome selection from room properties.
    /// </summary>
    public static bool HasReceivedFromHost { get; private set; }

    /// <summary>
    /// The received biome2 selection ("T" for Tropics, "R" for Roots).
    /// </summary>
    public static string ReceivedBiome2 { get; private set; } = "";

    /// <summary>
    /// The received biome3 selection ("A" for Alpine, "M" for Mesa).
    /// </summary>
    public static string ReceivedBiome3 { get; private set; } = "";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _initialized = true;
        Plugin.Log.LogInfo("NetworkSync initialized");
    }

    /// <summary>
    /// Called by host to store biome selection in room properties.
    /// </summary>
    public static void SetRoomBiomeSelection()
    {
        if (!PhotonNetwork.InRoom)
        {
            Plugin.Log.LogWarning("Not in a room, cannot set biome properties");
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Plugin.Log.LogInfo("Not master client, skipping room property set");
            return;
        }

        string biome2 = BiomeController.SelectedBiome2 ?? "";
        string biome3 = BiomeController.SelectedBiome3 ?? "";

        var props = new Hashtable
        {
            { PROP_BIOME2, biome2 },
            { PROP_BIOME3, biome3 }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Plugin.Log.LogInfo($"[HOST] Set room properties: Biome2={biome2}, Biome3={biome3}");
    }

    /// <summary>
    /// Called by clients to read biome selection from room properties.
    /// </summary>
    public static bool TryGetRoomBiomeSelection(out string biome2, out string biome3)
    {
        biome2 = "";
        biome3 = "";

        if (!PhotonNetwork.InRoom)
            return false;

        var props = PhotonNetwork.CurrentRoom.CustomProperties;

        bool found = false;
        if (props.TryGetValue(PROP_BIOME2, out object? biome2Obj))
        {
            biome2 = biome2Obj as string ?? "";
            ReceivedBiome2 = biome2;
            found = true;
        }

        if (props.TryGetValue(PROP_BIOME3, out object? biome3Obj))
        {
            biome3 = biome3Obj as string ?? "";
            ReceivedBiome3 = biome3;
            found = true;
        }

        if (found)
            HasReceivedFromHost = true;

        return found;
    }

    /// <summary>
    /// Photon callback when room properties change.
    /// </summary>
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Plugin.Log.LogInfo($"[CALLBACK] OnRoomPropertiesUpdate with {propertiesThatChanged.Count} properties");

        bool updated = false;

        if (propertiesThatChanged.TryGetValue(PROP_BIOME2, out object? biome2Obj))
        {
            ReceivedBiome2 = biome2Obj as string ?? "";
            updated = true;
            Plugin.Log.LogInfo($"[CALLBACK] Biome2 updated to: {ReceivedBiome2}");
        }

        if (propertiesThatChanged.TryGetValue(PROP_BIOME3, out object? biome3Obj))
        {
            ReceivedBiome3 = biome3Obj as string ?? "";
            updated = true;
            Plugin.Log.LogInfo($"[CALLBACK] Biome3 updated to: {ReceivedBiome3}");
        }

        if (updated)
        {
            HasReceivedFromHost = true;

            // Apply on clients (not host - they set it)
            if (!PhotonNetwork.IsMasterClient)
            {
                BiomeController.SetFromNetwork(ReceivedBiome2, ReceivedBiome3);
                Plugin.Log.LogInfo($"[CALLBACK] Applied biome selection from host");
            }
        }
    }

    /// <summary>
    /// Photon callback when joining a room.
    /// </summary>
    public override void OnJoinedRoom()
    {
        Plugin.Log.LogInfo($"[CALLBACK] OnJoinedRoom - IsMasterClient={PhotonNetwork.IsMasterClient}");

        if (!PhotonNetwork.IsMasterClient)
        {
            // Client joining - try to read host's biome selection
            if (TryGetRoomBiomeSelection(out string biome2, out string biome3))
            {
                BiomeController.SetFromNetwork(biome2, biome3);
            }
        }
    }

    /// <summary>
    /// Check if networking is available.
    /// </summary>
    public static bool IsAvailable => _initialized && PhotonNetwork.InRoom;

    /// <summary>
    /// Check if we are the host/master client.
    /// </summary>
    public static bool IsHost => PhotonNetwork.IsMasterClient;

    /// <summary>
    /// Reset state for new game.
    /// </summary>
    public static void Reset()
    {
        HasReceivedFromHost = false;
        ReceivedBiome2 = "";
        ReceivedBiome3 = "";
        Plugin.Log.LogInfo("NetworkSync state reset");
    }
}
