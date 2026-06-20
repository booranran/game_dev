using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System;

public class ElbowGameController : MonoBehaviour
{
    public static ElbowGameController Instance;
    public static event Action<bool> OnElbowGameEnd;

    [Header("UI 참조")]
    public GameObject elbowGamePanel;
    public Slider gaugeSlider;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI resultText;

    [Header("엘보우 비주얼 (두 엘보우를 하나의 부모로 묶은 RectTransform)")]
    public RectTransform elbowGroupTransform;
    public RectTransform leftBoundMarker; // gaugeValue=0(완전히 밀림) - 이 마커의 X 위치를 그대로 씀, Scene에서 직접 배치
    public RectTransform rightBoundMarker; // gaugeValue=1(완전히 이김)

    [Header("디버그")]
    public bool debugMode = false;

    [Header("게임 설정")]
    public float roundDuration = 3f;
    public float tapAmount = 0.1f;

    [Header("밀기 속도 - 학생")]
    public float studentBasePushSpeed = 0.15f;
    public float studentSpeedIncreasePerRound = 0.05f;

    [Header("밀기 속도 - 직장인")]
    public float workerBasePushSpeed = 0.2f;
    public float workerSpeedIncreasePerRound = 0.07f;

    float BasePushSpeed => GameManager.Instance != null && GameManager.Instance.characterType == GameManager.CharacterType.Worker
        ? workerBasePushSpeed : studentBasePushSpeed;
    float SpeedIncreasePerRound => GameManager.Instance != null && GameManager.Instance.characterType == GameManager.CharacterType.Worker
        ? workerSpeedIncreasePerRound : studentSpeedIncreasePerRound;

    [Header("독백 (TurnController가 메인 화면에 표시 - 턴 전환 분리용)")]
    [TextArea] public string[] startMonologues;
    [TextArea] public string[] winMonologues;
    [TextArea] public string[] loseMonologues;

    private int currentRound;
    private int playerWins;
    private float gaugeValue;
    private float roundTimer;
    private bool isPlaying;
    public bool IsPlaying => isPlaying;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public string GetStartLine() => PickLine(startMonologues);
    public string GetEndLine(bool won) => PickLine(won ? winMonologues : loseMonologues);

    string PickLine(string[] lines)
    {
        if (lines == null || lines.Length == 0) return "";
        return lines[UnityEngine.Random.Range(0, lines.Length)];
    }

    public void StartElbowGame()
    {
        currentRound = 0;
        playerWins = 0;
        elbowGamePanel.SetActive(true);
        if (resultText) resultText.text = "";
        StartRound();
    }

    void StartRound()
    {
        gaugeValue = 0.5f;
        roundTimer = roundDuration;
        isPlaying = true;
        if (gaugeSlider) gaugeSlider.value = gaugeValue;
        UpdateElbowGroupPosition();
        if (roundText) roundText.text = $"{currentRound + 1} / 3 라운드";
        if (resultText) resultText.text = "";
        Debug.Log($"[팔꿈치] 라운드 {currentRound + 1} 시작 | 밀기 속도: {BasePushSpeed + SpeedIncreasePerRound * currentRound:F2}");
    }

    void Update()
    {
        if (debugMode && Keyboard.current != null && Keyboard.current[Key.Digit2].wasPressedThisFrame)
            StartElbowGame();

        // 디버그용 - debugMode 체크 없이 4 누르면 바로 강제 실행
        if (Keyboard.current != null && Keyboard.current[Key.Digit4].wasPressedThisFrame)
            StartElbowGame();

        if (!isPlaying) return;

        float pushSpeed = BasePushSpeed + SpeedIncreasePerRound * currentRound;
        gaugeValue = Mathf.Clamp01(gaugeValue - pushSpeed * Time.deltaTime);
        if (gaugeSlider) gaugeSlider.value = gaugeValue;
        UpdateElbowGroupPosition();

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            OnTapButton();

        roundTimer -= Time.deltaTime;
        if (roundTimer <= 0f)
            EndRound();
    }

    void UpdateElbowGroupPosition()
    {
        if (!elbowGroupTransform || !leftBoundMarker || !rightBoundMarker) return;
        Vector2 pos = elbowGroupTransform.anchoredPosition;
        pos.x = Mathf.Lerp(leftBoundMarker.anchoredPosition.x, rightBoundMarker.anchoredPosition.x, gaugeValue);
        elbowGroupTransform.anchoredPosition = pos;
    }

    public void OnTapButton()
    {
        if (!isPlaying) return;
        gaugeValue = Mathf.Clamp01(gaugeValue + tapAmount);
        if (gaugeSlider) gaugeSlider.value = gaugeValue;
        UpdateElbowGroupPosition();
    }

    void EndRound()
    {
        isPlaying = false;
        bool roundWon = gaugeValue >= 0.5f;
        if (roundWon) playerWins++;

        if (resultText) resultText.text = roundWon ? "방어 성공!" : "밀렸다!";
        Debug.Log($"[팔꿈치] 라운드 {currentRound + 1} → {(roundWon ? "성공" : "실패")} (게이지: {gaugeValue:F2}) | 누적 승: {playerWins}");

        currentRound++;
        if (currentRound >= 3)
            Invoke(nameof(EndGameDelayed), 1f);
        else
            Invoke(nameof(StartRound), 1f);
    }

    public void ForceEnd()
    {
        isPlaying = false;
        CancelInvoke();
        StopAllCoroutines();
        elbowGamePanel.SetActive(false);
        Debug.Log("[팔꿈치] 강제 종료");
    }

    void EndGameDelayed()
    {
        bool won = playerWins >= 2;
        Debug.Log($"[팔꿈치] 게임 종료 → {(won ? "승리" : "패배")} ({playerWins}/3)");
        elbowGamePanel.SetActive(false);
        OnElbowGameEnd?.Invoke(won);
    }
}
