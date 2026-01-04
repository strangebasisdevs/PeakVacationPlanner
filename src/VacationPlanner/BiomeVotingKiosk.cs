using UnityEngine;
using Zorro.Core;

namespace VacationPlanner;

/// <summary>
/// Interactive kiosk for biome voting. Opens the BiomeVotingMenu (a MenuWindow).
/// </summary>
public class BiomeVotingKiosk : MonoBehaviour, IInteractible
{
    private MaterialPropertyBlock? mpb;
    private MeshRenderer[]? _mr;

    private MeshRenderer[] meshRenderers
    {
        get
        {
            if (_mr == null)
            {
                _mr = GetComponentsInChildren<MeshRenderer>();
            }
            return _mr;
        }
    }

    public void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    // --- IInteractible Implementation ---

    public bool IsInteractible(Character interactor) => true;

    public void Interact(Character interactor)
    {
        Plugin.Log.LogInfo($"BiomeVotingKiosk Interact - Menu exists: {BiomeVotingMenu.Instance != null}");
        
        if (BiomeVotingMenu.Instance != null)
        {
            if (BiomeVotingMenu.Instance.isOpen)
            {
                BiomeVotingMenu.Instance.Close();
            }
            else
            {
                BiomeVotingMenu.Instance.Open();
            }
        }
        else
        {
            Plugin.Log.LogError("BiomeVotingMenu.Instance is null!");
        }
    }

    public void HoverEnter()
    {
        if (mpb == null) return;
        mpb.SetFloat(Item.PROPERTY_INTERACTABLE, 1f);
        foreach (var mr in meshRenderers)
        {
            mr?.SetPropertyBlock(mpb);
        }
    }

    public void HoverExit()
    {
        if (mpb == null) return;
        mpb.SetFloat(Item.PROPERTY_INTERACTABLE, 0f);
        foreach (var mr in meshRenderers)
        {
            mr?.SetPropertyBlock(mpb);
        }
    }

    public Vector3 Center() => transform.position + Vector3.up;
    public Transform GetTransform() => transform;
    public string GetInteractionText() => "SELECT DESTINATION";
    public string GetName() => "DESTINATION KIOSK";
}
