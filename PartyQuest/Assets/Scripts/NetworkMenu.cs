using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkMenu : MonoBehaviour
{
    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        gameObject.SetActive(false); // cache le menu
        SceneManager.LoadScene("Game");
    }

    public void StartClient()
    {
        NetworkManager.Singleton.StartClient();
        gameObject.SetActive(false); // cache le menu
        SceneManager.LoadScene("Game");
    }
}