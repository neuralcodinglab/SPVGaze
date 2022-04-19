using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class RenderTest : MonoBehaviour
{
    [SerializeField] [Range(0.5f, 10f)]protected float sigma;
    protected Material material;
    [SerializeField] protected Shader shader;
    private static readonly int _Sigma = Shader.PropertyToID("_Sigma");

    public enum Quality
    {
        LITTLE_KERNEL,
        MEDIUM_KERNEL,
        BIG_KERNEL
    };
    public Quality quality;
    
    
    private void Awake()
    {
        material = new Material(shader);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        material.SetFloat (_Sigma, sigma);
        material.EnableKeyword (quality.ToString ());
        Graphics.Blit(src, null as RenderTexture, material);
    }
}
