using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class HttpTopicClient // : MonoBehaviour
{
    private string endpoint;
    private string authToken;

    private Action<string> messageCallback;

    private Action<string> errorCallback;

    private Dictionary<string, Dictionary<string, bool>> cancelledSubscriptions = new Dictionary<string, Dictionary<string, bool>>();

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
        while (!request.isDone)
        {
            Debug.Log("Publishing message...");
        }
        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
        }
    }

    public IEnumerator Subscribe(string cacheName, string topicName, Action<string> onMessage, Action<string> onError)
    {
        messageCallback = onMessage;
        errorCallback = onError;

        yield return Poll(cacheName, topicName, messageCallback, errorCallback);
    }

    public void cancelSubscription(string cacheName, string topicName)
    {
        if (cancelledSubscriptions.ContainsKey(cacheName))
        {
            cancelledSubscriptions[cacheName][topicName] = true;
        }
        else
        {
            cancelledSubscriptions[cacheName] = new Dictionary<string, bool>();
            cancelledSubscriptions[cacheName][topicName] = true;
        }
    }

    private IEnumerator Poll(string cacheName, string topicName, Action<string> messageCallback, Action<string> errorCallback)
    {
        var sequenceNumber = 1;
        var baseUri = "https://" + endpoint + "/topics/" + cacheName + "/" + topicName + "?sequence_number=";
        while (true) {
            if (cancelledSubscriptions.ContainsKey(cacheName) && cancelledSubscriptions[cacheName].ContainsKey(topicName))
            {
                Debug.Log("Cancelling subscription to " + cacheName + "/" + topicName);
                cancelledSubscriptions[cacheName].Remove(topicName);
                yield break;
            }
            var uri = baseUri + sequenceNumber;
            Debug.Log("Polling " + uri);
            UnityWebRequest www = UnityWebRequest.Get(uri);
            www.SetRequestHeader("Authorization", authToken);
            yield return www.SendWebRequest();

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
