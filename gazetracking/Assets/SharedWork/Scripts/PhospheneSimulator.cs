using UnityEngine;
using UnityEngine.Serialization;

public class PhospheneSimulator : MonoBehaviour
{
    public Material focusDotMaterial;

    internal Camera targetCamera;

    private void Awake()
    {
        targetCamera ??= GetComponent<Camera>();
    }

    public void SetEyePosition(Vector2 eyeL, Vector2 eyeR, Vector2 center)
    {
        focusDotMaterial.SetVector("_LeftEyePos", eyeL);
        focusDotMaterial.SetVector("_RightEyePos", eyeR);
    }

    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        
        Graphics.Blit(src, dst, focusDotMaterial);
    }
}
