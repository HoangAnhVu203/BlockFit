using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private LevelManager levelManager;
    [SerializeField] private float timeLimitSeconds;

    public GameState CurrentState { get; private set; } = GameState.Gameplay;
    public float RemainingTime { get; private set; }
    public event Action<GameState> StateChanged;
    public bool HasNextLevel => levelManager != null && levelManager.HasNextLevel;
    public int CurrentLevelNumber => levelManager == null ? 1 : levelManager.CurrentLevelNumber;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        StartGameplay();
    }

    private void Update()
    {
        if (CurrentState != GameState.Gameplay || timeLimitSeconds <= 0f)
        {
            return;
        }

        RemainingTime -= Time.deltaTime;
        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            Lose();
        }
    }

    public void StartGameplay()
    {
        CurrentState = GameState.Gameplay;
        RemainingTime = timeLimitSeconds;
        StateChanged?.Invoke(CurrentState);

        if (levelManager != null)
        {
            levelManager.StartLevel();
        }

        if (CurrentState == GameState.Gameplay)
        {
            ShowStateUI(CurrentState);
        }
    }

    public void RestartLevel()
    {
        StartGameplay();
    }

    public void StartNextLevel()
    {
        if (levelManager == null || !levelManager.LoadNextLevel())
        {
            return;
        }

        StartGameplay();
    }

    public void Win()
    {
        SetState(GameState.Win);
    }

    public void Lose()
    {
        SetState(GameState.Lose);
    }

    private void SetState(GameState state)
    {
        if (CurrentState == state)
        {
            return;
        }

        CurrentState = state;
        StateChanged?.Invoke(CurrentState);
        ShowStateUI(CurrentState);
    }

    private void ShowStateUI(GameState state)
    {
        if (UIManager.instance == null)
        {
            return;
        }

        switch (state)
        {
            case GameState.Gameplay:
                UIManager.Instance.CloseUIDirectly<PanelWin>();
                UIManager.Instance.CloseUIDirectly<PanelLose>();
                UIManager.Instance.OpenUI<PanelGameplay>();
                break;
            case GameState.Win:
                UIManager.Instance.CloseUIDirectly<PanelGameplay>();
                UIManager.Instance.CloseUIDirectly<PanelLose>();
                UIManager.Instance.OpenUI<PanelWin>();
                break;
            case GameState.Lose:
                UIManager.Instance.CloseUIDirectly<PanelGameplay>();
                UIManager.Instance.CloseUIDirectly<PanelWin>();
                UIManager.Instance.OpenUI<PanelLose>();
                break;
        }
    }
}
