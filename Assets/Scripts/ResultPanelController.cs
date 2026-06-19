using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
using TMPro;
using System.Collections;

public class ResultPanelController : MonoBehaviour
{
    [System.Serializable]
    public struct ResultData
    {
        public GameManager.CharacterType character;
        public GameManager.EndingType ending;
        public Sprite illustration;
        public string title; // "GOOD ENDING" 등 마지막에만 단독으로 표시
        [TextArea] public string[] monologueLines; // 일러스트 아래에 순서대로 한 줄씩 표시
    }

    [Header("UI 참조")]
    public GameObject resultPanel;
    public Image illustrationImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI healthScoreText;
    public TextMeshProUGUI considerationScoreText;
    public PlayableDirector scoreTimeline; // scoreFadeInDuration 끝나면 재생

    [Header("하차 일러스트 (엔딩 공통, 풀스크린 - 캐릭터/엔딩 상관없이 동일)")]
    public CanvasGroup exitIllustrationGroup; // 안에 들어있는 Image의 스프라이트는 에디터에서 고정으로 박아두면 됨

    [Header("페이드 단계별 그룹 (전부 CanvasGroup 필요)")]
    public Image fadeOverlay; // 풀스크린 검은 이미지 - 게임 뷰를 암전시키는 배경, 한 번 켜진 후 끝까지 유지됨
    public CanvasGroup scoreGroup; // 최종 점수
    public CanvasGroup illustrationGroup; // 일러스트 + 독백
    public CanvasGroup endingTitleGroup; // 엔딩 이름만

    [Header("타이밍 - 게임 뷰 암전")]
    public float fadeOverlayInDuration = 1f;

    [Header("타이밍 - 하차 일러스트 (시간 지나면 자동 전환)")]
    public float exitIllustrationFadeInDuration = 1f;
    public float exitIllustrationDisplayDuration = 2.5f;
    public float exitIllustrationFadeOutDuration = 1f;

    [Header("타이밍 - 최종 점수 (클릭할 때까지 대기, 페이드 길이만)")]
    public float scoreFadeInDuration = 1f;
    public float scoreFadeOutDuration = 1f;

    [Header("타이밍 - 캐릭터 일러스트 + 독백 (한 줄씩 클릭, 페이드 길이만)")]
    public float illustrationFadeInDuration = 1f;
    public float illustrationFadeOutDuration = 1f;

    [Header("타이밍 - 엔딩 이름")]
    public float endingTitleFadeInDuration = 1f;

    [Header("클릭 안내 (TriggerEventController의 continuePrompt와 동일한 구조)")]
    public GameObject continuePrompt; // 일정 시간 후 나타나는 "클릭하세요" 안내 - 풀스크린 버튼의 OnClick에서 OnContinueClicked() 연결
    public float continuePromptDelay = 2f;

    private bool waitingForContinue;

    [Header("엔딩 데이터 (캐릭터 x 엔딩 조합별로 등록)")]
    public ResultData[] resultEntries;

    public void Show(GameManager.CharacterType character, GameManager.EndingType ending)
    {
        resultPanel.SetActive(true);

        ResultData data = default;
        bool found = false;
        foreach (var entry in resultEntries)
        {
            if (entry.character != character || entry.ending != ending) continue;
            data = entry;
            found = true;
            break;
        }
        if (!found)
            Debug.LogWarning($"[ResultPanel] {character}/{ending} 엔딩 데이터가 등록되지 않음");

        StartCoroutine(PlayEndingSequence(data, found ? data.title : ending.ToString()));
    }

    IEnumerator PlayEndingSequence(ResultData data, string fallbackTitle)
    {
        SetAlpha(exitIllustrationGroup, 0f);
        SetAlpha(scoreGroup, 0f);
        SetAlpha(illustrationGroup, 0f);
        SetAlpha(endingTitleGroup, 0f);
        if (continuePrompt) continuePrompt.SetActive(false);

        // 1. 게임 뷰 → 암전 (이후 끝까지 검은 배경 유지)
        if (fadeOverlay) fadeOverlay.gameObject.SetActive(true);
        yield return FadeImage(fadeOverlay, 0f, 1f, fadeOverlayInDuration);

        // 2. 하차 일러스트 (엔딩 공통, 풀스크린) - 시간 지나면 자동 전환
        yield return FadeGroup(exitIllustrationGroup, 0f, 1f, exitIllustrationFadeInDuration);
        yield return new WaitForSeconds(exitIllustrationDisplayDuration);
        yield return FadeGroup(exitIllustrationGroup, 1f, 0f, exitIllustrationFadeOutDuration);

        // 3. 최종 점수 (fade-in 끝나면 director 재생) - 클릭할 때까지 대기
        if (healthScoreText) healthScoreText.text = $"{Mathf.RoundToInt(GameManager.Instance.GetHealthScore())}점";
        if (considerationScoreText) considerationScoreText.text = $"{Mathf.RoundToInt(GameManager.Instance.GetConsiderationScore())}점";
        yield return FadeGroup(scoreGroup, 0f, 1f, scoreFadeInDuration);

        if (scoreTimeline) scoreTimeline.Play();

        yield return WaitForContinue();
        yield return FadeGroup(scoreGroup, 1f, 0f, scoreFadeOutDuration);

        // 4. 일러스트 + 독백 (한 줄씩 클릭해서 넘김)
        if (illustrationImage) illustrationImage.sprite = data.illustration;
        yield return FadeGroup(illustrationGroup, 0f, 1f, illustrationFadeInDuration);

        if (data.monologueLines != null)
        {
            foreach (var line in data.monologueLines)
            {
                if (descriptionText) descriptionText.text = line;
                yield return WaitForContinue();
            }
        }
        yield return FadeGroup(illustrationGroup, 1f, 0f, illustrationFadeOutDuration);

        // 5. 엔딩 이름만 단독으로
        if (titleText) titleText.text = fallbackTitle;
        yield return FadeGroup(endingTitleGroup, 0f, 1f, endingTitleFadeInDuration);
    }

    // 풀스크린 버튼의 OnClick에서 호출 - TriggerEventController.OnTriggerContinueButton과 동일한 역할
    public void OnContinueClicked()
    {
        if (!waitingForContinue) return;
        waitingForContinue = false;
    }

    IEnumerator WaitForContinue()
    {
        waitingForContinue = true;
        if (continuePrompt) continuePrompt.SetActive(false);
        yield return new WaitForSeconds(continuePromptDelay);
        if (continuePrompt) continuePrompt.SetActive(true);
        yield return new WaitUntil(() => !waitingForContinue);
        if (continuePrompt) continuePrompt.SetActive(false);
    }

    void SetAlpha(CanvasGroup group, float a)
    {
        if (group) group.alpha = a;
    }

    IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        group.alpha = from;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        if (img == null) yield break;
        Color c = img.color;
        c.a = from;
        img.color = c;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / duration);
            img.color = c;
            yield return null;
        }
        c.a = to;
        img.color = c;
    }
}
