using UnityEngine;
using TMPro;
using Momento.Sdk.Auth;
using Momento.Sdk.Exceptions;
using Momento.Sdk.Responses;
using System.Threading.Tasks;
using System;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using SFB;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

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

    public GameObject imagePreview;

    public EventSystem eventSystem;

    public TMP_Dropdown tmpLanguageDropdown;

    public UnityEngine.UI.Button sendMessageButton;

    private Dictionary<int, string> dropdownValueToLanguage = new Dictionary<int, string>();
    private string currentLanguage = "en";

    public GameObject ChatMessagePrefab;
    public GameObject ImageMessagePrefab;

    public GameObject loadingCircle;

    Dictionary<string, Color> usernameColorMap = new Dictionary<string, Color>();
    static readonly string[] colors = { "#C2B2A9", "#E1D9D5", "#EAF8B6", "#ABE7D2" };
    Color[] unityColors = new Color[colors.Length];
    Color mintGreen;

    private string clientName = "Client";

    User myUser;

    // helper variable to update the main text area from the background thread
    //private string textAreaString = "";
    private bool error = false;

    private bool messageInputReadOnly = false;

    private readonly object chatLock = new object();
    private List<ChatMessageEvent> chatsToConsumeOnMainThread = new List<ChatMessageEvent>();
    private List<Tuple<UnityEngine.UI.RawImage, string>> imageChatsToUpdateOnMainThread = new List<Tuple<UnityEngine.UI.RawImage, string>>();

    private byte[] imageBytes;

    private bool getAuthTokenNextFrame = false;

    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < colors.Length; i++)
        {
            Color unityColor;
            ColorUtility.TryParseHtmlString(colors[i], out unityColor);
            unityColors[i] = unityColor;
        }
        ColorUtility.TryParseHtmlString("#00c88c", out mintGreen);

        nameInputTextField.ActivateInputField();

        MomentoWebApi.invokeGetAuthToken = () =>
        {
            this.getAuthTokenNextFrame = true;
        };
    }

    Color GetUsernameColor(string username)
    {
        if (!usernameColorMap.ContainsKey(username))
        {
            // https://stackoverflow.com/a/24031467
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(username);
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                string hash = sb.ToString();
                int colorIndex = Convert.ToInt32(hash.Substring(0, 1), 16) % colors.Length;

                usernameColorMap[username] = unityColors[colorIndex];
            }
        }
        return usernameColorMap[username];
    }

    Transform CreateChatMessageContainer(ChatMessageEvent chatMessage, bool imagePrefab = false)
    {
        Transform chatMessageContainer = GameObject.Instantiate(
            imagePrefab ? ImageMessagePrefab : ChatMessagePrefab).transform;

        // TODO: maybe username should be passed in, but the JavaScript version passes
        // in the id, so let's match that for now...
        Color color = GetUsernameColor(chatMessage.user.id);

        UnityEngine.UI.Image userBubbleImage = chatMessageContainer.GetChild(0).GetComponent<UnityEngine.UI.Image>();
        userBubbleImage.color = color;

        if (chatMessage.user.id == myUser.id)
        {
            UnityEngine.UI.Image messageColor = chatMessageContainer.GetChild(1).GetChild(1).GetComponent<UnityEngine.UI.Image>();
            messageColor.color = mintGreen;
        }

        TextMeshProUGUI userBubbleTMP = chatMessageContainer.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>();
        userBubbleTMP.text = chatMessage.user.username.Substring(0, 1).ToUpper();

        TextMeshProUGUI timestampTMP = chatMessageContainer.GetChild(1).GetChild(0).GetComponent<TextMeshProUGUI>();
        DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(chatMessage.timestamp);
        timestampTMP.text = chatMessage.user.username + " - " + dateTimeOffset.ToLocalTime().ToString("t");

        if (chatMessage.user.id == myUser.id)
        {
            timestampTMP.text += " (You)";
        }

        return chatMessageContainer;
    }

    Transform CreateTextChat(ChatMessageEvent chatMessage)
    {
        Transform chatMessageContainer = CreateChatMessageContainer(chatMessage);

        TextMeshProUGUI messageTMP = 
            chatMessageContainer.GetChild(1).GetChild(1).GetChild(0).GetComponent<TextMeshProUGUI>();
        messageTMP.text = chatMessage.message;

        return chatMessageContainer;
    }

    void UpdateImageChat(UnityEngine.UI.RawImage rawImage, string base64image)
    {
        Texture2D tex = new Texture2D(2, 2); // size doesn't matter
        byte[] imageData = Convert.FromBase64String(base64image);
        ImageConversion.LoadImage(tex, imageData);

        rawImage.texture = tex;

        RectTransform rt = rawImage.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(tex.width, tex.height);
    }

    Transform CreateImageChat(ChatMessageEvent chatMessage, bool fetchFromCache = false)
    {
        Transform chatMessageContainer = CreateChatMessageContainer(chatMessage, true);

        RectTransform verticalLayoutRT = 
            chatMessageContainer.GetChild(1).GetComponent<RectTransform>();

        RectTransform timestampRT = chatMessageContainer.GetChild(1).GetChild(0).GetComponent<RectTransform>();
        timestampRT.sizeDelta = new Vector2(verticalLayoutRT.sizeDelta.x, timestampRT.sizeDelta.y);

        UnityEngine.UI.RawImage rawImage = 
            chatMessageContainer.GetChild(1).GetChild(1).GetChild(0).GetComponent<UnityEngine.UI.RawImage>();

        if (fetchFromCache)
        {
            string imageId = chatMessage.message;
            Debug.Log("Fetching image " + imageId + " from cache...");
            Task.Run(async () =>
            {
                await MomentoWebApi.GetImageMessage(imageId, base64image =>
                {
                    Debug.Log("Got image " + imageId + " from cache...");
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
        // first remove any old chat GameObjects
        foreach (Transform child in ScrollingContentContainer.transform)
        {
            Destroy(child.gameObject);
        }

        // now create GameObjects for each chat message
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

    public async Task Main()
    {
        try
        {
            await MomentoWebApi.SubscribeToTopic(currentLanguage,
                () =>
                {
                    // successfully subscribed
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

                                // we can't update the Unity UI in a background thread, so let's save
                                // the chat message for consumption on the main thread
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
                            //textAreaString += "Error receiving message, cancelling...";
                            this.error = true;
                            break;
                    }
                },
                error =>
                {
                    Debug.LogError(String.Format("Error subscribing to a topic: {0}", error.Message));
                    //textAreaString += "Error trying to connect to chat, cancelling...";
                    this.error = true;
                }
            );
        }
        finally
        {
            Debug.Log("Finished Momento Topic subscription in main thread");
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
            await MomentoWebApi.SendTextMessage(MessageTypes.text, message, currentLanguage);
        });
        inputTextField.text = "";
        inputTextField.ActivateInputField();
    }

    private IEnumerator GetAuthToken()
    {
        TokenResponse tokenResponse = null;
        string error = "";

        yield return TranslationApi.CreateToken(
            myUser,
            response => { tokenResponse = response; },
            _error => { error = _error; }
        );

        if (tokenResponse != null)
        {
            Debug.Log("authToken: " + tokenResponse.token);

            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds((long)tokenResponse.expiresAtEpoch);

            long numSecondsToExpiration = (long)tokenResponse.expiresAtEpoch - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Debug.Log("expiresAt: " + tokenResponse.expiresAtEpoch + " (" + dateTimeOffset.ToLocalTime() +
                "), which is " + numSecondsToExpiration + " seconds from now");

            StringMomentoTokenProvider? authProvider = null;
            try
            {
                authProvider = new StringMomentoTokenProvider(tokenResponse.token);
                MomentoWebApi.authProvider = authProvider;
            }
            catch (InvalidArgumentException e)
            {
                Debug.LogError("Invalid auth token provided! " + e);
                yield break; // TODO
            }
        }
        else
        {
            //textAreaString = error;
            this.error = true;
        }
    }

    private IEnumerator SetupChat(string name)
    {
        messageInputReadOnly = true;

        myUser = new User { username = name, id = Guid.NewGuid().ToString() };
        MomentoWebApi.user = myUser;

        yield return GetAuthToken();

        // start the subscription in the background and then get latest chats...
        Task.Run(async () => { await Main(); });

        yield return GetLatestChats();
    }

    IEnumerator GetLatestChats()
    {
        loadingCircle.SetActive(true);
        yield return TranslationApi.GetLatestChats(
            currentLanguage,
            latestChats =>
            {
                Debug.Log("Got latest chats: " + latestChats);
                SetLatestChats(latestChats);
                loadingCircle.SetActive(false);
            },
            error =>
            {
                loadingCircle.SetActive(false);
                Debug.LogError("Error trying to get the latest chats");
            }
        );
    }

    public void SetName()
    {
        Debug.Log("User entered name " + nameInputTextField.text);

        clientName = nameInputTextField.text;

        nameCanvas.SetActive(false);
        messagingCanvas.SetActive(true);
        inputTextField.ActivateInputField();

        StartCoroutine(SetupChat(clientName));

        // get supported languages in parallel with setting up the chat
        StartCoroutine(TranslationApi.GetSupportedLanguages(languageOptions =>
        {
            tmpLanguageDropdown.interactable = true;
            Debug.Log("Got language options: " + languageOptions);
            List<string> dropdownOptions = new List<string>();
            foreach (LanguageOption languageOption in languageOptions.supportedLanguages)
            {
                Debug.Log("Got option: " + languageOption.value + ", " + languageOption.label);
                dropdownValueToLanguage.Add(dropdownOptions.Count, languageOption.value);

                dropdownOptions.Add(languageOption.label);
            }
            tmpLanguageDropdown.AddOptions(dropdownOptions);
        }, error =>
        {
            Debug.Log("Error trying to get languages..." + error);
        }));
    }

    public void LanguageChanged()
    {
        Debug.Log("new lang " + tmpLanguageDropdown.value + ", which is " + dropdownValueToLanguage[tmpLanguageDropdown.value]);
        currentLanguage = dropdownValueToLanguage[tmpLanguageDropdown.value];
        // restart topic language subscription...
        MomentoWebApi.Dispose();
        StartCoroutine(GetLatestChats());
        Task.Run(async () => { await Main(); });
    }

    public void ImageButtonClicked()
    {
        try
        {
            var extensions = new[] {
                new ExtensionFilter("Image Files", "png", "jpg", "jpeg" )
            };
            string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", extensions, false);

            if (paths.Length == 0)
            {
                Debug.Log("No image selected");
                return;
            }
            Debug.Log("Loading in image at path " + paths[0]);

            imageBytes = File.ReadAllBytes(paths[0]);

            // run image compression
            // https://stackoverflow.com/a/9432319
            var jpegQuality = 50;
            System.Drawing.Image image;
            using (var inputStream = new MemoryStream(imageBytes))
            {
                image = System.Drawing.Image.FromStream(inputStream);
                var jpegEncoder = ImageCodecInfo.GetImageDecoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
                byte[] outputBytes;
                using (var outputStream = new MemoryStream())
                {
                    image.Save(outputStream, jpegEncoder, encoderParameters);
                    outputBytes = outputStream.ToArray();
                    Debug.Log("Image compression changed image byte size from " + imageBytes.Length + " to " + outputBytes.Length);
                    imageBytes = outputBytes;
                }
            }

            // TODO: ensure file size is under 1MB (Momento Cache max size)
            // See https://docs.momentohq.com/cache/limits

            // preview image
            Texture2D tex = new Texture2D(2, 2); // size doesn't matter
            ImageConversion.LoadImage(tex, imageBytes);
            UnityEngine.UI.RawImage rawImage = imagePreview.transform.GetComponentInChildren<UnityEngine.UI.RawImage>();
            rawImage.texture = tex;
            imagePreview.SetActive(true);
        }
        catch (Exception e)
        {
            Debug.LogError("Error while trying to load file " + e);
        }
    }

    public void SendImageButtonClicked()
    {
        Debug.Log("User clicked send image button");

        imagePreview.SetActive(false);

        string base64Image = Convert.ToBase64String(imageBytes);

        Task.Run(async () =>
        {
            await MomentoWebApi.SendImageMessage(base64Image, currentLanguage);
        });
    }

    public void CancelImageButtonClicked()
    {
        Debug.Log("User canceled sending image");
        imagePreview.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (this.getAuthTokenNextFrame)
        {
            this.getAuthTokenNextFrame = false;
            StartCoroutine(GetAuthToken());
        }
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
                    chatMessageContainer = CreateImageChat(chatMessage, true);
                }
                else
                {
                    chatMessageContainer = CreateTextChat(chatMessage);
                }
                chatMessageContainer.SetParent(ScrollingContentContainer.transform, false);
                RebuildChatMessageLayout(chatMessageContainer);
            }
            chatsToConsumeOnMainThread.Clear();

            foreach (var tuple in imageChatsToUpdateOnMainThread)
            {
                UpdateImageChat(tuple.Item1, tuple.Item2);

                // TODO: this rebuild isn't working anymore... so we just rebuild the entire window
                // at the end for now...
                //    RebuildChatMessageLayout(tuple.Item1.transform.parent.parent.parent);
            }

            if (imageChatsToUpdateOnMainThread.Count > 0)
            {
                RebuildChatMessageLayout(ScrollingContentContainer.transform);
            }
            imageChatsToUpdateOnMainThread.Clear();
        }

        inputTextField.readOnly = messageInputReadOnly;

        if (!messageInputReadOnly)
        {
            sendMessageButton.interactable = inputTextField.text != "";

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
    }

    private void OnDestroy()
    {
        Debug.Log("Cancelling tasks...");
        //cts?.Cancel();
        MomentoWebApi.Dispose();
    }
}
