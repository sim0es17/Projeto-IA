using UnityEngine;

public class BackgroundFollowCamera : MonoBehaviour
{
    public Transform target;  // câmara a seguir

    private void LateUpdate()
    {
        // Se ainda não tivermos alvo, tentamos usar a Main Camera
        if (target == null)
        {
            if (Camera.main == null) return;
            target = Camera.main.transform;
        }

        // Segue a posição X/Y da câmara, mas mantém o Z do fundo
        transform.position = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );
    }
}
