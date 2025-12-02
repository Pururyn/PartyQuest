/*using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class Spinner : MonoBehaviour
{
    public PoppingBox poppingbox;

    public Image DisplayImage;
    public Sprite[] ElementSprite;
    public float SpinSpeed;

    public int currentIndex = 0;
    private bool isSpinning = false;
    private float timer = 0f;

    [SerializeField] private InputActionReference stopSpinAction; 

    void Start()
    {
        if (ElementSprite.Length > 0)
        {
            DisplayImage.sprite = ElementSprite[currentIndex];
        }

        if (poppingbox != null)
        {
            poppingbox.OnMovementStopped += StartSpin;
        }
    }

    void OnEnable()
    {
        if (stopSpinAction != null && stopSpinAction.action != null)
            stopSpinAction.action.performed += OnStopSpinPerformed;
        stopSpinAction?.action?.Enable();
    }

    void OnDisable()
    {
        if (stopSpinAction != null && stopSpinAction.action != null)
            stopSpinAction.action.performed -= OnStopSpinPerformed;
        stopSpinAction?.action?.Disable();
    }

    void Update()
    {
        if (isSpinning)
        {
            timer += Time.deltaTime;
            if (timer >= SpinSpeed)
            {
                timer = 0f;
                NextItem();
            }
        }
    }

    private void OnStopSpinPerformed(InputAction.CallbackContext context)
    {
        if (poppingbox != null && !poppingbox.IsMoving)
        {
            StopSpin();
        }
    }

    void StartSpin()
    {
        isSpinning = true;
    }

    public void StopSpin()
    {
        isSpinning = false;
        poppingbox.OnSpinStopped();
    }

    void NextItem()
    {
        currentIndex = (currentIndex + 1) % ElementSprite.Length;
        DisplayImage.sprite = ElementSprite[currentIndex];
    }
}*/

using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class Spinner : NetworkBehaviour
{
    [Header("Refs")]
    public PoppingBox poppingBox;
    public Image displayImage;
    public Sprite[] elementSprites;

    [Header("Spin Settings")]
    public float shuffleSpeed = 0.05f;  // vitesse du shuffle
    private bool isShuffling = false;

    [SerializeField] private InputActionReference stopSpinInput;

    private float shuffleTimer;
    private int currentIndex = 0;

    void Start()
    {
        if (elementSprites.Length > 0)
            displayImage.sprite = elementSprites[0];

        // Quand la box a fini d'arriver, on démarre le shuffle
        if (poppingBox != null)
            poppingBox.OnMovementStopped += StartShuffleLocal;
    }

    void OnEnable()
    {
        stopSpinInput.action.performed += OnStopPressed;
        stopSpinInput.action.Enable();
    }

    void OnDisable()
    {
        stopSpinInput.action.performed -= OnStopPressed;
        stopSpinInput.action.Disable();
    }

    void Update()
    {
        if (isShuffling)
        {
            shuffleTimer += Time.deltaTime;
            if (shuffleTimer >= shuffleSpeed)
            {
                shuffleTimer = 0f;
                currentIndex = Random.Range(0, elementSprites.Length);
                displayImage.sprite = elementSprites[currentIndex];
            }
        }
    }

    // Trigger local : le joueur lance le shuffle quand la box arrive
    void StartShuffleLocal()
    {
        if (!IsOwner) return;   // seul le joueur du tour peut contrôler

        StartShuffleServerRpc();
    }

    [ServerRpc]
    void StartShuffleServerRpc()
    {
        StartShuffleClientRpc();
    }

    [ClientRpc]
    void StartShuffleClientRpc()
    {
        isShuffling = true;
        shuffleTimer = 0f;
    }

    // Quand le joueur appuie pour arrêter
    private void OnStopPressed(InputAction.CallbackContext context)
    {
        if (!IsOwner) return; // seul le joueur du tour peut arrêter

        RequestStopServerRpc();
    }

    [ServerRpc]
    void RequestStopServerRpc(ServerRpcParams rpcParams = default)
    {
        // le serveur choisit le résultat final
        int finalIndex = Random.Range(0, elementSprites.Length);

        // 1. Affiche le résultat visuel sur tous les clients
        StopShuffleClientRpc(finalIndex);

        // 2. Animation retour de la boîte
        poppingBox.ReturnPopClientRpc();

        // 3. IMPORTANT : On convertit l'index en nombre de pas (Index 0 = 1 pas, Index 5 = 6 pas)
        int stepsToMove = finalIndex + 1;

        // 4. On prévient le TurnManager que le dé a parlé
        TurnManager.Instance.OnDiceResult(stepsToMove);
    }

    [ClientRpc]
    void StopShuffleClientRpc(int finalIndex)
    {
        isShuffling = false;
        displayImage.sprite = elementSprites[finalIndex];
        currentIndex = finalIndex;
    }
}
