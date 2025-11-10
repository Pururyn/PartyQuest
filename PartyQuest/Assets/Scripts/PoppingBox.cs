using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class PoppingBox : MonoBehaviour
{
    [SerializeField]private Vector3 startPosition;
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private int speed;
    [SerializeField] private float delayBeforeReturn = 1.5f;

    public bool IsMoving
    {
        get { return isMoving; }
    }

    public delegate void MovementStoppedHandler();
    public event MovementStoppedHandler OnMovementStopped;
    
    private bool isMoving = false;
    private bool isReturning = false;

    // Start is called before the first frame update
    void Start()
    {
        transform.position = startPosition;
        isMoving = true;
    }

    // Update is called once per frame
    void Update()
    {
        float step = speed * Time.deltaTime;

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step); 

            if (transform.position == targetPosition)
            {
                isMoving = false;
                OnMovementStopped?.Invoke(); //If isMoving is false, invoke the event
            }
        }
        else if (isReturning)
        {
            transform.position = Vector3.MoveTowards(transform.position, startPosition, step);

            if (transform.position == startPosition)
            {
                isReturning = false;
            }
        }
    }

    // Called when spinning stops
    public void OnSpinStopped()
    {
        StartCoroutine(WaitAndReturn());
    }

    private IEnumerator WaitAndReturn()
    {
        yield return new WaitForSeconds(delayBeforeReturn);
        isReturning = true;
    }
}