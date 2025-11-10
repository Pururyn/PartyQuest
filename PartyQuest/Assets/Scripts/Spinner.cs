using System.Collections;
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
}