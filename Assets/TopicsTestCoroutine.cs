using UnityEngine;
using TMPro;
using Momento.Sdk;
using Momento.Sdk.Auth;
using Momento.Sdk.Config;
using Momento.Sdk.Exceptions;
using Momento.Sdk.Responses;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections;
using UnityEngine.EventSystems;

public class TopicsTestCoroutine : MonoBehaviour
{
    private const string AuthTokenEnvVar = "MOMENTO_AUTH_TOKEN";
    private const string TopicName = "example-topic";
    private const string cacheName = "Unity-Topics-Cache";
    private CancellationTokenSource cts = null;
    private ITopicClient topicClient = null;
    private ICacheClient client = null;
    private TopicSubscribeResponse.Subscription subscription = null;

    public GameObject messagingCanvas;
    public GameObject nameCanvas;
    public TextMeshProUGUI textArea;
    public TMP_InputField inputTextField;
    public TMP_InputField nameInputTextField;

    public EventSystem eventSystem;

    private string clientName = "Client";
    private string textAreaString = "";

    private bool loading = false;

    // Start is called before the first frame update
    void Start()
    {
        nameInputTextField.ActivateInputField();
    }

    public async void Main()
    {
        try
        {
            textAreaString = "LOADING...";
            loading = true;
            var authToken = ReadAuthToken();

            // Set up the client
            client = new CacheClient(Configurations.Laptop.V1(), authToken, TimeSpan.FromSeconds(60));
            await EnsureCacheExistsAsync(client, cacheName);
            topicClient = new TopicClient(TopicConfigurations.Laptop.latest(), authToken);

            cts = new CancellationTokenSource();

            // Subscribe and begin receiving messages
            var subscribeResponse = await topicClient.SubscribeAsync(cacheName, TopicName);
            StartCoroutine(SubscriptionCoroutine(subscribeResponse));
        }
        catch (Exception e)
        {
            Debug.LogError("Could not set up clients " + e.ToString());
        }
    }

    IEnumerator SubscriptionCoroutine(TopicSubscribeResponse subscribeResponse)
    {
        switch (subscribeResponse)
        {
            case TopicSubscribeResponse.Subscription:
                subscription = (TopicSubscribeResponse.Subscription)subscribeResponse;
                Debug.Log("Successfully subscribed to topic " + TopicName);
                textAreaString = "";
                loading = false;

                try
                {
                    var cancellableSubscription = subscription.WithCancellation(cts.Token);
                    var enumerator = cancellableSubscription.GetAsyncEnumerator();
                    while (!cts.IsCancellationRequested)
                    {
                        var message = enumerator.Current;
                        switch (message)
                        {
                            case TopicMessage.Binary:
                                Debug.Log("Received unexpected binary message from topic.");
                                break;
                            case TopicMessage.Text text:
                                Debug.Log(String.Format("Received string message from topic: {0}",
                                    text.Value));
                                textAreaString += text.Value + "\n";
                                break;
                            case TopicMessage.Error error:
                                Debug.LogError(String.Format("Received error message from topic: {0}",
                                    error.Message));
                                textAreaString += "Error receiving message, cancelling...";
                                cts.Cancel();
                                break;
                        }
                        yield return null;

                        // wait for the next message
                        var awaitable = enumerator.MoveNextAsync().GetAwaiter();
                        while (!awaitable.IsCompleted)
                        {
                            if (cts.IsCancellationRequested)
                            {
                                break;
                            }
                            yield return null;
                        }
                    }
                }
                finally
                {
                }

                break;
            case TopicSubscribeResponse.Error error:
                Debug.LogError(String.Format("Error subscribing to a topic: {0}", error.Message));
                textAreaString += "Error trying to connect to chat, cancelling...";
                cts.Cancel();
                break;
        }

        Dispose();
    }

    public void PublishString(string message)
    {
        message = clientName + ": " + message;
        PostMessage(message);
        inputTextField.text = "";
        inputTextField.ActivateInputField();
    }

    public void SendMessage()
    {
        PublishString(inputTextField.text);
    }

    private async void PostMessage(string message)
    {
        Debug.Log("About to publish message: " + message);
        if (cts != null && !cts.IsCancellationRequested)
        {
            var publishResponse =
                await topicClient.PublishAsync(cacheName, TopicName, message);
            switch (publishResponse)
            {
                case TopicPublishResponse.Success:
                    Debug.Log("Successfully published message " + message);
                    break;
                case TopicPublishResponse.Error error:
                    Debug.LogError(String.Format("Error publishing a message to the topic: {0}", error.Message));
                    cts.Cancel();
                    break;
            }
        }
        else
        {
            Debug.LogError("Could not publish message since cancellation already occurred");
        }
    }

    private ICredentialProvider ReadAuthToken()
    {
        try
        {
            return new EnvMomentoTokenProvider(AuthTokenEnvVar);
        }
        catch (InvalidArgumentException)
        {
            Debug.Log("Could not get auth token from environment variable");
        }

        string authToken = "ADD_YOUR_TOKEN_HERE";
        StringMomentoTokenProvider? authProvider = null;
        try
        {
            // TODO: this call seems to never return with the default "ADD_YOUR_TOKEN_HERE" value of authToken
            // it should throw an Exception but doesn't appear to right now...
            authProvider = new StringMomentoTokenProvider(authToken);
        }
        catch (InvalidArgumentException e)
        {
            Debug.LogError("Invalid auth token provided! " + e);
        }

        return authProvider;
    }

    private async Task EnsureCacheExistsAsync(ICacheClient client, string cacheName)
    {
        Debug.Log(String.Format("Creating cache {0} if it doesn't already exist.", cacheName));
        var createCacheResponse = await client.CreateCacheAsync(cacheName);
        switch (createCacheResponse)
        {
            case CreateCacheResponse.Success:
                Debug.Log(String.Format("Created cache {0}.", cacheName));
                break;
            case CreateCacheResponse.CacheAlreadyExists:
                Debug.Log(String.Format("Cache {0} already exists.", cacheName));
                break;
            case CreateCacheResponse.Error:
                Debug.LogError(String.Format("Error creating cache: {0}", cacheName));
                break;
        }
    }

    public void OnNameEntered(string name)
    {
        Debug.Log("User entered name " + name);
        clientName = name;

        nameCanvas.SetActive(false);
        messagingCanvas.SetActive(true);
        inputTextField.ActivateInputField();

        Main();
    }

    public void OnStartPressed()
    {
        OnNameEntered(nameInputTextField.text);
    }

    // Update is called once per frame
    void Update()
    {
        textArea.text = textAreaString;

        inputTextField.readOnly = loading;

        if (!loading && Input.GetKeyDown(KeyCode.Return))
        {
            if (eventSystem.currentSelectedGameObject == inputTextField.gameObject || inputTextField.isFocused)
                SendMessage();
            else if (eventSystem.currentSelectedGameObject == nameInputTextField.gameObject || nameInputTextField.isFocused)
                OnStartPressed();
        }
    }

    private void Dispose()
    {
        Debug.Log("Disposing subscription to topic " + TopicName);
        subscription?.Dispose();

        Debug.Log("Disposing cache and topic clients...");
        client?.Dispose();
        topicClient?.Dispose();
    }

    private void OnDestroy()
    {
        Debug.Log("Cancelling tasks...");
        cts?.Cancel();
        Dispose();
    }
}
