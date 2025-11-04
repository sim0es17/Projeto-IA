using System.Collections;
using UnityEngine;

public class CameraIntroZoomFOV : MonoBehaviour
{
    public Camera cam;

    [Header("Zoom (Orthographic)")]
    public float startSize = 12f;   // tamanho inicial (mais afastado)
    public float targetSize = 6f;   // tamanho final (zoom normal)
    public float duration = 3f;     // tempo do zoom

    private bool hasZoomed = false;

    private void Awake()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        // Define o tamanho inicial
        cam.orthographicSize = startSize;
    }

    // Chama isto quando o player local for ativado
    public void StartZoom()
    {
        if (!gameObject.activeInHierarchy || hasZoomed)
            return;

        hasZoomed = true;
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
