using UnityEngine;
using TMPro;
using Momento.Sdk;
using Momento.Sdk.Auth;
using Momento.Sdk.Exceptions;
using Momento.Sdk.Responses;
using System.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class ModeratedChat : MonoBehaviour
{
    // keep a reference to the chat UI canvas so we can unhide it after the user
    // types in their name
    public GameObject messagingCanvas;

    // keep a reference to the name UI canvas so we can hide it after the user
    // types in their name
    public GameObject nameCanvas;

    // this is where we'll show the incoming subscribed Topics messages
    public GameObject ScrollingContentContainer;

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

    public GameObject ChatMessagePrefab;

    private string clientName = "Client";

    // helper variable to update the main text area from the background thread
    private string textAreaString = "";
    private bool error = false;

    private bool messageInputReadOnly = false;

    private List<ChatMessageEvent> chats = new List<ChatMessageEvent>();

    private ITopicClient topicClient = null;
    private CancellationTokenSource cts = null;

    public Texture2D tex; // debug

    // Start is called before the first frame update
    void Start()
    {
        //if (tokenVendingMachineURL == "")
        //{
        //    Debug.LogError("Token Vending Machine URL is not specified!");
        //    titleText.text = "ERROR: Token Vending Machine URL is not specified!\n\nPlease set tokenVendingMachineURL appropriately.";
        //    titleText.color = Color.red;
        //}
        nameInputTextField.ActivateInputField();
    }

    Transform CreateChatMessageContainer(ChatMessageEvent chatMessage)
    {
        Transform chatMessageContainer = GameObject.Instantiate(ChatMessagePrefab).transform;

        Transform userBubble = chatMessageContainer.GetChild(0);
        userBubble.GetComponent<TextMeshProUGUI>().text = chatMessage.user.username.Substring(0, 1);

        Transform timestamp = chatMessageContainer.GetChild(1).GetChild(0);
        DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(chatMessage.timestamp);
        timestamp.GetComponent<TextMeshProUGUI>().text = chatMessage.user.username + " - " + dateTimeOffset.ToLocalTime();

        return chatMessageContainer;
    }

    Transform CreateTextChat(ChatMessageEvent chatMessage)
    {
        Transform chatMessageContainer = CreateChatMessageContainer(chatMessage);

        Transform message = chatMessageContainer.GetChild(1).GetChild(1);
        message.GetComponent<TextMeshProUGUI>().text = chatMessage.message;

        return chatMessageContainer;
    }

    Transform CreateImageChat(ChatMessageEvent chatMessage, bool fetchFromCache = false)
    {
        Transform chatMessageContainer = CreateChatMessageContainer(chatMessage);

        UnityEngine.UI.VerticalLayoutGroup vlg = chatMessageContainer.GetChild(1).GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.childControlWidth = false;

        Transform message = chatMessageContainer.GetChild(1).GetChild(1);
        Destroy(message.gameObject);

        message = new GameObject("ImageMessage").transform;
        message.SetParent(chatMessageContainer.GetChild(1));

        UnityEngine.UI.RawImage rawImage = message.gameObject.AddComponent<UnityEngine.UI.RawImage>();

        Texture2D tex = new Texture2D(2, 2);

        if (fetchFromCache)
        {
            //string imageId = chatMessage.message;
            //MomentoWebApi.GetImageMessage(imageId)
        }
        else
        {
            byte[] imageData = Convert.FromBase64String(chatMessage.message);
            ImageConversion.LoadImage(tex, imageData);
        }

        rawImage.texture = tex;

        RectTransform rt = rawImage.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tex.width, tex.height);

        return chatMessageContainer;
    }

    void SetLatestChats(LatestChats latestChats)
    {
        textAreaString = "";
        foreach (ChatMessageEvent chatMessage in latestChats.messages)
        {
            Transform chatMessageContainer;
            if (chatMessage.messageType == "text")
            {
                chatMessageContainer = CreateTextChat(chatMessage);
            }
            else
            {
                // must be an image
                chatMessageContainer = CreateImageChat(chatMessage);
            }

            chatMessageContainer.SetParent(ScrollingContentContainer.transform, false);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(chatMessageContainer.GetComponent<RectTransform>());

            RectTransform[] childRectTransforms = chatMessageContainer.GetComponentsInChildren<RectTransform>();
            foreach (RectTransform rt in childRectTransforms)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }

        //UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(ScrollingContentContainer.transform.GetComponent<RectTransform>());
    }

    public async Task Main(ICredentialProvider authProvider)
    {
        try
        {
            await MomentoWebApi.SubscribeToTopic(authProvider, "en",
                () =>
                {
                    // successfully subscribed
                    //textAreaString = "";
                    messageInputReadOnly = false;
                },
                message =>
                {
                    switch (message)
                    {
                        case TopicMessage.Binary:
                            Debug.Log("Received unexpected binary message from topic.");
                            break;
                        case TopicMessage.Text text:
                            try
                            {
                                Debug.Log(String.Format("Received string message from topic: {0} (with tokenId {1})",
                                text.Value, text.TokenId));

                                // TODO: this is returning a null user
                                ChatMessageEvent chatMessage = JsonUtility.FromJson<ChatMessageEvent>(text.Value);

                                if (chatMessage.messageType == "image")
                                {
                                    // TODO
                                }
                                else
                                {
                                    textAreaString += "<b>" + chatMessage.user.username + "</b>: " + chatMessage.message + "\n";
                                }

                                chats.Add(chatMessage); // TODO
                            }
                            catch (Exception e)
                            {
                                Debug.LogError("Exception handling text Topic message: " + e);
                            }

                            break;
                        case TopicMessage.Error error:
                            Debug.LogError(String.Format("Received error message from topic: {0}",
                            error.Message));
                            textAreaString += "Error receiving message, cancelling...";
                            this.error = true;
                            break;
                    }
                },
                error =>
                {
                    Debug.LogError(String.Format("Error subscribing to a topic: {0}", error.Message));
                    textAreaString += "Error trying to connect to chat, cancelling...";
                    this.error = true;
                }
            );
        }
        finally
        {
            // TODO
            //Debug.Log("Disposing cache and topic clients...");
            //client.Dispose();
            //topicClient.Dispose();
        }
    }

    public void PublishMessage()
    {
        // Unlike the other examples, because we're using the Token Vending Machine,
        // we now no longer need to send the user's name in the message since all
        // subscribers will receive the publishers' names via the message's TokenId.
        string message = inputTextField.text;
        //Task.Run(async () =>
        //{
        //    Debug.Log("About to publish message: " + message);
        //    if (cts != null && !cts.IsCancellationRequested)
        //    {
        //        var publishResponse =
        //            await topicClient.PublishAsync(cacheName, TopicName, message);
        //        switch (publishResponse)
        //        {
        //            case TopicPublishResponse.Success:
        //                Debug.Log("Successfully published message " + message);
        //                break;
        //            case TopicPublishResponse.Error error:
        //                Debug.LogError(String.Format("Error publishing a message to the topic: {0}", error.Message));
        //                cts.Cancel();
        //                break;
        //        }
        //    }
        //    else
        //    {
        //        Debug.LogError("Could not publish message since cancellation already occurred");
        //    }
        //});
        inputTextField.text = "";
        inputTextField.ActivateInputField();
    }

    private IEnumerator GetTokenFromVendingMachine(string name)
    {
        textAreaString = "LOADING...";
        messageInputReadOnly = true;

        TokenResponse tokenResponse = null;
        string error = "";

        yield return TranslationApi.CreateToken(
            new User { username = name, id = Guid.NewGuid().ToString() },
            response => { tokenResponse = response; },
            _error => { error = _error; }
        );

        if (tokenResponse != null) {
            Debug.Log("authToken: " + tokenResponse.token);

            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds((long)tokenResponse.expiresAtEpoch);
            Debug.Log("expiresAt: " + tokenResponse.expiresAtEpoch + " (" + dateTimeOffset.ToLocalTime() + ")");

            StringMomentoTokenProvider? authProvider = null;
            try
            {
                authProvider = new StringMomentoTokenProvider(tokenResponse.token);
                MomentoWebApi.authProvider = authProvider;
            }
            catch (InvalidArgumentException e)
            {
                Debug.LogError("Invalid auth token provided! " + e);
            }

            yield return TranslationApi.GetLatestChats(
                "en",
                latestChats =>
                {
                    Debug.Log("Got latest chats: " + latestChats);
                    SetLatestChats(latestChats);
                },
                error =>
                {
                    Debug.LogError("Error trying to get the latest chats");
                }
            );

            Task.Run(async () => { await Main(authProvider); });
        } 
        else
        {
            textAreaString = error;
            this.error = true;
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

        // TODO add profanity filter

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
        //textArea.text = textAreaString;
        //if (this.error)
        //{
        //    textArea.color = Color.red;
        //}

        inputTextField.readOnly = messageInputReadOnly;

        if (!messageInputReadOnly && Input.GetKeyDown(KeyCode.Return))
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
        //cts?.Cancel();
        MomentoWebApi.Dispose();
    }
}
