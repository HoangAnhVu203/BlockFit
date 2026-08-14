using UnityEngine;
using UnityEngine.UI;

public class PanelWin : UICanvas
{
    [SerializeField] private Button nextBtn;

    public override void SetUp()
    {
        if (nextBtn == null)
        {
            nextBtn = GetComponentInChildren<Button>(true);
        }

        if (nextBtn != null)
        {
            nextBtn.onClick.RemoveListener(OnNextButtonClicked);
            nextBtn.onClick.AddListener(OnNextButtonClicked);
            nextBtn.interactable = GameManager.Instance != null && GameManager.Instance.HasNextLevel;
        }
    }

    private void OnNextButtonClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartNextLevel();
        }
    }
}
