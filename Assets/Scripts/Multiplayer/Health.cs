using UnityEngine;
using Photon.Pun;
using TMPro;
using ExitGames.Client.Photon;
using UnityEngine;

public class Health : MonoBehaviourPunCallbacks
{
    [Header("Vida")]
    public int health = 100;
    public bool isLocalPlayer;

    public RectTransform healthBar;
    private float originalHealthBarsize;

    [Header("UI")]
    public TextMeshProUGUI healthText;

    private void Start()
    {
        originalHealthBarsize = healthBar.sizeDelta.x;
    }

    private void Update()
    {
        //healthBar.sizeDelta = new Vector2(originalHealthBarsize * health / 100f, healthBar.sizeDelta.y);
    }

    /*void Start()
    {
        if (healthText != null)
            healthText.text = health.ToString();
    }*/

    [PunRPC]
    public void TakeDamage(int _damage, int attackerViewID = -1)
    {
        health -= _damage;

        healthBar.sizeDelta = new Vector2(originalHealthBarsize * health / 100f, healthBar.sizeDelta.y);

        if (healthText != null)
            healthText.text = health.ToString();

        Debug.Log($"{gameObject.name} recebeu {_damage} de dano. Vida restante: {health}");

        if (health <= 0)
        {
            Debug.Log($"{gameObject.name} morreu!");

            // Atualiza deaths do jogador local
            if (isLocalPlayer) // <- Pode remover a verificação do RoomManager.instance
            {
                int currentDeaths = 0;
                if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey("Deaths"))
                    currentDeaths = (int)PhotonNetwork.LocalPlayer.CustomProperties["Deaths"];
                currentDeaths++;

                ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { "Deaths", currentDeaths } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);

                // Só chama o Respawn se o RoomManager existir
                if (RoomManager.instance != null)
                    RoomManager.instance.RespawnPlayer();
            }

            // Notifica o atacante
            if (attackerViewID != -1)
            {
                PhotonView attackerView = PhotonView.Find(attackerViewID);
                if (attackerView != null)
                {
                    CombatSystem2D attackerCombat = attackerView.GetComponent<CombatSystem2D>();
                    if (attackerCombat != null)
                        attackerView.RPC(nameof(CombatSystem2D.KillConfirmed), attackerView.Owner);
                }
            }

            // Destroi o objeto morto (somente dono)
            PhotonView view = GetComponent<PhotonView>();
            if (view != null && view.IsMine)
                PhotonNetwork.Destroy(gameObject);
        }
    }
}
