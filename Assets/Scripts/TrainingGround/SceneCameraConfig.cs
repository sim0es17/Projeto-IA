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
    public float cameraMinY = -10f;

    [Header("Limites de Morte (Y) - Altura da Arena")]
    public float bottomLimitY = -25f; // <--- NOVO: O fundo do mapa
    public float topLimitY = 30f;     // <--- NOVO: O teto do mapa

    // Margem para o countdown horizontal
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

        // 2. LIMITES FÍSICOS DA CÂMARA
        CameraFollowLimited follow = cam.GetComponent<CameraFollowLimited>();
        if (follow != null)
        {
            follow.minX = leftTriggerX - 50f;
            follow.maxX = rightTriggerX + 50f;

            // Define onde a câmara para de descer
            follow.minY = cameraMinY;
        }

        // 3. LIMITES DE MORTE (OutOfArena)
        // AQUI ESTÁ A CORREÇÃO!
        OutOfArenaCountdown deathScript = player.GetComponent<OutOfArenaCountdown>();
        if (deathScript != null)
        {
            // Agora atualizamos TAMBÉM o Y (bottomLimitY e topLimitY)
            deathScript.minBounds = new Vector2(leftTriggerX - buffer, bottomLimitY);
            deathScript.maxBounds = new Vector2(rightTriggerX + buffer, topLimitY);

            Debug.Log($"[SceneCameraConfig] Limites de Morte atualizados. X: {deathScript.minBounds.x} a {deathScript.maxBounds.x} | Y: {bottomLimitY} a {topLimitY}");
        }
    }
}