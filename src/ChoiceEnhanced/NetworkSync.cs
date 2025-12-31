using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace ChoiceEnhanced;

/// <summary>
/// Handles syncing biome selections from host to clients using Photon Room Custom Properties.
/// Room properties persist and are automatically sent to joining players.
/// </summary>
public class NetworkSync : MonoBehaviourPunCallbacks
{
    // Room property keys for biome selection and seed
    private const string PROP_BIOME2 = "CE_Biome2";
    private const string PROP_BIOME3 = "CE_Biome3";
    private const string PROP_SEED = "CE_Seed";

    private static NetworkSync? _instance;
    private static bool _initialized;

    /// <summary>
    /// True if this client has received/read the host's biome selection from room properties.
    /// </summary>
    public static bool HasReceivedFromHost { get; private set; }

    /// <summary>
    /// The received biome2 selection (Tropics/Roots).
    /// </summary>
    public static string ReceivedBiome2 { get; private set; } = "";

    /// <summary>
    /// The received biome3 selection (Alpine/Mesa).
    /// </summary>
    public static string ReceivedBiome3 { get; private set; } = "";

    /// <summary>
    /// The received seed for world generation.
    /// </summary>
    public static int ReceivedSeed { get; private set; } = 0;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        _initialized = true;
        Plugin.Log.LogInfo("NetworkSync initialized (using Photon Room Properties)");
    }

    /// <summary>
    /// Called by host to store biome selection in room properties.
    /// This data persists and is automatically sent to joining players.
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
        int seed = BiomeController.GetCurrentSeed();

        var props = new Hashtable
        {
            { PROP_BIOME2, biome2 },
            { PROP_BIOME3, biome3 },
            { PROP_SEED, seed }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Plugin.Log.LogInfo($"[HOST] Set room properties: Biome2={biome2}, Biome3={biome3}, Seed={seed}");
    }

    /// <summary>
    /// Called by clients to read biome selection from room properties.
    /// Returns true if properties were found and applied.
    /// </summary>
    public static bool TryGetRoomBiomeSelection()
    {
        return TryGetRoomBiomeSelection(out _, out _, out _);
    }

    /// <summary>
    /// Called by clients to read biome selection from room properties.
    /// Returns true if properties were found, with values in out parameters.
    /// </summary>
    public static bool TryGetRoomBiomeSelection(out string biome2, out string biome3, out int seed)
    {
        biome2 = "";
        biome3 = "";
        seed = 0;

        if (!PhotonNetwork.InRoom)
        {
            return false;
        }

        var props = PhotonNetwork.CurrentRoom.CustomProperties;

        if (props.TryGetValue(PROP_BIOME2, out object? biome2Obj) ||
            props.TryGetValue(PROP_BIOME3, out object? biome3Obj) ||
            props.TryGetValue(PROP_SEED, out object? seedObj))
        {
            biome2 = biome2Obj as string ?? "";
            biome3 = props.TryGetValue(PROP_BIOME3, out object? b3) ? b3 as string ?? "" : "";
            seed = props.TryGetValue(PROP_SEED, out object? s) && s is int sInt ? sInt : 0;
            
            ReceivedBiome2 = biome2;
            ReceivedBiome3 = biome3;
            ReceivedSeed = seed;
            HasReceivedFromHost = true;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Photon callback when room properties change.
    /// This is called on all clients when the host sets properties.
    /// </summary>
    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        Plugin.Log.LogInfo($"[CALLBACK] OnRoomPropertiesUpdate called with {propertiesThatChanged.Count} properties");

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

        if (propertiesThatChanged.TryGetValue(PROP_SEED, out object? seedObj) && seedObj is int seedInt)
        {
            ReceivedSeed = seedInt;
            updated = true;
            Plugin.Log.LogInfo($"[CALLBACK] Seed updated to: {ReceivedSeed}");
        }

        if (updated)
        {
            HasReceivedFromHost = true;

            // Don't apply on host (they set it)
            if (!PhotonNetwork.IsMasterClient)
            {
                BiomeController.SetFromNetwork(ReceivedBiome2, ReceivedBiome3, ReceivedSeed);
                Plugin.Log.LogInfo($"[CALLBACK] Applied biome selection from host");
            }
        }
    }

    /// <summary>
    /// Photon callback when joining a room.
    /// Read existing room properties set by host.
    /// </summary>
    public override void OnJoinedRoom()
    {
        Plugin.Log.LogInfo($"[CALLBACK] OnJoinedRoom - IsMasterClient={PhotonNetwork.IsMasterClient}");

        if (!PhotonNetwork.IsMasterClient)
        {
            // Client joining - try to read host's biome selection
            TryGetRoomBiomeSelection();
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
