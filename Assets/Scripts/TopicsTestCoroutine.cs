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

    // keep a reference to the chat UI canvas so we can unhide it after the user
    // types in their name
    public GameObject messagingCanvas;

    // keep a reference to the name UI canvas so we can hide it after the user
    // types in their name
    public GameObject nameCanvas;

    // this is where we'll show the incoming subscribed Topics messages
    public TextMeshProUGUI textArea;

    // the main chat input field
    public TMP_InputField inputTextField;

    // the name input field
    public TMP_InputField nameInputTextField;

    public EventSystem eventSystem;

    private string clientName = "Client";

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
            textArea.text = "LOADING...";
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
                textArea.text = "";
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
                                textArea.text += text.Value + "\n";
                                break;
                            case TopicMessage.Error error:
                                Debug.LogError(String.Format("Received error message from topic: {0}",
                                    error.Message));
                                textArea.text += "Error receiving message, cancelling...";
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
                    Debug.Log("Subscription to the Topic has been cancelled");
                }

                break;
            case TopicSubscribeResponse.Error error:
                Debug.LogError(String.Format("Error subscribing to a topic: {0}", error.Message));
                textArea.text += "Error trying to connect to chat, cancelling...";
                cts.Cancel();
                break;
        }

        Dispose();
    }

    public void PublishMessage()
    {
        string message = "<b>" + clientName + "</b>: " + inputTextField.text;
        DoPublishMessageAsync(message);
        inputTextField.text = "";
        inputTextField.ActivateInputField();
    }

    private async void DoPublishMessageAsync(string message)
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

        string authToken = Secrets.MomentoApiKey;
        StringMomentoTokenProvider? authProvider = null;
        try
        {
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

    public void SetName()
    {
        Debug.Log("User entered name " + nameInputTextField.text);
        clientName = nameInputTextField.text;

        nameCanvas.SetActive(false);
        messagingCanvas.SetActive(true);
        inputTextField.ActivateInputField();

        Main();
    }

    // Update is called once per frame
    void Update()
    {
        inputTextField.readOnly = loading;

        if (!loading && Input.GetKeyDown(KeyCode.Return))
        {
            // make sure the input field is focused on and it's not empty...
            if ((eventSystem.currentSelectedGameObject == inputTextField.gameObject || inputTextField.isFocused)
                && inputTextField.text != "")
            {
                PublishMessage();
            }
            else if ((eventSystem.currentSelectedGameObject == nameInputTextField.gameObject || nameInputTextField.isFocused)
                     && nameInputTextField.text != "")
            {
                SetName();
            }
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
