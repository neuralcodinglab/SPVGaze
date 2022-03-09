using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Cam2UITexture : MonoBehaviour
{
    public RawImage rawimage;
    
    private WebCamTexture _webTex;
    void Start () 
    {
        _webTex = new WebCamTexture(WebCamTexture.devices[0].name);
        rawimage.texture = _webTex;
        rawimage.material.mainTexture = _webTex;
        _webTex.Play();
        
        StartCoroutine(InitWebcam());
    }
    
    private IEnumerator InitWebcam()
    {
        while (_webTex.width < 100)
        {
            Debug.Log(_webTex.width);
            yield return null;
        }
    }
}
