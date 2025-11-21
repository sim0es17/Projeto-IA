using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using System.Collections;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance;

    [Header("Configuração da Vitória")]
    public int scoreToWin = 700;
    public GameObject winPanel;

    private bool gameEnded = false;
    private bool canCheckWin = false;

    private void Awake()
    {
        if (instance == null) instance = this;

        // --- SOLUÇÃO NUCLEAR ---
        // O Awake corre ANTES de tudo. 
        // Força bruta para desligar o painel, independentemente de como ele foi salvo.
        if (winPanel != null)
        {
            winPanel.SetActive(false);
            Debug.Log("ScoreManager: WinPanel desligado no Awake à força.");
        }
    }

    private IEnumerator Start()
    {
        gameEnded = false;
        canCheckWin = false;

        // Segurança extra: espera 2 segundos antes de sequer OLHAR para o score
        Debug.Log("ScoreManager: A aguardar sincronização...");
        yield return new WaitForSeconds(2f);

        canCheckWin = true;
        Debug.Log("ScoreManager: Pronto para verificar vitórias.");
    }

    private void Update()
    {
        // Se ainda estamos no tempo de espera (Start), não fazemos nada
        if (!canCheckWin || gameEnded) return;

        // Só verificamos se estivermos mesmo ligados
        if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InRoom)
        {
            int networkScore = PhotonNetwork.LocalPlayer.GetScore();

            // Se o Photon ainda tiver o score antigo (700) por erro de sincronização,
            // o TGRoomManager deve resetá-lo em breve.
            // Mas se o score for alto E já passou o tempo de espera, então ganhámos.
            if (networkScore >= scoreToWin)
            {
                WinGame();
            }
        }
    }

    void WinGame()
    {
        gameEnded = true;
        Debug.Log("VITÓRIA ALCANÇADA! Score: " + PhotonNetwork.LocalPlayer.GetScore());

        if (winPanel != null)
            winPanel.SetActive(true);
    }
}