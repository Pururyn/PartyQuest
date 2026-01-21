using UnityEngine;
using Unity.Netcode;

public class MiniGameManager : NetworkBehaviour
{
    public GameObject playerPrefab;
    public GameObject aiPrefab;
    public Transform[] spawnPoints;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnPlayersForMiniGame();
        }
    }

    void SpawnPlayersForMiniGame()
    {
        var sessionPlayers = GameSessionManager.Instance.PlayersInSession;

        for (int i = 0; i < sessionPlayers.Count; i++)
        {
            PlayerData data = sessionPlayers[i];
            GameObject prefabToSpawn = data.IsAI ? aiPrefab : playerPrefab;

            GameObject instance = Instantiate(prefabToSpawn, spawnPoints[i].position, Quaternion.identity);
            NetworkObject netObj = instance.GetComponent<NetworkObject>();

            // Si c'est un humain, on lui donne l'autorité sur son perso de mini-jeu
            if (!data.IsAI)
            {
                netObj.SpawnWithOwnership(data.ClientId);
            }
            else
            {
                netObj.Spawn(); // L'IA appartient au serveur
            }

            // Optionnel : Envoyer le CharacterId au script du perso pour changer son apparence
            // instance.GetComponent<MiniGamePlayerController>().Setup(data.CharacterId);
        }
    }
}