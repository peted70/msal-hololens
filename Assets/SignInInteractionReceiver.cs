using HoloToolkit.Unity.InputModule;
using HoloToolkit.Unity.Receivers;
using UnityEngine;

public class SignInInteractionReceiver : InteractionReceiver
{
    private SignInScript _signInScript;
    private float lastTimeTapped = 0f;
    private float coolDownTime = 0.5f;

    private void Start()
    {
        _signInScript = GetComponent<SignInScript>();    
    }

    protected override async void InputClicked(GameObject obj, InputClickedEventData eventData)
    {
        if (Time.time < lastTimeTapped + coolDownTime)
        {
            return;
        }

        lastTimeTapped = Time.time;

        switch (obj.name)
        {
            case "signin":
                Debug.Log("SignIn button clicked - calling SignInAsync");
                await _signInScript.SignInAsync();
                break;
            case "codeflow":
                Debug.Log("codeflow button clicked - calling SignInWithCodeFlowAsync");
                await _signInScript.SignInWithCodeFlowAsync();
                break;
            case "signout":
                Debug.Log("signout button clicked - calling SignOutAsync");
                await _signInScript.SignOutAsync();
                break;
            default:
                break;
        }
    }
}
