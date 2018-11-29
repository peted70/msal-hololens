using HoloToolkit.Unity.InputModule;
using HoloToolkit.Unity.Receivers;
using UnityEngine;

public class SignInInteractionReceiver : InteractionReceiver
{
    private SignInScript _signInScript;

    private void Start()
    {
        _signInScript = GetComponent<SignInScript>();    
    }

    protected override async void InputClicked(GameObject obj, InputClickedEventData eventData)
    {
        base.InputClicked(obj, eventData);

        switch (obj.name)
        {
            case "signin":
                await _signInScript.SignInAsync();
                break;
            case "codeflow":
                await _signInScript.SignInWithCodeFlowAsync();
                break;
            case "signout":
                await _signInScript.SignOutAsync();
                break;
            default:
                break;
        }
    }
}
