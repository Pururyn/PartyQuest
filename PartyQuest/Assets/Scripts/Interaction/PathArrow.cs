using UnityEngine;
using System;
using UnityEngine.InputSystem; // <--- INDISPENSABLE POUR LE NOUVEAU SYSTÈME

public class PathArrow : MonoBehaviour
{
    private GameNode targetNode;
    private Action<GameNode> onClickCallback;

    public void Setup(GameNode target, Action<GameNode> callback)
    {
        this.targetNode = target;
        this.onClickCallback = callback;
    }

    void Update()
    {
        // Vérifier si la souris existe (pour éviter les erreurs si pas de souris branchée)
        if (Mouse.current == null) return;

        // Détecter le clic gauche avec le Nouveau Système
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckClick();
        }
    }

    private void CheckClick()
    {
        // 1. Récupérer la position de la souris avec le Nouveau Système
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // 2. Créer le rayon
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

        // --- TENTATIVE 3D ---
        RaycastHit hit3D;
        if (Physics.Raycast(ray, out hit3D))
        {
            if (hit3D.transform == transform)
            {
                TriggerClick();
                return;
            }
        }

        // --- TENTATIVE 2D (Recommandé pour ton projet) ---
        RaycastHit2D hit2D = Physics2D.GetRayIntersection(ray);
        if (hit2D.collider != null)
        {
            if (hit2D.transform == transform)
            {
                TriggerClick();
            }
        }
    }

    private void TriggerClick()
    {
        Debug.Log("Clic validé sur : " + (targetNode != null ? targetNode.name : "Inconnu"));

        if (onClickCallback != null && targetNode != null)
        {
            onClickCallback.Invoke(targetNode);
        }
    }

    // Les fonctions OnMouseEnter/Exit dépendent de la physique "Old School" 
    // et peuvent ne pas marcher avec le New Input System sans configuration extra.
    // Je les laisse au cas où, mais ne t'inquiète pas si le survol ne grossit pas l'objet.
    void OnMouseEnter() { transform.localScale *= 1.2f; }
    void OnMouseExit() { transform.localScale /= 1.2f; }
}