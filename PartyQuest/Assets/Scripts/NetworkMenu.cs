using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkMenu : MonoBehaviour
{
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        // Utiliser NetworkSceneManager pour que tous les clients suivent !
        NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
        gameObject.SetActive(false);
    }

    public void StartClient()
    {
        // On se connecte SEULEMENT. On ne charge PAS de scène manuellement.
        NetworkManager.Singleton.StartClient();
        gameObject.SetActive(false);
    }
}