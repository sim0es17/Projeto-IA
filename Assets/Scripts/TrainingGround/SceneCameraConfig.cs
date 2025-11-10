using System.Collections;
using UnityEngine;

public class SceneCameraConfig : MonoBehaviour
{
    [Header("Zoom desta cena")]
    public float introStartSize = 12f;   // zoom inicial (mais afastado)
    public float normalSize = 6f;       // tamanho normal no centro
    public float edgeSize = 9f;         // zoom quando chega à borda

    [Header("Triggers de borda (X do player)")]
    public float leftTriggerX = -8f;
    public float rightTriggerX = 8f;

    private IEnumerator Start()
    {
        // Espera pela câmara do player
        while (Camera.main == null)
            yield return null;

        Camera cam = Camera.main;
        CameraDynamicZoom dyn = cam.GetComponent<CameraDynamicZoom>();

        if (dyn != null)
        {
            dyn.ConfigureForScene(
                introStartSize,
                normalSize,
                edgeSize,
                leftTriggerX,
                rightTriggerX
            );

            Debug.Log("[SceneCameraConfig] Configurou CameraDynamicZoom para esta cena.");
        }
        else
        {
            // fallback: se por algum motivo não tiver o script, pelo menos define o tamanho normal
            if (cam.orthographic)
                cam.orthographicSize = normalSize;
        }
    }
}
