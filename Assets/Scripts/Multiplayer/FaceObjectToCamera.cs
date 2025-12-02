using UnityEngine;

public class FaceObjectT : MonoBehaviour
{
    private Transform mainCameraTransform;

    void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("FaceObjectT: Nenhuma c�mara com a tag 'MainCamera' encontrada.");
            enabled = false;
        }
    }

    void Update()
    {
        if (mainCameraTransform != null)
        {
            Quaternion cameraRotation = mainCameraTransform.rotation;

            transform.rotation = Quaternion.identity;
        }
    }
}