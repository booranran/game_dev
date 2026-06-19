using UnityEngine;
using TMPro;

public class TurnController : MonoBehaviour
{
    public static TurnController Instance;

    [Header("Event Panels")]
    public GameObject yieldPanel;
    public GameObject emptySeatPanel;
    public GameObject noEventPanel;

    [Header("빈자리 이벤트 설정")]
    [Range(0f, 1f)] public float phase1Chance = 0.5f;
    [Range(0f, 1f)] public float seatCompetitionChance = 0.3f;
    [Range(0f, 1f)] public float extraSeatChance = 0.3f; // 빈자리가 추가로 하나 더 열릴 확률 (누적 적용)
    public int maxSeatsPerOpen = 3;

    [Header("TriggerNPC 설정")]
    public float triggerPanelDelay = 1.5f;

    [Header("미니게임 독백 (턴 전환 분리용)")]
    public float preMinigameMonologueDelay = 1.5f;
    public float postMinigameMonologueDelay = 1.5f;

    [Header("References")]
    public HUDManager hudManager;
    public CharacterDisplay characterDisplay;
    public TextMeshProUGUI characterMonologueText;
    public ResultPanelController resultPanelController;

    private System.Action pendingMinigameStart;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        GameManager.OnTurnProcessed += OnTurnStart;
        GameManager.OnGameOver += GoToResult;
        GameManager.OnGameEnd += GoToResult;
        GameManager.OnGameInitialized += OnGameStart;
        EventManager.OnEventGenerated += ShowEventUI;
        EyeGameController.OnEyeGameEnd += OnEyeGameEnd;
        ElbowGameController.OnElbowGameEnd += OnElbowGameResult;
        BagDefenseController.OnBagGameEnd += OnBagGameResult;
    }

    void OnDisable()
    {
        GameManager.OnTurnProcessed -= OnTurnStart;
        GameManager.OnGameOver -= GoToResult;
        GameManager.OnGameEnd -= GoToResult;
        GameManager.OnGameInitialized -= OnGameStart;
        EventManager.OnEventGenerated -= ShowEventUI;
        EyeGameController.OnEyeGameEnd -= OnEyeGameEnd;
        ElbowGameController.OnElbowGameEnd -= OnElbowGameResult;
        BagDefenseController.OnBagGameEnd -= OnBagGameResult;
    }

    void OnGameStart()
    {
        // OnGameInitialized 구독 순서는 보장 안 되니, 여기서 직접 한번 더 갱신해서
        // 플레이어 위치가 확정된 후에 NPC 배치/보딩이 일어나게 함
        characterDisplay.UpdateSprite();
        HideAllPanels();
        BoardingController.Instance?.RollBoarding();
        EventManager.Instance.GenerateEvent();
        BoardingController.Instance?.PlaceBoardingNPCs();
    }

    void OnTurnStart()
    {
        // CharacterDisplay도 같은 OnTurnProcessed를 구독하고 있어서 순서가 보장 안 됨 - 직접 먼저 갱신
        characterDisplay.UpdateSprite();
        if (SeatManager.Instance.openSeats.Count > 0)
            Debug.Log($"[TurnController] OnTurnStart 시점 openSeats에 {SeatManager.Instance.openSeats.Count}개 남아있음 → 자동 채움 (선택 안 됐던 자리)");
        SeatManager.Instance.FillAllOpenSeats(); // 전 턴에 선택 안 된 빈자리들 다른 NPC로 채움
        HideAllPanels();
        BoardingController.Instance?.RollBoarding();
        if (SeatManager.Instance.currentEmptySeat == null)
            EventManager.Instance.GenerateEvent();
        else
            emptySeatPanel.SetActive(true);
        BoardingController.Instance?.PlaceBoardingNPCs();
    }

    void ShowEventUI(EventManager.EventType eventType, EventManager.NPCType npcType)
    {
        HideAllPanels();
        switch (eventType)
        {
            case EventManager.EventType.YieldNPC:
                yieldPanel.SetActive(true);
                break;
            case EventManager.EventType.EmptySeat:
                HandleEmptySeat();
                break;
            case EventManager.EventType.TriggerNPC:
                TriggerEventController.Instance.PickRandom();
                Invoke(nameof(ShowTriggerPanel), triggerPanelDelay);
                break;
            case EventManager.EventType.ElbowGame:
                ShowPreMinigameMonologue(ElbowGameController.Instance.GetStartLine(), ElbowGameController.Instance.StartElbowGame);
                break;
            case EventManager.EventType.BagDefense:
                ShowPreMinigameMonologue(BagDefenseController.Instance.GetStartLine(), BagDefenseController.Instance.StartBagGame);
                break;
            case EventManager.EventType.None:
                noEventPanel.SetActive(true);
                Invoke(nameof(AdvanceTurn), 1.5f);
                break;
        }
    }

    // 양보 패널 버튼
    public void OnYieldButton()
    {
        HideAllPanels(); // 바로 숨겨서 AdvanceTurn 전까지 중복 클릭으로 다시 실행되는 것 방지
        var yieldedSeat = SeatManager.Instance.playerCurrentSeat;
        NPCManager.Instance.ShowNPC(EventManager.Instance.currentNPC, true);
        EventManager.Instance.ResolveYield();
        if (yieldedSeat?.standingInFrontPosition != null)
            characterDisplay.SetStandingOverride(yieldedSeat.standingInFrontPosition.position);
        characterDisplay.UpdateSprite();
        hudManager.UpdateHUD();
        Invoke(nameof(AdvanceTurn), 1f);
    }

    public void OnIgnoreButton()
    {
        GameManager.Instance.comboCount = 0;
        EventManager.Instance.ResolveIgnore();
        HideAllPanels();

        var npc = EventManager.Instance.currentNPC;
        if (npc == EventManager.NPCType.Elderly)
        {
            if (characterMonologueText) characterMonologueText.text = "양보하지 않아 죄책감이 느껴졌다.";
            noEventPanel.SetActive(true);
        }

        Invoke(nameof(ClearMonologueAndAdvance), 1f);
    }

    // 빈자리 위 아이콘 클릭 시 호출 - 그 자리에 앉으려고 시도 (나머지 빈자리는 openSeats에 남아 다음 턴에 채워짐)
    public void OnOpenSeatSelected(SeatManager.SeatSlot seat)
    {
        Debug.Log($"[TurnController] OnOpenSeatSelected 호출 → index {seat.index}");
        SeatManager.Instance.SelectOpenSeat(seat);
        SeatManager.Instance.DeactivateAllOpenSeatIcons(); // 남은 빈자리 아이콘도 중복 선택 못 하게 정리
        emptySeatPanel.SetActive(false);
        AttemptSit();
    }

    void AttemptSit()
    {
        if (UnityEngine.Random.value < seatCompetitionChance)
        {
            Debug.Log("[TurnController] 빈자리 경쟁 발생 → Phase2");
            EyeGameController.Instance.StartCompetitionForCurrentSeat();
            return;
        }

        EventManager.Instance.ResolveSit();
        SeatManager.Instance.OccupySeat();
        characterDisplay.UpdateSprite();
        hudManager.UpdateHUD();
        Invoke(nameof(AdvanceTurn), 0.5f);
    }

    // 빈자리 패널의 "서있기" 버튼 - 열려있던 빈자리들 전부 다른 NPC로 즉시 채움
    public void OnStandIntentionallyButton()
    {
        Debug.Log("[TurnController] OnStandIntentionallyButton 호출");
        HideAllPanels(); // 바로 숨겨서 AdvanceTurn 전까지 중복 클릭으로 다시 실행되는 것 방지
        SeatManager.Instance.FillAllOpenSeats();
        EventManager.Instance.ResolveStandIntentionally();
        hudManager.UpdateHUD();
        Invoke(nameof(AdvanceTurn), 0.5f);
    }

    void OnEyeGameEnd(bool playerWon)
    {
        Debug.Log($"[TurnController] OnEyeGameEnd | 결과: {(playerWon ? "플레이어 승리" : "NPC 승리")} | 현재 턴: {GameManager.Instance.currentTurn}");
        if (playerWon)
        {
            EventManager.Instance.ResolveSit();
            SeatManager.Instance.OccupySeat();
            characterDisplay.UpdateSprite();
        }
        else
        {
            SeatManager.Instance.NPCOccupySeat();
        }
        hudManager.UpdateHUD();
        Debug.Log($"[TurnController] AdvanceTurn 호출 → 다음 턴으로");
        AdvanceTurn();
    }

    void HandleEmptySeat()
    {
        if (UnityEngine.Random.value < phase1Chance)
        {
            Debug.Log("[TurnController] Phase 1 시작 (누가 내릴지)");
            EyeGameController.Instance.StartPhase1();
            return;
        }

        int count = RollSeatOpenCount();
        SeatManager.Instance.OpenSeats(count);
        emptySeatPanel.SetActive(true);
        Debug.Log($"[TurnController] 빈자리 {count}개 오픈 → 좌석 선택 또는 서있기");
    }

    int RollSeatOpenCount()
    {
        int count = 1;
        while (count < maxSeatsPerOpen && UnityEngine.Random.value < extraSeatChance)
            count++;
        return count;
    }

    bool IsCompetingNPC(EventManager.NPCType npc)
    {
        return npc == EventManager.NPCType.TiredWorker ||
               npc == EventManager.NPCType.YoungPassenger ||
               npc == EventManager.NPCType.SmartphonePassenger;
    }

    void ClearMonologueAndAdvance()
    {
        if (characterMonologueText) characterMonologueText.text = "";
        AdvanceTurn();
    }

    void ShowTriggerPanel() => TriggerEventController.Instance.ShowPanel();

    // 미니게임 패널이 뜨기 전, 메인 화면에서 캐릭터 독백 먼저 보여주고 그 다음에 실제로 게임 시작
    void ShowPreMinigameMonologue(string line, System.Action startAction)
    {
        if (characterMonologueText) characterMonologueText.text = line;
        if (noEventPanel) noEventPanel.SetActive(true); // characterMonologueText가 이 패널의 자식이라 같이 켜야 보임
        pendingMinigameStart = startAction;
        Invoke(nameof(RunPendingMinigameStart), preMinigameMonologueDelay);
    }

    void RunPendingMinigameStart()
    {
        if (characterMonologueText) characterMonologueText.text = "";
        if (noEventPanel) noEventPanel.SetActive(false);
        pendingMinigameStart?.Invoke();
        pendingMinigameStart = null;
    }

    // 미니게임 패널이 닫힌 후, 메인 화면에서 캐릭터 독백 보여주고 그 다음에야 턴 진행
    void ShowPostMinigameMonologue(string line)
    {
        if (characterMonologueText) characterMonologueText.text = line;
        if (noEventPanel) noEventPanel.SetActive(true); // characterMonologueText가 이 패널의 자식이라 같이 켜야 보임
        Invoke(nameof(ClearMonologueAndAdvance), postMinigameMonologueDelay);
    }

    void OnElbowGameResult(bool playerWon)
    {
        GameManager.Instance.ChangeCondition(playerWon ? 1 : -1);
        hudManager.UpdateHUD();
        ShowPostMinigameMonologue(ElbowGameController.Instance.GetEndLine(playerWon));
    }

    void OnBagGameResult(int conditionDelta, int healthDamage)
    {
        // 가방방어는 체력만 담당(컨디션은 안 건드림) - 팔꿈치 게임이 컨디션 전담
        float multiplier = GameManager.Instance.GetConditionDamageMultiplier();
        int scaledDamage = Mathf.RoundToInt(healthDamage * multiplier);

        GameManager.Instance.ChangeHealth(scaledDamage);
        hudManager.UpdateHUD();
        ShowPostMinigameMonologue(BagDefenseController.Instance.GetEndLine(conditionDelta >= 0));
    }

    public void OnTriggerContinueButton()
    {
        TriggerEventController.Instance.ResolveCurrent();
        hudManager.UpdateHUD();
        AdvanceTurn();
    }

    public void AdvanceTurn()
    {
        HideAllPanels();
        GameManager.Instance.ProcessTurn();
        hudManager.UpdateHUD();
    }

    public void HideAllPanelsPublic() => HideAllPanels();

    void HideAllPanels()
    {
        if (yieldPanel)     yieldPanel.SetActive(false);
        if (emptySeatPanel) emptySeatPanel.SetActive(false);
        if (noEventPanel)   noEventPanel.SetActive(false);
    }

    void GoToResult() => resultPanelController.Show(GameManager.Instance.characterType, GameManager.EndingType.GameOver);
    void GoToResult(GameManager.EndingType ending) => resultPanelController.Show(GameManager.Instance.characterType, ending);
}
