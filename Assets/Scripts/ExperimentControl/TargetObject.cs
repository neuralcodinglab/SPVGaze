using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class TargetObject : MonoBehaviour
{
    // public static TargetObject Instance { get; private set; }
    //
    // public int Idx {get; set;}
    // public GameObject Object {get; set;}
    [SerializeField] public bool defaultActive;
    [SerializeField] private bool blinkWhenActive;

    [SerializeField] private AudioSource audioSource;
    
    private bool _blinking;
        
    public void Activate()
    {
        gameObject.SetActive(true);
        if (blinkWhenActive)
        {
            _blinking = true;
            StartCoroutine(Blink());
        }
    }

    public void PlayClip()
    {
        if (audioSource != null) 
            audioSource.Play();
        else
            Debug.Log("NO AUDIOCLIP ASSIGNED TO CURRENT TARGET OBJECT");
    }
        
    public void Deactivate()
    {
        gameObject.SetActive(false);
        _blinking = false;
        StopCoroutine(Blink());
    }
        
    private IEnumerator Blink()
    {
        var renderers = gameObject.GetComponentsInChildren<Renderer>();
        // Blink this target on and off
        while (_blinking)
        {
            foreach (var r in renderers) r.enabled = false;
            yield return new WaitForSeconds(0.2f);
            foreach (var r in renderers) r.enabled = true;
            yield return new WaitForSeconds(0.2f);
        }
    }
}
    
    