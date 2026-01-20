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
    public NetworkVariable<int> Score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // CORRECTION : Ajout de la variable manquante pour le bonus de dé
    public NetworkVariable<int> nextRollBonus = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        Score.OnValueChanged += (oldVal, newVal) => {
            Debug.Log($"Score mis à jour : {newVal}");
        };
    }

    [ClientRpc]
    public void MoveToStepClientRpc(int steps)
    {
        _ = MoveRoutineAsync(steps);
    }

    private async Task MoveRoutineAsync(int steps)
    {
        if (spawner == null) spawner = FindFirstObjectByType<Spawner>();

        for (int i = 0; i < steps; i++)
        {
            if (this == null || transform == null) return;

            List<GameNode> neighbors = currentNode.connectedNodes;
            GameNode nextTarget = null;

            if (neighbors.Count > 1)
            {
                List<GameNode> choices = new List<GameNode>();
                foreach (var n in neighbors) if (n != previousNode) choices.Add(n);

                if (choices.Count > 1)
                {
                    if (IsOwner && !IsAI.Value)
                    {
                        nextTarget = await IntersectionManager.Instance.WaitForPlayerChoice(currentNode, choices);
                        if (this == null) return;
                        SubmitChoiceServerRpc(spawner.allNodes.IndexOf(nextTarget));
                    }
                    else
                    {
                        if (IsServer && IsAI.Value)
                        {
                            nextTarget = choices[Random.Range(0, choices.Count)];
                            // CORRECTION CS1503 : On passe l'index (int) et non l'objet (GameNode)
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
        if (tile == null) return;

        if (tile.type == TileType.Green)
        {
            if (Random.value > 0.5f)
            {
                nextRollBonus.Value = 2;
                Debug.Log("Case VERTE : Bonus +2 au prochain tour !");
            }
            else
            {
                Score.Value += 5;
                Debug.Log("Case VERTE : +5 points !");
            }
        }
        else if (tile.type == TileType.Red)
        {
            if (Random.value > 0.5f)
            {
                Score.Value = Mathf.Max(0, Score.Value - 3);
                Debug.Log("Case ROUGE : -3 points !");
            }
            else
            {
                Debug.Log("Case ROUGE : Recul !");
                _ = MoveBackwardsAsync(1);
            }
        }
    }

    // CORRECTION CS0103 : Ajout de la méthode de recul manquante
    private async Task MoveBackwardsAsync(int steps)
    {
        if (previousNode == null) return;

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
        if (IsServer) currentNodeIndex.Value = spawner.allNodes.IndexOf(currentNode);
    }

    [ServerRpc]
    private void SubmitChoiceServerRpc(int targetIndex)
    {
        currentNodeIndex.Value = targetIndex;
    }
}