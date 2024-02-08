using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// based on https://salusgames.com/2017-01-08-circle-loading-animation-in-unity/
public class LoadingCircle : MonoBehaviour
{
    private RectTransform rectComponent;
    public float rotateSpeed = 200f;

    // Start is called before the first frame update
    void Start()
    {
        rectComponent = GetComponent<RectTransform>();
    }

    // Update is called once per frame
    void Update()
    {
        rectComponent.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
    }
}
