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

    void Update()
    {


        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (isInputFieldToggled)
            {
                isInputFieldToggled = false;

                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

                Debug.Log("Toggled off");
            }
            else
            {
                isInputFieldToggled = true;
                inputField.Select();
                inputField.ActivateInputField();

                Debug.Log("Toggled on");
            }
        }

        //Sending a message
        /*if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && isInputFieldToggled && !InputField.text.IsNullOrEmpty())
        {
            //sending a message

            Debug.Log("Message sent");

        }*/
    }
}
