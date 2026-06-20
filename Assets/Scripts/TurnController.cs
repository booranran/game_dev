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
    public GameObject characterMonologueText; // Image 박스 (보이기/숨기기용)
    public TextMeshProUGUI characterMonologueTMP; // 위 박스의 자식 TMP (실제 텍스트)
    public ResultPanelController resultPanelController;

    private System.Action pendingMinigameStart;
    private string pendingTurnStartMonologue;
    private EventManager.NPCType? pendingSeatedNPCType;
    private SeatManager.SeatSlot pendingSeatedNPCSeat;
    private SeatManager.SeatSlot pendingNormalizeSeat; // 눈치게임 승리로 막 앉은 NPC - 다음 턴에 정착 포즈로 교체
    private EventManager.NPCType pendingNormalizeNPCType; // 위 좌석에 앉은 NPC가 어떤 타입인지 (정착 포즈 조회용)

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

        // 양보 다음 턴이면 - 그제서야 실제로 좌석에 앉히기 (서있는 미리보기 정리 후)
        if (pendingSeatedNPCType.HasValue)
        {
            NPCManager.Instance.HideNPC();
            NPCManager.Instance.SeatNPCAt(pendingSeatedNPCType.Value, pendingSeatedNPCSeat);
            pendingSeatedNPCType = null;
            pendingSeatedNPCSeat = null;
        }

        // 눈치게임에서 진 다음 턴이면 - 막 앉은 포즈에서 평소 정착 포즈로 교체
        if (pendingNormalizeSeat != null)
        {
            var npcObj = pendingNormalizeSeat.seatedNPCObject;
            if (npcObj != null) // Unity fake null 주의 - ?. 대신 명시적 체크
            {
                var sr = npcObj.GetComponent<SpriteRenderer>();
                var seatedSprite = EyeGameController.Instance.GetSeatedSprite(pendingNormalizeNPCType);
                if (sr != null && seatedSprite != null) sr.sprite = seatedSprite;
            }
            pendingNormalizeSeat = null;
        }

        // 이전 턴에 (Phase1 실패 등) 다음 턴 시작 시 보여줄 독백이 예약돼있으면, 그것부터 보여주고 나머지는 미룸
        if (!string.IsNullOrEmpty(pendingTurnStartMonologue))
        {
            string line = pendingTurnStartMonologue;
            pendingTurnStartMonologue = null;
            HideAllPanels();
            if (characterMonologueTMP) characterMonologueTMP.text = line;
            if (characterMonologueText) characterMonologueText.SetActive(true);
            if (noEventPanel) noEventPanel.SetActive(true);
            Invoke(nameof(ContinueTurnStart), postMinigameMonologueDelay);
            return;
        }

        ContinueTurnStart();
    }

    void ContinueTurnStart()
    {
        if (characterMonologueTMP) characterMonologueTMP.text = "";
        if (characterMonologueText) characterMonologueText.SetActive(false);
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

    // Phase1 실패 등 - 지금 당장이 아니라 "다음 턴이 시작될 때" 독백을 보여주고 싶을 때 사용
    public void AdvanceTurnWithMonologue(string monologue)
    {
        pendingTurnStartMonologue = monologue;
        AdvanceTurn();
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
                characterDisplay.UpdateSprite(CharacterDisplay.PoseOverride.ElbowDefense);
                ShowPreMinigameMonologue(ElbowGameController.Instance.GetStartLine(), ElbowGameController.Instance.StartElbowGame);
                break;
            case EventManager.EventType.BagDefense:
                BoardingController.Instance?.TriggerBoardingSurge(); // 독백 보여주는 동안 탑승객 확 늘어나는 연출
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
        pendingSeatedNPCType = EventManager.Instance.currentNPC; // 다음 턴 시작할 때 이 자리에 앉힘
        pendingSeatedNPCSeat = yieldedSeat;
        NPCManager.Instance.ShowThankYouDialogue(EventManager.Instance.currentNPC); // NPC는 이번 턴엔 서있는 채 대사만 갱신
        EventManager.Instance.ResolveYield();
        if (yieldedSeat?.standingInFrontPosition != null)
            characterDisplay.SetStandingOverride(yieldedSeat.standingInFrontPosition.position);
        characterDisplay.UpdateSprite(CharacterDisplay.PoseOverride.Yielding);
        hudManager.UpdateHUD();
        Invoke(nameof(AdvanceTurn), 1f);
    }

    public void OnIgnoreButton()
    {
        GameManager.Instance.comboCount = 0;
        EventManager.Instance.ResolveIgnore();
        HideAllPanels();

        var npc = EventManager.Instance.currentNPC;
        string ignoreMonologue = null;
        switch (npc)
        {
            case EventManager.NPCType.Elderly: ignoreMonologue = "양보하지 않아 죄책감이 느껴졌다."; break;
            case EventManager.NPCType.Pregnant: ignoreMonologue = "임산부에게 자리를 양보하지 않아 마음이 무거웠다."; break;
        }
        if (ignoreMonologue != null)
        {
            if (characterMonologueTMP) characterMonologueTMP.text = ignoreMonologue;
            if (characterMonologueText) characterMonologueText.SetActive(true);
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
            characterDisplay.UpdateSprite(CharacterDisplay.PoseOverride.JustSat); // 다음 턴 OnTurnStart()에서 자동으로 평소 포즈로 복귀
        }
        else
        {
            pendingNormalizeSeat = SeatManager.Instance.currentEmptySeat; // NPCOccupySeat()가 null로 비우기 전에 미리 캡쳐
            pendingNormalizeNPCType = EyeGameController.Instance.CurrentCompetitor;
            SeatManager.Instance.NPCOccupySeat();
        }
        hudManager.UpdateHUD();
        ShowPostMinigameMonologue(EyeGameController.Instance.GetEndLine(playerWon));
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
               npc == EventManager.NPCType.Girl ||
               npc == EventManager.NPCType.SmartphonePassenger;
    }

    void ClearMonologueAndAdvance()
    {
        if (characterMonologueTMP) characterMonologueTMP.text = "";
        if (characterMonologueText) characterMonologueText.SetActive(false);
        AdvanceTurn();
    }

    void ShowTriggerPanel() => TriggerEventController.Instance.ShowPanel();

    // 미니게임 패널이 뜨기 전, 메인 화면에서 캐릭터 독백 먼저 보여주고 그 다음에 실제로 게임 시작
    void ShowPreMinigameMonologue(string line, System.Action startAction)
    {
        if (characterMonologueTMP) characterMonologueTMP.text = line;
        if (characterMonologueText) characterMonologueText.SetActive(true);
        if (noEventPanel) noEventPanel.SetActive(true);
        pendingMinigameStart = startAction;
        Invoke(nameof(RunPendingMinigameStart), preMinigameMonologueDelay);
    }

    void RunPendingMinigameStart()
    {
        if (characterMonologueTMP) characterMonologueTMP.text = "";
        if (characterMonologueText) characterMonologueText.SetActive(false);
        if (noEventPanel) noEventPanel.SetActive(false);
        pendingMinigameStart?.Invoke();
        pendingMinigameStart = null;
    }

    // 미니게임 패널이 닫힌 후, 메인 화면에서 캐릭터 독백 보여주고 그 다음에야 턴 진행
    void ShowPostMinigameMonologue(string line)
    {
        if (characterMonologueTMP) characterMonologueTMP.text = line;
        if (characterMonologueText) characterMonologueText.SetActive(true);
        if (noEventPanel) noEventPanel.SetActive(true);
        Invoke(nameof(ClearMonologueAndAdvance), postMinigameMonologueDelay);
    }

    void OnElbowGameResult(bool playerWon)
    {
        GameManager.Instance.ChangeCondition(playerWon ? 1 : -1);
        characterDisplay.UpdateSprite(); // 기존 앉은 스프라이트로 복귀
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
        if (yieldPanel)            yieldPanel.SetActive(false);
        if (emptySeatPanel)        emptySeatPanel.SetActive(false);
        if (noEventPanel)          noEventPanel.SetActive(false);
        if (characterMonologueText) characterMonologueText.SetActive(false);
    }

    void GoToResult() => resultPanelController.Show(GameManager.Instance.characterType, GameManager.EndingType.GameOver);
    void GoToResult(GameManager.EndingType ending) => resultPanelController.Show(GameManager.Instance.characterType, ending);
}
