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

    private bool subscriptionPaused = false;

    private Action<string> messageCallback;

    private Action<string> errorCallback;

    private Dictionary<string, Dictionary<string, int>> sequenceNumbers = new Dictionary<string, Dictionary<string, int>>();

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

    public IEnumerator Subscribe(string cacheName, string topicName, Action<string> onMessage, Action<string> onError)
    {
        if (!sequenceNumbers.ContainsKey(cacheName))
        {
            sequenceNumbers[cacheName] = new Dictionary<string, int>();
        }

        if (!sequenceNumbers[cacheName].ContainsKey(topicName))
        {
            sequenceNumbers[cacheName][topicName] = 1;
        }

        messageCallback = onMessage;
        errorCallback = onError;

        yield return Poll(cacheName, topicName, messageCallback, errorCallback);
    }

    public void PauseSubscription()
    {
        subscriptionPaused = true;
    }

    public void ResumeSubscription()
    {
        subscriptionPaused = false;
    }

    private IEnumerator Poll(string cacheName, string topicName, Action<string> messageCallback, Action<string> errorCallback)
    {
        sequenceNumber = 1;
        while (true) {
            if (subscriptionPaused)
            {
                yield return new WaitForSeconds(1);
                continue;
            }
            var uri = "https://" + endpoint + "/topics/" + cacheName + "/" + topicName + "?sequence_number=" + sequenceNumbers[cacheName][topicName];
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
