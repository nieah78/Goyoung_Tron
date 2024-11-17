using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCoin : MonoBehaviour
{
    public float rotationSpeed;

    public float changeDirection;
    private float changeDirectionTimer;
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        transform.Rotate (0, rotationSpeed * Time.deltaTime, 0);
        changeDirectionTimer -= Time.deltaTime;
        if(changeDirectionTimer < 0) {
            changeDirectionTimer = changeDirection;
            rotationSpeed *= -1;
        }
    }
}
