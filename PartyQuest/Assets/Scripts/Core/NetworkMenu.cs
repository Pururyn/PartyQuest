using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkMenu : MonoBehaviour
{
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private GameObject clientPanel;

    public void StartHost()
    {
        NetworkManager.Singleton.StartHost();
        // Utilisation de NetworkManager pour que tous les clients suivent
        NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
        HidePanel();
    }

    public void StartClient()
    {
        // On se connecte SEULEMENT pas besoin de charger
        NetworkManager.Singleton.StartClient();
        HidePanel();
    }

    private void HidePanel()
    {
        if (hostPanel != null && clientPanel != null)
        {
            hostPanel.SetActive(false);
            clientPanel.SetActive(false);
        }
    }
}