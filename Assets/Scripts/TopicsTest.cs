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
using UnityEngine.EventSystems;

public class TopicsTest : MonoBehaviour
{
    private const string AuthTokenEnvVar = "MOMENTO_AUTH_TOKEN";
    private const string TopicName = "example-topic";
    private const string cacheName = "Unity-Topics-Cache";
    private CancellationTokenSource cts = null;
    private ITopicClient topicClient = null;

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

    // helper variable to update the main text area from the background thread
    private string textAreaString = ""; 

    private bool loading = false;

    // Start is called before the first frame update
    void Start()
    {
        nameInputTextField.ActivateInputField();
    }

    public async Task Main()
    {
        textAreaString = "LOADING...";
        loading = true;
        var authToken = ReadAuthToken();

        // Set up the client
        using ICacheClient client =
            new CacheClient(Configurations.Laptop.V1(), authToken, TimeSpan.FromSeconds(60));
        await EnsureCacheExistsAsync(client, cacheName);
        topicClient = new TopicClient(TopicConfigurations.Laptop.latest(), authToken);

        try
        {
            cts = new CancellationTokenSource();

            // Subscribe and begin receiving messages
            await Task.Run(async () =>
            {
                var subscribeResponse = await topicClient.SubscribeAsync(cacheName, TopicName);
                switch (subscribeResponse)
                {
                    case TopicSubscribeResponse.Subscription subscription:
                        Debug.Log("Successfully subscribed to topic " + TopicName);
                        textAreaString = "";
                        loading = false;
                        try
                        {
                            var cancellableSubscription = subscription.WithCancellation(cts.Token);
                            await foreach (var message in cancellableSubscription)
                            {
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
                            }
                        }
                        finally
                        {
                            Debug.Log("Disposing subscription to topic " + TopicName);
                            subscription.Dispose();
                        }

                        break;
                    case TopicSubscribeResponse.Error error:
                        Debug.LogError(String.Format("Error subscribing to a topic: {0}", error.Message));
                        textAreaString += "Error trying to connect to chat, cancelling...";
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

    public void PublishMessage()
    {
        string message = "<b>" + clientName + "</b>: " + inputTextField.text;
        Task.Run(async () =>
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
        });
        inputTextField.text = "";
        inputTextField.ActivateInputField();
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

        Task.Run(async () => { await Main(); });
    }

    // Update is called once per frame
    void Update()
    {
        // Update UI text on the main thread
        textArea.text = textAreaString;

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

    private void OnDestroy()
    {
        Debug.Log("Cancelling tasks...");
        cts?.Cancel();
    }
}
