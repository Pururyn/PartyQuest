using Unity.Netcode;
using UnityEngine;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [SerializeField] private NetworkObject spinnerObject;

    private void Awake()
    {
        Instance = this;
    }

    // Appelé par le joueur quand son tour commence
    [ServerRpc(RequireOwnership = false)]
    public void RequestSpinnerOwnershipServerRpc(ulong playerId)
    {
        if (!IsServer) return;

        if (spinnerObject != null && spinnerObject.IsSpawned)
        {
            spinnerObject.ChangeOwnership(playerId);
            Debug.Log($"Ownership du Spinner donné au joueur {playerId}");
        }
    }

    public void GiveTurnToPlayer(ulong playerId)
    {
        RequestSpinnerOwnershipServerRpc(playerId);
    }

    public void OnClickLaunchDice()
    {
        Debug.Log("Bouton cliqué !");
        GiveTurnToPlayer(NetworkManager.Singleton.LocalClientId);
        Debug.Log("Ownership demandé");

        Spinner spinner = spinnerObject.GetComponent<Spinner>();
        if (spinner != null)
        {
            spinner.poppingBox.StartPopClientRpc();
            Debug.Log("Animation lancée");
        }
        else
            Debug.Log("Spinner non trouvé !");

        if (spinnerObject == null)
        {
            Debug.LogError("spinnerObject n'est pas assigné dans TurnManager !");
            return;
        }
    }

}
