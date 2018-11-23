# MSAL HoloLens
Use Microsoft Auth Library to login to Microsoft Graph API using AAD V2 (either personal MSA or organization login credentials) on #HoloLens see http://peted.azurewebsites.net/microsoft-graph-auth-on-hololens/ for further details.

The original version is tagged here https://github.com/peted70/msal-hololens/tree/v1.0

The current master branch has moved on to include a device code flow which AAD has subsequently added support for. See https://oauth.net/2/grant-types/device-code/. The device code flow allows login from a device that doesn't have a browser so the flow will wait whilst the user logs ion from another device such as a mobile phone. After the flow is initiated navigate a browser on the second device to https://microsoft.com/devicelogin and input the code provided by the HoloLens app UI.
