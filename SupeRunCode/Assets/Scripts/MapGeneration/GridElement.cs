using UnityEngine;

public class GridElement : MonoBehaviour
{
    [SerializeField] private Renderer rend;

    private Vector3Int coordinates;
    public Vector3Int Coordinates => coordinates;

    public void Init(Vector3Int coords, Color color)
    {
        coordinates = coords;

        if (rend == null)
            rend = GetComponent<Renderer>();

        if (rend != null)
            rend.material.color = color;
    }

    // Optional: if you want to move it later
    public void SetElevation(int elevation, float verticalSpacing)
    {
        coordinates.y = elevation;
        transform.position = new Vector3(
            transform.position.x,
            elevation * verticalSpacing,
            transform.position.z
        );
    }
}



//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class GridElement : MonoBehaviour
//{
//    public Vector3Int Coordinates { get; private set; }

//    public void SetCoordinates(Vector3Int coords)
//    {
//        Coordinates = coords;
//    }

//    public void SetElevation(int elevation, float blockSpacingVertical)
//    {
//        Coordinates = new Vector3Int(Coordinates.x, elevation, Coordinates.z);

//        transform.position = new Vector3(
//            transform.position.x,
//            elevation * blockSpacingVertical,
//            transform.position.z);
//    }
//}
