using UnityEngine;
using System;

public class EyeGameController : MonoBehaviour
{
    public static EyeGameController Instance;

    [Header("참조")]
    public CharacterDisplay characterDisplay; // Phase1/Phase2 중 플레이어 포즈 표시용

    // ── Phase 1: 관찰 ─────────────────────────────────
    [Header("Phase 1 - 관찰 패널")]
    public GameObject observePanel;
    public float observeTimeLimit = 5f;
    public float phase1FailDelay = 1.5f;

    [Header("Phase1 실패 독백 (다음 턴 시작 시 TurnController가 메인 화면에 표시)")]
    [TextArea] public string[] phase1FailMonologues;

    [Header("눈치게임(Phase2) 승패 독백 (TurnController가 메인 화면에 표시 - 턴 전환 분리용)")]
    [TextArea] public string[] winMonologues;
    [TextArea] public string[] loseMonologues;

    public string GetEndLine(bool won) => PickLine(won ? winMonologues : loseMonologues);

    // ── Phase 2: 경쟁 ─────────────────────────────────
    [Header("Phase 2 - 경쟁 패널")]
    public GameObject competitionPanel;
    public SpriteRenderer competitorRenderer;
    public int competitorSortingOrder = 2; // 좌석 실루엣(-29)/앉은 NPC보다 항상 앞에 보이게

    [Header("경쟁 NPC 위치 (플레이어 기준 오프셋 - NPCManager 양보NPC와 동일한 패턴)")]
    public Transform playerTransform;
    public Vector3 competitorStandingOffset;

    [Header("Phase 2 - 타이밍 게임")]
    public float phase2Duration = 5f;
    public float seatedCompetitorScale = 0.09f; // 경쟁에서 진 NPC가 자리에 앉을 때 스프라이트 배율

    [Serializable]
    public struct CompetitorConfig
    {
        public EventManager.NPCType npcType;
        public float redChance;
        public float redDuration;
        [Header("이 NPC의 신호 사진 (타이밍용)")]
        public Sprite whiteSprite;
        public Sprite redSprite;
        public Sprite yellowSprite;
        [Header("이 NPC가 자리에 앉을 때 사진")]
        public Sprite justSatSprite; // 앉은 그 턴에만 표시
        public Sprite seatedSprite;  // 다음 턴부터 이걸로 전환 (TurnController가 처리)
    }

    [Header("경쟁 NPC 설정 (이 배열에 추가하면 PickCompetitor() 풀에도 자동으로 포함됨)")]
    public CompetitorConfig[] competitorConfigs = new CompetitorConfig[]
    {
        new CompetitorConfig { npcType = EventManager.NPCType.TiredWorker, redChance = 0.4f, redDuration = 0.6f },
        new CompetitorConfig { npcType = EventManager.NPCType.Girl, redChance = 0.25f, redDuration = 0.4f },
        new CompetitorConfig { npcType = EventManager.NPCType.SmartphonePassenger, redChance = 0.15f, redDuration = 0.2f },
    };

    [Header("호선별 경쟁 확률")]
    public float line9CompetitionRate = 0.7f;
    public float line7CompetitionRate = 0.3f;

    // ── 내부 상태 ─────────────────────────────────────
    private SeatManager.SeatSlot[] candidates;
    private int correctIndex = -1;
    private float observeTimer;
    private bool isObserving = false;
    private bool isPhase1 = false;

    private struct Segment { public Sprite sprite; public float duration; public bool isRed; }
    private Segment[] segments;
    private int segmentIndex;
    private float segmentTimer;
    private bool isRedNow;
    private bool isCompeting = false;
    private EventManager.NPCType currentCompetitor;
    public EventManager.NPCType CurrentCompetitor => currentCompetitor;

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
        characterDisplay?.UpdateSprite(CharacterDisplay.PoseOverride.Phase1);

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

