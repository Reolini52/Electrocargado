using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerFieldLines : MonoBehaviour
{
    [Header("Field Lines")]
    public int lineCount = 6;
    public float lineLength = 2.5f;
    public float lineWidth = 0.04f;

    private LineRenderer[] lines;
    private PlayerController player;
    private ChargedObject[] chargedObjects;
    private bool linesVisible = true;

    void Start()
    {
        player = GetComponent<PlayerController>();
        CreateLines();
    }

    void CreateLines()
    {
        lines = new LineRenderer[lineCount];
        for (int i = 0; i < lineCount; i++)
        {
            GameObject lineObj = new GameObject($"PlayerFieldLine_{i}");
            lineObj.transform.parent = transform;
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 3;
            lr.startWidth = lineWidth;
            lr.endWidth = 0f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 2;
            lr.useWorldSpace = true;
            lines[i] = lr;
        }
    }

    void ToggleLines()
    {
        linesVisible = !linesVisible;
        foreach (var lr in lines)
            lr.enabled = linesVisible;
    }

    void Update()
    {
        if (Keyboard.current.lKey.wasPressedThisFrame)
            ToggleLines();

        if (!linesVisible || lines == null) return;

        chargedObjects = FindObjectsByType<ChargedObject>(FindObjectsSortMode.None);

        bool isPositive = player.GetCharge() > 0;
        Color baseColor = isPositive ?
            new Color(0.3f, 0.6f, 1f, 0.9f) :
            new Color(1f, 0.3f, 0.3f, 0.9f);

        ChargedObject nearest = GetNearest();

        for (int i = 0; i < lineCount; i++)
        {
            float angle = (360f / lineCount) * i;
            Vector3 baseDir = Quaternion.Euler(0, 0, angle) * Vector3.right;

            Vector3 start = transform.position + baseDir * 0.25f;
            Vector3 end = transform.position + baseDir * lineLength;

            if (nearest != null)
            {
                float dist = Vector2.Distance(transform.position, nearest.transform.position);
                if (dist < nearest.effectRadius)
                {
                    Vector3 toObj = (nearest.transform.position - transform.position).normalized;
                    float alignment = Vector3.Dot(baseDir, toObj);
                    bool attracting = (player.GetCharge() * nearest.charge) < 0;
                    float bend = alignment * 0.6f * (1f - dist / nearest.effectRadius);

                    if (attracting)
                        end += toObj * bend * lineLength;
                    else
                        end -= toObj * bend * lineLength * 0.4f;
                }
            }

            Vector3 mid = (start + end) * 0.5f;

            if (isPositive)
            {
                lines[i].SetPosition(0, start);
                lines[i].SetPosition(1, mid);
                lines[i].SetPosition(2, end);
            }
            else
            {
                lines[i].SetPosition(0, end);
                lines[i].SetPosition(1, mid);
                lines[i].SetPosition(2, start);
            }

            lines[i].startColor = baseColor;
            lines[i].endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }
    }

    ChargedObject GetNearest()
    {
        ChargedObject nearest = null;
        float minDist = float.MaxValue;
        if (chargedObjects == null) return null;
        foreach (var obj in chargedObjects)
        {
            if (obj == null) continue;
            float d = Vector2.Distance(transform.position, obj.transform.position);
            if (d < minDist) { minDist = d; nearest = obj; }
        }
        return nearest;
    }
}