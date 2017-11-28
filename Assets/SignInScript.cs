using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.InputModule;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;
using HoloToolkit.Unity;

#if !UNITY_EDITOR && UNITY_WSA
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Storage;
#endif

public class SignInScript : MonoBehaviour, ISpeechHandler
{
    class AuthResult
    {
        public AuthenticationResult res;
        public string err;
    }

    IEnumerable<string> _scopes;
    PublicClientApplication _client;
    string _userId;

    TextMesh _welcomeText;
    TextMesh _statusText;

    async void Start()
    {
#if !UNITY_EDITOR && UNITY_WSA
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        Debug.Break();
        _userId = localSettings.Values["UserId"] as string;
#endif
        _scopes = new List<string>() { "User.Read", "Mail.Read" };
        _client = new PublicClientApplication("e90a5e05-a177-468a-9f6e-eee32b946f86");
        _welcomeText = transform.Find("WelcomeText").GetComponent<TextMesh>();
        _statusText = transform.Find("StatusText").GetComponent<TextMesh>();
        _statusText.text = "--- Not Signed In ---";

        Debug.Log($"User ID: {_userId}");
        if (string.IsNullOrEmpty(_userId))
        {
            var tts = GetComponent<TextToSpeech>();
            tts.StartSpeaking(_welcomeText.text);
        }
        else
        {
            _statusText.text = "Signing In...";
            _welcomeText.text = "";

            await SignInAsync();
        }
    }

    private async Task<AuthResult> AcquireTokenAsync(IPublicClientApplication app,
                                                     IEnumerable<string> scopes,
                                                     string usrId)
    {
        var usr = !string.IsNullOrEmpty(usrId) ? app.GetUser(usrId) : null;
        var userStr = usr != null ? usr.Name : "null";
        Debug.Log($"Found User {userStr}");
        AuthResult res = new AuthResult();
        try
        {
            Debug.Log($"Calling AcquireTokenSilentAsync");
            res.res = await app.AcquireTokenSilentAsync(scopes, usr).ConfigureAwait(false);
            Debug.Log($"app.AcquireTokenSilentAsync called {res.res}");
        }
        catch (MsalUiRequiredException)
        {
            Debug.Log($"Needs UI for Login");
            try
            {
                res.res = await app.AcquireTokenAsync(scopes).ConfigureAwait(false);
                Debug.Log($"app.AcquireTokenAsync called {res.res}");
            }
            catch (MsalException msalex)
            {
                res.err = $"Error Acquiring Token:{Environment.NewLine}{msalex}";
                Debug.Log($"{res.err}");
                return res;
            }
        }
        catch (Exception ex)
        {
            res.err = $"Error Acquiring Token Silently:{Environment.NewLine}{ex}";
            Debug.Log($"{res.err}");
            return res;
        }

#if !UNITY_EDITOR && UNITY_WSA
        Debug.Log($"Access Token - {res.res.AccessToken}");
        ApplicationData.Current.LocalSettings.Values["UserId"] = res.res.User.Identifier;
#endif
        return res;
    }

    private async Task ListEmailAsync(string accessToken, Action<Value> success, Action<string> error)
    {
#if !UNITY_EDITOR && UNITY_WSA
        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.GetAsync("https://graph.microsoft.com/v1.0/me/messages?$top=5");
        if (!response.IsSuccessStatusCode)
        {
            error(response.ReasonPhrase);
            return;
        }

        var respStr = await response.Content.ReadAsStringAsync();
        Debug.Log(respStr);

        Rootobject email = null;
        try
        {
            // Parse the Json...
            email = JsonUtility.FromJson<Rootobject>(respStr);
        }
        catch (Exception ex)
        {
            Debug.Log($"Error = {ex.Message}");
            return;
        }
        Debug.Log($"msg count = {email.value.Length}");
        foreach (var msg in email.value)
        {
            success(msg);
        }
#endif
    }

    private async Task SignInAsync()
    {
        var res = await AcquireTokenAsync(_client, _scopes, _userId);

        if (string.IsNullOrEmpty(res.err))
        {
            _statusText.text = $"Signed in as {res.res.User.Name}";

            await ListEmailAsync(res.res.AccessToken, t =>
            {
                // put messages in a text ui element...
                _statusText.text += $"\nFrom: {t.from.emailAddress.address}\nSubject:{t.subject}";
            },
            t =>
            {
                _statusText.text = $"{t}";
            });
        }
        else
        {
            _statusText.text = $"Error - {res.err}";
        }
    }

    private void SignOut()
    {
#if !UNITY_EDITOR && UNITY_WSA
        ApplicationData.Current.LocalSettings.Values["UserId"] = _userId = null;
        var usr = _client.GetUser(_userId);
        if (usr != null)
        {
            _client.Remove(usr);
        }
#endif
    }

    public async void OnSpeechKeywordRecognized(SpeechEventData eventData)
    {
        if (eventData.RecognizedText == "sign in")
        {
            _statusText.text = "Signing In...";
            _welcomeText.text = "";

            await SignInAsync();
        }

        if (eventData.RecognizedText == "sign out")
        {
            SignOut();
            _statusText.text = "--- Not Signed In ---";
        }
    }


    [Serializable]
    public class Rootobject
    {
        public string odatacontext;
        public string odatanextLink;
        public Value[] value;
    }

    [Serializable]
    public class Value
    {
        public string odataetag;
        public string id;
        public DateTime createdDateTime;
        public DateTime lastModifiedDateTime;
        public string changeKey;
        public object[] categories;
        public DateTime receivedDateTime;
        public DateTime sentDateTime;
        public bool hasAttachments;
        public string internetMessageId;
        public string subject;
        public string bodyPreview;
        public string importance;
        public string parentFolderId;
        public string conversationId;
        public object isDeliveryReceiptRequested;
        public bool isReadReceiptRequested;
        public bool isRead;
        public bool isDraft;
        public string webLink;
        public string inferenceClassification;
        public Body body;
        public Sender sender;
        public From from;
        public Torecipient[] toRecipients;
        public object[] ccRecipients;
        public object[] bccRecipients;
        public Replyto[] replyTo;
    }

    [Serializable]
    public class Body
    {
        public string contentType;
        public string content;
    }

    [Serializable]
    public class Sender
    {
        public Emailaddress emailAddress;
    }

    [Serializable]
    public class Emailaddress
    {
        public string name;
        public string address;
    }

    [Serializable]
    public class From
    {
        public Emailaddress1 emailAddress;
    }

    [Serializable]
    public class Emailaddress1
    {
        public string name;
        public string address;
    }

    [Serializable]
    public class Torecipient
    {
        public Emailaddress2 emailAddress;
    }

    [Serializable]
    public class Emailaddress2
    {
        public string name;
        public string address;
    }

    [Serializable]
    public class Replyto
    {
        public Emailaddress3 emailAddress;
    }

    [Serializable]
    public class Emailaddress3
    {
        public string name;
        public string address;
    }
}
