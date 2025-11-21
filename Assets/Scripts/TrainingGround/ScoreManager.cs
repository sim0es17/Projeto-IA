using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts; // Importante para ler o Score do Photon

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager instance;

    [Header("Configuração da Vitória")]
    public int scoreToWin = 700;
    public GameObject winPanel;

    private bool gameEnded = false;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void Start()
    {
        if (winPanel != null) winPanel.SetActive(false);
    }

    private void Update()
    {
        // Se o jogo já acabou, não fazemos mais nada
        if (gameEnded) return;

        // --- AQUI ESTÁ A CORREÇÃO ---
        // Em vez de usarmos uma variável local, lemos diretamente do Photon
        // (Assim ficamos sincronizados com o que aparece no ScoreUI)
        int networkScore = PhotonNetwork.LocalPlayer.GetScore();

        // Verifica se já chegámos à meta
        if (networkScore >= scoreToWin)
        {
            WinGame();
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