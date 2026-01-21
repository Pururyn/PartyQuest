using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using System.Linq;

// AJOUT ESSENTIEL POUR CORRIGER L'ERREUR :
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class Spawner : MonoBehaviour
{
    [Header("Source des noeuds")]
    public bool useSplineContainer = true;
    public SplineContainer splineContainer;
    public List<Transform> manualKnotTransforms = new List<Transform>();

    [Header("Logique & Connexions")]
    public float mergeDistance = 0.5f;
    public bool autoConnectNodes = true;

    [Header("Prefabs & poids")]
    public List<GameObject> prefabs = new List<GameObject>();
    public List<float> prefabWeights = new List<float>();

    [Header("Options d'instanciation")]
    public bool instantiateInEditor = true;
    public bool useWeightsAsProbability = true;
    public bool clearPreviousInstances = true;
    public Transform instancesParent;

    [Header("Seed")]
    public int seed = 12345;
    public bool randomizeSeedOnPlay = false;

    // Liste accessible par le PlayerMover
    public List<GameNode> allNodes = new List<GameNode>();

    // Interne
    private List<GameObject> spawnedInstances = new List<GameObject>();
    private List<Vector3> knotPositionsCache = new List<Vector3>();
    private bool dirty = true;
    private int currentKnotCount = -1;

    void OnEnable() { SetupParent(); dirty = true; UpdateSpawned(); }
    void Start()
    {
        // MODIFICATION ICI :
        // Si on est en train de jouer, on ne génère RIEN. On garde ce qui est dans la scène.
        if (Application.isPlaying) return;

        dirty = true;
        UpdateSpawned();
    }
    void OnValidate() { dirty = true; }

    void SetupParent()
    {
        if (instancesParent == null)
        {
            Transform existing = transform.Find("__Spawner_Instances");
            if (existing != null)
            {
                instancesParent = existing;
            }
            else
            {
                GameObject go = new GameObject("__Spawner_Instances");
                // MODIFICATION ICI : On enlève le DontSaveInBuild pour que ça reste dans le jeu final
                go.hideFlags = HideFlags.None;
                go.transform.SetParent(transform, false);
                instancesParent = go.transform;
            }
        }
    }

    void Update()
    {
        // MODIFICATION ICI : Sécurité supplémentaire
        if (Application.isPlaying) return;

        if (!instantiateInEditor) return;
        UpdateKnotPositionsCache();
        int n = knotPositionsCache.Count;
        if (!dirty && currentKnotCount == n) return;
        currentKnotCount = n;
        UpdateSpawned();
    }

    public void ForceRespawn()
    {
        dirty = true;
        UpdateSpawned();
    }

    public void UpdateSpawned()
    {
        UpdateKnotPositionsCache();
        int n = knotPositionsCache.Count;
        currentKnotCount = n;

        // --- NETTOYAGE ROBUSTE ---
        if (clearPreviousInstances && instancesParent != null)
        {
            var children = new List<GameObject>();
            foreach (Transform child in instancesParent) children.Add(child.gameObject);

            foreach (var child in children)
            {
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            spawnedInstances.Clear();
            allNodes.Clear();
        }

        if (n == 0 || prefabs == null || prefabs.Count == 0) return;

        var rnd = new System.Random(Application.isPlaying && randomizeSeedOnPlay ? UnityEngine.Random.Range(0, 10000) : seed);

        for (int i = 0; i < n; i++)
        {
            GameObject chosenPrefab = prefabs[0];
            if (prefabs.Count > 1) chosenPrefab = prefabs[rnd.Next(prefabs.Count)];

            Vector3 position = knotPositionsCache[i];
            GameObject inst = InstantiateNewPrefab(chosenPrefab, position);

            if (inst != null)
            {
                inst.name = $"Node_{i}";
                spawnedInstances.Add(inst);

                GameNode node = inst.GetComponent<GameNode>();
                if (node == null) node = inst.AddComponent<GameNode>();
                node.connectedNodes.Clear();
                allNodes.Add(node);
            }
        }

        if (autoConnectNodes && useSplineContainer) BuildConnections();

        dirty = false;
    }

    GameObject InstantiateNewPrefab(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return null;
        GameObject inst = null;

        // C'est ici que l'erreur se produisait. Maintenant c'est protégé.
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Utilise PrefabUtility pour garder le lien avec le prefab original dans l'éditeur
            inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, instancesParent);
        }
        else
        {
            inst = Instantiate(prefab, instancesParent);
        }
#else
        // Dans le jeu compilé, on utilise le Instantiate classique
        inst = Instantiate(prefab, instancesParent);
#endif

        if (inst != null) inst.transform.position = pos;
        return inst;
    }

    void UpdateKnotPositionsCache()
    {
        knotPositionsCache.Clear();
        if (useSplineContainer && splineContainer != null)
        {
            foreach (var spline in splineContainer.Splines)
            {
                int len = spline.Count;
                for (int i = 0; i < len; i++)
                {
                    Vector3 worldPos = splineContainer.transform.TransformPoint(spline[i].Position);
                    bool duplicate = false;
                    foreach (var p in knotPositionsCache)
                        if (Vector3.Distance(p, worldPos) < mergeDistance) { duplicate = true; break; }

                    if (!duplicate) knotPositionsCache.Add(worldPos);
                }
            }
        }
    }

    void BuildConnections()
    {
        foreach (var spline in splineContainer.Splines)
        {
            for (int i = 0; i < spline.Count; i++)
            {
                Vector3 pA = splineContainer.transform.TransformPoint(spline[i].Position);
                GameNode nodeA = FindNodeAt(pA);
                if (nodeA == null) continue;

                if (i < spline.Count - 1 || spline.Closed)
                {
                    int nextI = (i + 1) % spline.Count;
                    Vector3 pB = splineContainer.transform.TransformPoint(spline[nextI].Position);
                    GameNode nodeB = FindNodeAt(pB);

                    if (nodeB != null && nodeA != nodeB)
                    {
                        if (!nodeA.connectedNodes.Contains(nodeB)) nodeA.connectedNodes.Add(nodeB);
                        if (!nodeB.connectedNodes.Contains(nodeA)) nodeB.connectedNodes.Add(nodeA);
                    }
                }
            }
        }
    }

    GameNode FindNodeAt(Vector3 pos)
    {
        foreach (var n in allNodes)
            if (Vector3.Distance(n.transform.position, pos) < mergeDistance) return n;
        return null;
    }
}