using UnityEngine;
using UnityEngine.UI;

public class OverlayAnimator : MonoBehaviour
{
    public float scrollX = 0.02f;
    private RawImage rawImage;

    void Start()
    {
        rawImage = GetComponent<RawImage>();
    }

    void Update()
    {
        if (rawImage == null) return;
        rawImage.uvRect = new Rect(
            rawImage.uvRect.x + scrollX * Time.deltaTime,
            rawImage.uvRect.y,
            rawImage.uvRect.width,
            rawImage.uvRect.height
        );
    }
}