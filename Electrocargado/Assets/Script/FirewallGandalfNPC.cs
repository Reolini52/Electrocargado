using UnityEngine;

/// <summary>
/// Clase oficial de la Capa 3 (Verificar): NPC Firewall "Gandalf" según la especificación de arquitectura
/// del proyecto "Electrocargado" (Uniautónoma del Cauca / Softlab - GITA).
/// Hereda e integra todo el comportamiento de evaluación conceptual y desbloqueo de puerta de GandalfNPC.
/// </summary>
[DisallowMultipleComponent]
public class FirewallGandalfNPC : GandalfNPC
{
    [Header("Identificador de Sector (Firewall Gandalf)")]
    [Tooltip("Nombre o código del sector custodiado por esta instancia de Gandalf.")]
    [SerializeField]
    private string sectorTitle = "Sector 01: Ley de Coulomb & Capacitancia RC";

    public string SectorTitle => sectorTitle;
}
