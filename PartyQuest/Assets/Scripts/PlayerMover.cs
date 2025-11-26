using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMover : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Référence au script Spawner pour obtenir la liste des positions.")]
    [SerializeField]
    private Spawner targetSpawner;

    [Header("Paramètres de mouvement")]
    [Tooltip("Vitesse de déplacement (plus c'est grand, plus c'est rapide)")]
    [SerializeField, Range(1f, 10f)]
    private float moveSpeed = 1.0f;

    [Tooltip("Temps d'attente à chaque nœud avant de passer au suivant.")]
    [SerializeField, Range(0f, 1f)]
    private float waitTimeAtKnot = 0.5f;

    // Index de la position actuelle du joueur
    private int currentKnotIndex = 0;

    // Cache de la liste de positions pour éviter d'appeler la méthode à chaque frame
    private List<Vector3> knotPositions;


    void Start()
    {
        if (targetSpawner == null)
        {
            Debug.LogError("PlayerMover nécessite une référence au Spawner ! Assurez-vous qu'il est assigné dans l'Inspecteur.");
            enabled = false;
            return;
        }

        // Récupère les positions une fois au début
        knotPositions = targetSpawner.GetKnotPositions();

        if (knotPositions == null || knotPositions.Count < 2)
        {
            Debug.LogError("Le Spawner n'a pas assez de nœuds pour le mouvement.");
            enabled = false;
            return;
        }

        // Déplace le joueur au premier nœud (index 0)
        transform.position = knotPositions[0];

        // Lance la séquence de déplacement automatique
        StartCoroutine(FollowPath());
    }

    /// <summary>
    /// Coroutine pour déplacer le joueur automatiquement de nœud en nœud.
    /// </summary>
    IEnumerator FollowPath()
    {
        // On commence au premier nœud
        currentKnotIndex = 0;

        // Boucle infinie pour suivre le chemin
        while (true)
        {
            // Position de départ
            Vector3 startPos = transform.position;

            // Index du nœud cible (le nœud suivant)
            int nextKnotIndex = (currentKnotIndex + 1) % knotPositions.Count;

            // Si la spline n'est pas "fermée", vous pourriez arrêter à la fin :
            // if (nextKnotIndex == 0 && currentKnotIndex == knotPositions.Count - 1) yield break;

            Vector3 targetPos = knotPositions[nextKnotIndex];

            // Calcule la distance pour déterminer le temps de mouvement (pour une vitesse constante)
            float distance = Vector3.Distance(startPos, targetPos);
            // Time.deltaTime n'est pas utilisé ici, nous utilisons le Time.time pour une interpolation temporelle
            float journeyDuration = distance / moveSpeed;

            float startTime = Time.time;

            // Déplacement fluide vers le nœud cible
            while (Time.time < startTime + journeyDuration)
            {
                float timeElapsed = Time.time - startTime;
                float t = timeElapsed / journeyDuration; // Valeur entre 0 et 1

                transform.position = Vector3.Lerp(startPos, targetPos, t);

                //// Optionnel : Rotation vers le point cible
                //Quaternion targetRotation = Quaternion.LookRotation(targetPos - transform.position);
                //transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);

                yield return null; // Attend la prochaine frame
            }

            // Assurez-vous que le joueur est exactement sur la cible
            transform.position = targetPos;

            // Met à jour l'index
            currentKnotIndex = nextKnotIndex;

            // Temps d'attente au nœud avant de repartir
            yield return new WaitForSeconds(waitTimeAtKnot);
        }
    }
}