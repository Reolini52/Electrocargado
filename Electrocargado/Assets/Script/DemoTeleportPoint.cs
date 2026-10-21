using UnityEngine;

/// <summary>
/// Componente opcional para marcar un punto de teletransporte manual para la demo.
/// Si colocas un GameObject con este script en cualquier escena, aparecerá automáticamente
/// en el menú [P] Demo TP.
/// </summary>
public class DemoTeleportPoint : MonoBehaviour
{
    [Tooltip("Nombre que aparecerá en la lista de [P] Demo TP")]
    public string pointLabel = "Punto Demo";

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, 0.45f);
        Gizmos.DrawLine(transform.position + Vector3.down * 0.45f, transform.position + Vector3.up * 0.45f);
    }
}
