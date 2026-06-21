using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

// ResultPanelController와 분리된 전용 패널 - 체력 0 게임오버는 역에 도착하는 엔딩들과 다르게
// 긴 시퀀스(하차 일러스트/점수/캐릭터 일러스트) 없이 타이틀 + 멘트 + 재시작 버튼만 보여줌
public class GameOverPanelController : MonoBehaviour
{
    [System.Serializable]
    public struct GameOverData
    {
        public GameManager.CharacterType character;
        [TextArea] public string message;
        public AudioClip bgm;
    }

    [Header("UI 참조")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    public GameObject retryButton;

    [Header("타이밍")]
    public float bgmFadeOutDuration = 1f;
    public float messageDelay = 1f;
    public float retryButtonDelay = 2f;

    [Header("캐릭터별 멘트/브금")]
    public GameOverData[] entries;

    public void Show(GameManager.CharacterType character)
    {
        gameOverPanel.SetActive(true);
        StartCoroutine(PlaySequence(character));
    }

    IEnumerator PlaySequence(GameManager.CharacterType character)
    {
        if (retryButton) retryButton.SetActive(false);
        if (titleText) titleText.text = "";
        if (messageText) messageText.text = "";

        AudioClip bgm = null;
        string message = "";
        foreach (var e in entries)
        {
            if (e.character != character) continue;
            bgm = e.bgm;
            message = e.message;
            break;
        }

        AudioManager.Instance?.FadeOutBGM(bgmFadeOutDuration);

        if (titleText) titleText.text = "GAME OVER";
        if (bgm != null) AudioManager.Instance?.PlayBGM(bgm, AudioManager.FadeStyle.Crossfade);

        yield return new WaitForSeconds(messageDelay);
        if (messageText) messageText.text = message;

        yield return new WaitForSeconds(retryButtonDelay);
        if (retryButton) retryButton.SetActive(true);
    }

    // 재시작 버튼 OnClick에 연결 - 단일 씬 구조라 씬을 통째로 다시 로드
    public void OnRetryButtonClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
