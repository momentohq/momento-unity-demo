using Momento.Sdk;
using Momento.Sdk.Auth;
using Momento.Sdk.Config;
using Momento.Sdk.Responses;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class MomentoWebApi
{
    private static CancellationTokenSource cts = null;
    private static ICacheClient _cacheClient = null;
    private static ITopicClient _topicClient = null;
    private const string cacheName = "moderator";
    private const string publishTopicName = "chat-publish";

    private static Action _onSubscribed = null;
    private static Action<TopicMessage> _onItem = null;
    private static Action<TopicSubscribeResponse.Error> _onSubscriptionError = null;

    public static ICredentialProvider authProvider = null;

    public static void Dispose()
    {
        cts?.Cancel();
        _cacheClient?.Dispose();
        _topicClient?.Dispose();

        _cacheClient = null;
        _topicClient = null;
    }

    private static ICacheClient GetCacheClient()
    {
        if (_cacheClient == null)
        {
            _cacheClient = new CacheClient(
                Configurations.Laptop.V1(),
                authProvider,
                TimeSpan.FromSeconds(24 * 60 * 60)
            );
        }

        return _cacheClient;
    }

    private static ITopicClient GetTopicClient()
    {
        if (_topicClient == null)
        {
            _topicClient = new TopicClient(
                TopicConfigurations.Laptop.latest(), 
                authProvider
            );
        }

        return _topicClient;
    }

    public static async Task SubscribeToTopic(
        string languageCode, 
        Action onSubscribed,
        Action<TopicMessage> onItem,
        Action<TopicSubscribeResponse.Error> onSubscriptionError,
        bool cacheActions = true
    ) {
        if (authProvider == null)
        {
            Debug.LogError("Authentication not set up yet, cannot subscribe to topic");
            return;
        }

        string topicName = "chat-" + languageCode;

        // cache actions for reconnects
        if (cacheActions)
        {
            _onSubscribed = onSubscribed;
            _onItem = onItem;
            _onSubscriptionError = onSubscriptionError;
        }

        // clear current client...
        Dispose();

        // Set up the client
        ICacheClient client = GetCacheClient();
        ITopicClient topicClient = GetTopicClient();

        try
        {
            cts = new CancellationTokenSource();

            // Subscribe and begin receiving messages
            await Task.Run(async () =>
            {
                var subscribeResponse = await topicClient.SubscribeAsync(cacheName, topicName);
                switch (subscribeResponse)
                {
                    case TopicSubscribeResponse.Subscription subscription:
                        Debug.Log("Successfully subscribed to topic " + topicName);
                        onSubscribed.Invoke();

                        try
                        {
                            var cancellableSubscription = subscription.WithCancellation(cts.Token);
                            await foreach (var message in cancellableSubscription)
                            {
                                switch (message)
                                {
                                    case TopicMessage.Binary:
                                    case TopicMessage.Text:
                                        onItem.Invoke(message);
                                        break;
                                    case TopicMessage.Error error:
                                        Debug.LogError(String.Format("Received error message from topic: {0}",
                                            error.Message));
                                        cts.Cancel(); // TODO restart subscription?
                                        break;
                                }
                            }
                        }
                        finally
                        {
                            Debug.Log("Disposing subscription to topic " + topicName);
                            subscription.Dispose();
                        }

                        break;
                    case TopicSubscribeResponse.Error error:
                        Debug.LogError(String.Format("Error subscribing to a topic: {0}", error.Message));
                        //textAreaString += "Error trying to connect to chat, cancelling...";
                        //this.error = true;
                        onSubscriptionError.Invoke(error);
                        cts.Cancel();
                        break;
                }
            });
        }
        finally
        {
            Debug.Log("Disposing cache and topic clients...");
            client?.Dispose();
            topicClient?.Dispose();
        }
    }

    public static async Task GetImageMessage(string imageId, Action<string> onHit)
    {
        ICacheClient cache = GetCacheClient();
        CacheGetResponse response = await cache.GetAsync(cacheName, imageId);
        if (response is CacheGetResponse.Hit)
        {
            onHit.Invoke((response as CacheGetResponse.Hit).ValueString);
        } 
        else if (response is CacheGetResponse.Miss)
        {
            // TODO
        }
        else if (response is CacheGetResponse.Error)
        {
            // TODO
        }
    }

    public static async Task Publish(string targetLanguage, string message)
    {
        ITopicClient topicClient = GetTopicClient();
        var publishResponse = await topicClient.PublishAsync(cacheName, publishTopicName, message);
        switch (publishResponse)
        {
            case TopicPublishResponse.Success:
                Debug.Log("Successfully published message " + message);
                break;
            case TopicPublishResponse.Error error:
                Debug.LogError(String.Format("Error publishing a message to the topic: {0}", error.Message));
                if (error.ErrorCode == Momento.Sdk.Exceptions.MomentoErrorCode.AUTHENTICATION_ERROR)
                {
                    Debug.LogError("token has expired, going to refresh subscription and retry publish");
                    await SubscribeToTopic(targetLanguage, async () =>
                    {
                        Debug.Log("refresh of subscription worked, retrying publish now...");
                        _onSubscribed.Invoke();
                        await Publish(targetLanguage, message);
                    }, _onItem, _onSubscriptionError, false);
                }
                break;
        }
    }

    public static async Task SendTextMessage(string messageType, string message, string sourceLanguage)
    {
        PostMessageEvent pme = new PostMessageEvent
        {
            messageType = messageType,
            message = message,
            sourceLanguage = sourceLanguage,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        Debug.Log("About to publish this json: " + JsonUtility.ToJson(pme));
        await Publish(sourceLanguage, JsonUtility.ToJson(pme));
    }
}
