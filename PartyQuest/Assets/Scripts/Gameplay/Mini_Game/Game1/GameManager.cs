using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject Fish; 
    public GameObject[] spawnPoints;
    [SerializeField] private float timer;
    [SerializeField] private float timeBetweenSpawns;
    void Start()
    {
        
    }

    
    void Update()
    {
        timer += Time.deltaTime;
        if(timer > timeBetweenSpawns)
        {
            timer = 0;
            int randNum = Random.Range(0, spawnPoints.Length);
            Instantiate(Fish, spawnPoints[randNum].transform.position, Quaternion.identity);
        }
    }
}
