using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FitBackgroundToCamera : MonoBehaviour
{
    private SpriteRenderer sr;
    private Camera cam;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (cam == null || sr == null) return;

        // --- 1. POSIÇÃO (Anti-Tremer) ---
        // O fundo cola-se à posição X e Y da câmara.
        // O Z fica a 10 positivo para garantir que está no fundo.
        transform.position = new Vector3(
            cam.transform.position.x,
            cam.transform.position.y,
            10f
        );

        // --- 2. ESCALA (Preencher Ecrã) ---
        float camHeight = cam.orthographicSize * 2f;
        float camWidth = camHeight * cam.aspect;

        float spriteHeight = sr.sprite.bounds.size.y;
        float spriteWidth = sr.sprite.bounds.size.x;

        // Calcula a proporção para não deformar a imagem
        float scaleY = camHeight / spriteHeight;
        float scaleX = camWidth / spriteWidth;

        // Usa o MAIOR valor para garantir que não sobram buracos
        float finalScale = Mathf.Max(scaleX, scaleY);

        transform.localScale = new Vector3(finalScale, finalScale, 1f);

        // --- 3. ORDEM (Atrás de tudo) ---
        sr.sortingOrder = -100;
    }
}