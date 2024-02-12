using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class User
{
    public string username;
    public string id;
}

[Serializable]
public class TokenResponse
{
    public string token;
    public double expiresAtEpoch;
}

[Serializable]
public class ChatMessageEvent
{
    public User user;
    public string messageType;
    public string message;
    public string sourceLanguage;
    public long timestamp;
}

[Serializable]
public class LatestChats
{
    public ChatMessageEvent[] messages;
}
public enum MessageTypes
{
    text,
    image
};

[Serializable]
public class PostMessageEvent
{
    public string messageType; // should be either "text" or "image"
    public string message;
    public string sourceLanguage;
    public long timestamp;
}

[Serializable]
public class LanguageOption
{
    public string value;
    public string label;
}

[Serializable]
public class LanguageOptionResponse
{
    public LanguageOption[] supportedLanguages;
}

// compare to
// https://github.com/momentohq/moderated-chat/blob/main/frontend/src/api/translation.ts
public static class TranslationApi
{
    private const string baseUrl = "https://chat-api.developer-mst-dev.preprod.a.momentohq.com";

    public static IEnumerator CreateToken(
        User user, 
        Action<TokenResponse> onResponse,
        Action<string> onError)
    {
        const string url = baseUrl + "/v1/translate/token";

        string body = JsonUtility.ToJson(user);

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

    public static IEnumerator GetLatestChats(
        string lang,
        Action<LatestChats> onResponse,
        Action<string> onError)
    {
        string url = baseUrl + "/v1/translate/latestMessages/" + lang;

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.SetRequestHeader("Cache-Control", "no-cache");
            Debug.Log("Sending request to translation api for latest chats...");
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Got result:" + webRequest.downloadHandler.text);

                onResponse.Invoke(JsonUtility.FromJson<LatestChats>(webRequest.downloadHandler.text));
            }
            else
            {
                string error = "Error trying to get latest chats: " + webRequest.error;
                Debug.LogError(error);
                onError.Invoke(error);
            }
        }
    }

    public static IEnumerator GetSupportedLanguages(
        Action<LanguageOptionResponse> onResponse,
        Action<string> onError)
    {
        string url = baseUrl + "/v1/translate/languages";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            webRequest.SetRequestHeader("Cache-Control", "no-cache");
            Debug.Log("Sending request to translation api for supported languages...");
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Got result:" + webRequest.downloadHandler.text);

                onResponse.Invoke(JsonUtility.FromJson<LanguageOptionResponse>(webRequest.downloadHandler.text));
            }
            else
            {
                string error = "Error trying to get supported langauges: " + webRequest.error;
                Debug.LogError(error);
                onError.Invoke(error);
            }
        }
    }
}
