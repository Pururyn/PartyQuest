using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Threading.Tasks;

public class PlayerMover : NetworkBehaviour
{
    [Header("Réglages")]
    [SerializeField] private float moveSpeed = 5.0f;

    public NetworkVariable<bool> IsAI = new NetworkVariable<bool>(false);
    public NetworkVariable<int> currentNodeIndex = new NetworkVariable<int>(0);

    private Spawner spawner;
    private GameNode currentNode;
    private GameNode previousNode;

    public override void OnNetworkSpawn()
    {
        spawner = FindFirstObjectByType<Spawner>();
        if (spawner != null && spawner.allNodes.Count > currentNodeIndex.Value)
        {
            currentNode = spawner.allNodes[currentNodeIndex.Value];
            transform.position = currentNode.transform.position;
        }
    }

    [ClientRpc]
    public void MoveToStepClientRpc(int steps)
    {
        // On lance la Task sans attendre (Fire and Forget)
        _ = MoveRoutineAsync(steps);
    }

    private async Task MoveRoutineAsync(int steps)
    {
        if (spawner == null) spawner = FindFirstObjectByType<Spawner>();

        for (int i = 0; i < steps; i++)
        {
            // --- SÉCURITÉ : Si l'objet est détruit pendant l'async ---
            if (this == null || transform == null) return;

            List<GameNode> neighbors = currentNode.connectedNodes;
            GameNode nextTarget = null;

            if (neighbors.Count > 1)
            {
                // INTERSECTION : On filtre le retour arrière
                List<GameNode> choices = new List<GameNode>();
                foreach (var n in neighbors) if (n != previousNode) choices.Add(n);

                if (choices.Count > 1)
                {
                    if (IsOwner && !IsAI.Value)
                    {
                        // RESTAURATION : On utilise l'await original sur l'IntersectionManager
                        nextTarget = await IntersectionManager.Instance.WaitForPlayerChoice(currentNode, choices);
                        if (this == null) return; // Sécurité après l'attente
                        SubmitChoiceServerRpc(spawner.allNodes.IndexOf(nextTarget));
                    }
                    else
                    {
                        // IA ou autres : on attend que le serveur change l'index
                        if (IsServer && IsAI.Value)
                        {
                            nextTarget = choices[Random.Range(0, choices.Count)];
                            currentNodeIndex.Value = spawner.allNodes.IndexOf(nextTarget);
                        }

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
                else { nextTarget = choices[0]; }
            }
            else { nextTarget = neighbors[0]; }

            // --- ANIMATION DE DÉPLACEMENT ---
            Vector3 startPos = transform.position;
            Vector3 endPos = nextTarget.transform.position;
            float t = 0;

            while (t < 1)
            {
                if (this == null) return; // Crash preventer
                t += Time.deltaTime * moveSpeed / Vector3.Distance(startPos, endPos);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                await Task.Yield();
            }

            transform.position = endPos;
            previousNode = currentNode;
            currentNode = nextTarget;

            if (IsServer) currentNodeIndex.Value = spawner.allNodes.IndexOf(currentNode);
        }

        // --- FIN DU TOUR ---
        if (IsServer)
        {
            await Task.Delay(500); // Le petit délai de 0.5s pour la satisfaction visuelle
            if (this != null) TurnManager.Instance.OnPlayerFinishedMoving();
        }
    }

    [ServerRpc]
    private void SubmitChoiceServerRpc(int targetIndex)
    {
        currentNodeIndex.Value = targetIndex;
    }
}