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

    [Header("디버그")]
    public bool debugMode = false;

    [Header("게임 설정")]
    public float roundDuration = 3f;
    public float basePushSpeed = 0.15f;
    public float speedIncreasePerRound = 0.05f;
    public float tapAmount = 0.1f;

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
        if (roundText) roundText.text = $"{currentRound + 1} / 3 라운드";
        if (resultText) resultText.text = "";
        Debug.Log($"[팔꿈치] 라운드 {currentRound + 1} 시작 | 밀기 속도: {basePushSpeed + speedIncreasePerRound * currentRound:F2}");
    }

    void Update()
    {
        if (debugMode && Keyboard.current != null && Keyboard.current[Key.Digit2].wasPressedThisFrame)
            StartElbowGame();

        if (!isPlaying) return;

        float pushSpeed = basePushSpeed + speedIncreasePerRound * currentRound;
        gaugeValue = Mathf.Clamp01(gaugeValue - pushSpeed * Time.deltaTime);
        if (gaugeSlider) gaugeSlider.value = gaugeValue;

        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            OnTapButton();

        roundTimer -= Time.deltaTime;
        if (roundTimer <= 0f)
            EndRound();
    }

    public void OnTapButton()
    {
        if (!isPlaying) return;
        gaugeValue = Mathf.Clamp01(gaugeValue + tapAmount);
        if (gaugeSlider) gaugeSlider.value = gaugeValue;
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
