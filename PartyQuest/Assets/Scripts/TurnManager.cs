using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [Header("Références")]
    [SerializeField] private Spinner spinnerScript;
    [SerializeField] private GameObject playerPrefab;

    [Header("Références UI")]
    [SerializeField] private GameObject startGameButton;

    public List<PlayerMover> activePlayers = new List<PlayerMover>(); // Liste dynamique des joueurs (Humains + Bots)
    public NetworkVariable<int> currentTurnIndex = new NetworkVariable<int>(0); // Pour savoir à qui c'est le tour

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    // --- PHASE 1 : INITIALISATION ---

    // À lier à un bouton "START GAME" sur l'UI de l'Host uniquement
    public void StartGameSequence()
    {
        if (!IsServer) return;

        GameSessionManager.Instance.SetupSessionServerRpc();
        SpawnPlayersFromSession();
    }

    private void SpawnPlayersFromSession()
    {
        var sessionData = GameSessionManager.Instance.PlayersInSession;
        for (int i = 0; i < sessionData.Count; i++)
        {
            GameObject go = Instantiate(playerPrefab);
            NetworkObject netObj = go.GetComponent<NetworkObject>();

            if (sessionData[i].IsAI) netObj.Spawn();
            else netObj.SpawnWithOwnership(sessionData[i].ClientId);

            PlayerMover mover = go.GetComponent<PlayerMover>();
            mover.isAI.Value = sessionData[i].IsAI;
            activePlayers.Add(mover);
        }
        StartNextTurn();
    }

    public void StartNextTurn()
    {
        PlayerMover currentPlayer = activePlayers[currentTurnIndex.Value];
        if (!currentPlayer.isAI.Value)
        {
            spinnerScript.GetComponent<NetworkObject>().ChangeOwnership(currentPlayer.OwnerClientId);
            spinnerScript.EnableSpinClientRpc(true);
        }
        else
        {
            spinnerScript.BotSpin();
        }
    }

    // Appelé par le Spinner quand le résultat est tombé
    public void ProcessDiceResult(int steps)
    {
        if (!IsServer) return;
        spinnerScript.EnableSpinClientRpc(false);
        activePlayers[currentTurnIndex.Value].MoveToStepClientRpc(steps);
    }

    public void OnPlayerFinishedMoving()
    {
        if (!IsServer) return;
        currentTurnIndex.Value = (currentTurnIndex.Value + 1) % activePlayers.Count;
        StartNextTurn();
    }
}