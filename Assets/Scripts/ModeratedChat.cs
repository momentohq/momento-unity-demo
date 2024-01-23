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

    private readonly object chatLock = new object();
    private List<ChatMessageEvent> chatsToConsumeOnMainThread = new List<ChatMessageEvent>();
    private List<Tuple<UnityEngine.UI.RawImage, string>> imageChatsToUpdateOnMainThread = new List<Tuple<UnityEngine.UI.RawImage, string>>();

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
        timestamp.GetComponent<TextMeshProUGUI>().text = chatMessage.user.username + " - " + dateTimeOffset.ToLocalTime().ToString("t");

        return chatMessageContainer;
    }

    Transform CreateTextChat(ChatMessageEvent chatMessage)
    {
        Transform chatMessageContainer = CreateChatMessageContainer(chatMessage);

        Transform message = chatMessageContainer.GetChild(1).GetChild(1);
        message.GetComponent<TextMeshProUGUI>().text = chatMessage.message;

        return chatMessageContainer;
    }

    void UpdateImageChat(UnityEngine.UI.RawImage rawImage, string base64image)
    {
        Texture2D tex = new Texture2D(2, 2);
        byte[] imageData = Convert.FromBase64String(base64image);
        ImageConversion.LoadImage(tex, imageData);

        rawImage.texture = tex;

        RectTransform rt = rawImage.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tex.width, tex.height);
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

        if (fetchFromCache)
        {
            string imageId = chatMessage.message;
            Task.Run(async () =>
            {
                await MomentoWebApi.GetImageMessage(imageId, base64image =>
                {
                    var tuple = new Tuple<UnityEngine.UI.RawImage, string>(rawImage, base64image);
                    lock (chatLock)
                    {
                        imageChatsToUpdateOnMainThread.Add(tuple);
                    }
                });
            });
        }
        else
        {
            UpdateImageChat(rawImage, chatMessage.message);
        }

        return chatMessageContainer;
    }

    void RebuildChatMessageLayout(Transform chatMessageContainer)
    {
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(chatMessageContainer.GetComponent<RectTransform>());

        RectTransform[] childRectTransforms = chatMessageContainer.GetComponentsInChildren<RectTransform>();
        foreach (RectTransform rt in childRectTransforms)
        {
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
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
            RebuildChatMessageLayout(chatMessageContainer);
        }
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

                                ChatMessageEvent chatMessage = JsonUtility.FromJson<ChatMessageEvent>(text.Value);

                                lock (chatLock)
                                {
                                    chatsToConsumeOnMainThread.Add(chatMessage);
                                }
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
        Task.Run(async () =>
        {
            await MomentoWebApi.SendTextMessage("text", message, "en"); // TODO
        });
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
        //textArea.text = textAreaString;
        //if (this.error)
        //{
        //    textArea.color = Color.red;
        //}

        lock (chatLock)
        {
            foreach (ChatMessageEvent chatMessage in chatsToConsumeOnMainThread)
            {
                Transform chatMessageContainer;
                if (chatMessage.messageType == "image")
                {
                    // TODO
                    chatMessageContainer = CreateImageChat(chatMessage, true);
                }
                else
                {
                    // TODO: do this on the main thread...
                    chatMessageContainer = CreateTextChat(chatMessage);
                }
                chatMessageContainer.SetParent(ScrollingContentContainer.transform, false);
                RebuildChatMessageLayout(chatMessageContainer);
            }
            chatsToConsumeOnMainThread.Clear();

            // TODO: add thread lock
            foreach (var tuple in imageChatsToUpdateOnMainThread)
            {
                UpdateImageChat(tuple.Item1, tuple.Item2);
                RebuildChatMessageLayout(tuple.Item1.transform.parent.parent);
            }
            imageChatsToUpdateOnMainThread.Clear();
        }

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
