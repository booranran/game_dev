using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TriggerEventController : MonoBehaviour
{
    public static TriggerEventController Instance;

    [System.Serializable]
    public struct TriggerEventData
    {
        public Sprite standingSprite; // 컷씬 전 트레인 안에서 미리 보여줄 스프라이트
        public float standingScale; // standingSprite 전용 배율 (0이면 1로 처리됨), NPCManager 쪽 scale과 무관하게 독립적으로 적용됨
        public Sprite illustration;   // 컷씬 패널 일러스트
        [TextArea] public string dialogue;
        public int conditionDelta; // 양수면 컨디션 호전, 음수면 악화
        [TextArea] public string postMonologue; // 컷씬 닫힌 후 메인 화면에서 보여줄 후속 독백 (턴 전환 전 한 박자 쉬어가는 용도)
        public AudioClip triggerBGM; // 이 컷씬 전용 브금 - 비워두면 브금 전환 없이 기존 그대로 유지
    }

    [Header("UI 참조")]
    public GameObject triggerPanel; // 풀스크린 투명 버튼이 달려있어야 함 (OnClick → TurnController.OnTriggerContinueButton)
    public Image illustrationImage;
    public TextMeshProUGUI dialogueText;
    public GameObject continuePrompt; // 일정 시간 후 나타나는 "클릭하세요" 안내 텍스트/박스
    public float continuePromptDelay = 2f;

    [Header("사운드")]
    public AudioManager.FadeStyle bgmFadeStyle = AudioManager.FadeStyle.Crossfade;

    [Header("트리거 이벤트 데이터 (인스펙터에서 자유롭게 추가/수정)")]
    public TriggerEventData[] events;

    private bool[] used;
    private int currentIndex = -1;
    private string _pendingPostMonologue;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        used = new bool[events?.Length ?? 0];
    }

    public bool HasAvailableEvent()
    {
        if (used == null) return false;
        foreach (var u in used) if (!u) return true;
        return false;
    }

    // 이벤트 발생 즉시 호출 - 트레인 안에 NPC가 서있는 모습을 미리 보여줌
    public void PickRandom()
    {
        var pool = new List<int>();
        for (int i = 0; i < used.Length; i++) if (!used[i]) pool.Add(i);
        if (pool.Count == 0) return;

        currentIndex = pool[UnityEngine.Random.Range(0, pool.Count)];
        used[currentIndex] = true;

        var data = events[currentIndex];
        float scale = data.standingScale > 0f ? data.standingScale : 1f;
        NPCManager.Instance?.ShowStanding(data.standingSprite, "", scale);
    }

    // triggerPanelDelay 후 호출 - 풀스크린 컷씬 패널 등장 (클릭은 이 순간부터 바로 가능)
    public void ShowPanel()
    {
        if (currentIndex < 0 || currentIndex >= events.Length) return;
        var data = events[currentIndex];

        if (illustrationImage) illustrationImage.sprite = data.illustration;
        if (dialogueText) dialogueText.text = data.dialogue;
        if (data.triggerBGM != null)
            AudioManager.Instance?.PlayBGM(data.triggerBGM, bgmFadeStyle);

        triggerPanel.SetActive(true);
        if (continuePrompt) continuePrompt.SetActive(false);
        Invoke(nameof(ShowContinuePrompt), continuePromptDelay);
    }

    void ShowContinuePrompt()
    {
        if (continuePrompt) continuePrompt.SetActive(true);
    }

    // 풀스크린 버튼 클릭 시 호출 (TurnController.OnTriggerContinueButton에서 호출) - 컨디션 적용 후 패널 닫기
    public void ResolveCurrent()
    {
        CancelInvoke(nameof(ShowContinuePrompt));

        if (currentIndex >= 0 && currentIndex < events.Length)
        {
            GameManager.Instance.ChangeCondition(events[currentIndex].conditionDelta);
            _pendingPostMonologue = events[currentIndex].postMonologue;
            if (events[currentIndex].triggerBGM != null)
                AudioManager.Instance?.RequestReturnToMainGameIfUnclaimed();
        }

        triggerPanel.SetActive(false);
        currentIndex = -1;
    }

    public string GetPostMonologue() => _pendingPostMonologue;
}
