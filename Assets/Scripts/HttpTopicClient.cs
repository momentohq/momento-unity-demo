using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;


public class HttpTopicMessage {}

public class BinaryMessage : HttpTopicMessage
{
    private byte[] bytes;

    public BinaryMessage(byte[] bytes)
    {
        this.bytes = bytes;
    }

    public byte[] Value
    {
        get { return bytes; }
    }
}

public class TextMessage : HttpTopicMessage
{
    private string text;

    public TextMessage(string text)
    {
        this.text = text;
    }

    public string Value
    {
        get { return text; }
    }
}

internal class Base64DecodedV1Token
{
    public string? api_key = null;
    public string? endpoint = null;
}

struct MessageContentType
{
    internal const string Text = "text/plain";
    internal const string Binary = "application/octet-stream";
}

public class HttpTopicSubscription
{
    private IEnumerator _poller;
    private bool cancelled = false;

    public bool Cancelled {
        get { return cancelled; }
    }

    public void Cancel() {
        Debug.Log("CANCELLING SUBSCRIPTION");
        cancelled = true;
    }

    public HttpTopicSubscription() {}

    public IEnumerator Poller
    {
        get { return _poller; }
        set { _poller = value; }
    }
}

public class HttpTopicClient
{
    private string endpoint;
    private string authToken;

    public HttpTopicClient(string authToken){
        this.endpoint = GetEndpointFromKey(authToken);
        Debug.Log("Using endpoint: " + this.endpoint);
        this.authToken = authToken;
    }

    public IEnumerator Publish(string cacheName, string topicName, byte[] message)
    {
        Debug.Log("Publishing binary message");
        using var request = new UnityWebRequest(
            "https://" + endpoint + "/topics/" + cacheName + "/" + topicName,
            "POST"
        );
        request.SetRequestHeader("Content-Type", MessageContentType.Binary);
        request.uploadHandler = new UploadHandlerRaw(message);
        request.uploadHandler.contentType = MessageContentType.Binary;
        request.SetRequestHeader("Authorization", authToken);
        yield return request.SendWebRequest();
        
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Publish failed: " + request.error);
        }
    }

    public IEnumerator Publish(string cacheName, string topicName, string message)
    {
        Debug.Log("Publishing string message");
        var contentType = MessageContentType.Text;
        using var request = UnityWebRequest.Post(
            "https://" + endpoint + "/topics/" + cacheName + "/" + topicName,
            message,
            contentType
        );
        request.SetRequestHeader("Authorization", authToken);
        yield return request.SendWebRequest();
        
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Publish failed: " + request.error);
        }
    }

    public HttpTopicSubscription Subscribe(
        string cacheName, string topicName, Action<HttpTopicMessage> onMessage, Action<string> onError
    )
    {
        var sub = new HttpTopicSubscription();
        sub.Poller = Poll(cacheName, topicName, onMessage, onError, sub);
        return sub;
    }

    private IEnumerator Poll(
        string cacheName, string topicName, Action<HttpTopicMessage> messageCallback, Action<string> errorCallback, HttpTopicSubscription parent
    )
    {
        var sequenceNumber = 1;
        var baseUri = "https://" + endpoint + "/topics/" + cacheName + "/" + topicName + "?sequence_number=";
        while (true) {
            var uri = baseUri + sequenceNumber;
            Debug.Log("Polling " + uri);
            using var www = UnityWebRequest.Get(uri);
            www.SetRequestHeader("Authorization", authToken);
            yield return www.SendWebRequest();

            if (parent.Cancelled)
            {
                Debug.Log("subscription to " + cacheName + "/" + topicName + " has been cancelled.");
                yield break;
            }
            if (www.result != UnityWebRequest.Result.Success)
            {
                errorCallback(www.error);
            }
            else
            {
                var itemJson = www.downloadHandler.text;
                var stuff = JObject.Parse(itemJson);
                foreach (var item in stuff["items"])
                {
                    if (item["discontinuity"] != null)
                    {
                        sequenceNumber = (int)item["discontinuity"]["new_topic_sequence"];
                    }
                    else
                    {
                        sequenceNumber = (int)item["item"]["topic_sequence_number"] + 1;
                        var jsonDict = item["item"]["value"].ToObject<Dictionary<string, object>>();
                        if (jsonDict.ContainsKey("text"))
                        {
                            var message = new TextMessage(jsonDict["text"].ToString());
                            messageCallback(message);
                        }
                        else if (jsonDict.ContainsKey("binary"))
                        {
                            var obj = item["item"]["value"]["binary"].ToObject<byte[]>();
                            var message = new BinaryMessage(obj);
                            messageCallback(message);
                        }
                    }
                }
            }
        }
    }

    private string GetEndpointFromKey(string apiKey) {
        var base64Bytes = System.Convert.FromBase64String(apiKey);
        var theString = System.Text.Encoding.UTF8.GetString(base64Bytes);
        var decodedToken = JsonConvert.DeserializeObject<Base64DecodedV1Token>(theString);
        if (String.IsNullOrEmpty(decodedToken.api_key) || String.IsNullOrEmpty(decodedToken.endpoint))
        {
            throw new Exception("Malformed authentication token");
        }
        return "api.cache." + decodedToken.endpoint;
    }
}
