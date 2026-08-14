using UnityEngine;
using UnityEngine.UI;

public class PanelLose : UICanvas
{
    [SerializeField] private Button replayBtn;

    public override void SetUp()
    {
        if (replayBtn == null)
        {
            replayBtn = GetComponentInChildren<Button>(true);
        }

        if (replayBtn != null)
        {
            replayBtn.onClick.RemoveListener(OnReplayButtonClicked);
            replayBtn.onClick.AddListener(OnReplayButtonClicked);
        }
    }

    private void OnReplayButtonClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestartLevel();
        }
    }
}
