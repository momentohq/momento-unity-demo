using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

internal class Base64DecodedV1Token
{
    public string? api_key = null;
    public string? endpoint = null;
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

    public void Publish(string cacheName, string topicName, string message)
    {   
        var request = UnityWebRequest.Post(
            "https://" + endpoint + "/topics/" + cacheName + "/" + topicName,
            message,
            "text/plain"
        );
        request.SetRequestHeader("Authorization", authToken);
        request.SendWebRequest();
    }

    public HttpTopicSubscription Subscribe(string cacheName, string topicName, Action<string> onMessage, Action<string> onError)
    {
        var sub = new HttpTopicSubscription();
        sub.Poller = Poll(cacheName, topicName, onMessage, onError, sub);
        return sub;
    }

    private IEnumerator Poll(
        string cacheName, string topicName, Action<string> messageCallback, Action<string> errorCallback, HttpTopicSubscription parent
    )
    {
        var sequenceNumber = 1;
        var baseUri = "https://" + endpoint + "/topics/" + cacheName + "/" + topicName + "?sequence_number=";
        while (true) {
            var uri = baseUri + sequenceNumber;
            Debug.Log("Polling " + uri);
            UnityWebRequest www = UnityWebRequest.Get(uri);
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
                        messageCallback(item["item"]["value"]["text"].ToString());
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
