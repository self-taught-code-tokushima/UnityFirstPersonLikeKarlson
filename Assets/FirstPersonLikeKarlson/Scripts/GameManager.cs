using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public PlayerMovement playerMovement;
    
    public GameObject pauseUI;
    public GameObject clearUI;
    public GameObject retryButton;
    public GameObject backToTitleButton;
    public GameObject retryButtonInClear;
    public GameObject backToTitleButtonInClear;
    public Text clearedTimeText;

    public Goal goal;

    public Text timerText;

    private bool isPlaying;
    private float timer;

    private void Awake()
    {
        isPlaying = false;
        pauseUI.SetActive(false);
        clearUI.SetActive(false);
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        retryButton.GetComponent<Button>().onClick.AddListener(RetryStage);
        backToTitleButton.GetComponent<Button>().onClick.AddListener(BackToTitle);
        retryButtonInClear.GetComponent<Button>().onClick.AddListener(RetryStage);
        backToTitleButtonInClear.GetComponent<Button>().onClick.AddListener(BackToTitle);
        
        goal.OnGoalReached += ClearStage;
        playerMovement.OnPause += Pause;
        
        StartStage();
    }

    void Update()
    {
        if (isPlaying)
        {
            timer += Time.deltaTime;
            var timeSpan = TimeSpan.FromSeconds(timer);
            timerText.text = $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
    }

    private void StartStage()
    {
        isPlaying = true;
        Time.timeScale = 1f;
    }

    private void Pause(bool paused)
    {
        if (paused)
        {
            Time.timeScale = 0f;
            isPlaying = false;
            pauseUI.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
            isPlaying = true;
            pauseUI.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
    }

    private void ClearStage()
    {
        isPlaying = false;
        Time.timeScale = 0f;
        playerMovement.GoalReached();
        var timeSpan = TimeSpan.FromSeconds(timer);
        clearedTimeText.text = $"Cleared Time: {timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        clearUI.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void BackToTitle()
    {
        SceneManager.LoadScene("MainTitle");
    }
    
    private void RetryStage()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
