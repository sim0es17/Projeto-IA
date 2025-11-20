using System.Collections;
using UnityEngine;

public class SceneCameraConfig : MonoBehaviour
{
    [Header("Zoom desta cena")]
    public float introStartSize = 12f;
    public float normalSize = 6f;
    public float edgeSize = 9f;

    [Header("Limites Horizontais (X)")]
    public float leftTriggerX = -15f;
    public float rightTriggerX = 73f;

    [Header("Limite Vertical (Y) - Onde a câmara para de descer")]
    public float cameraMinY = -10f; // <--- NOVO: Controla o fundo da câmara

    // Margem para o countdown
    private float buffer = 5f;

    private IEnumerator Start()
    {
        while (GameObject.FindGameObjectWithTag("Player") == null)
        {
            yield return null;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Camera cam = Camera.main;

        // 1. ZOOM
        CameraDynamicZoom dyn = cam.GetComponent<CameraDynamicZoom>();
        if (dyn != null)
        {
            dyn.ConfigureForScene(introStartSize, normalSize, edgeSize, leftTriggerX, rightTriggerX);
        }

        // 2. LIMITES FÍSICOS DA CÂMARA (X e Y)
        CameraFollowLimited follow = cam.GetComponent<CameraFollowLimited>();
        if (follow != null)
        {
            // Limites horizontais (com folga)
            follow.minX = leftTriggerX - 50f;
            follow.maxX = rightTriggerX + 50f;

            // Limite VERTICAL (Aqui está a correção!)
            follow.minY = cameraMinY;
        }

        // 3. LIMITES DE MORTE
        OutOfArenaCountdown deathScript = player.GetComponent<OutOfArenaCountdown>();
        if (deathScript != null)
        {
            deathScript.minBounds = new Vector2(leftTriggerX - buffer, deathScript.minBounds.y);
            deathScript.maxBounds = new Vector2(rightTriggerX + buffer, deathScript.maxBounds.y);
        }
    }
}