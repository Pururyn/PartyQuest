using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Splines;

[ExecuteAlways]
public class SpawnerOptimized : MonoBehaviour
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

    // Internal - Optimisation
    private List<GameObject> spawnedInstances = new List<GameObject>();
    private List<int> chosenIndices = new List<int>();
    private List<Vector3> knotPositionsCache = new List<Vector3>(); // Cache pour éviter les allocations
    private bool dirty = true;
    private int activeSeed;
    private int currentKnotCount = -1; // Pour vérifier les changements rapidement

    void OnEnable()
    {
        SetupParent();
        DetermineActiveSeed();
        dirty = true;
        UpdateSpawned();
    }

    void Start()
    {
        DetermineActiveSeed(); // Re-déterminer si on entre en PlayMode
        dirty = true;
        UpdateSpawned();
    }

    void OnValidate()
    {
        dirty = true;
        // OnValidate est souvent appelé, on ne fait pas la lourde UpdateSpawned() ici
    }

    void SetupParent()
    {
        if (instancesParent == null)
        {
            // Tente de trouver ou crée le parent des instances
            Transform existing = transform.Find("__Spawner_Instances");
            if (existing != null)
            {
                instancesParent = existing;
            }
            else
            {
                GameObject go = new GameObject("__Spawner_Instances");
                // On affiche en scène mais on ne veut pas que ça soit dans les builds
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
            // Seed pseudo-aléatoire à l'entrée en play (non deterministe entre runs)
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

        // Met à jour la position cache et vérifie si le nombre de noeuds a changé
        UpdateKnotPositionsCache();
        int n = knotPositionsCache.Count;

        // Condition de mise à jour (optimisée)
        if (!dirty && currentKnotCount == n) return;

        // Mise à jour si dirty ou si le nombre de noeuds a changé
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
        // 1. Pré-vérifications
        UpdateKnotPositionsCache();
        int n = knotPositionsCache.Count;
        currentKnotCount = n;

        if (n == 0 || prefabs == null || prefabs.Count == 0)
        {
            ClearSpawned();
            return;
        }

        // 2. Valider poids (allocation mémoire seulement si besoin)
        if (prefabWeights == null || prefabWeights.Count != prefabs.Count)
        {
            prefabWeights = new List<float>(prefabs.Count);
            for (int i = 0; i < prefabs.Count; i++) prefabWeights.Add(1f);
        }

        // 3. Gestion des instances
        if (clearPreviousInstances) ClearSpawned();
        int alreadySpawned = spawnedInstances.Count;

        // 4. Gestion des indices choisis (allocation/resize seulement si besoin)
        while (chosenIndices.Count < n) chosenIndices.Add(-1);
        if (chosenIndices.Count > n) chosenIndices.RemoveRange(n, chosenIndices.Count - n);

        // 5. Générer/mettre à jour les indices déterministes
        // C'est ici que le seed est utilisé pour la sélection.
        for (int i = 0; i < n; i++)
        {
            // Si dirty, ou si la position est nouvelle (index < 0)
            if (dirty || i >= alreadySpawned)
            {
                chosenIndices[i] = ChooseIndexByWeightDeterministic(prefabWeights, activeSeed + i);
            }
        }

        // 6. Instanciation / Repositionnement
        for (int i = 0; i < n; i++)
        {
            int chosen = Mathf.Clamp(chosenIndices[i], 0, prefabs.Count - 1);
            GameObject chosenPrefab = prefabs[chosen];
            if (chosenPrefab == null) continue;

            Vector3 position = knotPositionsCache[i];

            // Repositionner une instance existante
            if (!clearPreviousInstances && i < alreadySpawned)
            {
                var existing = spawnedInstances[i];
                if (existing != null)
                {
                    existing.transform.position = position;
                    // On ne gère pas le cas où le prefab a changé pour le même index
                }
                continue;
            }

<<<<<<< Updated upstream:PartyQuest/Assets/Scripts/Spawner.cs
            GameObject inst = null;
            #if UNITY_EDITOR
            if (!Application.isPlaying)
=======
            // Créer une nouvelle instance
            GameObject inst = InstantiateNewPrefab(chosenPrefab, position, chosen);
            if (inst != null)
>>>>>>> Stashed changes:PartyQuest/Assets/Spawner.cs
            {
                // Si on a ClearSpawned() on ajoute tout. Si on n'a pas ClearSpawned(), on ajoute seulement les nouveaux.
                spawnedInstances.Add(inst);
            }
        }

        // Supprimer les instances en trop si le nombre de nœuds a diminué (seulement si clearPreviousInstances est false)
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
<<<<<<< Updated upstream:PartyQuest/Assets/Scripts/Spawner.cs
            #else
            inst = GameObject.Instantiate(chosenPrefab, instancesParent);
            #endif
            if (inst == null) continue;

            inst.transform.position = positions[i];
            inst.transform.rotation = Quaternion.identity;
            inst.transform.SetParent(instancesParent, true);

            if (!useWeightsAsProbability)
            {
                float scale = 1f;
                if (prefabWeights != null && prefabWeights.Count > chosen) scale = Mathf.Max(0.0001f, prefabWeights[chosen]);
                inst.transform.localScale = Vector3.one * scale;
            }

            spawnedInstances.Add(inst);
=======
>>>>>>> Stashed changes:PartyQuest/Assets/Spawner.cs
        }

        dirty = false;
    }

<<<<<<< Updated upstream:PartyQuest/Assets/Scripts/Spawner.cs
    public List<Vector3> GetKnotPositions()
=======
    GameObject InstantiateNewPrefab(GameObject chosenPrefab, Vector3 position, int chosenIndex)
>>>>>>> Stashed changes:PartyQuest/Assets/Spawner.cs
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

    // Anciennement GetKnotPositions, maintenant il met à jour un cache List<Vector3> pour éviter les allocations en Update.
    void UpdateKnotPositionsCache()
    {
        knotPositionsCache.Clear();

        if (useSplineContainer && splineContainer != null && splineContainer.Splines != null)
        {
            try
            {
                // Gère MULTIPLES splines
                foreach (var spline in splineContainer.Splines)
                {
                    if (spline != null)
                    {
                        // On n'instancie que sur les noeuds réels (knots)
                        for (int i = 0; i < spline.Count; i++)
                        {
                            var knot = spline[i];
                            // Transforme la position locale du noeud en position mondiale (World Space)
                            knotPositionsCache.Add(splineContainer.transform.TransformPoint(knot.Position));
                        }
                    }
                }

                if (knotPositionsCache.Count > 0) return;
            }
            catch { /* fallback below */ }
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

    // Le reste du code reste le même, car il est efficace

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
<<<<<<< Updated upstream:PartyQuest/Assets/Scripts/Spawner.cs
           #if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(go);
            else Destroy(go);
           #else
            Destroy(go);
           #endif
=======
    #if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(go);
                else Destroy(go);
    #else
                Destroy(go);
    #endif
>>>>>>> Stashed changes:PartyQuest/Assets/Spawner.cs
        }
        spawnedInstances.Clear();
    }
}