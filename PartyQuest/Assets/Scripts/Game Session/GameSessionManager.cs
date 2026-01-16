using UnityEngine;
using Unity.Netcode;

public class GameSessionManager : NetworkBehaviour
{
    public static GameSessionManager Instance;
    public NetworkList<PlayerData> PlayersInSession;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        PlayersInSession = new NetworkList<PlayerData>();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetupSessionServerRpc()
    {
        PlayersInSession.Clear();
        // Ajout des humains
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            PlayersInSession.Add(new PlayerData
            {
                ClientId = client.ClientId,
                IsAI = false,
                CharacterId = (int)client.ClientId,
                PlayerName = "Joueur " + (client.ClientId + 1)
            });
        }
        // Remplissage avec des IA jusqu'à 4
        int aiCount = 4 - PlayersInSession.Count;
        for (int i = 0; i < aiCount; i++)
        {
            PlayersInSession.Add(new PlayerData
            {
                ClientId = 999,
                IsAI = true,
                CharacterId = 10 + i,
                PlayerName = "Bot " + (i + 1)
            });
        }
    }
}