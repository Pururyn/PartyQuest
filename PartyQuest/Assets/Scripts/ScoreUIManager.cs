using UnityEngine;
using TMPro; // ou UnityEngine.UI
using Unity.Netcode;

public class ScoreUI : MonoBehaviour
{
    [Header("Lier les 4 textes ici")]
    public TextMeshProUGUI[] scoreTexts;

    void Start()
    {
        // On attend que la session soit prête ou on s'abonne direct
        if (GameSessionManager.Instance != null)
        {
            // S'abonner aux changements de la liste
            GameSessionManager.Instance.PlayersInSession.OnListChanged += UpdateScoreDisplay;

            // Mise à jour initiale
            RefreshAllScores();
        }
    }

    // Appelé automatiquement quand une donnée change (score, nom, etc.)
    private void UpdateScoreDisplay(NetworkListEvent<PlayerData> changeEvent)
    {
        RefreshAllScores();
    }

    private void RefreshAllScores()
    {
        if (GameSessionManager.Instance == null) return;

        var list = GameSessionManager.Instance.PlayersInSession;

        for (int i = 0; i < scoreTexts.Length; i++)
        {
            if (i < list.Count)
            {
                PlayerData p = list[i];
                // Exemple : "P1 (Bot) : 15"
                scoreTexts[i].text = $"{p.PlayerName}\n{p.Score} pts";
            }
            else
            {
                scoreTexts[i].text = "-"; // Slot vide
            }
        }
    }

    // Nettoyage propre
    private void OnDestroy()
    {
        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.PlayersInSession.OnListChanged -= UpdateScoreDisplay;
        }
    }
}