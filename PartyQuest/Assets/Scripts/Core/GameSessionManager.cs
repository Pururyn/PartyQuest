using UnityEngine;
using Unity.Netcode;

public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance;
    public NetworkList<PlayerData> PlayersInSession;

    private void Awake()
    {
        // Singleton classique : on s'assure qu'il n'y en a qu'un seul
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Initialisation de la liste réseau
        PlayersInSession = new NetworkList<PlayerData>();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetupSessionServerRpc()
    {
        PlayersInSession.Clear();

        // 1. Ajout des joueurs humains connectés
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayersInSession.Add(new PlayerData
            {
                ClientId = client.ClientId,
                IsAI = false,
                CharacterId = (int)client.ClientId,
                PlayerName = "Joueur " + (client.ClientId + 1),
                Score = 0 // Score initial
            });
        }
        
        // 2. Remplissage avec des Bots jusqu'à 4 joueurs
        int aiCount = 4 - PlayersInSession.Count;
        for (int i = 0; i < aiCount; i++)
        {
            PlayersInSession.Add(new PlayerData
            {
                ClientId = 999, // ID fictif pour les bots
                IsAI = true,
                CharacterId = 10 + i,
                PlayerName = "Bot " + (i + 1),
                Score = 0 // Score initial
            });
        }
    }

    // --- FONCTION DE MODIFICATION DE SCORE (Corrigée) ---
    public void AddScore(int playerIndex, int amount)
    {
        // Sécurité : Seul le serveur peut modifier la NetworkList
        if (!IsServer) return;

        // On vérifie que l'index demandé existe bien dans la liste
        if (playerIndex >= 0 && playerIndex < PlayersInSession.Count)
        {
            // 1. On récupère la donnée (copie car c'est une struct)
            PlayerData data = PlayersInSession[playerIndex];

            // 2. On modifie le score
            data.Score += amount;
            if (data.Score < 0) data.Score = 0; // Pas de score négatif
            // CORRECTION ICI : J'ai supprimé la ligne "if (data.Score < 0)..."
            // Le score peut maintenant être négatif (ex: -3).

            // 3. On réinjecte la donnée modifiée dans la liste pour synchroniser tout le monde
            PlayersInSession[playerIndex] = data;

            Debug.Log($"Score update: {data.PlayerName} (Index {playerIndex}) a maintenant {data.Score} points.");
        }
        else
        {
            Debug.LogWarning($"Tentative de modifier le score d'un index invalide : {playerIndex}");
        }
    }
}