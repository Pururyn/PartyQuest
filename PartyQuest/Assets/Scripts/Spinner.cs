using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem; // Si tu utilises le nouveau système d'input

public class Spinner : NetworkBehaviour
{
    [Header("Références")]
    public Image displayImage;
    public Sprite[] elementSprites;

    [Header("Réglages Animation")]
    [SerializeField] private float spinSpeed = 0.05f; // Vitesse de défilement (secondes)

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
        if (IsOwner) canSpin = state;

        if (state)
        {
            StartSpinning(); // Lance l'animation visuelle quand le tour commence
        }
       
    }

    // Input : À lier à ton bouton UI "Lancer le dé"
    public void OnPressStop()
    {
        // Vérifications de sécurité
        if (!IsOwner || !canSpin) return; // Ce n'est pas mon tour ou j'ai déjà cliqué
        canSpin = false; 
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
        int resultIndex = Random.Range(0, elementSprites.Length);
        int steps = resultIndex + 1;

        ShowResultClientRpc(resultIndex);

        // 2. Envoi du résultat au TurnManager
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