using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks; // INDISPENSABLE pour Task

public class IntersectionManager : MonoBehaviour
{
    public static IntersectionManager Instance;

    [Header("Préfab")]
    public GameObject arrowPrefab; // Glisse ici ton prefab de flèche (Arrow)

    // Variables internes
    private List<GameObject> activeArrows = new List<GameObject>();
    private GameNode selectedNode = null;

    void Awake()
    {
        // Singleton simple
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    /// <summary>
    /// Cette fonction est appelée par PlayerMover. 
    /// Elle met le jeu en pause (via await) jusqu'à ce qu'un choix soit fait.
    /// </summary>
    public async Task<GameNode> WaitForPlayerChoice(GameNode currentNode, List<GameNode> options)
    {
        selectedNode = null;
        ClearArrows();

        foreach (var option in options)
        {
            // 1. Calcul de la direction en 2D
            Vector3 direction = (option.transform.position - currentNode.transform.position).normalized;

            // 2. Position : On place la flèche entre les deux points
            // On ne touche PAS au Y (hauteur), on reste sur le plan 2D
            Vector3 arrowPos = currentNode.transform.position + (direction * 2.0f);

            // 3. Rotation 2D (Mathématiques d'angle Z)
            // On calcule l'angle pour que la flèche pointe vers la destination
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            // NOTE : Si ta flèche pointe vers le HAUT dans son image d'origine, ajoute -90 ici
            // exemple : Quaternion.Euler(0, 0, angle - 90);
            Quaternion rotation = Quaternion.Euler(0, 0, angle);

            // 4. Instantiation
            GameObject go = Instantiate(arrowPrefab, arrowPos, rotation);

            // 5. IMPORTANT : S'assurer que la flèche est visible (Order in Layer)
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = 10; // Force la flèche à s'afficher DEVANT le décor
                sr.color = Color.white; // S'assure qu'elle n'est pas transparente
            }

            // Configuration du script de clic
            PathArrow arrowScript = go.GetComponent<PathArrow>();
            if (arrowScript == null) arrowScript = go.AddComponent<PathArrow>();

            arrowScript.Setup(option, OnArrowClicked);
            activeArrows.Add(go);
        }

        // Attente du choix
        while (selectedNode == null)
        {
            await Task.Yield();
        }

        ClearArrows();
        return selectedNode;
    }

    // Callback appelé par le script de la flèche
    void OnArrowClicked(GameNode choice)
    {
        selectedNode = choice;
    }

    void ClearArrows()
    {
        foreach (var arrow in activeArrows)
        {
            if (arrow != null) Destroy(arrow);
        }
        activeArrows.Clear();
    }
}