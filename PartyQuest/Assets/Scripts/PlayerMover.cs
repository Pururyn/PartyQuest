using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode; // Nécessaire pour le réseau

public class PlayerMover : NetworkBehaviour
{
    [Header("Références")]
    [SerializeField] private Spawner targetSpawner;

    [Header("Paramètres de mouvement")]
    [SerializeField, Range(1f, 10f)] private float moveSpeed = 5.0f;
    [SerializeField, Range(0f, 1f)] private float waitTimeAtKnot = 0.1f;

    // Index de la position actuelle du joueur
    // NetworkVariable permet de synchroniser la position logique (index) entre tous
    public NetworkVariable<int> currentKnotIndex = new NetworkVariable<int>(0);

    private List<Vector3> knotPositions;

    public override void OnNetworkSpawn()
    {
        if (targetSpawner == null) targetSpawner = FindObjectOfType<Spawner>();

        // Récupère les positions
        if (targetSpawner != null)
        {
            knotPositions = targetSpawner.GetKnotPositions();

            // Place le joueur au départ s'il vient d'arriver et que c'est le serveur qui décide
            if (IsServer && knotPositions.Count > 0)
            {
                transform.position = knotPositions[currentKnotIndex.Value];
            }
        }
    }

    /// <summary>
    /// Appelée par le TurnManager (côté Serveur) après le lancer de dé.
    /// </summary>
    public void StartMoveSequence(int steps)
    {
        // On prévient tous les clients de lancer l'animation
        MoveClientRpc(steps);
    }

    [ClientRpc]
    private void MoveClientRpc(int steps)
    {
        StartCoroutine(MoveRoutine(steps));
    }

    IEnumerator MoveRoutine(int steps)
    {
        if (knotPositions == null || knotPositions.Count == 0) yield break;

        for (int i = 0; i < steps; i++)
        {
            // 1. Calcul du prochain index
            int nextIndex = (currentKnotIndex.Value + 1) % knotPositions.Count;
            Vector3 startPos = transform.position;
            Vector3 targetPos = knotPositions[nextIndex];

            // 2. Animation de déplacement vers le prochain noeud
            float distance = Vector3.Distance(startPos, targetPos);
            float duration = distance / moveSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = targetPos;

            // 3. Mise à jour de l'index (seulement si on est le serveur pour la variable réseau, 
            // mais on le simule localement pour la suite de la boucle)
            if (IsServer) currentKnotIndex.Value = nextIndex;

            yield return new WaitForSeconds(waitTimeAtKnot);
        }

        // 4. Fin du déplacement
        if (IsServer)
        {
            // On notifie le TurnManager que le tour est techniquement fini
            TurnManager.Instance.FinishTurn();
        }
    }
}