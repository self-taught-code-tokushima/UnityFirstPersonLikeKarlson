using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    public Button exitButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        exitButton.onClick.AddListener(Exit);
    }

    public void SelectStage(string stage)
    {
        SceneManager.LoadScene(stage);
    }
    
    private static void Exit()
    {
        Debug.Log("Exit Game");
        Application.Quit();
    }
}
