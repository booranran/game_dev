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

    [Header("YieldNPC 가중치 (호선별 - 7호선은 배려점수 얻을 기회 자체를 줄여서 배려점수 관리를 어렵게)")]
    public int yieldNPCWeightLine9 = 4;
    public int yieldNPCWeightLine7 = 2;

    [Header("EmptySeat 가중치 (호선별 - 9호선은 한번 일어나면 다시 앉기 어렵게 해서 체력 관리를 어렵게)")]
    public int emptySeatWeightLine9 = 1;
    public int emptySeatWeightLine7 = 3;

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

    [Header("BagDefense 설정 (보딩 연출이 있을 때만 등장 - 호선별 가중치/최대 발생횟수)")]
    [Tooltip("가방 방어 게임이 등장하기 시작하는 턴")]
    public int bagGameStartTurn = 5;
    public int bagDefenseWeightLine9 = 2;
    public int bagDefenseWeightLine7 = 2;
    [Tooltip("게임 전체에서 BagDefense가 최대 몇 번까지 발생할지 (호선별) - 9호선은 체력관리가 핵심이라 더 자주 발생")]
    public int maxBagDefenseLine9 = 4;
    public int maxBagDefenseLine7 = 3;

    [Header("미니게임 쿨다운 (각자 따로 적용)")]
    [Tooltip("ElbowGame이 발생하면, 이후 최소 이만큼 턴이 지나야 ElbowGame이 다시 발생함")]
    public int elbowGameCooldownTurns = 2;
    [Tooltip("BagDefense가 발생하면, 이후 최소 이만큼 턴이 지나야 BagDefense가 다시 발생함")]
    public int bagDefenseCooldownTurns = 2;

    private int emptySeatCount = 0;
    private int yieldNPCCount = 0;
    private int bagDefenseCount = 0;
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
            bagDefenseCount++;
            int maxBagDefense = gm.lineType == GameManager.LineType.Line9 ? maxBagDefenseLine9 : maxBagDefenseLine7;
            Debug.Log($"[EventManager] BagDefense 발생 ({bagDefenseCount}/{maxBagDefense})");
        }

        OnEventGenerated?.Invoke(currentEvent, currentNPC);
    }

    EventType PickEventType(GameManager.PlayerState state)
    {
        List<EventType> pool = new List<EventType>();
        bool elbowOnCooldown = GameManager.Instance.currentTurn - lastElbowGameTurn < elbowGameCooldownTurns;
        bool bagOnCooldown = GameManager.Instance.currentTurn - lastBagDefenseTurn < bagDefenseCooldownTurns;

        bool isLine9 = GameManager.Instance.lineType == GameManager.LineType.Line9;

        if (state == GameManager.PlayerState.Sitting)
        {
            // 노약자석/임산부석이 둘 다 비어있으면 그 타입들이 굳이 플레이어에게 양보를 구할 이유가 없음 - 둘 다 자기 전용석이 있는데 비어있다면 이벤트 자체를 배제
            if (yieldNPCCount < maxYieldNPCs && (ElderlyYieldEligible() || PregnantYieldEligible()))
            {
                int yieldWeight = isLine9 ? yieldNPCWeightLine9 : yieldNPCWeightLine7;
                for (int i = 0; i < yieldWeight; i++) pool.Add(EventType.YieldNPC);
            }
            if (!elbowOnCooldown)
                for (int i = 0; i < elbowGameWeight; i++) pool.Add(EventType.ElbowGame);
            for (int i = 0; i < noneWeightSitting; i++) pool.Add(EventType.None);
        }
        else
        {
            if (emptySeatCount < maxEmptySeats)
            {
                int emptySeatWeight = isLine9 ? emptySeatWeightLine9 : emptySeatWeightLine7;
                for (int i = 0; i < emptySeatWeight; i++) pool.Add(EventType.EmptySeat);
            }
            int maxBagDefense = isLine9 ? maxBagDefenseLine9 : maxBagDefenseLine7;
            if (!bagOnCooldown && bagDefenseCount < maxBagDefense && GameManager.Instance.currentTurn >= bagGameStartTurn
                && BoardingController.Instance != null && BoardingController.Instance.CurrentBoardingCount > 0)
            {
                int bagWeight = isLine9 ? bagDefenseWeightLine9 : bagDefenseWeightLine7;
                for (int i = 0; i < bagWeight; i++) pool.Add(EventType.BagDefense);
            }
            for (int i = 0; i < noneWeightStanding; i++) pool.Add(EventType.None);
        }

        if (TriggerEventController.Instance != null && TriggerEventController.Instance.HasAvailableEvent())
            for (int i = 0; i < triggerNPCWeight; i++) pool.Add(EventType.TriggerNPC);

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }

    // 해당 타입 전용석이 비어있으면 그 타입은 거기 가서 앉으면 되니 플레이어에게 양보를 구할 이유가 없음 - 전용석이 없거나(미설정) 다 차있을 때만 후보
    bool ElderlyYieldEligible() =>
        !SeatManager.Instance.HasSeatType(SeatManager.SeatType.ElderlySeat) ||
        SeatManager.Instance.IsSpecialSeatTypeFull(SeatManager.SeatType.ElderlySeat);

    bool PregnantYieldEligible() =>
        !SeatManager.Instance.HasSeatType(SeatManager.SeatType.PregnantSeat) ||
        SeatManager.Instance.IsSpecialSeatTypeFull(SeatManager.SeatType.PregnantSeat);

    NPCType PickYieldNPC()
    {
        bool elderlyEligible = ElderlyYieldEligible();
        bool pregnantEligible = PregnantYieldEligible();

        if (elderlyEligible && pregnantEligible)
            return UnityEngine.Random.value < 0.5f ? NPCType.Elderly : NPCType.Pregnant;
        return elderlyEligible ? NPCType.Elderly : NPCType.Pregnant;
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
