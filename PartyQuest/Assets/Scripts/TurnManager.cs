using Unity.Netcode;
using UnityEngine;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [SerializeField] private NetworkObject spinnerObject;
    public NetworkVariable<ulong> currentTurnPlayerId = new NetworkVariable<ulong>(ulong.MaxValue);
    private bool isTurnInProgress = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialisation : Le premier client connecté commence (ou l'hôte)
            // Note: Ceci est basique, pour un vrai jeu il faut une liste de joueurs triée
            if (NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
                currentTurnPlayerId.Value = NetworkManager.Singleton.ConnectedClientsIds[0];
        }
    }

    public void OnClickLaunchDice()
    {
        if (NetworkManager.Singleton.LocalClientId != currentTurnPlayerId.Value) return;
        if (isTurnInProgress) return;

        RequestTurnStartServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestTurnStartServerRpc(ulong playerId)
    {
        if (!IsServer) return;
        if (playerId != currentTurnPlayerId.Value) return;

        // Le tour commence
        if (spinnerObject != null && spinnerObject.IsSpawned)
        {
            spinnerObject.ChangeOwnership(playerId);
            Spinner spinner = spinnerObject.GetComponent<Spinner>();
            if (spinner != null)
            {
                spinner.poppingBox.StartPopClientRpc();
                SetTurnInProgressClientRpc(true);
            }
        }
    }

    // --- NOUVELLE FONCTION : Reçoit le résultat du Spinner ---
    public void OnDiceResult(int steps)
    {
        if (!IsServer) return;

        Debug.Log($"Le dé a fait {steps}. Déplacement du joueur {currentTurnPlayerId.Value}...");

        // 1. Trouver l'objet Player du joueur actuel
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(currentTurnPlayerId.Value, out NetworkClient client))
        {
            PlayerMover mover = client.PlayerObject.GetComponent<PlayerMover>();
            if (mover != null)
            {
                // 2. Lancer le mouvement
                mover.StartMoveSequence(steps);
            }
            else
            {
                Debug.LogError("PlayerMover introuvable sur l'objet joueur !");
                FinishTurn(); // Force la fin si erreur
            }
        }
    }

    [ClientRpc]
    private void SetTurnInProgressClientRpc(bool state)
    {
        isTurnInProgress = state;
    }

    public void FinishTurn()
    {
        if (!IsServer) return;

        SetTurnInProgressClientRpc(false);
        Debug.Log("Tour terminé.");

        // --- LOGIQUE DE CHANGEMENT DE TOUR ---
        // Exemple simple : on passe au ClientID suivant
        // Dans un vrai jeu, utilisez une List<ulong> turnOrder
        var clients = NetworkManager.Singleton.ConnectedClientsIds;
        int currentIndex = -1;

        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i] == currentTurnPlayerId.Value)
            {
                currentIndex = i;
                break;
            }
        }

        // Joueur suivant (boucle)
        if (clients.Count > 0)
        {
            int nextIndex = (currentIndex + 1) % clients.Count;
            currentTurnPlayerId.Value = clients[nextIndex];
            Debug.Log($"C'est au tour du joueur {currentTurnPlayerId.Value}");
        }
    }
}