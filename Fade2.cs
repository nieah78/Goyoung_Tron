using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Fade2 : MonoBehaviour
{
    public float FadeRate;
   
    private float targetAlpha;

    private Image image;
    public int phaseCount;
    public int phaseIndex;
    public float stop;
    private float stopTimer;
    // Use this for initialization
    void Start()
    {
        this.image = this.GetComponent<Image>();
        if (this.image == null)
        {
            Debug.LogError("Error: No image on " + this.name);
        }
        this.targetAlpha = this.image.color.a;

        if (phaseIndex % phaseCount == 0)
        {
            FadeIn();
        }
        else
        {
            FadeOut();
        }
        phaseIndex++;
        stopTimer = stop;
    }

    // Update is called once per frame
    void Update()
    {
        if (stopTimer > 0f)
        {
            stopTimer -= Time.deltaTime;
            return;
        }
        Color curColor = this.image.color;
        float alphaDiff = Mathf.Abs(curColor.a - this.targetAlpha);
        if (alphaDiff > 0.0001f)
        {
            curColor.a = Mathf.Lerp(curColor.a, targetAlpha, this.FadeRate * Time.deltaTime);
            this.image.color = curColor;
        } 
        else
        {
            if (phaseIndex % phaseCount == 0)
            {
                FadeIn();
            }
            else
            {
                FadeOut();
            }
            phaseIndex++ ;
            stopTimer = stop;
        }
    }

    public void FadeOut()
    {
        this.targetAlpha = 0.0f;
    }

    public void FadeIn()
    {
        this.targetAlpha = 1.0f;
    }
}
