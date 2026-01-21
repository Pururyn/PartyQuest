using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Threading.Tasks;

public class PlayerMover : NetworkBehaviour
{
    [Header("Réglages")]
    [SerializeField] private float moveSpeed = 5.0f;

    // --- VARIABLES DE SYNCHRONISATION ---
    public NetworkVariable<int> playerIndex = new NetworkVariable<int>(0);
    public NetworkVariable<int> nextRollBonus = new NetworkVariable<int>(0);
    public NetworkVariable<bool> IsAI = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentNodeIndex = new NetworkVariable<int>(0);

    // ATTENTION : Ce script a besoin que le "Spawner" existe pour avoir la liste des Nodes du terrain
    private Spawner spawner;
    private GameNode currentNode;
    private GameNode previousNode;

    public override void OnNetworkSpawn()
    {
        spawner = FindFirstObjectByType<Spawner>();

        // Placement initial
        if (spawner != null && spawner.allNodes.Count > currentNodeIndex.Value)
        {
            currentNode = spawner.allNodes[currentNodeIndex.Value];
            transform.position = currentNode.transform.position;
        }
    }

    [ClientRpc]
    public void MoveToStepClientRpc(int steps)
    {
        _ = MoveRoutineAsync(steps);
    }

    private async Task MoveRoutineAsync(int steps)
    {
        if (spawner == null) spawner = FindFirstObjectByType<Spawner>();
        if (spawner == null) { Debug.LogError("Spawner introuvable pour la liste des nodes !"); return; }

        for (int i = 0; i < steps; i++)
        {
            if (this == null || transform == null) return;

            List<GameNode> neighbors = currentNode.connectedNodes;
            GameNode nextTarget = null;

            // --- LOGIQUE DE CHOIX DU CHEMIN ---
            if (neighbors.Count > 1)
            {
                List<GameNode> choices = new List<GameNode>();
                foreach (var n in neighbors) if (n != previousNode) choices.Add(n);

                if (choices.Count > 1)
                {
                    if (IsOwner && !IsAI.Value)
                    {
                        // Choix Humain via UI
                        nextTarget = await IntersectionManager.Instance.WaitForPlayerChoice(currentNode, choices);
                        if (this == null) return;
                        SubmitChoiceServerRpc(spawner.allNodes.IndexOf(nextTarget));
                    }
                    else
                    {
                        // Choix Bot ou synchro
                        if (IsServer && IsAI.Value)
                        {
                            nextTarget = choices[Random.Range(0, choices.Count)];
                            currentNodeIndex.Value = spawner.allNodes.IndexOf(nextTarget);
                        }

                        // Attente synchro client
                        while (nextTarget == null)
                        {
                            if (this == null) return;
                            int targetIdx = currentNodeIndex.Value;
                            if (targetIdx != spawner.allNodes.IndexOf(currentNode))
                                nextTarget = spawner.allNodes[targetIdx];
                            await Task.Yield();
                        }
                    }
                }
                else { nextTarget = choices.Count > 0 ? choices[0] : neighbors[0]; }
            }
            else { nextTarget = neighbors[0]; }

            // --- DEPLACEMENT ---
            Vector3 startPos = transform.position;
            Vector3 endPos = nextTarget.transform.position;
            float t = 0;

            while (t < 1)
            {
                if (this == null) return;
                t += Time.deltaTime * moveSpeed / Vector3.Distance(startPos, endPos);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                await Task.Yield();
            }

            transform.position = endPos;
            previousNode = currentNode;
            currentNode = nextTarget;

            if (IsServer) currentNodeIndex.Value = spawner.allNodes.IndexOf(currentNode);
        }

        if (IsServer)
        {
            ApplyTileEffect();
            await Task.Delay(1000);
            if (this != null) TurnManager.Instance.OnPlayerFinishedMoving();
        }
    }

    private void ApplyTileEffect()
    {
        Tile tile = currentNode.GetComponent<Tile>();
        if (tile == null || GameSessionManager.Instance == null) return;

        Debug.Log($"ApplyTileEffect activé pour Joueur Index: {playerIndex.Value} sur une case {tile.type}");

        if (tile.type == TileType.Green)
        {
            if (Random.value > 0.5f)
            {
                nextRollBonus.Value = 2;
                Debug.Log("Bonus dé !");
            }
            else
            {
                GameSessionManager.Instance.AddScore(playerIndex.Value, 5);
            }
        }
        else if (tile.type == TileType.Red)
        {
            if (Random.value > 0.5f)
            {
                GameSessionManager.Instance.AddScore(playerIndex.Value, -3);
            }
            else
            {
                Debug.Log("Recul !");
                _ = MoveBackwardsAsync(1);
            }
        }
    }

    private async Task MoveBackwardsAsync(int steps)
    {
        if (previousNode == null) return;

        GameNode nodeWeAreLeaving = currentNode;
        Vector3 startPos = transform.position;
        Vector3 endPos = previousNode.transform.position;
        float t = 0;

        while (t < 1)
        {
            if (this == null) return;
            t += Time.deltaTime * moveSpeed / Vector3.Distance(startPos, endPos);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            await Task.Yield();
        }

        transform.position = endPos;
        currentNode = previousNode;
        previousNode = nodeWeAreLeaving;

        if (IsServer) currentNodeIndex.Value = spawner.allNodes.IndexOf(currentNode);
    }

    [ServerRpc]
    private void SubmitChoiceServerRpc(int targetIndex)
    {
        currentNodeIndex.Value = targetIndex;
    }
}