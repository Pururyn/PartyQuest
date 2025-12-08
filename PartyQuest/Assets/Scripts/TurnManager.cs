using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class TurnManager : NetworkBehaviour
{
    public static TurnManager Instance;

    [Header("Références")]
    [SerializeField] private Spinner spinnerScript;
    [SerializeField] private GameObject playerPrefab; // Le MEME que dans NetworkManager

    [Header("Références UI")] // NOUVEAU
    [SerializeField] private GameObject startGameButton; // NOUVEAU : Référence au bouton

    // Liste dynamique des joueurs (Humains + Bots)
    public List<PlayerMover> activePlayers = new List<PlayerMover>();

    // Variable synchronisée pour savoir à qui c'est le tour (index dans la liste)
    public NetworkVariable<int> currentTurnIndex = new NetworkVariable<int>(0);

    private bool isGameRunning = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    // --- PHASE 1 : INITIALISATION ---

    // Appelé automatiquement par les PlayerMovers quand ils apparaissent
    public void RegisterPlayer(PlayerMover player)
    {
        if (!activePlayers.Contains(player))
        {
            activePlayers.Add(player);
        }
    }

    // À lier à un bouton "START GAME" sur l'UI de l'Host uniquement
    public void StartGameSequence()
    {
        if (!IsServer || isGameRunning) return;

        if (startGameButton != null)
        {
            startGameButton.SetActive(false);
        }

        StartCoroutine(SetupAndStartGame());
    }

    private IEnumerator SetupAndStartGame()
    {
        isGameRunning = true;

        // Combler les places vides avec des Bots (Objectif 4 joueurs)
        int currentCount = activePlayers.Count;
        int slotsNeeded = 4 - currentCount;

        for (int i = 0; i < slotsNeeded; i++)
        {
            // Spawn du Bot
            GameObject bot = Instantiate(playerPrefab);
            bot.GetComponent<NetworkObject>().Spawn(); // Spawn côté serveur

            PlayerMover mover = bot.GetComponent<PlayerMover>();
            if (mover != null)
            {
                mover.isAI.Value = true;
                mover.name = $"Bot_{i + 1}";
            }
        }

        // Attente pour être sûr que tout est synchronisé
        yield return new WaitForSeconds(1.0f);

        Debug.Log("La partie commence !");
        StartTurn();
    }

    // --- PHASE 2 : BOUCLE DE JEU ---

    private void StartTurn()
    {
        if (activePlayers.Count == 0) return;

        PlayerMover currentPlayer = activePlayers[currentTurnIndex.Value];
        Debug.Log($"Tour du joueur {currentTurnIndex.Value} (AI: {currentPlayer.isAI.Value})");

        // On donne le contrôle du dé au bon joueur
        if (!currentPlayer.isAI.Value)
        {
            // C'est un humain : on lui donne l'ownership du dé pour qu'il puisse cliquer
            if (spinnerScript != null && spinnerScript.GetComponent<NetworkObject>() != null)
            {
                spinnerScript.GetComponent<NetworkObject>().ChangeOwnership(currentPlayer.OwnerClientId);
                spinnerScript.EnableSpinClientRpc(true);
            }
        }
        else
        {
            // C'est un bot : le serveur garde la main
            if (spinnerScript != null)
            {
                spinnerScript.GetComponent<NetworkObject>().RemoveOwnership();
                spinnerScript.BotSpin();
            }
        }
    }

    // Appelé par le Spinner quand le résultat est tombé
    public void ProcessDiceResult(int steps)
    {
        if (!IsServer) return;

        // On désactive le dé pour tout le monde
        if (spinnerScript != null) spinnerScript.EnableSpinClientRpc(false);

        // On bouge le joueur actuel
        PlayerMover currentPlayer = activePlayers[currentTurnIndex.Value];
        currentPlayer.MoveToStepClientRpc(steps);
    }

    // Appelé par le PlayerMover quand il a fini de bouger
    public void OnPlayerFinishedMoving()
    {
        if (!IsServer) return;

        // Passage au joueur suivant
        int nextIndex = (currentTurnIndex.Value + 1) % activePlayers.Count;
        currentTurnIndex.Value = nextIndex;

        // Vérifier si on a fait un tour complet
        if (nextIndex == 0)
        {
            Debug.Log("Fin du round !");
        }

        StartTurn();
    }
}