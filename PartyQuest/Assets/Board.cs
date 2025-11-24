using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SplineKnotSpawner : MonoBehaviour
{
    [Header("Nodes (knots) of the spline")]
    [Tooltip("Les Transforms représentant les noeuds (knot) de ton spline.")]
    public List<Transform> knots = new List<Transform>();

    [Header("Prefabs (exactement 4)")]
    public GameObject[] prefabs = new GameObject[4];

    [Tooltip("Poids relatifs pour la sélection aléatoire (peuvent être 0).")]
    public float[] ratios = new float[4] { 1f, 1f, 1f, 1f };

    [Header("Options d'instanciation")]
    public Transform parentForInstances = null;
    public Vector3 localOffset = Vector3.zero;
    public bool rotateToTangent = false; // si tu fournis une méthode GetTangentAt(int index) tu peux l'utiliser
    public bool clearBeforeSpawn = true;
    public bool useSeed = false;
    public int seed = 12345;

    // runtime
    private System.Random rng;

    void OnValidate()
    {
        if (prefabs == null || prefabs.Length != 4) prefabs = new GameObject[4];
        if (ratios == null || ratios.Length != 4) ratios = new float[4] { 1f, 1f, 1f, 1f };
    }

    void Awake()
    {
        SetupRng();
    }

    void SetupRng()
    {
        rng = useSeed ? new System.Random(seed) : new System.Random();
    }

    /// <summary>
    /// Appelle cette méthode pour (re)générer les instances sur chaque noeud.
    /// </summary>
    public void SpawnOnKnots()
    {
        if (knots == null || knots.Count == 0)
        {
            Debug.LogWarning("Aucun knot fourni dans SplineKnotSpawner.");
            return;
        }

        SetupRng();

        if (clearBeforeSpawn && parentForInstances != null)
        {
            // supprime les enfants du parent (attention en éditeur)
            for (int i = parentForInstances.childCount - 1; i >= 0; i--)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(parentForInstances.GetChild(i).gameObject);
                else Destroy(parentForInstances.GetChild(i).gameObject);
#else
                Destroy(parentForInstances.GetChild(i).gameObject);
#endif
            }
        }

        // prépare distribution cumulative
        List<float> cum = BuildCumulativeRatios();

        for (int i = 0; i < knots.Count; i++)
        {
            var knot = knots[i];
            if (knot == null) continue;

            // pick prefab index by weighted random
            float r = (float)rng.NextDouble();
            int idx = PickIndexFromCumulative(cum, r);

            GameObject prefab = prefabs[idx];
            if (prefab == null) continue;

            Vector3 spawnPos = knot.position + knot.TransformVector(localOffset);
            Quaternion spawnRot = Quaternion.identity;

            if (rotateToTangent)
            {
                // Si tu as une fonction GetTangentAtIndex, tu peux remplacer la logique ci-dessous.
                // Ici on se contente d'utiliser la rotation locale du noeud pour l'orientation.
                spawnRot = knot.rotation;
            }

            Instantiate(prefab, spawnPos, spawnRot, parentForInstances != null ? parentForInstances : null);
        }
    }

    private List<float> BuildCumulativeRatios()
    {
        List<float> cum = new List<float>(4);
        float total = 0f;
        for (int i = 0; i < ratios.Length; i++) total += Mathf.Max(0f, ratios[i]);
        if (total <= 0f) total = 1f;
        float running = 0f;
        for (int i = 0; i < ratios.Length; i++)
        {
            running += Mathf.Max(0f, ratios[i]) / total;
            cum.Add(running);
        }
        cum[cum.Count - 1] = 1f;
        return cum;
    }

    private int PickIndexFromCumulative(List<float> cum, float value)
    {
        for (int i = 0; i < cum.Count; i++)
            if (value <= cum[i]) return i;
        return cum.Count - 1;
    }

#if UNITY_EDITOR
    // Bouton dans l'inspecteur pour faciliter le workflow en éditeur
    [CustomEditor(typeof(SplineKnotSpawner))]
    private class SplineKnotSpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SplineKnotSpawner sp = (SplineKnotSpawner)target;
            if (GUILayout.Button("Clear & Spawn on Knots"))
            {
                sp.SpawnOnKnots();
                // marquer la scène comme sale si on instancie en editor
                if (!Application.isPlaying)
                    UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            }
        }
    }
#endif
}
