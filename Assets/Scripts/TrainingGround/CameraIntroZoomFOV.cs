using System.Collections;
using UnityEngine;

public class CameraIntroZoomFOV : MonoBehaviour
{
    public Camera cam;

    [Header("Zoom (Orthographic)")]
    public float startSize = 10f;   // tamanho inicial (mais afastado)
    public float targetSize = 5f;   // tamanho normal
    public float duration = 2f;     // tempo do zoom em segundos

    private void OnEnable()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        cam.orthographicSize = startSize;
        StartCoroutine(ZoomIn());
    }

    private IEnumerator ZoomIn()
    {
        float elapsed = 0f;
        float initial = cam.orthographicSize;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cam.orthographicSize = Mathf.Lerp(initial, targetSize, t);
            yield return null;
        }

        cam.orthographicSize = targetSize;
    }
}
