using UnityEngine;

public class FaceObjectT : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        // 1. Encontra a câmara principal apenas uma vez
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("FaceObjectT: Nenhuma câmara com a tag 'MainCamera' encontrada. O script foi desativado.");
            enabled = false;
        }
    }

    void Update()
    {
        // Garante que a referência da câmara é válida
        if (mainCameraTransform != null)
        {
            // --- CÓDIGO 2D RECOMENDADO ---

            // 1. Calcular a diferença de rotação necessária.
            // O objetivo é fazer a rotação do objeto ser a mesma que a rotação da câmara,
            // garantindo que o objeto está sempre perpendicular ao ponto de vista.

            // Usamos a rotação da câmara (Camera.main.transform.rotation)
            // e aplicamos ao nosso objeto.

            // Para um efeito Billboard completo (sempre virado para o ecrã):
            transform.rotation = mainCameraTransform.rotation;

            // Se esta linha for a única responsável pelo erro:
            // transform.LookAt(Camera.main.transform); // Linha 7 original

            // A sua nova linha 7 (e a mais eficiente) deve ser:
            // transform.rotation = mainCameraTransform.rotation;
        }
    }
}
