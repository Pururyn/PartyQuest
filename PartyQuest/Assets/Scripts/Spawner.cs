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
    [Header("Source des noeuds")]
    public bool useSplineContainer = true;
    public SplineContainer splineContainer;
    public List<Transform> manualKnotTransforms = new List<Transform>();

    [Header("Prefabs & poids")]
    public List<GameObject> prefabs = new List<GameObject>();
    public List<float> prefabWeights = new List<float>();

    [Header("Options d'instanciation")]
    public bool instantiateInEditor = true;
    public bool useWeightsAsProbability = true;
    public bool clearPreviousInstances = true;
    public Transform instancesParent;

    [Header("Seed / stabilité")]
    [Tooltip("Seed deterministe pour la génération. Change ce nombre pour obtenir un autre placement.")]
    public int seed = 12345;
    [Tooltip("Si true, génère aléatoirement un seed à l'entrée en PlayMode (non deterministe).")]
    public bool randomizeSeedOnPlay = false;

    // Internal
    private List<GameObject> spawnedInstances = new List<GameObject>();
    private List<int> chosenIndices = new List<int>();
    private bool dirty = true;
    private int activeSeed;

    void OnEnable()
    {
        if (instancesParent == null)
        {
            GameObject go = transform.Find("__Spawner_Instances")?.gameObject;
            if (go == null)
            {
                go = new GameObject("__Spawner_Instances");
                // on affiche en scène mais on ne veut pas que ça soit dans les builds
                go.hideFlags = HideFlags.DontSaveInBuild;
                go.transform.SetParent(transform, false);
            }
            instancesParent = go.transform;
        }

        // determine active seed
        DetermineActiveSeed();

        dirty = true;
        UpdateSpawned();
    }

    void Start()
    {
        // si on entre en Play, on peut randomiser le seed si demandé
        DetermineActiveSeed();
        dirty = true;
        UpdateSpawned();
    }

    void OnValidate()
    {
        // appelé quand on change quelque chose dans l'inspector
        dirty = true;
    }

    void DetermineActiveSeed()
    {
        if (Application.isPlaying && randomizeSeedOnPlay)
        {
            // seed pseudo-aléatoire à l'entrée en play (non deterministe entre runs)
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

        // récupère les positions pour savoir s'il y a un changement de nombre de noeuds
        var positions = GetKnotPositions();
        if (positions == null) return;

        // si on n'est pas dirty et que le nombre d'instances correspond au nombre de positions -> rien à faire
        if (!dirty && spawnedInstances.Count == positions.Count) return;

        // sinon on met à jour
        UpdateSpawned();
    }

    public void ForceRespawn()
    {
        DetermineActiveSeed(); // si tu veux randomizer le seed avant chaque respawn selon randomizeSeedOnPlay
        dirty = true;
        UpdateSpawned();
    }

    public void UpdateSpawned()
    {
        var positions = GetKnotPositions();
        if (positions == null) return;
        int n = positions.Count;
        if (n == 0) return;
        if (prefabs == null || prefabs.Count == 0) return;

        // valider poids
        if (prefabWeights == null || prefabWeights.Count != prefabs.Count)
        {
            prefabWeights = new List<float>(prefabs.Count);
            for (int i = 0; i < prefabs.Count; i++) prefabWeights.Add(1f);
        }

        // Si clearPreviousInstances, on supprime les précédentes instanciations avant de recréer.
        if (clearPreviousInstances) ClearSpawned();

        // Ajuster la taille du cache de choix pour correspondre au nombre de positions
        if (chosenIndices == null) chosenIndices = new List<int>();
        while (chosenIndices.Count < n) chosenIndices.Add(-1);
        if (chosenIndices.Count > n) chosenIndices.RemoveRange(n, chosenIndices.Count - n);

        // Générer des indices déterministes seulement si nécessaire (si -1 ou dirty)
        for (int i = 0; i < n; i++)
        {
            if (dirty || chosenIndices[i] < 0)
            {
                chosenIndices[i] = ChooseIndexByWeightDeterministic(prefabWeights, activeSeed + i);
            }
        }

        // Instancie selon chosenIndices (ne réinstancie pas si already spawned pour le même index)
        // Si clearPreviousInstances était true, spawnedInstances est vide, donc on instancie tout.
        // Si clearPreviousInstances est false et spawnedInstances existe, on ajoute jusqu'à ce que counts match.
        int alreadySpawned = spawnedInstances?.Count ?? 0;

        for (int i = 0; i < n; i++)
        {
            int chosen = Mathf.Clamp(chosenIndices[i], 0, prefabs.Count - 1);
            GameObject chosenPrefab = prefabs[chosen];
            if (chosenPrefab == null) continue;

            // si nous avons déjà une instance pour cette position (et que nous ne clear pas), skip la création
            if (!clearPreviousInstances && i < alreadySpawned)
            {
                // repositionne simplement (utile si les positions ont changé)
                var existing = spawnedInstances[i];
                if (existing != null) existing.transform.position = positions[i];
                continue;
            }

            GameObject inst = null;
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                try
                {
                    inst = (GameObject)PrefabUtility.InstantiatePrefab(chosenPrefab, instancesParent != null ? instancesParent.gameObject.scene : gameObject.scene);
                }
                catch
                {
                    inst = GameObject.Instantiate(chosenPrefab, instancesParent);
                }
            }
            else
            {
                inst = GameObject.Instantiate(chosenPrefab, instancesParent);
            }
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
        }

        dirty = false;
    }

    public List<Vector3> GetKnotPositions()
    {
        var positions = new List<Vector3>();

        if (useSplineContainer && splineContainer != null)
        {
            try
            {
                if (splineContainer.Splines != null && splineContainer.Splines.Count > 0)
                {
                    var spline = splineContainer.Splines[0];
                    int count = spline.Count;

                    for (int i = 0; i < count; i++)
                    {
                        var knot = spline[i];
                        // selon la version du package, la propriété peut s'appeler Position / LocalPosition / Value.Position
                        // ici on essaye Position (si erreur, le try/catch retournera fallback)
                        positions.Add(splineContainer.transform.TransformPoint(knot.Position));
                    }

                    return positions;
                }
            }
            catch { /* fallback below */ }
        }

        if (manualKnotTransforms != null && manualKnotTransforms.Count > 0)
        {
            foreach (var t in manualKnotTransforms)
                if (t != null) positions.Add(t.position);
            return positions;
        }

        foreach (Transform child in transform)
            positions.Add(child.position);

        return positions;
    }

    // Choix deterministe à partir d'un seed (System.Random)
    int ChooseIndexByWeightDeterministic(List<float> weights, int deterministSeed)
    {
        double total = 0.0;
        foreach (var w in weights) total += Math.Max(0.0, w);
        if (total <= 0.0) return 0;

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
