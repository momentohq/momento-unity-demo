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
using System.Threading;
using UnityEngine.EventSystems;
using UnityEngine.Networking;

public class TopicsTestHttp : MonoBehaviour
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

    private bool loading = true;

    private string authToken = Secrets.MomentoApiKey;

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(GetNextTopicMessage());
        nameInputTextField.ActivateInputField();
    }

    IEnumerator GetNextTopicMessage(int sequence_number=1) {
        while (true)
        {
            var uri = "https://api.cache.cell-alpha-dev.preprod.a.momentohq.com/topics/" + cacheName + "/" + TopicName +
                "?sequence_number=" + sequence_number;
            Debug.Log("getting " + uri);
            UnityWebRequest www = UnityWebRequest.Get(uri);
            www.SetRequestHeader("Authorization", authToken);
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                // TODO: handle error
                Debug.Log(www.error);
            }
            else
            {
                var itemJson = www.downloadHandler.text;
                var stuff = JObject.Parse(itemJson);
                foreach (var item in stuff["items"]) {
                    Debug.Log("Got item: " + item);
                    if (item["discontinuity"] != null) {
                        Debug.Log("Got discontinuity: " + item["discontinuity"]);
                        sequence_number = (int)item["discontinuity"]["new_topic_sequence"];
                    } else {
                        Debug.Log("Got item #: " + item["item"]["topic_sequence_number"]);
                        sequence_number = (int)item["item"]["topic_sequence_number"] + 1;
                        textAreaString += item["item"]["value"]["text"] + "\n";
                    }
                }
            }
        }
    }

    public async Task Main()
    {
        textAreaString = "LOADING...";
        loading = true;
        var authProvider = ReadAuthToken();

        // Set up the client
        using ICacheClient client =
            new CacheClient(Configurations.Laptop.V1(), authProvider, TimeSpan.FromSeconds(60));
        await EnsureCacheExistsAsync(client, cacheName);

        try
        {
            await Task.Run(async () =>
            {
                Debug.Log("In Main Task");

                textAreaString = "";
                loading = false;

                while (true) {
                    Debug.Log("main loop pausing for 3 seconds");
                    await Task.Delay(3000);
                }
            });
        }
        finally
        {
            Debug.Log("Disposing cache client...");
            client.Dispose();
        }
    }

    public void PublishMessage()
    {
        Debug.Log("In PublishMessage");
        string message = "<b>" + clientName + "</b>: " + inputTextField.text;
        var request = UnityWebRequest.Post(
            "https://api.cache.cell-alpha-dev.preprod.a.momentohq.com/topics/" + cacheName + "/" + TopicName,
            message,
            "text/plain"
        );
        request.SetRequestHeader("Authorization", authToken);
        request.SendWebRequest();

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
            }
        }
    }

    private void OnDestroy()
    {
        Debug.Log("Cancelling tasks...");
        cts?.Cancel();
    }
}
