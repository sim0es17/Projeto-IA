using Photon.Pun;
using TMPro;
using UnityEngine;

public class PlayerSetup : MonoBehaviour
{
    public Movement2D movement;

    public GameObject camara;

    public CombatSystem2D combat;

    public string nickname;

    public TextMeshPro nicknameText;

    public void IsLocalPlayer()
    {
        movement.enabled = true;
        camara.SetActive(true);
        // inicia o zoom quando a câmara for ativada
        var zoom = camara.GetComponent<CameraIntroZoomFOV>();
        if (zoom != null)
            zoom.StartZoom();

        // Enable combat system for the local player only
        if (combat != null)
            combat.enabled = true;
    }

    [PunRPC]
    public void SetNickname(string _nickname)
    {
        nickname = _nickname;

        nicknameText.text = nickname;
    }
}
