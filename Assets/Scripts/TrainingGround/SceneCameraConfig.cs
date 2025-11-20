using System.Collections;
using UnityEngine;

public class SceneCameraConfig : MonoBehaviour
{
    [Header("Zoom desta cena")]
    public float introStartSize = 12f;
    public float normalSize = 6f;
    public float edgeSize = 9f;

    [Header("Triggers de borda (Limites do Mapa)")]
    public float leftTriggerX = -15f;
    public float rightTriggerX = 73f;

    // Margem extra para o countdown começar um pouco DEPOIS da câmara parar
    private float buffer = 5f;

    private IEnumerator Start()
    {
        // 1. Espera até o Player nascer na cena
        while (GameObject.FindGameObjectWithTag("Player") == null)
        {
            yield return null;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        // Assume que a câmara está dentro do player ou é a MainCamera
        Camera cam = Camera.main;

        Debug.Log("[SceneCameraConfig] Player detetado! A configurar a cena...");

        // --- A. CONFIGURAR ZOOM ---
        CameraDynamicZoom dyn = cam.GetComponent<CameraDynamicZoom>();
        if (dyn != null)
        {
            dyn.ConfigureForScene(introStartSize, normalSize, edgeSize, leftTriggerX, rightTriggerX);
        }

        // --- B. CONFIGURAR LIMITES FÍSICOS DA CÂMARA ---
        // Alargamos os limites físicos para a câmara não travar antes do trigger de zoom
        CameraFollowLimited follow = cam.GetComponent<CameraFollowLimited>();
        if (follow != null)
        {
            follow.minX = leftTriggerX - 50f;
            follow.maxX = rightTriggerX + 50f;
        }

        // --- C. CONFIGURAR MORTE (OutOfArenaCountdown) ---
        // Resolve o problema do limite 18.1 antigo!
        OutOfArenaCountdown deathScript = player.GetComponent<OutOfArenaCountdown>();

        if (deathScript != null)
        {
            // Atualiza os limites de morte com base no tamanho do mapa + margem (buffer)
            deathScript.minBounds = new Vector2(leftTriggerX - buffer, deathScript.minBounds.y);
            deathScript.maxBounds = new Vector2(rightTriggerX + buffer, deathScript.maxBounds.y);

            Debug.Log($"[SceneCameraConfig] Limites de Morte atualizados para: {deathScript.minBounds.x} e {deathScript.maxBounds.x}");
        }
    }
}