using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

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

        if (spawner != null)
        {
            // Sécurité : On s'assure que les nœuds existent
            if (spawner.allNodes.Count > currentNodeIndex.Value)
            {
                currentNode = spawner.allNodes[currentNodeIndex.Value];
                transform.position = currentNode.transform.position;
            }
        }
    }

    [ClientRpc]
    public void MoveToStepClientRpc(int steps)
    {
        // On stoppe toute routine en cours pour éviter les conflits
        StopAllCoroutines();
        StartCoroutine(MoveRoutine(steps));
    }

    private IEnumerator MoveRoutine(int steps)
    {
        // --- SÉCURITÉ ANTI-NULL (Ligne 83 probable) ---
        if (spawner == null) spawner = FindFirstObjectByType<Spawner>();
        if (currentNode == null && spawner != null) currentNode = spawner.allNodes[currentNodeIndex.Value];

        if (spawner == null || currentNode == null)
        {
            Debug.LogError("Mouvement impossible : Spawner ou Node actuel introuvable !");
            yield break;
        }

        for (int i = 0; i < steps; i++)
        {
            // 1. Déterminer la destination
            List<GameNode> neighbors = currentNode.connectedNodes;
            if (neighbors == null || neighbors.Count == 0) yield break;

            GameNode nextTarget = null;

            if (neighbors.Count > 1)
            {
                // Logique d'intersection (si tu en as une)
                // Pour l'instant on prend le premier qui n'est pas le précédent
                nextTarget = neighbors[0] == previousNode ? neighbors[1] : neighbors[0];
            }
            else
            {
                nextTarget = neighbors[0];
            }

            // 2. Animation de déplacement
            Vector3 startPos = transform.position;
            Vector3 endPos = nextTarget.transform.position;
            float t = 0;

            while (t < 1)
            {
                if (this == null) yield break; // Sécurité si l'objet est détruit

                t += Time.deltaTime * moveSpeed / Vector3.Distance(startPos, endPos);
                transform.position = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            // 3. Mise à jour des positions
            transform.position = endPos;
            previousNode = currentNode;
            currentNode = nextTarget;

            // Le serveur met à jour l'index réseau pour la synchronisation
            if (IsServer)
            {
                int index = spawner.allNodes.IndexOf(currentNode);
                currentNodeIndex.Value = index;
            }

            yield return new WaitForSeconds(0.1f);
        }

        // 4. Fin du tour
        if (IsServer)
        {
            Invoke(nameof(NotifyTurnManager), 0.5f);
        }
    }

    private void NotifyTurnManager()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnPlayerFinishedMoving();
    }
}