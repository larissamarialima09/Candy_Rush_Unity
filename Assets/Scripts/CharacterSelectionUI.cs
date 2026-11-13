using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterSelectionUI : MonoBehaviour
{
    [SerializeField] private Image selectedCharacterImage;
    [SerializeField] private TextMeshProUGUI selectedCharacterName;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;

    [SerializeField] private Sprite[] characterSprites;
    [SerializeField] private string[] characterNames;

    private void Start()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmCharacter);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButton);

        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (CharacterSelectionManager.Instance == null) return;

        string selectedName = CharacterSelectionManager.Instance.SelectedCharacter;

        for (int i = 0; i < characterNames.Length; i++)
        {
            if (characterNames[i] == selectedName && i < characterSprites.Length)
            {
                if (selectedCharacterImage != null)
                    selectedCharacterImage.sprite = characterSprites[i];

                if (selectedCharacterName != null)
                    selectedCharacterName.text = selectedName;

                break;
            }
        }
    }

    private void OnConfirmCharacter()
    {
        if (CharacterSelectionManager.Instance != null)
            CharacterSelectionManager.Instance.ConfirmCharacter();
    }

    private void OnBackButton()
    {
        if (CharacterSelectionManager.Instance != null)
            CharacterSelectionManager.Instance.GoBackToMenu();
    }
}
