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

        // Sécurité : Si le Spawner est vide, on force la génération
        if (spawner != null && spawner.allNodes.Count == 0) spawner.ForceRespawn();

        if (spawner != null && spawner.allNodes.Count > currentNodeIndex.Value)
        {
            currentNode = spawner.allNodes[currentNodeIndex.Value];
            transform.position = currentNode.transform.position;

            // --- ASTUCE ANTI-RECUL ---
            // Au lieu de dire "je n'ai pas de précédent", on essaie de deviner lequel est derrière.
            // On cherche un voisin qui a un index inférieur dans la liste du Spawner.
            int myIndex = currentNodeIndex.Value;
            foreach (var neighbor in currentNode.connectedNodes)
            {
                int nIndex = spawner.allNodes.IndexOf(neighbor);
                // Si le voisin est "avant" moi (i-1) ou si je suis au début (0) et lui à la fin (boucle)
                if (nIndex != -1)
                {
                    // Si le voisin est l'index juste avant, ou le dernier (cas de la boucle fermée start->end)
                    if (nIndex < myIndex || (myIndex == 0 && nIndex > myIndex + 1))
                    {
                        previousNode = neighbor;
                        break;
                    }
                }
            }
            // Si on a rien trouvé, previousNode reste null, mais c'est rare.
        }
    }

    [ClientRpc]
    public void MoveToStepClientRpc(int steps)
    {
        MoveRoutineAsync(steps);
    }

    private async void MoveRoutineAsync(int steps)
    {
        // Re-vérification de sécurité (si le spawner a été reset entre temps)
        if (currentNode == null && spawner != null && spawner.allNodes.Count > currentNodeIndex.Value)
            currentNode = spawner.allNodes[currentNodeIndex.Value];

        if (currentNode == null) return;

        for (int i = 0; i < steps; i++)
        {
            List<GameNode> neighbors = new List<GameNode>(currentNode.connectedNodes);

            // Filtrage : On enlève le noeud d'où l'on vient
            if (previousNode != null && neighbors.Contains(previousNode))
            {
                // Exception : Si c'est un cul-de-sac (un seul voisin qui est le précédent), on est obligé de faire demi-tour
                if (neighbors.Count > 1)
                {
                    neighbors.Remove(previousNode);
                }
            }

            GameNode nextTarget = null;

            if (neighbors.Count == 0)
            {
                Debug.LogWarning("Bloqué ! Aucun chemin.");
                break;
            }
            else if (neighbors.Count == 1)
            {
                nextTarget = neighbors[0];
            }
            else
            {
                // --- INTERSECTION ---
                if (IsOwner)
                {
                    if (IsAI.Value)
                    {
                        await Task.Delay(500);
                        nextTarget = neighbors[Random.Range(0, neighbors.Count)];
                    }
                    else
                    {
                        // Humain : Choix UI
                        nextTarget = await IntersectionManager.Instance.WaitForPlayerChoice(currentNode, neighbors);
                    }

                    // Validation Serveur
                    if (spawner != null)
                    {
                        int targetIdx = spawner.allNodes.IndexOf(nextTarget);
                        if (targetIdx != -1) // Sécurité anti-reset à 0
                            SubmitChoiceServerRpc(targetIdx);
                    }
                }
                else
                {
                    // Spectateur : Attente synchro
                    float timeOut = 0;
                    // On attend que l'index change
                    while (spawner.allNodes.IndexOf(currentNode) == currentNodeIndex.Value && timeOut < 10f)
                    {
                        timeOut += Time.deltaTime;
                        await Task.Yield();
                    }

                    // Récupération de la nouvelle cible
                    if (spawner.allNodes.Count > currentNodeIndex.Value)
                        nextTarget = spawner.allNodes[currentNodeIndex.Value];

                    // Fallback sécurité si désynchro
                    if (nextTarget == null || !neighbors.Contains(nextTarget))
                        nextTarget = neighbors[0];
                }
            }

            // Déplacement
            Vector3 startPos = transform.position;
            Vector3 endPos = nextTarget.transform.position;
            float t = 0;

            while (t < 1)
            {
                t += Time.deltaTime * moveSpeed / Vector3.Distance(startPos, endPos);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                await Task.Yield();
            }

            transform.position = endPos;
            previousNode = currentNode;
            currentNode = nextTarget;

            // Mise à jour Serveur continue
            if (IsOwner && spawner != null)
            {
                int index = spawner.allNodes.IndexOf(currentNode);
                if (index != -1) UpdatePositionServerRpc(index);
            }
        }

        if (IsServer)
        {
            TurnManager.Instance.OnPlayerFinishedMoving();
        }
    }

    [ServerRpc]
    void UpdatePositionServerRpc(int index)
    {
        currentNodeIndex.Value = index;
    }

    [ServerRpc]
    void SubmitChoiceServerRpc(int targetIndex)
    {
        currentNodeIndex.Value = targetIndex;
    }
}