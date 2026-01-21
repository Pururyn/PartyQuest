using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [Header("Références")]
    [SerializeField] private Spinner spinnerScript;
    [SerializeField] private GameObject playerPrefab; // Assure-toi que ce prefab a bien PlayerMover dessus

    [Header("Références UI")]
    [SerializeField] private GameObject startGameButton;

    public List<PlayerMover> activePlayers = new List<PlayerMover>();
    public NetworkVariable<int> currentTurnIndex = new NetworkVariable<int>(0);

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void StartGameSequence()
    {
        if (!IsServer) return;

        if (startGameButton != null)
        {
            startGameButton.SetActive(false);
        }

        // On peut aussi prévenir les autres clients si besoin
        HideStartButtonClientRpc();

        GameSessionManager.Instance.SetupSessionServerRpc();
        SpawnPlayersFromSession();
    }

    private void SpawnPlayersFromSession()
    {
        var sessionData = GameSessionManager.Instance.PlayersInSession;

        // On vide la liste locale pour être propre
        activePlayers.Clear();

        for (int i = 0; i < sessionData.Count; i++)
        {
            // 1. Création du pion
            GameObject go = Instantiate(playerPrefab);
            NetworkObject netObj = go.GetComponent<NetworkObject>();

            if (sessionData[i].IsAI) netObj.Spawn();
            else netObj.SpawnWithOwnership(sessionData[i].ClientId);

            PlayerMover mover = go.GetComponent<PlayerMover>();

            if (mover != null)
            {
                // --- C'EST ICI QUE LE CODE FAIT CE QUE TU FAISAIS A LA MAIN ---

                // On injecte le numéro (0, 1, 2 ou 3) dans la variable Network
                mover.playerIndex.Value = i;

                // On configure l'IA
                mover.IsAI.Value = sessionData[i].IsAI;

                // On l'ajoute à la liste du tour par tour
                activePlayers.Add(mover);
            }
        }

        // On lance le premier tour
        StartNextTurn();
    }
    public void StartNextTurn()
    {
        if (activePlayers.Count == 0) return;

        PlayerMover currentPlayer = activePlayers[currentTurnIndex.Value];

        if (!currentPlayer.IsAI.Value)
        {
            // Donnes explicitement la propriété du Spinner au joueur dont c'est le tour
            spinnerScript.GetComponent<NetworkObject>().ChangeOwnership(currentPlayer.OwnerClientId);
            // On active le dé chez lui
            spinnerScript.EnableSpinClientRpc(true);
        }
        else
        {
            // Si c'est un bot, le serveur reprend la propriété pour être sûr
            spinnerScript.GetComponent<NetworkObject>().RemoveOwnership();
            spinnerScript.BotSpin();
        }
    }

    public void ProcessDiceResult(int steps)
    {
        if (!IsServer) return;
        spinnerScript.EnableSpinClientRpc(false);
        activePlayers[currentTurnIndex.Value].MoveToStepClientRpc(steps);
    }

    public void OnPlayerFinishedMoving()
    {
        if (!IsServer) return;

        // Nettoyage de la liste au cas où un joueur a été détruit/déconnecté
        activePlayers.RemoveAll(item => item == null);

        currentTurnIndex.Value = (currentTurnIndex.Value + 1) % activePlayers.Count;
        StartNextTurn();
    }

    [ClientRpc]
    private void HideStartButtonClientRpc()
    {
        if (startGameButton != null) startGameButton.SetActive(false);
    }
}