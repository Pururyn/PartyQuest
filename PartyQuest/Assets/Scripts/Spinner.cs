using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem; // Si tu utilises le nouveau système d'input
using UnityEngine.UI;

public class Spinner : NetworkBehaviour
{
    [Header("Références")]
    public Image displayImage;
    public Sprite[] elementSprites;

    [Header("Réglages Animation")]
    [SerializeField] private float spinSpeed = 0.05f; // Vitesse de défilement (secondes)

    [Header("Références UI")]
    [SerializeField] private GameObject spinButton;

    // Pour empêcher de spammer la touche
    private bool canSpin = false;
    private bool isSpinning = false;
    private Coroutine spinCoroutine;

    // --- 1. LOGIQUE D'ANIMATION (Locale) ---

    private IEnumerator SpinAnimationRoutine()
    {
        isSpinning = true;
        int lastIndex = -1;

        while (isSpinning)
        {
            // On choisit un sprite aléatoire différent du précédent pour l'effet visuel
            int randomIndex = Random.Range(0, elementSprites.Length);
            if (randomIndex == lastIndex) randomIndex = (randomIndex + 1) % elementSprites.Length;

            displayImage.sprite = elementSprites[randomIndex];
            lastIndex = randomIndex;

            yield return new WaitForSeconds(spinSpeed);
        }
    }
    private void StartSpinning()
    {
        if (spinCoroutine != null) StopCoroutine(spinCoroutine);
        displayImage.gameObject.SetActive(true);
        spinCoroutine = StartCoroutine(SpinAnimationRoutine());
    }

    private void StopSpinning()
    {
        isSpinning = false;
        if (spinCoroutine != null) StopCoroutine(spinCoroutine);
    }

    // --- 2. FONCTIONS RÉSEAU APPELÉES PAR LE JEU ---

    // Appelé par le TurnManager pour dire "C'est à toi de jouer"
    [ClientRpc]
    public void EnableSpinClientRpc(bool state)
    {
        // On active le contrôle seulement si on est le propriétaire de l'objet
        if (IsOwner)
        {
            canSpin = state;
            if (spinButton != null) spinButton.SetActive(state);

            if (state) StartSpinning();
        }
        else
        {
            // Pour les autres, on s'assure que c'est caché
            if (spinButton != null) spinButton.SetActive(false);
        }
    }

    public void OnPressStop()
    {
        // DOUBLE SÉCURITÉ:
        // 1. Est-ce que cet objet m'appartient vraiment ? (IsOwner)
        // 2. Ai-je le droit de cliquer ? (canSpin)
        if (!IsOwner || !canSpin) return;

        // BLOQUAGE IMMÉDIAT DU SPAM
        canSpin = false;
        if (spinButton != null) spinButton.SetActive(false); // Disparition instantanée

        SubmitResultServerRpc();
    }

    // Version automatique pour les Bots (appelée par TurnManager)
    public void BotSpin()
    {
        if (!IsServer) return;

        // On affiche le dé pour tout le monde via le ClientRpc
        EnableSpinClientRpc(true);

        // Le bot attend 3 secondes avant de "cliquer"
        Invoke(nameof(SubmitResultServerRpc), 3.0f);
    }

    [ServerRpc]
    void SubmitResultServerRpc()
    {
        // Sécurité Serveur : on vérifie que le client qui envoie le RPC est bien le proprio
        if (!IsOwner) return;

        int resultIndex = Random.Range(0, elementSprites.Length);
        int steps = resultIndex + 1;

        ShowResultClientRpc(resultIndex);

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ProcessDiceResult(steps);
        }
    }

    [ClientRpc]
    void ShowResultClientRpc(int index)
    {
        StopSpinning(); // Arrête le défilement aléatoire

        if (displayImage != null && elementSprites.Length > index)
        {
            displayImage.sprite = elementSprites[index]; // Affiche le vrai résultat
            displayImage.gameObject.SetActive(true);
        }
    }
}