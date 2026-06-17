using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class TurnController : MonoBehaviour
{
    public static TurnController Instance;

    [Header("Event Panels")]
    public GameObject yieldPanel;
    public GameObject emptySeatPanel;
    public GameObject triggerPanel;
    public GameObject noEventPanel;

    [Header("빈자리 이벤트 설정")]
    [Range(0f, 1f)] public float phase1Chance = 0.5f;

    [Header("TriggerNPC 설정")]
    public float triggerPanelDelay = 1.5f;

    [Header("References")]
    public HUDManager hudManager;
    public CharacterDisplay characterDisplay;
    public TextMeshProUGUI characterMonologueText;

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
        EventManager.OnEventGenerated += ShowEventUI;
        EyeGameController.OnEyeGameEnd += OnEyeGameEnd;
        ElbowGameController.OnElbowGameEnd += OnElbowGameResult;
    }

    void OnDisable()
    {
        GameManager.OnTurnProcessed -= OnTurnStart;
        GameManager.OnGameOver -= GoToResult;
        GameManager.OnGameEnd -= GoToResult;
        EventManager.OnEventGenerated -= ShowEventUI;
        EyeGameController.OnEyeGameEnd -= OnEyeGameEnd;
        ElbowGameController.OnElbowGameEnd -= OnElbowGameResult;
    }

    void Start()
    {
        HideAllPanels();
        EventManager.Instance.GenerateEvent();
    }

    void OnTurnStart()
    {
        HideAllPanels();
        if (SeatManager.Instance.currentEmptySeat != null)
        {
            emptySeatPanel.SetActive(true);
            return;
        }
        EventManager.Instance.GenerateEvent();
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
                Invoke(nameof(ShowTriggerPanel), triggerPanelDelay);
                break;
            case EventManager.EventType.ElbowGame:
                ElbowGameController.Instance.StartElbowGame();
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

    // 빈자리 패널 버튼
    public void OnSitButton()
    {
        EventManager.Instance.ResolveSit();
        SeatManager.Instance.OccupySeat();
        characterDisplay.UpdateSprite();
        hudManager.UpdateHUD();
        Invoke(nameof(AdvanceTurn), 0.5f);
    }

    public void OnStandIntentionallyButton()
    {
        SeatManager.Instance.RestoreCurrentEmptySeat();
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
        int emptyCount = SeatManager.Instance.GetCurrentEmptySeatCount();

        if (emptyCount == 0)
        {
            if (UnityEngine.Random.value < phase1Chance)
            {
                Debug.Log("[TurnController] Phase 1 시작 (누가 내릴지)");
                EyeGameController.Instance.StartPhase1();
            }
            else
            {
                Debug.Log("[TurnController] 랜덤 빈자리 오픈");
                SeatManager.Instance.OpenRandomSeat(false);
                emptySeatPanel.SetActive(true);
            }
        }
        else if (emptyCount == 1)
        {
            Debug.Log("[TurnController] 빈자리 1개 → 앉기/서있기 선택");
            emptySeatPanel.SetActive(true);
        }
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

    void ShowTriggerPanel() => triggerPanel.SetActive(true);

    void OnElbowGameResult(bool playerWon)
    {
        GameManager.Instance.ChangeCondition(playerWon ? 1 : -1);
        hudManager.UpdateHUD();
        AdvanceTurn();
    }

    public void OnTriggerContinueButton()
    {
        EventManager.Instance.ResolveTriggerNPC();
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
        if (triggerPanel)   triggerPanel.SetActive(false);
        if (noEventPanel)   noEventPanel.SetActive(false);
    }

    void GoToResult() => SceneManager.LoadScene("ResultScene");
    void GoToResult(GameManager.EndingType _) => SceneManager.LoadScene("ResultScene");
}
