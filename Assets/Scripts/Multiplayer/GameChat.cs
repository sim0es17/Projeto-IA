using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

public class GameChat : MonoBehaviour
{
    public TextMeshProUGUI chatText;
    public TMP_InputField inputField;

    private bool isInputFieldToggled;
    // Adiciona uma referência ao RoomManager para verificar o estado
    private RoomManager roomManager;

    void Start()
    {
        // Encontra a instância do RoomManager (que deve ser global/DontDestroyOnLoad)
        roomManager = RoomManager.instance;
        if (roomManager == null)
        {
            Debug.LogError("GameChat não conseguiu encontrar a instância do RoomManager!");
        }
    }

    void Update()
    {
        // !!! NOVO BLOQUEIO: Se o painel de nome estiver ativo, NÃO PROCESSA NENHUM INPUT DO CHAT !!!
        if (roomManager != null && roomManager.IsNamePanelActive)
        {
            // Se o chat estiver acidentalmente aberto, fecha-o
            if (isInputFieldToggled)
            {
                isInputFieldToggled = false;
                inputField.DeactivateInputField();
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }
            return; // Sai do Update imediatamente
        }
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        if (Input.GetKeyDown(KeyCode.T) && !isInputFieldToggled)
        {
            isInputFieldToggled = true;
            inputField.Select();
            inputField.ActivateInputField();

            Debug.Log("Toggled on");

        }

        if (Input.GetKeyDown(KeyCode.Escape) && isInputFieldToggled)
        {
            isInputFieldToggled = false;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            Debug.Log("Toggled off");
        }

        //Sending a message
        if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && isInputFieldToggled && !inputField.text.IsNullOrEmpty())
        {
            //sending a message

            string messageToSend = $"Player {PhotonNetwork.LocalPlayer.NickName}: {inputField.text}";

            GetComponent<PhotonView>().RPC("SendChatMessage", RpcTarget.AllBuffered, messageToSend);

            inputField.text = "";
            isInputFieldToggled = false;
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

            Debug.Log("Message sent");
        }
    }

    [PunRPC]
    public void SendChatMessage(string _message)
    {
        chatText.text = chatText.text + "\n" + _message;
    }
}
