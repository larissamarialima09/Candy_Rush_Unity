using UnityEngine;
using UnityEngine.SceneManagement;

public class CharacterSelectionManager : MonoBehaviour
{
    public static CharacterSelectionManager Instance { get; private set; }
    public string SelectedCharacter { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SelectCharacter(string characterName)
    {
        SelectedCharacter = characterName;
        SceneManager.LoadScene("CharacterSelection");
    }

    public void ConfirmCharacter()
    {
        if (!string.IsNullOrEmpty(SelectedCharacter))
        {
            SceneManager.LoadScene("Game");
        }
    }

    public void GoBackToMenu()
    {
        SceneManager.LoadScene("Menu");
    }
}
