using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Splines;

[ExecuteAlways]
public class Spawner : MonoBehaviour
{
    // --- Source des noeuds ---
    [Header("Source des noeuds")]
    public bool useSplineContainer = true;
    public SplineContainer splineContainer;
    public List<Transform> manualKnotTransforms = new List<Transform>();

    // --- Prefabs & poids ---
    [Header("Prefabs & poids")]
    public List<GameObject> prefabs = new List<GameObject>();
    public List<float> prefabWeights = new List<float>();

    // --- Options d'instanciation ---
    [Header("Options d'instanciation")]
    public bool instantiateInEditor = true;
    public bool useWeightsAsProbability = true;
    public bool clearPreviousInstances = true;
    public Transform instancesParent;

    // --- Seed / stabilité ---
    [Header("Seed / stabilité")]
    [Tooltip("Seed deterministe pour la génération. Change ce nombre pour obtenir un autre placement.")]
    public int seed = 12345;
    [Tooltip("Si true, génère aléatoirement un seed à l'entrée en PlayMode (non deterministe).")]
    public bool randomizeSeedOnPlay = false;

    // Internal - Optimisations
    private List<GameObject> spawnedInstances = new List<GameObject>();
    private List<int> chosenIndices = new List<int>();
    private List<Vector3> knotPositionsCache = new List<Vector3>(); // Cache pour éviter les allocations en Update
    private bool dirty = true;
    private int activeSeed;
    private int currentKnotCount = -1; // Compteur pour vérifier si le nombre de noeuds a changé

    public List<Vector3> GetKnotPositions()
    {
        // Assurez-vous que le cache est mis à jour avant de le retourner
        UpdateKnotPositionsCache();
        return knotPositionsCache;
    }

    void OnEnable()
    {
        SetupParent();
        DetermineActiveSeed();
        dirty = true;
        UpdateSpawned();
    }

    void Start()
    {
        DetermineActiveSeed();
        dirty = true;
        UpdateSpawned();
    }

    void OnValidate()
    {
        // Marque comme 'dirty' quand on change une propriété dans l'inspecteur
        dirty = true;
    }

