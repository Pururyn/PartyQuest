using Unity.Netcode;
using UnityEngine;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [SerializeField] private NetworkObject spinnerObject;

    // --- NOUVEAU : On retient l'ID du joueur dont c'est le tour ---
    // On l'initialise à une valeur impossible (ex: 9999) ou on attend le StartGame
    public NetworkVariable<ulong> currentTurnPlayerId = new NetworkVariable<ulong>(ulong.MaxValue);

    // --- NOUVEAU : On empêche de relancer tant que l'action n'est pas finie ---
    private bool isTurnInProgress = false;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // Le Serveur décide qui commence (par exemple le premier connecté ou l'hôte)
        if (IsServer)
        {
            // Exemple simple : c'est le joueur 0 (l'hôte) qui commence
            // Idéalement, tu devrais récupérer la vraie liste des joueurs connectés ici
            currentTurnPlayerId.Value = NetworkManager.Singleton.LocalClientId;
        }
    }

    // Fonction appelée par le bouton UI
    public void OnClickLaunchDice()
    {
        // 1. Vérification Client (pour l'expérience utilisateur)
        // Si ce n'est pas mon tour, je ne fais rien (le bouton pourrait aussi être grisé)
        if (NetworkManager.Singleton.LocalClientId != currentTurnPlayerId.Value)
        {
            Debug.Log("Ce n'est pas votre tour !");
            return;
        }

        if (isTurnInProgress)
        {
            Debug.Log("Un tour est déjà en cours (animation, déplacement...)");
            return;
        }

        // 2. Si c'est bon, j'envoie la demande au serveur
        RequestTurnStartServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestTurnStartServerRpc(ulong playerId)
    {
        if (!IsServer) return;

        // 3. Vérification Serveur (Sécurité anti-triche indispensable)
        if (playerId != currentTurnPlayerId.Value)
        {
            Debug.LogWarning($"Le joueur {playerId} essaie de jouer hors de son tour !");
            return;
        }

        // On lance la mécanique
        Debug.Log($"Le joueur {playerId} lance son tour.");

        // On donne l'ownership du Spinner au joueur actif pour qu'il puisse le stopper
        if (spinnerObject != null && spinnerObject.IsSpawned)
        {
            spinnerObject.ChangeOwnership(playerId);

            // On déclenche l'animation pour tout le monde
            Spinner spinner = spinnerObject.GetComponent<Spinner>();
            if (spinner != null)
            {
                spinner.poppingBox.StartPopClientRpc();
                // On signale que le tour est "engagé" pour éviter le spam
                SetTurnInProgressClientRpc(true);
            }
        }
    }

    [ClientRpc]
    private void SetTurnInProgressClientRpc(bool state)
    {
        isTurnInProgress = state;
    }

    // --- A APPELER QUAND LE TOUR EST FINI ---
    // Cette fonction devra être appelée par le serveur quand le déplacement est terminé
    public void FinishTurn()
    {
        if (!IsServer) return;

        SetTurnInProgressClientRpc(false); // On débloque l'état

        // Logique pour passer au joueur suivant
        // Ici c'est un exemple simplifié, il te faudra ta liste de joueurs connectés
        // currentTurnPlayerId.Value = [ID du prochain joueur];

        Debug.Log("Tour terminé, au suivant !");
    }
}