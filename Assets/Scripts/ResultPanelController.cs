using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultPanelController : MonoBehaviour
{
    [System.Serializable]
    public struct ResultData
    {
        public GameManager.CharacterType character;
        public GameManager.EndingType ending;
        public Sprite illustration;
        public string title;
        [TextArea] public string description;
    }

    [Header("UI 참조")]
    public GameObject resultPanel;
    public Image illustrationImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [Header("엔딩 데이터 (캐릭터 x 엔딩 조합별로 등록)")]
    public ResultData[] resultEntries;

    public void Show(GameManager.CharacterType character, GameManager.EndingType ending)
    {
        foreach (var entry in resultEntries)
        {
            if (entry.character != character || entry.ending != ending) continue;
            if (illustrationImage) illustrationImage.sprite = entry.illustration;
            if (titleText) titleText.text = entry.title;
            if (descriptionText) descriptionText.text = entry.description;
            resultPanel.SetActive(true);
            return;
        }

        Debug.LogWarning($"[ResultPanel] {character}/{ending} 엔딩 데이터가 등록되지 않음");
        resultPanel.SetActive(true);
    }
}
