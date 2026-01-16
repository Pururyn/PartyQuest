using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerMover : NetworkBehaviour
{
    [Header("Réglages")]
    [SerializeField] private float moveSpeed = 5.0f;

    // --- DONNÉES SYNCHRONISÉES ---
    public NetworkVariable<bool> isAI = new NetworkVariable<bool>(false);// Permet de savoir si ce pion est géré par l'IA
    public NetworkVariable<int> characterId = new NetworkVariable<int>(0);// Pour la future sélection de perso
    public NetworkVariable<int> currentKnotIndex = new NetworkVariable<int>(0);// Position actuelle sur le plateau (index du noeud)

    private List<Vector3> knotPositions;
    private Spawner targetSpawner;

    public override void OnNetworkSpawn()
    {
        // Trouve le spawner automatiquement
        targetSpawner = FindFirstObjectByType<Spawner>();

        if (targetSpawner != null)
        {
            knotPositions = targetSpawner.GetKnotPositions();
            // Placement initial
            if (knotPositions.Count > 0)
                transform.position = knotPositions[currentKnotIndex.Value];
        }
    }

    // Fonction appelée par le TurnManager pour lancer l'animation
    [ClientRpc]
    public void MoveToStepClientRpc(int steps)
    {
        StartCoroutine(MoveRoutine(steps));
    }

    private IEnumerator MoveRoutine(int steps)
    {
        if (knotPositions == null) yield break;

        for (int i = 0; i < steps; i++)
        {
            // Calcul du prochain noeud (boucle le circuit avec %)
            int nextIndex = (currentKnotIndex.Value + 1) % knotPositions.Count;
            Vector3 start = transform.position;
            Vector3 end = knotPositions[nextIndex];

            // Animation fluide
            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime * moveSpeed / Vector3.Distance(start, end);
                transform.position = Vector3.Lerp(start, end, t);
                yield return null;
            }

            // Validation de la position
            transform.position = end;

            // Mise à jour de la variable réseau (Seul le serveur a le droit d'écrire)
            if (IsServer) currentKnotIndex.Value = nextIndex;

            yield return new WaitForSeconds(0.1f); // Petite pause sur la case
        }

        // Mouvement terminé -> On prévient le chef
        if (IsServer)
        {
            TurnManager.Instance.OnPlayerFinishedMoving();
        }
    }
}