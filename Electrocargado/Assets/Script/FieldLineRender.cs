using UnityEngine;

public class FieldLineRenderer : MonoBehaviour
{
    [Header("Field Lines")]
    public int lineCount = 8;
    public float lineLength = 4f;
    public float lineWidth = 0.05f;
    public float bendStrength = 0.5f;

    private ChargedObject chargedObj;
    private LineRenderer[] lines;
    private PlayerController player;
    private bool linesVisible = true;

    void Start()
    {
        chargedObj = GetComponent<ChargedObject>();
        player = FindFirstObjectByType<PlayerController>();
        CreateLines();
    }

    void CreateLines()
    {
        lines = new LineRenderer[lineCount];
        for (int i = 0; i < lineCount; i++)
        {
            GameObject lineObj = new GameObject($"FieldLine_{i}");
            lineObj.transform.parent = transform;
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 3;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth * 0.2f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = 1;
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
        if (UnityEngine.InputSystem.Keyboard.current.lKey.wasPressedThisFrame)
            ToggleLines();

        if (!linesVisible || lines == null || player == null) return;

        Color lineColor = chargedObj.charge > 0 ?
            new Color(0.3f, 0.6f, 1f, 0.8f) :
            new Color(1f, 0.3f, 0.3f, 0.8f);

        Vector3 playerPos = player.transform.position;
        Vector3 objPos = transform.position;
        float distToPlayer = Vector2.Distance(objPos, playerPos);
        bool inRange = distToPlayer < chargedObj.effectRadius;
        bool attracting = inRange && (chargedObj.charge * player.GetCharge()) < 0;
        bool repelling = inRange && (chargedObj.charge * player.GetCharge()) > 0;

        for (int i = 0; i < lineCount; i++)
        {
            float angle = (360f / lineCount) * i;
            Vector3 baseDirection = Quaternion.Euler(0, 0, angle) * Vector3.right;

            Vector3 start = objPos + baseDirection * 0.3f;
            Vector3 end = objPos + baseDirection * lineLength;

            if (inRange)
            {
                Vector3 toPlayer = (playerPos - objPos).normalized;
                float alignment = Vector3.Dot(baseDirection, toPlayer);
                float bendAmount = alignment * bendStrength *
                    (1f - distToPlayer / chargedObj.effectRadius);

                if (attracting)
                    end += toPlayer * bendAmount * lineLength;
                else if (repelling)
                    end -= toPlayer * bendAmount * lineLength * 0.5f;
            }

            Vector3 mid = (start + end) * 0.5f;

            if (chargedObj.charge < 0)
            {
                lines[i].SetPosition(0, end);
                lines[i].SetPosition(1, mid);
                lines[i].SetPosition(2, start);
            }
            else
            {
                lines[i].SetPosition(0, start);
                lines[i].SetPosition(1, mid);
                lines[i].SetPosition(2, end);
            }

            Color finalColor = lineColor;
            if (attracting) finalColor = Color.Lerp(lineColor, Color.green, 0.4f);
            if (repelling) finalColor = Color.Lerp(lineColor, Color.yellow, 0.4f);

            lines[i].startColor = finalColor;
            lines[i].endColor = new Color(finalColor.r, finalColor.g, finalColor.b, 0f);
        }
    }
}