using UnityEngine;

public class FishMovement : MonoBehaviour
{
    private Rigidbody2D rb; 
    [SerializeField] private float speed = 2.5f;
    [SerializeField] private float deadZone = 10.0f;

    // pour stock la direction calculée
    private Vector2 direction;
    
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // si pos X positive > go vers la gauche
        if (transform.position.x > 0)
        {
            direction = Vector2.left;
           

        }// et inversement vous avez capté jpense
        else
        {
            direction = Vector2.right;
            
           
        }
       
    }

   
    void Update()
    {
        rb.linearVelocity = direction * speed;
        if (Mathf.Abs(transform.position.x) > deadZone)
        {
            Destroy(gameObject); 
        }
    }
}
