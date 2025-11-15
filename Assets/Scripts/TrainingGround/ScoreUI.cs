using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;   // Para GetScore()
using TMPro;

public class ScoreUI : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    private int lastScore = int.MinValue;

    void Start()
    {
        UpdateScoreText();
    }

    void Update()
    {
        // Lê o score do jogador local
        int currentScore = PhotonNetwork.LocalPlayer.GetScore();

        // Só actualiza se o valor tiver mudado
        if (currentScore != lastScore)
        {
            lastScore = currentScore;
            UpdateScoreText();
        }
    }

    void UpdateScoreText()
    {
        if (scoreText == null) return;

        int currentScore = PhotonNetwork.LocalPlayer.GetScore();
        scoreText.text = $"Score: {currentScore}";
    }

}
