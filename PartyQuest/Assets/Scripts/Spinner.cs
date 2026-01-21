using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Spinner : NetworkBehaviour
{
    [Header("Références")]
    public Image displayImage;
    public Sprite[] elementSprites;

    [Header("Réglages Animation")]
    [SerializeField] private float spinSpeed = 0.05f;

    [Header("Références UI")]
    [SerializeField] private GameObject spinButton;

    private bool canSpin = false;
    private bool isSpinning = false;
    private Coroutine spinCoroutine; 

    // --- 1. LOGIQUE D'ANIMATION ---

    private IEnumerator SpinAnimationRoutine()
    {
        isSpinning = true;
        int lastIndex = -1;

        while (isSpinning)
        {
            // On choisit un sprite aleatoire different du precedent pour l'effet visuel
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
        spinCoroutine = StartCoroutine(SpinAnimationRoutine());
    }

    private void StopSpinning()
    {
        isSpinning = false;
        if (spinCoroutine != null)
        {
            StopCoroutine(spinCoroutine);
            spinCoroutine = null;
        }
    }

    // --- 2. LOGIQUE RESEAU ---

    [ClientRpc]
    public void EnableSpinClientRpc(bool state)
    {
        // On affiche le bouton seulement pour le proprietaire actuel
        if (IsOwner)
        {
            canSpin = state;
            if (spinButton != null) spinButton.SetActive(state);
            if (state) StartSpinning();
        }
        else
        {
            if (spinButton != null) spinButton.SetActive(false); //cache pour les autres joueurs
        }
    }

    public void OnPressStop()
    {
        if (!canSpin) return;

        canSpin = false;
        if (spinButton != null) spinButton.SetActive(false);

        SubmitResultServerRpc();
    }

    [ServerRpc(RequireOwnership = false)] // Permet aux autres joueurs de cliquer sans lag d'ownership
    void SubmitResultServerRpc(ServerRpcParams rpcParams = default)
    {
        // Sécurité : Seul le propriétaire legitime peut valider le résultat
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        int resultIndex = Random.Range(0, elementSprites.Length);
        int steps = resultIndex + 1;

        ShowResultClientRpc(resultIndex);

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ProcessDiceResult(steps);
        }
    }

    public void BotSpin()
    {
        if (!IsServer) return;

        // On lance l'animation visuelle pour tout le monde
        StartSpinningClientRpc();

        // Le bot attend 2.5s puis s'arrête
        Invoke(nameof(BotSubmit), 2.5f);
    }

    private void BotSubmit()
    {
        SubmitResultServerRpc(); 
    }

    [ClientRpc]
    private void StartSpinningClientRpc()
    {
        StartSpinning();
    }

    [ClientRpc]
    void ShowResultClientRpc(int index)
    {
        StopSpinning();
        displayImage.sprite = elementSprites[index];
    }
}