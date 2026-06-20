using UnityEngine;
using System;
using System.Collections.Generic;

public class EventManager : MonoBehaviour
{
    public static EventManager Instance;

    public enum EventType { None, YieldNPC, EmptySeat, TriggerNPC, ElbowGame, BagDefense }
    public enum NPCType
    {
        Elderly,
        Pregnant,
        // Passenger,
        // Dog,
        // BabyHappy,
        // BabyCrying,
        TiredWorker,
        Girl,
        SmartphonePassenger,
    }

    public EventType currentEvent { get; private set; }
    public NPCType currentNPC { get; private set; }

    [Header("이벤트 한도")]
    public int maxEmptySeats = 10;
    public int maxYieldNPCs = 9;

    [Header("YieldNPC 가중치")]
    [Tooltip("Sitting 상태 풀에서 YieldNPC가 차지하는 비율 (높을수록 양보 기회 자주 생김) - 기존엔 3으로 하드코딩돼있었음")]
    public int yieldNPCWeight = 4;

    [Header("None 이벤트 가중치")]
    [Tooltip("Standing 상태에서 None 이벤트 비율 (낮을수록 EmptySeat 자주 발생)")]
    public int noneWeightStanding = 1;
    [Tooltip("Sitting 상태에서 None 이벤트 비율 (낮을수록 YieldNPC 자주 발생)")]
    public int noneWeightSitting = 1;

    [Header("TriggerNPC 가중치")]
    [Tooltip("TriggerNPC 이벤트 등장 가중치 (0이면 비활성화)")]
    public int triggerNPCWeight = 1;

    [Header("ElbowGame 가중치")]
    [Tooltip("팔꿈치 게임 등장 가중치 (0이면 비활성화)")]
    public int elbowGameWeight = 1;

    [Header("BagDefense 설정 (보딩 인원수 기반)")]
    [Tooltip("가방 방어 게임이 등장하기 시작하는 턴")]
    public int bagGameStartTurn = 5;
    [Tooltip("이번 정거장 보딩 인원 1명당 추가되는 가중치 (인원 0이면 BagDefense 안 나옴)")]
    public int bagDefenseWeightPerBoardingPerson = 1;

    [Header("미니게임 쿨다운 (각자 따로 적용)")]
    [Tooltip("ElbowGame이 발생하면, 이후 최소 이만큼 턴이 지나야 ElbowGame이 다시 발생함")]
    public int elbowGameCooldownTurns = 2;
    [Tooltip("BagDefense가 발생하면, 이후 최소 이만큼 턴이 지나야 BagDefense가 다시 발생함")]
    public int bagDefenseCooldownTurns = 2;

    private int emptySeatCount = 0;
    private int yieldNPCCount = 0;
    private int lastElbowGameTurn = -1000; // 충분히 큰 음수로, currentTurn과 빼도 오버플로우 안 나게
    private int lastBagDefenseTurn = -1000;

    public static event Action<EventType, NPCType> OnEventGenerated;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void GenerateEvent()
    {
        GameManager gm = GameManager.Instance;
        currentEvent = PickEventType(gm.playerState);
        currentNPC = NPCType.Elderly;

        Debug.Log($"[EventManager] 턴 {gm.currentTurn} | 상태: {gm.playerState} | 이벤트: {currentEvent}");

        if (currentEvent == EventType.YieldNPC)
        {
            yieldNPCCount++;
            currentNPC = PickYieldNPC();
            Debug.Log($"[EventManager] YieldNPC 발생 ({yieldNPCCount}/{maxYieldNPCs}) | NPC: {currentNPC}");
        }
        else if (currentEvent == EventType.EmptySeat)
        {
            emptySeatCount++;
            Debug.Log($"[EventManager] EmptySeat 발생 ({emptySeatCount}/{maxEmptySeats})");
            // 좌석 오픈은 여기서 안 함 - TurnController.HandleEmptySeat에서 처리
        }
        else if (currentEvent == EventType.TriggerNPC)
        {
            Debug.Log("[EventManager] TriggerNPC 발생");
        }
        else if (currentEvent == EventType.ElbowGame)
        {
            lastElbowGameTurn = gm.currentTurn;
            Debug.Log("[EventManager] ElbowGame 발생");
        }
        else if (currentEvent == EventType.BagDefense)
        {
            lastBagDefenseTurn = gm.currentTurn;
            Debug.Log("[EventManager] BagDefense 발생");
        }

        OnEventGenerated?.Invoke(currentEvent, currentNPC);
    }

    EventType PickEventType(GameManager.PlayerState state)
    {
        List<EventType> pool = new List<EventType>();
        bool elbowOnCooldown = GameManager.Instance.currentTurn - lastElbowGameTurn < elbowGameCooldownTurns;
        bool bagOnCooldown = GameManager.Instance.currentTurn - lastBagDefenseTurn < bagDefenseCooldownTurns;

        if (state == GameManager.PlayerState.Sitting)
        {
            if (yieldNPCCount < maxYieldNPCs)
                for (int i = 0; i < yieldNPCWeight; i++) pool.Add(EventType.YieldNPC);
            if (!elbowOnCooldown)
                for (int i = 0; i < elbowGameWeight; i++) pool.Add(EventType.ElbowGame);
            for (int i = 0; i < noneWeightSitting; i++) pool.Add(EventType.None);
        }
        else
        {
            if (emptySeatCount < maxEmptySeats)
            {
                pool.Add(EventType.EmptySeat);
                pool.Add(EventType.EmptySeat);
            }
            if (!bagOnCooldown && GameManager.Instance.currentTurn >= bagGameStartTurn && BoardingController.Instance != null)
            {
                int bagWeight = BoardingController.Instance.CurrentBoardingCount * bagDefenseWeightPerBoardingPerson;
                for (int i = 0; i < bagWeight; i++) pool.Add(EventType.BagDefense);
            }
            for (int i = 0; i < noneWeightStanding; i++) pool.Add(EventType.None);
        }

        if (TriggerEventController.Instance != null && TriggerEventController.Instance.HasAvailableEvent())
            for (int i = 0; i < triggerNPCWeight; i++) pool.Add(EventType.TriggerNPC);

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    NPCType PickCompetingNPC()
    {
        int r = UnityEngine.Random.Range(0, 3);
        if (r == 0) return NPCType.TiredWorker;
        if (r == 1) return NPCType.Girl;
        return NPCType.SmartphonePassenger;
    }

    NPCType PickYieldNPC()
    {
        return UnityEngine.Random.value < 0.5f ? NPCType.Elderly : NPCType.Pregnant;
    }

    public void ResolveYield()
    {
        GameManager.Instance.Yield();
    }

    public void ResolveIgnore() { }

    public void ResolveSit()
    {
        GameManager.Instance.Sit();
    }

    public void ResolveStandIntentionally()
    {
        GameManager.Instance.StandIntentionally();
    }

}
