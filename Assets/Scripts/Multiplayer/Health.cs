using UnityEngine;
using Photon.Pun;
using TMPro;

public class Health : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int health;
    public bool isLocalPlayer;

    [Header("UI")]
    public TextMeshProUGUI healthText;

    void Start()
    {
        if (healthText != null)
            healthText.text = health.ToString();
    }

    [PunRPC]
    public void TakeDamage(int _damage)
    {
        health -= _damage;

        if (healthText != null)
            healthText.text = health.ToString();

        Debug.Log($"{gameObject.name} recebeu {_damage} de dano. Vida restante: {health}");

        if (health <= 0)
        {
            if (isLocalPlayer)
                RoomManager.instance.RespawnPlayer();

            Debug.Log($"{gameObject.name} morreu!");

            PhotonView view = GetComponent<PhotonView>();
            if (view != null && view.IsMine)
                PhotonNetwork.Destroy(gameObject);
        }
    }
}
