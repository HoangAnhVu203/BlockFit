using UnityEngine;
using UnityEngine.UI;

public class PanelGameplay : UICanvas
{
    [SerializeField] private Text levelTxt;

    public override void SetUp()
    {
        if (levelTxt == null)
        {
            levelTxt = FindLevelText();
        }

        if (levelTxt != null)
        {
            int levelNumber = GameManager.Instance == null ? 1 : GameManager.Instance.CurrentLevelNumber;
            levelTxt.text = $"Level {levelNumber}";
        }
    }

    private Text FindLevelText()
    {
        Text[] texts = GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            if (text.name == "LevelTxt")
            {
                return text;
            }
        }

        return texts.Length == 0 ? null : texts[0];
    }
}
