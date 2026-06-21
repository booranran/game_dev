using UnityEngine;

// 패널 GameObject에 붙이기만 하면 됨 - SetActive(true)로 켜질 때(OnEnable) 자동으로 그 BGM으로 전환
public class PanelBGMPlayer : MonoBehaviour
{
    public AudioClip bgm;
    public AudioManager.FadeStyle fadeStyle = AudioManager.FadeStyle.Crossfade;

    void OnEnable()
    {
        if (bgm != null)
            AudioManager.Instance?.PlayBGM(bgm, fadeStyle);
    }

    // 패널이 꺼지면 본게임 브금으로 복귀 시도 - 단, 같은 프레임에 다른 패널이 곧바로 켜지면서
    // BGM을 새로 요청하면(같은 곡이든 다른 곡이든) 그 요청이 우선이라 복귀 안 하고 그대로 이어짐
    void OnDisable()
    {
        if (bgm != null)
            AudioManager.Instance?.RequestReturnToMainGameIfUnclaimed();
    }
}
