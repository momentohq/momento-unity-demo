using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

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

    private Action<string> messageCallback;

    private Action<string> errorCallback;

    public HttpTopicClient(string endpoint, string authToken){
        this.endpoint = endpoint;
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
        messageCallback = onMessage;
        errorCallback = onError;
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
}
