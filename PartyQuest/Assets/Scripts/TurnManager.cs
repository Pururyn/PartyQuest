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
        // Assure-toi que GameSessionManager existe, sinon commente cette ligne pour tester
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

            // --- CORRECTION ICI : IsAI avec majuscule ---
            mover.IsAI.Value = sessionData[i].IsAI;

            activePlayers.Add(mover);
        }
        StartNextTurn();
    }

    public void StartNextTurn()
    {
        if (activePlayers.Count == 0) return;

        PlayerMover currentPlayer = activePlayers[currentTurnIndex.Value];

        // --- CORRECTION ICI : IsAI avec majuscule ---
        if (!currentPlayer.IsAI.Value)
        {
            // C'est un humain
            spinnerScript.GetComponent<NetworkObject>().ChangeOwnership(currentPlayer.OwnerClientId);
            spinnerScript.EnableSpinClientRpc(true);
        }
        else
        {
            // C'est un bot
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
        currentTurnIndex.Value = (currentTurnIndex.Value + 1) % activePlayers.Count;
        StartNextTurn();
    }
}