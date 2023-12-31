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
using System.Collections;
using UnityEngine.Networking;

public struct TokenVendingMachineResponse
{
    public string authToken;
    public double expiresAt;
}

public class TopicsTestTokenVendingMachine : MonoBehaviour
{
    private const string AuthTokenEnvVar = "MOMENTO_AUTH_TOKEN";
    private const string TopicName = "example-topic";
    private const string cacheName = "default-cache";
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
    
    // The Token Vending Machine URL should be suffixed with "?name="
    // such as like https://9jkmukxn68.execute-api.us-west-2.amazonaws.com/prod?name=
    // See the instructions at the following URL for more information:
    // https://github.com/momentohq/client-sdk-javascript/tree/main/examples/nodejs/token-vending-machine
    public string tokenVendingMachineURL = "";

    public TextMeshProUGUI titleText;

    private string clientName = "Client";

    // helper variable to update the main text area from the background thread
    private string textAreaString = "";
    private bool error = false;

    private bool loading = false;

    // Start is called before the first frame update
    void Start()
    {
        if (tokenVendingMachineURL == "")
        {
            Debug.LogError("Token Vending Machine URL is not specified!");
            titleText.text = "ERROR: Token Vending Machine URL is not specified!\n\nPlease set tokenVendingMachineURL appropriately.";
            titleText.color = Color.red;
        }
        nameInputTextField.ActivateInputField();
    }

    public async Task Main(ICredentialProvider authProvider)
    {
        // Set up the client
        using ICacheClient client =
            new CacheClient(Configurations.Laptop.V1(), authProvider, TimeSpan.FromSeconds(60));
        await EnsureCacheExistsAsync(client, cacheName);
        topicClient = new TopicClient(TopicConfigurations.Laptop.latest(), authProvider);

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
                                        Debug.Log(String.Format("Received string message from topic: {0} (with tokenId {1})",
                                            text.Value, text.TokenId));
                                        // Notice how we use the TokenId as the username in the chat
                                        textAreaString += "<b>" + text.TokenId + "</b>: " + text.Value + "\n";
                                        break;
                                    case TopicMessage.Error error:
                                        Debug.LogError(String.Format("Received error message from topic: {0}",
                                            error.Message));
                                        textAreaString += "Error receiving message, cancelling...";
                                        this.error = true;
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
                        this.error = true;
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
        // Unlike the other examples, because we're using the Token Vending Machine,
        // we now no longer need to send the user's name in the message since all
        // subscribers will receive the publishers' names via the message's TokenId.
        string message = inputTextField.text;
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

    private IEnumerator GetTokenFromVendingMachine(string name)
    {
        textAreaString = "LOADING...";
        loading = true;

        // Set your Token Vending Machine URL here. For more information, see:
        // https://github.com/momentohq/client-sdk-javascript/tree/main/examples/nodejs/token-vending-machine
        string uri = tokenVendingMachineURL + UnityWebRequest.EscapeURL(name);

        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            webRequest.SetRequestHeader("Cache-Control", "no-cache");
            Debug.Log("Sending request to token vending machine...");
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Got result:" + webRequest.downloadHandler.text);

                TokenVendingMachineResponse response = JsonUtility.FromJson<TokenVendingMachineResponse>(webRequest.downloadHandler.text);

                Debug.Log("authToken: " + response.authToken);

                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds((long)response.expiresAt);
                Debug.Log("expiresAt: " + response.expiresAt + " (" + dateTimeOffset.ToLocalTime() + ")");

                StringMomentoTokenProvider? authProvider = null;
                try
                {
                    authProvider = new StringMomentoTokenProvider(response.authToken);
                }
                catch (InvalidArgumentException e)
                {
                    Debug.LogError("Invalid auth token provided! " + e);
                }

                Task.Run(async () => { await Main(authProvider); });
            } 
            else
            {
                Debug.LogError("Error trying to get token from vending machine: " + webRequest.error);
                textAreaString = "Error connecting to token vending machine: " + webRequest.error;
                this.error = true;
            }
        }
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

        StartCoroutine(GetTokenFromVendingMachine(clientName));
    }

    // Update is called once per frame
    void Update()
    {
        // Update UI text on the main thread
        textArea.text = textAreaString;
        if (this.error)
        {
            textArea.color = Color.red;
        }

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
