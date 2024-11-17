using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChangeSize : MonoBehaviour
{
    public float resizeSpeed;

    public float changeDirectionCycle;
    private float changeDirectionTimer;
    public float targetSize;
    private float[] sizes;
    private float resizeDirection;
    private int index;

    // Start is called before the first frame update
    void Start()
    {
        changeDirectionTimer = changeDirectionCycle;
        sizes = new float[] {targetSize, Mathf.Pow(targetSize, -1)};
        resizeDirection = sizes[0];
        index = 0;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 tempScale = new Vector3(1, 1, 1);
        transform.localScale 
            = Vector3.Lerp (transform.localScale, tempScale * resizeDirection, Time.deltaTime * resizeSpeed);
                
        changeDirectionTimer -= Time.deltaTime;
        if(changeDirectionTimer < 0) {
            changeDirectionTimer = changeDirectionCycle;
            resizeDirection = sizes[index++ % 2];
        }
    }
}
