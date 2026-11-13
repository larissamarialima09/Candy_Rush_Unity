using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [ContextMenu("Open Menu Scene")]
    public void OpenMenuScene()
    {
        SceneManager.LoadScene("Menu");
    }

    [ContextMenu("Open CharacterSelection Scene")]
    public void OpenCharacterSelectionScene()
    {
        SceneManager.LoadScene("CharacterSelection");
    }

    public static void GoToMenu()
    {
        SceneManager.LoadScene("Menu");
    }

    public static void GoToCharacterSelection()
    {
        SceneManager.LoadScene("CharacterSelection");
    }
}
