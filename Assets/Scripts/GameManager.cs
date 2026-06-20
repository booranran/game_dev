using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum CharacterType { Student, Worker }
    public enum LineType { Line9, Line7 }
    public enum ConditionLevel { Great, Good, Normal, Bad, Worst }
    public enum PlayerState { Sitting, Standing }
    public enum EndingType { Good, NormalA, NormalB, Bad, GameOver }

    [Header("Game State")]
    public int currentTurn = 0;
    public int MaxTurns = 25; // 기존 15 - 게임이 3분도 안 걸려서 늘림, 양보 기회도 같이 늘어남
    public PlayerState playerState = PlayerState.Sitting;

    [Header("Character Setup")]
    public CharacterType characterType;
    public LineType lineType;

    [Header("디버그 (StartPanelController 없이 바로 테스트)")]
    public bool debugMode = false;
    public CharacterType debugCharacterType = CharacterType.Student;
    public LineType debugLineType = LineType.Line9;

    [Header("Stats")]
    public int maxHealth;
    public int currentHealth;
    public int healthRecoveryPerTurn;
    public int baseConsiderationGain;
    public float consideration = 0f;
    public int comboCount = 0;
    public ConditionLevel condition = ConditionLevel.Normal;

    public static event Action OnTurnProcessed;
    public static event Action OnGameOver;
    public static event Action<EndingType> OnGameEnd;
    public static event Action OnGameInitialized;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        if (debugMode)
            BeginGame(debugCharacterType, debugLineType);
    }

    // StartPanelController가 캐릭터/모드 선택 + 시작 연출을 끝낸 뒤 호출 - 그 전까지 게임 로직은 시작 안 함
    public void BeginGame(CharacterType character, LineType line)
    {
        InitializeGame(character, line);
        if (playerState == PlayerState.Sitting)
            SeatManager.Instance?.HidePlayerSeat();
        OnGameInitialized?.Invoke();
    }

    public void InitializeGame(CharacterType character, LineType line)
    {
        characterType = character;
        lineType = line;
        currentTurn = 0;
        consideration = 0f;
        comboCount = 0;
        condition = ConditionLevel.Normal;
        playerState = PlayerState.Sitting;

        if (character == CharacterType.Student)
        {
            maxHealth = 200;
            healthRecoveryPerTurn = 8;
            baseConsiderationGain = 8; // 기존 5 - 안전하게 앉아만 있는 플레이로는 배려심 60 못 넘던 문제 픽스
        }
        else if (character == CharacterType.Worker)
        {
            maxHealth = 150;
            healthRecoveryPerTurn = 4;
            baseConsiderationGain = 10; // 기존 7
        }
        currentHealth = maxHealth;
    }

    public void ProcessTurn()
    {
        if (playerState == PlayerState.Sitting)
            currentHealth = Mathf.Min(currentHealth + healthRecoveryPerTurn, maxHealth);
        else
        {
            currentHealth -= GetHealthDrain();
            comboCount = Mathf.Min(comboCount + 1, MaxTurns);
        }

        currentTurn++;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            OnGameOver?.Invoke();
            return;
        }

        if (currentTurn >= MaxTurns)
        {
            OnGameEnd?.Invoke(DetermineEnding());
            return;
        }

        OnTurnProcessed?.Invoke();
    }

    int GetHealthDrain()
    {
        switch (condition)
        {
            case ConditionLevel.Great:  return 1;
            case ConditionLevel.Good:   return 2;
            case ConditionLevel.Normal: return 4;
            case ConditionLevel.Bad:    return 8;
            case ConditionLevel.Worst:  return 16;
            default: return 4;
        }
    }

    [Header("컨디션별 외부 데미지 배율 (가방방어 등, Normal 기준 1배)")]
    public float conditionMultGreat = 0.25f;
    public float conditionMultGood = 0.5f;
    public float conditionMultNormal = 1f;
    public float conditionMultBad = 2f;
    public float conditionMultWorst = 4f;

    // GetHealthDrain()과 같은 비율 구조(1/2/4/8/16) - 컨디션이 나쁠수록 외부 데미지에도 더 취약해짐
    public float GetConditionDamageMultiplier()
    {
        switch (condition)
        {
            case ConditionLevel.Great:  return conditionMultGreat;
            case ConditionLevel.Good:   return conditionMultGood;
            case ConditionLevel.Normal: return conditionMultNormal;
            case ConditionLevel.Bad:    return conditionMultBad;
            case ConditionLevel.Worst:  return conditionMultWorst;
            default: return 1f;
        }
    }

    public void Sit()
    {
        playerState = PlayerState.Sitting;
    }

    public void StandIntentionally()
    {
        playerState = PlayerState.Standing;
    }

    public void Yield()
    {
        float multiplier = 1f + comboCount * 0.2f;
        consideration = Mathf.Min(consideration + baseConsiderationGain * multiplier, 100f);
        comboCount = 0; // 양보로 콤보 소진 - 그냥 쌓이기만 하는 스택이 아니라 다시 서있어야 콤보가 쌓이게
        playerState = PlayerState.Standing;
        SeatManager.Instance?.ClearPlayerSeat();
    }

    public void YieldInEyeGame()
    {
        ChangeCondition(-1);
    }

    public void ChangeCondition(int delta)
    {
        int next = Mathf.Clamp((int)condition - delta, 0, 4);
        condition = (ConditionLevel)next;
    }

    public void ChangeHealth(int delta)
    {
        currentHealth = Mathf.Clamp(currentHealth + delta, 0, maxHealth);
    }

    EndingType DetermineEnding()
    {
        float healthPercent = (float)currentHealth / maxHealth * 100f;
        bool healthOk = healthPercent >= 60f;
        bool considerationOk = consideration >= 60f;

        if (healthOk && considerationOk)  return EndingType.Good;
        if (healthOk && !considerationOk) return EndingType.NormalA;
        if (!healthOk && considerationOk) return EndingType.NormalB;
        return EndingType.Bad;
    }

    // 캐릭터마다 maxHealth가 달라서(학생 200, 직장인 150), 100점 만점으로 정규화해서 보여줌
    public float GetHealthScore() => (float)currentHealth / maxHealth * 100f;
    public float GetConsiderationScore() => consideration; // 이미 0~100 척도
}
