using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraDynamicZoom : MonoBehaviour
{
    public Camera cam;
    public CameraFollowLimited follow;

    [Header("Tamanhos da câmara")]
    public float introStartSize = 12f;
    public float normalSize = 6f;
    public float edgeSize = 9f;

    [Header("Tempos de animação")]
    public float introDuration = 2f;
    public float zoomOutDuration = 0.5f;
    public float zoomInDuration = 0.5f;

    [Header("Posições onde o zoom começa")]
    public float leftTriggerX = -8f;
    public float rightTriggerX = 8f;

    private Coroutine currentRoutine;
    private bool introDone = false;

    private void Awake()
    {
        if (cam == null)
            cam = GetComponent<Camera>();

        if (follow == null)
            follow = GetComponent<CameraFollowLimited>();

        cam.orthographicSize = introStartSize;
    }

    private void OnEnable()
    {
        StartZoom(normalSize, introDuration, () => introDone = true);
    }

    private void LateUpdate()
    {
        if (!introDone) return;
        if (follow == null || follow.target == null) return;

        float playerX = follow.target.position.x;

        bool nearLeftEdge = playerX <= leftTriggerX;
        bool nearRightEdge = playerX >= rightTriggerX;
        bool nearEdge = nearLeftEdge || nearRightEdge;

        if (nearEdge)
        {
            // assim que entra na zona de borda, bloqueia X onde a câmara está
            if (!follow.lockX)
                follow.LockCurrentX();

            // zoom OUT se ainda não estiver no tamanho de borda
            if (cam.orthographicSize < edgeSize - 0.01f)
                StartZoom(edgeSize, zoomOutDuration, null);
        }
        else
        {
            // voltou para zona segura -> desbloqueia X para seguir outra vez
            if (follow.lockX)
                follow.UnlockX();

            // zoom IN de volta ao normal
            if (cam.orthographicSize > normalSize + 0.01f)
                StartZoom(normalSize, zoomInDuration, null);
        }
    }

    private void StartZoom(float targetSize, float duration, System.Action onComplete)
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ZoomTo(targetSize, duration, onComplete));
    }

    private IEnumerator ZoomTo(float targetSize, float duration, System.Action onComplete)
    {
        float startSize = cam.orthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }

        cam.orthographicSize = targetSize;
        onComplete?.Invoke();
    }

    // ??? NOVO: permitir configurar por cena e reiniciar a intro
    public void ConfigureForScene(
        float _introStartSize,
        float _normalSize,
        float _edgeSize,
        float _leftTriggerX,
        float _rightTriggerX)
    {
        introStartSize = _introStartSize;
        normalSize = _normalSize;
        edgeSize = _edgeSize;
        leftTriggerX = _leftTriggerX;
        rightTriggerX = _rightTriggerX;

        if (cam == null)
            cam = GetComponent<Camera>();

        // volta ao tamanho inicial
        cam.orthographicSize = introStartSize;

        // reinicia o estado da intro
        introDone = false;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        // volta a começar a animação de zoom da intro
        StartZoom(normalSize, introDuration, () => introDone = true);
    }
}
