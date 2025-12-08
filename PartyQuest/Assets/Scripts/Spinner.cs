using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine.InputSystem; // Si tu utilises le nouveau système d'input

public class Spinner : NetworkBehaviour
{
    [Header("Références")]
    public PoppingBox poppingBox;
    public Image displayImage;
    public Sprite[] elementSprites;

    // Pour empêcher de spammer la touche
    private bool canSpin = false;

    // --- 1. FONCTIONS APPELÉES PAR LE JEU ---

    // Appelé par le TurnManager pour dire "C'est à toi de jouer"
    [ClientRpc]
    public void EnableSpinClientRpc(bool state)
    {
        // On active le contrôle seulement si on est le propriétaire de l'objet
        if (IsOwner)
        {
            canSpin = state;
            if (state && poppingBox != null) poppingBox.StartPopClientRpc(); // Sort la boîte
        }
    }

    // Input : À lier à ton bouton UI "Lancer le dé"
    public void OnPressStop()
    {
        // Vérifications de sécurité
        if (!IsOwner) return; // Ce n'est pas mon objet
        if (!canSpin) return; // Ce n'est pas mon tour ou j'ai déjà cliqué

        canSpin = false; // On désactive immédiatement pour éviter le double-clic
        SubmitResultServerRpc();
    }

    // Version automatique pour les Bots (appelée par TurnManager)
    public void BotSpin()
    {
        if (!IsServer) return;

        if (poppingBox != null) poppingBox.StartPopClientRpc();

        // Le bot attend 2 secondes avant de "cliquer"
        Invoke(nameof(SubmitResultServerRpc), 2.0f);
    }

    // --- 2. LOGIQUE RÉSEAU (C'est ici que l'erreur se produisait sûrement) ---

    [ServerRpc]
    void SubmitResultServerRpc()
    {
        // 1. Calcul du résultat (1 à 6)
        int resultIndex = Random.Range(0, elementSprites.Length);
        int steps = resultIndex + 1;

        // 2. Affichage visuel pour tout le monde
        ShowResultClientRpc(resultIndex);

        if (poppingBox != null) poppingBox.ReturnPopClientRpc(); // Rentre la boîte

        // 3. Envoi du résultat au TurnManager
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.ProcessDiceResult(steps);
        }
    }

    [ClientRpc]
    void ShowResultClientRpc(int index)
    {
        if (displayImage != null && elementSprites.Length > index)
        {
            displayImage.sprite = elementSprites[index];
        }
    }
}