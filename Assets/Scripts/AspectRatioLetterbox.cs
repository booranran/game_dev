using UnityEngine;

// Main Camera에 붙이기 - 1920x1080(16:9) 기준으로 짜둔 화면을 다른 비율 기기에서도
// 레이아웃이 안 깨지게, 16:9 비율 그대로 화면 중앙에 맞추고 남는 영역은 검은 바(레터박스/필러박스)로 채움
[RequireComponent(typeof(Camera))]
public class AspectRatioLetterbox : MonoBehaviour
{
    public float targetAspect = 16f / 9f;

    private Camera cam;
    private float lastWindowAspect;

    void Start()
    {
        cam = GetComponent<Camera>();
        Apply();
    }

    void Update()
    {
        float windowAspect = (float)Screen.width / Screen.height;
        if (!Mathf.Approximately(windowAspect, lastWindowAspect))
            Apply();
    }

    void Apply()
    {
        float windowAspect = (float)Screen.width / Screen.height;
        lastWindowAspect = windowAspect;
        float scaleHeight = windowAspect / targetAspect;

        Rect rect = cam.rect;
        if (scaleHeight < 1f)
        {
            // 화면이 타겟보다 더 길쭉함(세로로 긴 모바일) → 위아래 레터박스
            rect.width = 1f;
            rect.height = scaleHeight;
            rect.x = 0f;
            rect.y = (1f - scaleHeight) / 2f;
        }
        else
        {
            // 화면이 타겟보다 더 넓음 → 좌우 필러박스
            float scaleWidth = 1f / scaleHeight;
            rect.width = scaleWidth;
            rect.height = 1f;
            rect.x = (1f - scaleWidth) / 2f;
            rect.y = 0f;
        }
        cam.rect = rect;
    }
}