    // Nouvelle fonction pour initialiser/trouver le parent des instances
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
                go.hideFlags = HideFlags.DontSaveInBuild;
                go.transform.SetParent(transform, false);
                instancesParent = go.transform;
            }
        }
    }

    void DetermineActiveSeed()
    {
        if (Application.isPlaying && randomizeSeedOnPlay)
        {
            activeSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }
        else
        {
            activeSeed = seed;
        }
    }

    void Update()
    {
        if (!Application.isPlaying && !instantiateInEditor) return;

        // Met à jour la position cache
        UpdateKnotPositionsCache();
        int n = knotPositionsCache.Count;

        // Condition de mise à jour optimisée : vérifie si dirty OU si le nombre de noeuds a changé
        if (!dirty && currentKnotCount == n) return;

        currentKnotCount = n;
        UpdateSpawned();
    }

    public void ForceRespawn()
    {
        DetermineActiveSeed();
        dirty = true;
        UpdateSpawned();
    }

    public void UpdateSpawned()
    {
        // Utilise le cache des positions au lieu d'allouer une nouvelle liste
        UpdateKnotPositionsCache();
        int n = knotPositionsCache.Count;
        currentKnotCount = n;

        if (n == 0 || prefabs == null || prefabs.Count == 0)
        {
            ClearSpawned();
            return;
        }

        // Valider poids (inchangé, nécessaire)
        if (prefabWeights == null || prefabWeights.Count != prefabs.Count)
        {
            prefabWeights = new List<float>(prefabs.Count);
            for (int i = 0; i < prefabs.Count; i++) prefabWeights.Add(1f);
        }

        // Gérer le nettoyage
        if (clearPreviousInstances) ClearSpawned();
        int alreadySpawned = spawnedInstances.Count;

        // Ajuster la taille des indices choisis
        while (chosenIndices.Count < n) chosenIndices.Add(-1);
        if (chosenIndices.Count > n) chosenIndices.RemoveRange(n, chosenIndices.Count - n);

        // Générer les indices déterministes (utilise activeSeed + i pour la stabilité)
        for (int i = 0; i < n; i++)
        {
            // Réinitialise l'indice si 'dirty' ou si c'est une nouvelle position
            if (dirty || i >= alreadySpawned)
            {
                chosenIndices[i] = ChooseIndexByWeightDeterministic(prefabWeights, activeSeed + i);
            }
        }

        // Instanciation / Repositionnement
        for (int i = 0; i < n; i++)
        {
            int chosen = Mathf.Clamp(chosenIndices[i], 0, prefabs.Count - 1);
            GameObject chosenPrefab = prefabs[chosen];
            if (chosenPrefab == null) continue;

            Vector3 position = knotPositionsCache[i];

            // Repositionnement (si on ne clear pas et qu'une instance existe déjà pour cet index)
            if (!clearPreviousInstances && i < alreadySpawned)
            {
                var existing = spawnedInstances[i];
                if (existing != null) existing.transform.position = position;
                continue;
            }

            // Création d'une nouvelle instance
            GameObject inst = InstantiateNewPrefab(chosenPrefab, position, chosen);
            if (inst != null)
            {
                spawnedInstances.Add(inst);
            }
        }

        // Supprimer les instances en trop si le nombre de nœuds a diminué
        if (!clearPreviousInstances && spawnedInstances.Count > n)
        {
            int itemsToRemove = spawnedInstances.Count - n;
            for (int i = 0; i < itemsToRemove; i++)
            {
                var go = spawnedInstances[spawnedInstances.Count - 1];
                spawnedInstances.RemoveAt(spawnedInstances.Count - 1);
            #if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(go);
                else Destroy(go);
            #else
                            Destroy(go);
            #endif
            }
        }

        dirty = false;
    }

    // Nouvelle fonction pour gérer l'instanciation (plus propre)
    GameObject InstantiateNewPrefab(GameObject chosenPrefab, Vector3 position, int chosenIndex)
    {
        GameObject inst = null;
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Instanciation spécifique à l'éditeur pour conserver le lien prefab
            inst = PrefabUtility.InstantiatePrefab(chosenPrefab, instancesParent) as GameObject;
        }
        else
        {
            inst = GameObject.Instantiate(chosenPrefab, instancesParent);
        }
        #else
                inst = GameObject.Instantiate(chosenPrefab, instancesParent);
        #endif

        if (inst == null) return null;

        inst.transform.position = position;
        inst.transform.rotation = Quaternion.identity;
        // La mise en parent est gérée dans InstantiatePrefab/Instantiate

        if (!useWeightsAsProbability)
        {
            float scale = 1f;
            if (prefabWeights != null && prefabWeights.Count > chosenIndex) scale = Mathf.Max(0.0001f, prefabWeights[chosenIndex]);
            inst.transform.localScale = Vector3.one * scale;
        }

        return inst;
    }

    // Remplace GetKnotPositions(). Met à jour le cache sans créer de nouvelle liste.
    void UpdateKnotPositionsCache()
    {
        knotPositionsCache.Clear();

        if (useSplineContainer && splineContainer != null && splineContainer.Splines != null)
        {
            try
            {
                // *** SUPPORT MULTI-SPLINE ICI ***
                foreach (var spline in splineContainer.Splines)
                {
                    if (spline != null)
                    {
                        // On itère sur les noeuds de CHAQUE spline
                        for (int i = 0; i < spline.Count; i++)
                        {
                            var knot = spline[i];
                            // Transforme la position locale du noeud en position mondiale
                            knotPositionsCache.Add(splineContainer.transform.TransformPoint(knot.Position));
                        }
                    }
                }

                if (knotPositionsCache.Count > 0) return;
            }
            catch { /* Fallback en cas d'erreur avec le package Splines */ }
        }

        if (manualKnotTransforms != null)
        {
            foreach (var t in manualKnotTransforms)
                if (t != null) knotPositionsCache.Add(t.position);

            if (knotPositionsCache.Count > 0) return;
        }

        // Fallback: utilise la position des enfants
        foreach (Transform child in transform)
            knotPositionsCache.Add(child.position);
    }

    // Choix deterministe à partir d'un seed (System.Random)
    int ChooseIndexByWeightDeterministic(List<float> weights, int deterministSeed)
    {
        double total = 0.0;
        foreach (var w in weights) total += Math.Max(0.0, w);
        if (total <= 0.0) return 0;

        // System.Random est essentiel pour le DÉTERMINISME avec un seed donné
        var rnd = new System.Random(deterministSeed);
        double r = rnd.NextDouble() * total;
        double s = 0.0;
        for (int i = 0; i < weights.Count; i++)
        {
            s += Math.Max(0.0, weights[i]);
            if (r <= s) return i;
        }
        return weights.Count - 1;
    }

    void ClearSpawned()
    {
        for (int i = spawnedInstances.Count - 1; i >= 0; i--)
        {
            var go = spawnedInstances[i];
            if (go == null) continue;
        #if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(go);
            else Destroy(go);
        #else
            Destroy(go);
        #endif
        }
        spawnedInstances.Clear();
    }
}