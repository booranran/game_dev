using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class EyeGameController : MonoBehaviour
{
    public static EyeGameController Instance;

    // ── Phase 1: 관찰 ─────────────────────────────────
    [Header("Phase 1 - 관찰 패널")]
    public GameObject observePanel;
    public float observeTimeLimit = 5f;
    public float phase1FailDelay = 1.5f;
    public Color hintColor = new Color(1f, 1f, 0.5f, 1f);

    [Header("Phase1 실패 독백 (다음 턴 시작 시 TurnController가 메인 화면에 표시)")]
    [TextArea] public string[] phase1FailMonologues;

    // ── Phase 2: 경쟁 ─────────────────────────────────
    [Header("Phase 2 - 경쟁 패널")]
    public GameObject competitionPanel;
    public SpriteRenderer competitorRenderer;

    [Header("Phase 2 - 타이밍 게임")]
    public float phase2Duration = 5f;
    public Color whiteColor = Color.white;
    public Color redColor = Color.red;
    public Color yellowColor = Color.yellow;

    [Header("NPC별 빨강 등장 확률")]
    public float redChanceTired = 0.4f;
    public float redChanceYoung = 0.25f;
    public float redChanceSmartphone = 0.15f;

    [Header("NPC별 빨강 구간 길이 (초)")]
    public float redDurationTired = 0.6f;
    public float redDurationYoung = 0.4f;
    public float redDurationSmartphone = 0.2f;

    [Header("호선별 경쟁 확률")]
    public float line9CompetitionRate = 0.7f;
    public float line7CompetitionRate = 0.3f;

    // ── 내부 상태 ─────────────────────────────────────
    private SeatManager.SeatSlot[] candidates;
    private int correctIndex = -1;
    private float observeTimer;
    private bool isObserving = false;
    private bool isPhase1 = false;

    private struct ColorSegment { public Color color; public float duration; public bool isRed; }
    private ColorSegment[] segments;
    private int segmentIndex;
    private float segmentTimer;
    private bool isRedNow;
    private bool isCompeting = false;
    private EventManager.NPCType currentCompetitor;

    public static event Action<bool> OnEyeGameEnd;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        if (competitorRenderer) { competitorRenderer.color = Color.white; competitorRenderer.enabled = false; }
    }

    void Update()
    {
        if (isObserving)
        {
            observeTimer -= Time.deltaTime;
            if (observeTimer <= 0f)
            {
                isObserving = false;
                ClearHints();
                observePanel.SetActive(false);
                if (isPhase1)
                    TurnController.Instance?.AdvanceTurnWithMonologue(GetPhase1FailLine());
                else
                    OnEyeGameEnd?.Invoke(false);
            }
        }

        if (debugMode && Keyboard.current != null && Keyboard.current[Key.Digit1].wasPressedThisFrame)
            DebugStartPhase2();

        if (isCompeting)
        {
            segmentTimer -= Time.deltaTime;
            if (segmentTimer <= 0f)
            {
                segmentIndex++;
                if (segmentIndex >= segments.Length)
                {
                    EndCompetition(false);
                    return;
                }
                ApplySegment(segments[segmentIndex]);
            }
        }
    }

    // ── Phase 1 시작 ──────────────────────────────────
    public void StartPhase1()
    {
        isPhase1 = true;
        candidates = SeatManager.Instance.GetCandidateSeats(5);
        if (candidates.Length < 2)
        {
            TurnController.Instance?.AdvanceTurn();
            return;
        }

        correctIndex = UnityEngine.Random.Range(0, candidates.Length);
        observeTimer = observeTimeLimit;
        isObserving = true;
        observePanel.SetActive(true);

        Debug.Log($"[EyeGame] Phase1 후보: {string.Join(", ", System.Array.ConvertAll(candidates, c => c.index.ToString()))} | 정답: {candidates[correctIndex].index}");

        HighlightCandidates();
    }

    public void StartEyeGame(EventManager.NPCType competitor)
    {
        isPhase1 = false;
        currentCompetitor = competitor;
        candidates = SeatManager.Instance.GetCandidateSeats(5);
        if (candidates.Length < 2)
        {
            OnEyeGameEnd?.Invoke(false);
            return;
        }

        correctIndex = UnityEngine.Random.Range(0, candidates.Length);
        observeTimer = observeTimeLimit;
        isObserving = true;
        observePanel.SetActive(true);

        HighlightCandidates();
    }

    // 후보 좌석들에 힌트 색 입히고, 기존 스프라이트 클릭은 끈 채로 선택 아이콘을 띄움
    void HighlightCandidates()
    {
        foreach (var seat in candidates)
        {
            var obj = (seat.seatedNPCObject != null) ? seat.seatedNPCObject : seat.silhouette;
            if (obj != null)
            {
                var sr = obj.GetComponentInChildren<SpriteRenderer>(true);
                if (sr) sr.color = hintColor;

                var col = obj.GetComponent<Collider2D>();
                if (col) col.enabled = false; // 아이콘 클릭과 겹쳐서 헷갈리지 않도록 비활성화
            }

            var icon = SeatManager.Instance.SpawnSeatIcon(seat, 1.1f); // 사람이 앉아있어서 기본 높이보다 더 올림
            if (icon != null)
            {
                var detector = icon.GetComponent<CandidateClickDetector>();
                if (detector == null) detector = icon.AddComponent<CandidateClickDetector>();
                detector.seat = seat;
            }
        }
    }

    // 이미 정해진 빈자리(SeatManager.currentEmptySeat)에 바로 경쟁 NPC를 붙일 때 사용 - 관찰 단계 없음
    public void StartCompetitionForCurrentSeat()
    {
        isPhase1 = false;
        currentCompetitor = PickCompetitor();
        StartCompetition();
    }

    public void OnSelectCandidate(int index)
    {
        if (!isObserving) return;
        isObserving = false;
        ClearHints();
        observePanel.SetActive(false);

        int correctSeatIndex = candidates[correctIndex].index;
        bool isCorrect = index == correctSeatIndex;
        Debug.Log($"[Phase1] 클릭한 자리: {index} | 정답 자리: {correctSeatIndex} | 결과: {(isCorrect ? "정답" : "오답")}");

        if (!isCorrect)
        {
            if (isPhase1)
            {
                var seat = candidates[correctIndex];
                SeatManager.Instance.FillSeatWithRandomNPC(seat);
                Invoke(nameof(AdvanceTurnDelayed), phase1FailDelay);
            }
            else
            {
                OnEyeGameEnd?.Invoke(false);
            }
            return;
        }

        if (isPhase1)
        {
            var seat = candidates[correctIndex];
            SeatManager.Instance.SetCurrentEmptySeat(seat);
            currentCompetitor = PickCompetitor();
            StartCompetition();
        }
        else
        {
            float rate = GameManager.Instance.lineType == GameManager.LineType.Line9
                ? line9CompetitionRate : line7CompetitionRate;
            if (UnityEngine.Random.value < rate)
                StartCompetition();
            else
                OnEyeGameEnd?.Invoke(true);
        }
    }

    public void OnStandDuringPhase1()
    {
        if (!isObserving || !isPhase1) return;
        isObserving = false;
        ClearHints();
        observePanel.SetActive(false);

        GameManager.Instance.StandIntentionally();

        var seat = candidates[correctIndex];
        SeatManager.Instance.FillSeatWithRandomNPC(seat);
        Invoke(nameof(AdvanceTurnDelayed), phase1FailDelay);
    }

    // ── Phase 2 경쟁 ──────────────────────────────────
    void StartCompetition()
    {
        segments = GenerateSegments(currentCompetitor);
        segmentIndex = 0;
        isCompeting = true;
        Debug.Log($"[Phase2] 시작 | NPC: {currentCompetitor} | 세그먼트 수: {segments.Length} | competitionPanel: {competitionPanel != null} | competitorRenderer: {competitorRenderer != null}");
        competitionPanel.SetActive(true);
        if (competitorRenderer) competitorRenderer.enabled = true;
        Debug.Log($"[Phase2] competitionPanel 활성화: {competitionPanel.activeSelf}");
        ApplySegment(segments[0]);
    }

    void ApplySegment(ColorSegment seg)
    {
        segmentTimer = seg.duration;
        isRedNow = seg.isRed;
        Debug.Log($"[Phase2] 세그먼트 {segmentIndex}: 색={seg.color} isRed={seg.isRed} 길이={seg.duration:F2}s");
        if (competitorRenderer) competitorRenderer.color = seg.color;
        else Debug.LogWarning("[Phase2] competitorRenderer가 null!");
    }

    ColorSegment[] GenerateSegments(EventManager.NPCType npcType)
    {
        float redChance = npcType == EventManager.NPCType.TiredWorker ? redChanceTired
                        : npcType == EventManager.NPCType.YoungPassenger ? redChanceYoung
                        : redChanceSmartphone;
        float redDur = npcType == EventManager.NPCType.TiredWorker ? redDurationTired
                     : npcType == EventManager.NPCType.YoungPassenger ? redDurationYoung
                     : redDurationSmartphone;

        var list = new System.Collections.Generic.List<ColorSegment>();
        float remaining = phase2Duration;
        bool lastWasRed = false;

        while (remaining > 0.05f)
        {
            bool makeRed = !lastWasRed && UnityEngine.Random.value < redChance;
            Color c;
            float dur;

            if (makeRed)
            {
                c = redColor;
                dur = Mathf.Min(UnityEngine.Random.Range(redDur * 0.7f, redDur * 1.3f), remaining);
                lastWasRed = true;
            }
            else
            {
                c = UnityEngine.Random.value < 0.5f ? whiteColor : yellowColor;
                dur = Mathf.Min(UnityEngine.Random.Range(0.4f, 2f), remaining);
                lastWasRed = false;
            }

            list.Add(new ColorSegment { color = c, duration = dur, isRed = makeRed });
            remaining -= dur;
        }

        return list.ToArray();
    }

    public void OnSitButton()
    {
        if (!isCompeting) return;
        if (isRedNow)
        {
            Debug.Log($"[Phase2] 앉기 성공 (빨강 타이밍) | 턴: {GameManager.Instance.currentTurn}");
            EndCompetition(true);
        }
        else
        {
            Debug.Log($"[Phase2] 앉기 실패 (잘못된 타이밍: isRed={isRedNow}) | 턴: {GameManager.Instance.currentTurn}");
            GameManager.Instance.ChangeCondition(-1);
            EndCompetition(false);
        }
    }

    public void OnYieldButton()
    {
        if (!isCompeting) return;
        Debug.Log($"[Phase2] 양보 | 턴: {GameManager.Instance.currentTurn}");
        isCompeting = false;
        competitionPanel.SetActive(false);
        SpawnSeatedCompetitor();
        ResetCompetitorRenderer();
        GameManager.Instance.ChangeCondition(-1);
        OnEyeGameEnd?.Invoke(false);
    }

    void EndCompetition(bool playerWon)
    {
        isCompeting = false;
        competitionPanel.SetActive(false);
        if (!playerWon) SpawnSeatedCompetitor();
        ResetCompetitorRenderer();
        Debug.Log($"[Phase2] 종료 → {(playerWon ? "플레이어 승리" : "NPC 승리")} | OnEyeGameEnd 발생 → TurnController.OnEyeGameEnd 호출 예정");
        OnEyeGameEnd?.Invoke(playerWon);
    }

    void SpawnSeatedCompetitor()
    {
        var seat = SeatManager.Instance.currentEmptySeat;
        if (seat == null || seat.seatPosition == null) return;
        if (competitorRenderer == null || competitorRenderer.sprite == null) return;

        var go = new GameObject("SeatedCompetitor");
        go.transform.position = seat.seatPosition.position;
        go.transform.localScale = Vector3.one * 0.09f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = competitorRenderer.sprite;
        sr.sortingOrder = -29;
        seat.seatedNPCObject = go;
    }

    // ── 디버그 ────────────────────────────────────────
    [Header("디버그")]
    public bool debugMode = false;
    public EventManager.NPCType debugCompetitor = EventManager.NPCType.TiredWorker;
    public int debugSeatIndex = 0;

    [ContextMenu("테스트: Phase 2 시작")]
    public void DebugStartPhase2()
    {
        Debug.Log("[Debug] Phase2 디버그 시작");

        isObserving = false;
        isCompeting = false;
        CancelInvoke();

        TurnController.Instance?.HideAllPanelsPublic();
        if (observePanel) observePanel.SetActive(false);

        var seats = SeatManager.Instance.seats;
        int idx = Mathf.Clamp(debugSeatIndex, 0, seats.Length - 1);
        SeatManager.Instance.SetCurrentEmptySeat(seats[idx]);
        Debug.Log($"[Debug] 지정 좌석: {idx}, currentEmptySeat: {SeatManager.Instance.currentEmptySeat?.silhouette?.name}");

        currentCompetitor = debugCompetitor;
        StartCompetition();
    }

    // ── 공통 유틸 ─────────────────────────────────────
    void ClearHints()
    {
        if (candidates == null) return;
        foreach (var seat in candidates)
        {
            var obj = (seat.seatedNPCObject != null) ? seat.seatedNPCObject : seat.silhouette;
            if (obj != null)
            {
                var sr = obj.GetComponentInChildren<SpriteRenderer>(true);
                if (sr) sr.color = Color.white;

                var col = obj.GetComponent<Collider2D>();
                if (col) col.enabled = true;
            }
            SeatManager.Instance.RemoveSeatIcon(seat);
        }
    }

    void ResetCompetitorRenderer()
    {
        if (!competitorRenderer) return;
        competitorRenderer.color = Color.white;
        competitorRenderer.enabled = false;
    }

    void AdvanceTurnDelayed() => TurnController.Instance?.AdvanceTurnWithMonologue(GetPhase1FailLine());

    public string GetPhase1FailLine() => PickLine(phase1FailMonologues);

    string PickLine(string[] lines)
    {
        if (lines == null || lines.Length == 0) return "";
        return lines[UnityEngine.Random.Range(0, lines.Length)];
    }

    EventManager.NPCType PickCompetitor()
    {
        int r = UnityEngine.Random.Range(0, 3);
        if (r == 0) return EventManager.NPCType.TiredWorker;
        if (r == 1) return EventManager.NPCType.YoungPassenger;
        return EventManager.NPCType.SmartphonePassenger;
    }
}
