using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EmojiHelper : MonoBehaviour
{
    // the emojiGrid is where we'll show all the available
    // emojis to the user
    public GameObject emojiGrid;

    // the main chat input field
    public TMP_InputField inputTextField;

    // prefab from which we'll dynamically create all
    // 16 available emojis in the emojiGrid
    public GameObject emojiButtonPrefab;

    // Start is called before the first frame update
    void Start()
    {
        // Assets/TextMesh Pro/Sprites/EmojiOne.png shows that it has 16 available
        // emoji sprites that we can reference via the following syntax in TextMeshPro:
        // <sprite index="N">
        for (int i = 0; i < 16; i++)
        {
            GameObject newButton = Instantiate(emojiButtonPrefab, emojiGrid.transform);

            // position appropriately in a 4x4 grid
            Vector2 anchoredPosition = new Vector2((i % 4) * 60, -Mathf.Floor(i / 4) * 60);
            newButton.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;

            // show the correct emoji
            string emojiSprite = "<sprite index=\"" + i + "\">";
            newButton.GetComponentInChildren<TextMeshProUGUI>().text = emojiSprite;

            // set the button callback to add the emoji to the text input field
            newButton.GetComponent<Button>().onClick.AddListener(() =>
            {
                inputTextField.text += emojiSprite;

                // when the user clicks the button, we need to reset the focus
                // to be on the input text field and also reset its caret to the
                // end of its line
                inputTextField.ActivateInputField();
                inputTextField.MoveToEndOfLine(false, false);
            });
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnEmojiMainButtonClicked()
    {
        emojiGrid.SetActive(!emojiGrid.activeSelf);
    }
}
