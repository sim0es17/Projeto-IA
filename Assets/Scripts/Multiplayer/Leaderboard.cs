using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using TMPro;

public class Leaderboard : MonoBehaviour
{
    public GameObject playerHolder;

    [Header("Options")]
    public float refreshRate = 1f;

    [Header("UI")]
    public GameObject[] slots;

    [Space]
    public TMPro.TextMeshProUGUI[] scoreTexts;
    public TMPro.TextMeshProUGUI[] nameTexts;
    public TMPro.TextMeshProUGUI[] kdTexts;

    private void Start()
    {
        InvokeRepeating(nameof(Refresh), 1f, refreshRate);
    }

    public void Refresh()
    {
        foreach (var slot in slots)
        {
            slot.SetActive(false);
        }

        var slotedPlayerList =
            (from player in PhotonNetwork.PlayerList orderby player.GetScore() descending select player).ToList();

        int i = 0;
        foreach (var player in slotedPlayerList)
        {
            slots[i].SetActive(true);

            if (player.NickName == "")
                player.NickName = "Nameless" + player.ActorNumber;

            nameTexts[i].text = player.NickName;
            scoreTexts[i].text = player.GetScore().ToString();

            // Pega as Kills, ou 0 se não existir
            int kills = 0;
            if (player.CustomProperties.ContainsKey("Kills"))
            {
                kills = (int)player.CustomProperties["Kills"];
            }

            // Pega as Deaths, ou 0 se não existir
            int deaths = 0;
            if (player.CustomProperties.ContainsKey("Deaths"))
            {
                deaths = (int)player.CustomProperties["Deaths"];
            }

            // Mostra os valores
            kdTexts[i].text = kills + "/" + deaths;

            i++;
        }
    }

    private void Update()
    {
        playerHolder.SetActive(Input.GetKey(KeyCode.Tab));
    }
}
