using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class User
{
    public string username;
    public string id;
}

public class TokenResponse
{
    public string token;
    public double expiresAtEpoch;
}

// compare to
// https://github.com/momentohq/moderated-chat/blob/main/frontend/src/api/translation.ts
public static class TranslationApi
{
    private const string baseUrl = "https://57zovcekn0.execute-api.us-west-2.amazonaws.com/prod";

    public static IEnumerator CreateToken(User user, 
        Action<TokenResponse> onResponse,
        Action<string> onError)
    {
        const string url = baseUrl + "/v1/translate/token";

        string body = JsonUtility.ToJson(user);

        Debug.Log(body);

        const string contentType = "application/json";

        using (UnityWebRequest webRequest = UnityWebRequest.Post(url, body, contentType))
        {
            webRequest.SetRequestHeader("Cache-Control", "no-cache");
            Debug.Log("Sending request to translation api for token...");
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Got result:" + webRequest.downloadHandler.text);

                onResponse.Invoke(JsonUtility.FromJson<TokenResponse>(webRequest.downloadHandler.text));
            }
            else
            {
                string error = "Error trying to get token from vending machine: " + webRequest.error;
                Debug.LogError(error);
                onError.Invoke(error);
            }
        }
    }
}