    // 후보 좌석들을 강조용 스프라이트로 교체하고, 선택 아이콘을 띄움
    void HighlightCandidates()
    {
        foreach (var seat in candidates)
        {
            var obj = (seat.seatedNPCObject != null) ? seat.seatedNPCObject : seat.silhouette;
            if (obj != null && seat.currentHighlightSprite != null)
            {
                var sr = obj.GetComponentInChildren<SpriteRenderer>(true);
                if (sr) sr.sprite = seat.currentHighlightSprite;
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
            if (seat.seatType != SeatManager.SeatType.Normal)
                OnEyeGameEnd?.Invoke(true); // 특수석은 일반 경쟁 NPC가 끼어들지 않음 - 바로 착석
            else
            {
                currentCompetitor = PickCompetitor();
                StartCompetition();
            }
        }
        else
        {
            float rate = GameManager.Instance.lineType == GameManager.LineType.Line9
                ? line9CompetitionRate : line7CompetitionRate;
            if (UnityEngine.Random.value < rate)
            {
                currentCompetitor = PickCompetitor(); // 이전엔 빠져있어서 currentCompetitor가 직전 값(또는 기본값 Elderly)으로 남아있던 버그
                StartCompetition();
            }
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
        Invoke(nameof(AdvanceTurnDelayedNoMonologue), phase1FailDelay);
    }

    // ── Phase 2 경쟁 ──────────────────────────────────
    void StartCompetition()
    {
        segments = GenerateSegments(currentCompetitor);
        segmentIndex = 0;
        isCompeting = true;
        Debug.Log($"[Phase2] 시작 | NPC: {currentCompetitor} | 세그먼트 수: {segments.Length} | competitionPanel: {competitionPanel != null} | competitorRenderer: {competitorRenderer != null}");
        competitionPanel.SetActive(true);
        characterDisplay?.UpdateSprite(CharacterDisplay.PoseOverride.Phase2);
        if (competitorRenderer)
        {
            competitorRenderer.enabled = true;
            competitorRenderer.sortingOrder = competitorSortingOrder; // 주변 좌석 실루엣(-29)보다 항상 앞에 보이게 고정
            competitorRenderer.transform.position = playerTransform != null
                ? playerTransform.position + competitorStandingOffset
                : competitorStandingOffset;
        }
        Debug.Log($"[Phase2] competitionPanel 활성화: {competitionPanel.activeSelf}");
        ApplySegment(segments[0]);
    }

    void ApplySegment(Segment seg)
    {
        segmentTimer = seg.duration;
        isRedNow = seg.isRed;
        Debug.Log($"[Phase2] 세그먼트 {segmentIndex}: 사진={(seg.sprite ? seg.sprite.name : "null")} isRed={seg.isRed} 길이={seg.duration:F2}s");
        if (competitorRenderer) competitorRenderer.sprite = seg.sprite;
        else Debug.LogWarning("[Phase2] competitorRenderer가 null!");
    }

    CompetitorConfig GetCompetitorConfig(EventManager.NPCType npcType)
    {
        foreach (var c in competitorConfigs)
            if (c.npcType == npcType) return c;
        return new CompetitorConfig { npcType = npcType, redChance = 0.25f, redDuration = 0.4f }; // 배열에 등록 안 된 타입 - 폴백
    }

    Segment[] GenerateSegments(EventManager.NPCType npcType)
    {
        var config = GetCompetitorConfig(npcType);
        float redChance = config.redChance;
        float redDur = config.redDuration;

        var list = new System.Collections.Generic.List<Segment>();
        float remaining = phase2Duration;
        bool lastWasRed = false;

        while (remaining > 0.05f)
        {
            bool makeRed = !lastWasRed && UnityEngine.Random.value < redChance;
            Sprite s;
            float dur;

            if (makeRed)
            {
                s = config.redSprite;
                dur = Mathf.Min(UnityEngine.Random.Range(redDur * 0.7f, redDur * 1.3f), remaining);
                lastWasRed = true;
            }
            else
            {
                s = UnityEngine.Random.value < 0.5f ? config.whiteSprite : config.yellowSprite;
                dur = Mathf.Min(UnityEngine.Random.Range(0.4f, 2f), remaining);
                lastWasRed = false;
            }

            list.Add(new Segment { sprite = s, duration = dur, isRed = makeRed });
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

        var config = GetCompetitorConfig(currentCompetitor);
        var initialSprite = config.justSatSprite != null ? config.justSatSprite : config.seatedSprite;
        if (initialSprite == null) return;

        var go = new GameObject("SeatedCompetitor");
        go.transform.position = seat.seatPosition.position;
        go.transform.localScale = Vector3.one * seatedCompetitorScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = initialSprite; // 막 앉은 포즈 - 다음 턴 시작 시 TurnController가 seatedSprite로 교체
        sr.sortingOrder = -29;
        seat.seatedNPCObject = go;
    }

    // 경쟁에서 진 다음 턴, TurnController가 해당 NPC의 "정착 포즈"로 교체할 때 사용
    public Sprite GetSeatedSprite(EventManager.NPCType npcType) => GetCompetitorConfig(npcType).seatedSprite;

    // ── 공통 유틸 ─────────────────────────────────────
    void ClearHints()
    {
        if (candidates == null) return;
        foreach (var seat in candidates)
        {
            var obj = (seat.seatedNPCObject != null) ? seat.seatedNPCObject : seat.silhouette;
            if (obj != null && seat.currentSilhouetteSprite != null)
            {
                var sr = obj.GetComponentInChildren<SpriteRenderer>(true);
                if (sr) sr.sprite = seat.currentSilhouetteSprite;
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
    void AdvanceTurnDelayedNoMonologue() => TurnController.Instance?.AdvanceTurn();

    public string GetPhase1FailLine() => PickLine(phase1FailMonologues);

    string PickLine(string[] lines)
    {
        if (lines == null || lines.Length == 0) return "";
        return lines[UnityEngine.Random.Range(0, lines.Length)];
    }

    EventManager.NPCType PickCompetitor()
    {
        if (competitorConfigs == null || competitorConfigs.Length == 0) return EventManager.NPCType.TiredWorker;
        return competitorConfigs[UnityEngine.Random.Range(0, competitorConfigs.Length)].npcType;
    }
}
