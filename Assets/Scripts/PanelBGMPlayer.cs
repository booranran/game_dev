using UnityEngine;

// 패널 GameObject에 붙이기만 하면 됨 - SetActive(true)로 켜질 때(OnEnable) 자동으로 그 BGM으로 전환
// bgm을 비워두면 "일부러 무음" 의도로 처리 - 켜지면 지금 브금을 무음으로 페이드아웃하고, 꺼지면 다시 복귀 시도함
public class PanelBGMPlayer : MonoBehaviour
{
    public AudioClip bgm;
    public AudioManager.FadeStyle fadeStyle = AudioManager.FadeStyle.Crossfade;
    public float silenceFadeOutDuration = 1f; // bgm이 비어있을 때, 무음으로 빠지는 시간

    void OnEnable()
    {
        if (bgm != null)
            AudioManager.Instance?.PlayBGM(bgm, fadeStyle);
        else
            AudioManager.Instance?.FadeOutBGM(silenceFadeOutDuration); // 일부러 무음으로
    }

    // 패널이 꺼지면 본게임 브금으로 복귀 시도(무음이었던 경우엔 다시 페이드인되는 효과) - 단, 같은 프레임에
    // 다른 패널이 곧바로 켜지면서 BGM을 새로 요청하면(같은 곡이든 다른 곡이든) 그 요청이 우선이라 복귀 안 함
    void OnDisable()
    {
        AudioManager.Instance?.RequestReturnToMainGameIfUnclaimed();
    }
}
