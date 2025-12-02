using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
public class NetworkedPowerup : MonoBehaviourPun
{
    public enum PowerupType { Health, Speed }

    [Header("Configuração")]
    public PowerupType type;
    public float amount = 30f;
    public float duration = 5f;

    private bool isCollected = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. Evita coleta dupla
        if (isCollected) return;

        // 2. Verifica se é um jogador
        if (collision.CompareTag("Player"))
        {
            PhotonView targetView = collision.GetComponent<PhotonView>();

            if (targetView != null)
            {
                isCollected = true; // Marca como apanhado para não disparar 2 vezes

                // --- APLICAR EFEITO ---
                if (type == PowerupType.Health)
                {
                    targetView.RPC("Heal", RpcTarget.All, (int)amount);
                }
                else if (type == PowerupType.Speed)
                {
                    targetView.RPC("BoostSpeed", RpcTarget.All, amount, duration);
                }

                

                // Se eu sou o DONO DA SALA (Master Client), tenho permissão para destruir.
                if (PhotonNetwork.IsMasterClient)
                {

                    PhotonNetwork.Destroy(gameObject);
                }
                else
                {
                    // Passo A: Escondo o objeto visualmente PARA MIM (para parecer instantâneo)
                    GetComponent<Renderer>().enabled = false;
                    GetComponent<Collider2D>().enabled = false;

                    // Passo B: Peço ao Master Client (por favor) para destruir o objeto
                    photonView.RPC("DestroyMe", RpcTarget.MasterClient);
                }
            }
        }
    }

    // Esta função só corre no computador do Master Client
    [PunRPC]
    public void DestroyMe()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}