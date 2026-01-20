using UnityEngine;

// On définit les types possibles
public enum TileType { Blue, Red, Green, Yellow, Star }

public class Tile : MonoBehaviour
{
    [Header("Configuration de la case")]
    public TileType type; // Tu choisiras le type dans l'inspecteur
}