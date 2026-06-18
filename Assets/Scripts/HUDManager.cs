using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Slider healthBar;
    public Slider considerationBar;
    public TextMeshProUGUI conditionText;
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI comboText;
    public TextMeshProUGUI healthValueText;
    public TextMeshProUGUI considerationValueText;

    [Header("콤보 텍스트 - 캐릭터 위치 따라다님")]
    public Transform characterTransform; // 플레이어 캐릭터(CharacterDisplay) Transform
    public Vector2 comboTextOffset = new Vector2(80f, 0f); // 캐릭터 오른쪽으로 떨어진 거리 (스크린 픽셀)

    void OnEnable()
    {
        GameManager.OnTurnProcessed += UpdateHUD;
        GameManager.OnGameOver += UpdateHUD;
        GameManager.OnGameInitialized += UpdateHUD;
    }

    void OnDisable()
    {
        GameManager.OnTurnProcessed -= UpdateHUD;
        GameManager.OnGameOver -= UpdateHUD;
        GameManager.OnGameInitialized -= UpdateHUD;
    }

    void Update()
    {
        if (characterTransform == null || comboText == null || Camera.main == null) return;
        Vector3 screenPos = Camera.main.WorldToScreenPoint(characterTransform.position);
        comboText.rectTransform.position = screenPos + (Vector3)comboTextOffset;
    }

    public void UpdateHUD()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        healthBar.value = (float)gm.currentHealth / gm.maxHealth;
        considerationBar.value = gm.consideration / 100f;
        if (healthValueText) healthValueText.text = gm.currentHealth + " / " + gm.maxHealth;
        if (considerationValueText) considerationValueText.text = Mathf.FloorToInt(gm.consideration) + " / 100";
        conditionText.text = "컨디션: " + GetConditionText(gm.condition);
        turnText.text = "남은 역: " + (GameManager.MaxTurns - gm.currentTurn);
        comboText.text = gm.comboCount > 0 ? "콤보: x" + gm.comboCount : "";
    }

    string GetConditionText(GameManager.ConditionLevel level)
    {
        switch (level)
        {
            case GameManager.ConditionLevel.Great:  return "완전 좋음";
            case GameManager.ConditionLevel.Good:   return "적당히 좋음";
            case GameManager.ConditionLevel.Normal: return "보통";
            case GameManager.ConditionLevel.Bad:    return "안좋음";
            case GameManager.ConditionLevel.Worst:  return "최악";
            default: return "보통";
        }
    }
}
