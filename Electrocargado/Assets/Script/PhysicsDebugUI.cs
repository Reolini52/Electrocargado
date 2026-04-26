using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PhysicsDebugUI : MonoBehaviour
{
    private PlayerController player;
    private ChargedObject[] chargedObjects;
    private bool debugVisible = false;

    private GUIStyle headerStyle;
    private GUIStyle valueStyle;

    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        if (Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
        {
            debugVisible = !debugVisible;
            chargedObjects = FindObjectsByType<ChargedObject>(FindObjectsSortMode.None);
        }
    }

    void OnGUI()
    {
        if (!debugVisible || player == null) return;

        // Semi transparent background
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(new Rect(10, 10, 320, 200), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(20, 20, 300, 180));

        GUI.skin.label.fontSize = 13;
        GUI.skin.label.normal.textColor = Color.green;

        GUILayout.Label("=== ELECTRO DEBUG [F] ===");
        GUILayout.Label($"Player charge: q = {(player.GetCharge() > 0 ? "+" : "")}{player.GetCharge()}");

        if (chargedObjects != null && chargedObjects.Length > 0)
        {
            ChargedObject nearest = GetNearest();
            if (nearest != null)
            {
                float r = Vector2.Distance(player.transform.position, nearest.transform.position);
                float k = 8.99f; // Coulomb constant simplified
                float F = k * Mathf.Abs(player.GetCharge() * nearest.charge) / (r * r);
                string interaction = (player.GetCharge() * nearest.charge > 0) ? "REPULSION" : "ATTRACTION";

                GUILayout.Label($"Nearest object: Q = {(nearest.charge > 0 ? "+" : "")}{nearest.charge}");
                GUILayout.Label($"Distance: r = {r:F2} units");
                GUILayout.Label($"F = k|qQ|/r²");
                GUILayout.Label($"F = {F:F2} N  [{interaction}]");
            }
        }

        GUILayout.Label($"Grounded: {(IsGrounded() ? "yes" : "no")}");
        GUILayout.EndArea();
    }

    ChargedObject GetNearest()
    {
        ChargedObject nearest = null;
        float minDist = float.MaxValue;
        foreach (var obj in chargedObjects)
        {
            float d = Vector2.Distance(player.transform.position, obj.transform.position);
            if (d < minDist) { minDist = d; nearest = obj; }
        }
        return nearest;
    }

    bool IsGrounded()
    {
        return Physics2D.Raycast(player.transform.position, Vector2.down, 0.6f,
            ~LayerMask.GetMask("Player")).collider != null;
    }
}