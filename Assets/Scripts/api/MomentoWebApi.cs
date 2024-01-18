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
    private static ITopicClient topicClient = null;

    public static void Dispose()
    {
        if (cts != null) cts.Cancel();
    }

    public static async Task SubscribeToTopic(
        ICredentialProvider authProvider,
        string languageCode, 
        Action onSubscribed,
        Action<TopicMessage> onItem,
        Action<TopicSubscribeResponse.Error> onSubscriptionError
    ) {
        const string cacheName = "moderator";
        string topicName = "chat-" + languageCode;

        // TODO clear current client...

        // Set up the client
        using ICacheClient client =
            new CacheClient(Configurations.Laptop.V1(), authProvider, TimeSpan.FromSeconds(24 * 60 * 60));

        topicClient = new TopicClient(TopicConfigurations.Laptop.latest(), authProvider);

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
            client.Dispose();
            topicClient.Dispose();
        }
    }
}
