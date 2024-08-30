using UnityEngine;
using TMPro;
using Momento.Sdk;
using Momento.Sdk.Auth;
using Momento.Sdk.Config;
using Momento.Sdk.Exceptions;
using Momento.Sdk.Responses;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System;
using System.Collections;
// using System.Threading;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class TopicsTestHttp : MonoBehaviour
{
    private const string AuthTokenEnvVar = "MOMENTO_AUTH_TOKEN";
    private const string TopicName = "example-topic";
    private const string cacheName = "Unity-Topics-Cache";
    // private CancellationTokenSource cts = null;
    // private ITopicClient topicClient = null;

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

    private bool loading = true;

    private string authToken = Secrets.MomentoApiKey;

    private HttpTopicClient httpTopicClient;

    // Start is called before the first frame update
    void Start()
    {
        httpTopicClient = new HttpTopicClient("api.cache.cell-alpha-dev.preprod.a.momentohq.com", authToken);
        httpTopicClient.PauseSubscription();
        var coroutine = httpTopicClient.Subscribe(cacheName, TopicName, OnMessage, OnError);
        StartCoroutine(coroutine);
        nameInputTextField.ActivateInputField();
    }

    void OnMessage(string message)
    {
        Debug.Log("Got message: " + message);
        textAreaString += message + "\n";
    }

    void OnError(string error)
    {
        Debug.LogError("Got error: " + error);
    }

    public IEnumerator Main()
    {
        Debug.Log("In Main");
        textAreaString = "LOADING...";
        loading = true;
        var authProvider = ReadAuthToken();

        // Set up the client
        using ICacheClient client =
            new CacheClient(Configurations.Laptop.V1(), authProvider, TimeSpan.FromSeconds(60));
        Task.Run(async() => await EnsureCacheExistsAsync(client, cacheName));

        try
        {
            Debug.Log("In Main Task");

            textAreaString = "";
            loading = false;
            httpTopicClient.ResumeSubscription();

            while (true) {
                Debug.Log("main loop pausing for 3 seconds");
                yield return new WaitForSeconds(3);
            }
        }
        finally
        {
            Debug.Log("Disposing cache client...");
            client.Dispose();
        }
        yield break;
    }

    public void PublishMessage()
    {
        Debug.Log("In PublishMessage");
        string message = "<b>" + clientName + "</b>: " + inputTextField.text;
        httpTopicClient.Publish(cacheName, TopicName, message);

        inputTextField.text = "";
        inputTextField.ActivateInputField();
    }

    private ICredentialProvider ReadAuthToken()
    {
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
    }

    // Update is called once per frame
    void Update()
    {
        // Update UI text on the main thread
        textArea.text = textAreaString;

        inputTextField.readOnly = loading;

        if (Input.GetKeyDown(KeyCode.Return))
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
                StartCoroutine(Main());
            }
        }
    }

    private void OnDestroy()
    {
        Debug.Log("Cancelling tasks...");
        // cts?.Cancel();
    }
}
