using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity.InputModule;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;
using HoloToolkit.Unity;
using System.Threading;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using HoloToolkit.Unity.Collections;
using System.Net.Http;
using System.Net.Http.Headers;
using HoloToolkit.Unity.Buttons;

#if !UNITY_EDITOR && UNITY_WSA
using Windows.Storage;
#endif

public class SignInScript : MonoBehaviour, ISpeechHandler
{
    public class AuthResult
    {
        public AuthenticationResult res;
        public string err;
    }

    IEnumerable<string> _scopes;
    PublicClientApplication _client;
    string _userId;

    TextMesh _welcomeText;
    TextMesh _statusText;
    TextMesh _signedInStatusText;

    string tempStatusText = string.Empty;

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
        _signedInStatusText = transform.Find("SignedInStatusText").GetComponent<TextMesh>();
        _signedInStatusText.text = "--- Not Signed In ---";

        Debug.Log($"User ID: {_userId}");
        if (string.IsNullOrEmpty(_userId))
        {
            var tts = GetComponent<TextToSpeech>();
            var text = Regex.Replace(_welcomeText.text, "<.*?>", String.Empty);
            tts.StartSpeaking(text);
        }
        else
        {
            _signedInStatusText.text = "Signing In...";
            _welcomeText.text = "";

            await SignInAsync();
        }
    }

    public void SignIn()
    {
        Debug.Log("SignIn() handler called");
        SignInAsync();
    }

    public void SignOut()
    {
        Debug.Log("SignOut() handler called");
        SignOutAsync();
    }

    public void CodeFlow()
    {
        Debug.Log("CodeFlow() handler called");
        SignInWithCodeFlowAsync();
    }

    private volatile bool set = false;
    private const float ScatterConstant = 6.0f;

    private void Update()
    {
        if (set)
        {
            _statusText.text = tempStatusText;
            set = false;
        }
    }

    private async Task<AuthResult> AcquireTokenAsync(IPublicClientApplication app,
                                                     IEnumerable<string> scopes,
                                                     string usrId)
    {
        var acct = !string.IsNullOrEmpty(usrId) ? await app.GetAccountAsync(usrId) : null;
        var userStr = acct != null ? acct.Username : "null";
        Debug.Log($"Found User {userStr}");
        AuthResult res = new AuthResult();
        try
        {
            Debug.Log($"Calling AcquireTokenSilentAsync");
            res.res = await app.AcquireTokenSilentAsync(scopes, acct).ConfigureAwait(false);
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
        ApplicationData.Current.LocalSettings.Values["UserId"] = res.res.Account.HomeAccountId.Identifier;
#endif
        return res;
    }

    private async Task<AuthResult> AcquireTokenDeviceFlowAsync(PublicClientApplication app, 
                                                               IEnumerable<string> scopes, 
                                                               string userId)
    {
        AuthResult res = new AuthResult();

        try
        {
            // Should we do a silent request here first?

            res.res = await app.AcquireTokenWithDeviceCodeAsync(scopes, string.Empty, deviceCodeCallback =>
                {
                    // This will print the message on the console which tells the user where to go sign-in using 
                    // a separate browser and the code to enter once they sign in.
                    // The AcquireTokenWithDeviceCodeAsync() method will poll the server after firing this
                    // device code callback to look for the successful login of the user via that browser.
                    // This background polling (whose interval and timeout data is also provided as fields in the 
                    // deviceCodeCallback class) will occur until:
                    // * The user has successfully logged in via browser and entered the proper code
                    // * The timeout specified by the server for the lifetime of this code (typically ~15 minutes) has been reached
                    // * The developing application calls the Cancel() method on a CancellationToken sent into the method.
                    //   If this occurs, an OperationCanceledException will be thrown (see catch below for more details).

                    UnityEngine.WSA.Application.InvokeOnAppThread(() =>
                    {
#if UNITY_EDITOR
                        tempStatusText = InsertBreaks(deviceCodeCallback.Message);
                        set = true;
#else
                        _statusText.text = InsertBreaks(deviceCodeCallback.Message);
#endif

                    }, true);

                    return Task.FromResult(0);

                }, CancellationToken.None).ConfigureAwait(true);

            //Console.WriteLine(result.Account.Username);
        }
        catch (MsalServiceException ex)
        {
            // Kind of errors you could have (in ex.Message)

            // AADSTS50059: No tenant-identifying information found in either the request or implied by any provided credentials.
            // Mitigation: as explained in the message from Azure AD, the authoriy needs to be tenanted. you have probably created
            // your public client application with the following authorities:
            // https://login.microsoftonline.com/common or https://login.microsoftonline.com/organizations

            // AADSTS90133: Device Code flow is not supported under /common or /consumers endpoint.
            // Mitigation: as explained in the message from Azure AD, the authority needs to be tenanted

            // AADSTS90002: Tenant <tenantId or domain you used in the authority> not found. This may happen if there are 
            // no active subscriptions for the tenant. Check with your subscription administrator.
            // Mitigation: if you have an active subscription for the tenant this might be that you have a typo in the 
            // tenantId (GUID) or tenant domain name.
            res.err = $"Error Acquiring Token For Device Code:{Environment.NewLine}{ex}";
            Debug.Log($"{res.err}");
        }
        catch (OperationCanceledException)
        {
            // If you use a CancellationToken, and call the Cancel() method on it, then this may be triggered
            // to indicate that the operation was cancelled. 
            // See https://docs.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads 
            // for more detailed information on how C# supports cancellation in managed threads.
            res.err = $"Error Acquiring Token For Device Code:{Environment.NewLine} Operation Cancelled";
            Debug.Log($"{res.err}");
        }
        catch (MsalClientException ex)
        {
            // Verification code expired before contacting the server
            // This exception will occur if the user does not manage to sign-in before a time out (15 mins) and the
            // call to `AcquireTokenWithDeviceCodeAsync` is not cancelled in between
            res.err = $"Error Acquiring Token For Device Code - Toen Expired:{Environment.NewLine}{ex}";
            Debug.Log($"{res.err}");
        }

        return res;
    }

    // To sign in,
    // use a web browser to open the page
    // https://microsoft.com/devicelogin 
    // and enter the code
    // DXX83MFT7
    // to authenticate.
    static private string InsertBreaks(string message)
    {
        string ret = string.Empty;
        List<string> strs = message.Split(new char[] { ',' }).ToList();

        foreach (var str in strs)
        {
            var split = new List<string>();
            var startIdx = str.IndexOf("https:");
            if (startIdx > -1)
            {
                var endIdx = str.IndexOf(' ', startIdx);

                if (endIdx > -1)
                {
                    split.Add(str.Substring(0, startIdx - 1));
                    split.Add(str.Substring(startIdx, endIdx - startIdx));
                    split.Add(str.Substring(endIdx + 1));
                }
            }

            if (split.Count > 0)
                ret += string.Join("\n", split);
            else
                ret += str + "\n";
        }

        return ret;
    }

    private async Task ListEmailAsync(string accessToken, Action<Value> success, Action<string> error)
    {
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
    }

    public async Task SignInAsync()
    {
        var res = await AcquireTokenAsync(_client, _scopes, _userId);

        if (string.IsNullOrEmpty(res.err))
        {
            _statusText.text = "";
            _signedInStatusText.text = $"Signed in as {res.res.Account.Username}";

            await ListEmailAsync(res.res.AccessToken, OnEmailItem,
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

    private void OnEmailItem(Value item)
    {
        var collGameObj = gameObject.transform.Find("EmailCollection");
        var collection = collGameObj.GetComponent<ObjectCollection>();

        // Get a prefab to instantiate...
        var emailObj = (GameObject)Instantiate(Resources.Load("Envelope"));
        var emailData = emailObj.AddComponent<EmailData>();
        emailData.MessageData = item;

        var button = emailObj.GetComponentInChildren<Button>();
        button.OnButtonPressed += OnButtonPressed;

        var title = emailObj.transform.Find("EnvelopeParent/Title");
        var textMesh = title.GetComponent<TextMesh>();
        textMesh.text = item.subject;

        // Apply a random tilt to the envelope....
        var envelope = emailObj.transform.Find("EnvelopeParent/EmailPrefab");
        var vec = new Vector3(UnityEngine.Random.Range(-ScatterConstant, ScatterConstant), 
                              UnityEngine.Random.Range(-ScatterConstant, ScatterConstant), 
                              UnityEngine.Random.Range(-ScatterConstant, ScatterConstant));
        envelope.Rotate(vec);
        title.Rotate(vec);
 
        var node = new CollectionNode()
        {
            Name = item.subject,
            transform = emailObj.transform
        };
        emailObj.transform.parent = collection.transform;
        node.transform = emailObj.transform;

        collection.NodeList.Add(node);
        collection.UpdateCollection();
    }

    private void OnButtonPressed(GameObject obj)
    {
        var emailData = obj.GetComponent<EmailData>();

        // Display the email data...
        DisplayEmail(emailData);
    }

    private void DisplayEmail(EmailData emailData)
    {

    }

    public async Task<AuthResult> SignInWithCodeFlowAsync()
    {
        var res = await AcquireTokenDeviceFlowAsync(_client, _scopes, _userId);

        if (string.IsNullOrEmpty(res.err))
        {
            _statusText.text = "";
            _signedInStatusText.text = $"Signed in as {res.res.Account.Username}";

            await ListEmailAsync(res.res.AccessToken, OnEmailItem,
            t =>
            {
                _statusText.text = $"{t}";
            });
        }
        else if (res.err != null)
        {
            _statusText.text = $"Error - {res.err}";
        }
        return res;
    }

    public async Task SignOutAsync()
    {
#if !UNITY_EDITOR && UNITY_WSA
        ApplicationData.Current.LocalSettings.Values["UserId"] = _userId = null;
        var acct = await _client.GetAccountAsync(_userId);
        if (acct != null)
        {
            await _client.RemoveAsync(acct);
        }
#endif
    }

    public async void OnSpeechKeywordRecognized(SpeechEventData eventData)
    {
        if (eventData.RecognizedText == "sign in")
        {
            await HandleSignInAsync();
        }

        if (eventData.RecognizedText == "code flow")
        {
            await HandleSignInWithCodeFlowAsync();
        }

        if (eventData.RecognizedText == "sign out")
        {
            await HandleSignOutAsync();
        }
    }

    public async Task HandleSignOutAsync()
    {
        await SignOutAsync();
        _signedInStatusText.text = "--- Not Signed In ---";
    }

    public async Task HandleSignInWithCodeFlowAsync()
    {
        _signedInStatusText.text = "Signing In With Code Flow...";
        _welcomeText.text = "";

        await SignInWithCodeFlowAsync();
    }

    public async Task HandleSignInAsync()
    {
        _signedInStatusText.text = "Signing In...";
        _welcomeText.text = "";

        await SignInAsync();
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

public class EmailData : MonoBehaviour
{
    public SignInScript.Value MessageData { get; set; }
}