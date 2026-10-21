using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Constructor y configurador del Nivel 4 — Boss Final: POLAROUS (El Capacitor Viviente).
/// Utiliza directamente los prefabs y assets existentes en la escena (player.prefab animado,
/// Grid.prefab Tilemap con sus tiles reales, charged object, Dynamicobject, Detector y Emisor)
/// para que el nivel conserve toda la estética visual del juego.
/// </summary>
public class BossArenaBuilder : MonoBehaviour
{
    private static Sprite fallbackSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterBossSceneListener()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckAndBuildIfBossScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndBuildIfBossScene(scene);
    }

    private static void CheckAndBuildIfBossScene(Scene scene)
    {
        if (scene.name != "Level 4" && scene.name != "Level4") return;
        if (Object.FindAnyObjectByType<PolarousBossController>() != null) return;

        GameObject builderObj = new GameObject("[PolarousBossArena_Builder]");
        builderObj.AddComponent<BossArenaBuilder>().BuildCapacitorArena();
    }

    private static Sprite GetFallbackSprite()
    {
        // Intentar usar el sprite de cualquier SpriteRenderer existente en la escena antes de crear uno
        ChargedObject existingCo = Object.FindAnyObjectByType<ChargedObject>();
        if (existingCo != null && existingCo.TryGetComponent(out SpriteRenderer coSr) && coSr.sprite != null)
            return coSr.sprite;

        if (fallbackSprite != null) return fallbackSprite;

        Texture2D tex = new Texture2D(16, 16);
        Color[] cols = new Color[16 * 16];
        for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();

        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        return fallbackSprite;
    }

    public void BuildCapacitorArena()
    {
        // 1. Expandir el Tilemap existente (Grid.prefab) usando sus propios TileBase assets reales
        PaintCapacitorTilemapArena();

        // 2. Configurar Cámara Principal manteniendo el fondo animado (OverlayAnimator) de la escena
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.orthographicSize = 6.2f;
        }

        // 3. Usar el player.prefab existente en la escena (con todas sus animaciones y assets)
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerObj.transform.position = new Vector3(-3.5f, -0.4f, 0f);
            if (playerObj.TryGetComponent(out ChargeResource cr))
            {
                cr.charge = 0.8f;
            }
        }

        if (mainCam != null && playerObj != null)
        {
            CameraFollow follow = mainCam.GetComponent<CameraFollow>();
            if (follow == null) follow = mainCam.gameObject.AddComponent<CameraFollow>();
            follow.target = playerObj.transform;
            follow.offset = new Vector3(0f, 1.2f, -10f);
        }

        // 4. Configurar las 4 fuentes de carga fijas (+Q y -Q) usando los ChargedObject de la escena
        ConfigureSceneChargeSources();

        // 5. Configurar el objeto dinámico cargado (DynamicChargedObject) en la plataforma central
        DynamicChargedObject dynObj = Object.FindAnyObjectByType<DynamicChargedObject>();
        if (dynObj != null)
        {
            dynObj.transform.position = new Vector3(-1.2f, 4.3f, 0f);
            dynObj.charge = 0.9f;
            dynObj.UpdateVisual();
        }

        // 6. Crear o configurar la Barra de Corriente Central (CurrentRod) en la plataforma central
        CurrentRod centralRod = CreateOrConfigureCentralCurrentRod();

        // 7. Eliminar cualquier FieldZone del nivel del jefe para evitar interferencia con el mouse y el control
        FieldZone[] fieldZones = Object.FindObjectsByType<FieldZone>();
        for (int i = 0; i < fieldZones.Length; i++)
        {
            if (fieldZones[i] != null)
            {
                Destroy(fieldZones[i].gameObject);
            }
        }

        // 8. Convertir o posicionar los 4 terminales de placa (⊕ y ⊖) sobre las plataformas del Tilemap
        List<CapacitorPlateNode> plateNodes = ConfigureCapacitorPlateTerminals(centralRod.transform.position);

        // 9. Configurar a POLAROUS usando el sprite y material reales del prefab Emisor de la escena
        ConfigurePolarousBoss(plateNodes, centralRod, null);
    }

    /// <summary>
    /// Toma los TileBase originales del Tilemap (Grid.prefab) y dibuja la arena completa del Capacitor
    /// (suelo, paredes laterales, techo y 5 plataformas de combate) con el arte oficial del proyecto.
    /// </summary>
    private void PaintCapacitorTilemapArena()
    {
        Tilemap tilemap = Object.FindAnyObjectByType<Tilemap>();
        if (tilemap == null) return;

        // Extraer los tiles originales que vienen dentro de Grid.prefab
        TileBase topLeft = tilemap.GetTile(new Vector3Int(-11, -2, 0));
        TileBase topCenter = tilemap.GetTile(new Vector3Int(-10, -2, 0));
        TileBase topRight = tilemap.GetTile(new Vector3Int(9, -2, 0));
        TileBase fillCenter = tilemap.GetTile(new Vector3Int(-9, -4, 0));
        TileBase fillLeft = tilemap.GetTile(new Vector3Int(-11, -4, 0));
        TileBase fillRight = tilemap.GetTile(new Vector3Int(9, -4, 0));

        if (topCenter == null) return;
        if (topLeft == null) topLeft = topCenter;
        if (topRight == null) topRight = topCenter;
        if (fillCenter == null) fillCenter = topCenter;
        if (fillLeft == null) fillLeft = fillCenter;
        if (fillRight == null) fillRight = fillCenter;

        tilemap.ClearAllTiles();

        // A. Suelo principal del Capacitor (x = -14 a 14, superficie en y = -2)
        PaintHorizontalPlatform(tilemap, -14, 14, -2, topLeft, topCenter, topRight);
        for (int x = -14; x <= 14; x += 2)
        {
            TileBase fill = (x == -14) ? fillLeft : (x == 14 ? fillRight : fillCenter);
            tilemap.SetTile(new Vector3Int(x, -4, 0), fill);
            tilemap.SetTile(new Vector3Int(x, -6, 0), fill);
        }

        // B. Paredes laterales de contención (x = -14 y x = 14, desde y = -1 hasta y = 9)
        for (int y = -1; y <= 9; y++)
        {
            tilemap.SetTile(new Vector3Int(-14, y, 0), topCenter);
            tilemap.SetTile(new Vector3Int(-15, y, 0), topCenter);
            tilemap.SetTile(new Vector3Int(14, y, 0), topCenter);
            tilemap.SetTile(new Vector3Int(15, y, 0), topCenter);
        }

        // C. Techo superior del Capacitor (x = -14 a 14, y = 9)
        PaintHorizontalPlatform(tilemap, -14, 14, 9, topLeft, topCenter, topRight);

        // D. Plataformas intermedias del circuito (para saltar y usar propulsión de Coulomb)
        // Plataforma Izquierda Baja (y = 1)
        PaintHorizontalPlatform(tilemap, -9, -5, 1, topLeft, topCenter, topRight);
        // Plataforma Derecha Baja (y = 1)
        PaintHorizontalPlatform(tilemap, 5, 9, 1, topLeft, topCenter, topRight);
        // Plataforma Central Media (y = 3, aloja el CurrentRod y la esfera dinámica)
        PaintHorizontalPlatform(tilemap, -3, 3, 3, topLeft, topCenter, topRight);
        // Plataforma Izquierda Alta (y = 5)
        PaintHorizontalPlatform(tilemap, -9, -5, 5, topLeft, topCenter, topRight);
        // Plataforma Derecha Alta (y = 5)
        PaintHorizontalPlatform(tilemap, 5, 9, 5, topLeft, topCenter, topRight);

        tilemap.RefreshAllTiles();

        // Actualizar el CompositeCollider2D del Tilemap para que todas las plataformas tengan colisión sólida
        if (tilemap.TryGetComponent(out CompositeCollider2D composite))
        {
            composite.GenerateGeometry();
        }
        Physics2D.SyncTransforms();
    }

    private static void PaintHorizontalPlatform(Tilemap map, int xMin, int xMax, int y, TileBase left, TileBase center, TileBase right)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            TileBase t = (x == xMin) ? left : (x == xMax ? right : center);
            map.SetTile(new Vector3Int(x, y, 0), t);
        }
    }

    private void ConfigureSceneChargeSources()
    {
        ChargedObject[] existingSources = Object.FindObjectsByType<ChargedObject>();
        Vector2[] positions = new Vector2[]
        {
            new Vector2(-11.8f, -0.4f), // Abajo Izquierda (+Q Azul)
            new Vector2(11.8f, -0.4f),  // Abajo Derecha (-Q Roja)
            new Vector2(-11.8f, 5.8f),  // Arriba Izquierda (-Q Roja)
            new Vector2(11.8f, 5.8f)    // Arriba Derecha (+Q Azul)
        };
        float[] charges = new float[] { +1f, -1f, -1f, +1f };

        ChargedObject template = existingSources.Length > 0 ? existingSources[0] : null;

        for (int i = 0; i < 4; i++)
        {
            ChargedObject src;
            if (i < existingSources.Length)
            {
                src = existingSources[i];
            }
            else if (template != null)
            {
                src = Instantiate(template);
            }
            else
            {
                src = CreateFallbackChargeTerminal($"ChargeSource_{i}", positions[i], charges[i]);
            }

            src.name = charges[i] > 0 ? $"Generator_Positive_{i}" : $"Generator_Negative_{i}";
            src.transform.position = new Vector3(positions[i].x, positions[i].y, 0f);
            src.transform.localRotation = Quaternion.identity;
            src.charge = charges[i];
            src.isConductor = true;
            src.isFixedSource = true;
            src.effectRadius = 8.5f;
            src.forceStrength = 55f;
            src.UpdateVisual();
        }
    }

    private CurrentRod CreateOrConfigureCentralCurrentRod()
    {
        CurrentRod rod = Object.FindAnyObjectByType<CurrentRod>();
        if (rod == null)
        {
            GameObject rodObj = new GameObject("Central_Capacitor_CurrentRod");
            rodObj.transform.position = new Vector3(0f, 3.85f, 0f);
            rodObj.transform.localScale = new Vector3(3.8f, 0.32f, 1f);

            SpriteRenderer sr = rodObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetFallbackSprite();
            sr.color = new Color(0.3f, 0.32f, 0.38f, 1f);
            sr.sortingOrder = 2;

            rod = rodObj.AddComponent<CurrentRod>();
            rod.rodLength = 3.8f;
            rod.electronCount = 8;
            rod.electronSpeed = 4.5f;
        }
        return rod;
    }

    private List<CapacitorPlateNode> ConfigureCapacitorPlateTerminals(Vector3 busPos)
    {
        // Posiciones sobre las 4 plataformas laterales del Tilemap:
        // [0]: Plataforma Baja Izquierda, [1]: Plataforma Baja Derecha,
        // [2]: Plataforma Alta Izquierda, [3]: Plataforma Alta Derecha
        Vector2[] nodePositions = new Vector2[]
        {
            new Vector2(-7.0f, 1.75f),
            new Vector2(7.0f, 1.75f),
            new Vector2(-7.0f, 5.75f),
            new Vector2(7.0f, 5.75f)
        };
        float[] initialPolarities = new float[] { +1f, -1f, -1f, +1f };

        ChargeDetector[] existingDetectors = Object.FindObjectsByType<ChargeDetector>();
        Sprite detectorSprite = GetFallbackSprite();
        Material detectorMat = null;

        if (existingDetectors.Length > 0 && existingDetectors[0].TryGetComponent(out SpriteRenderer detSr))
        {
            if (detSr.sprite != null) detectorSprite = detSr.sprite;
            detectorMat = detSr.sharedMaterial;
        }

        // Desactivar los ChargeDetector de prueba para reemplazarlos por los terminales del Capacitor
        for (int i = 0; i < existingDetectors.Length; i++)
        {
            existingDetectors[i].gameObject.SetActive(false);
        }

        List<CapacitorPlateNode> nodes = new List<CapacitorPlateNode>();
        for (int i = 0; i < 4; i++)
        {
            GameObject nodeObj = new GameObject($"CapacitorPlate_Terminal_{i + 1}");
            nodeObj.transform.position = new Vector3(nodePositions[i].x, nodePositions[i].y, 0f);
            nodeObj.transform.localScale = new Vector3(2.2f, 0.36f, 1f);

            SpriteRenderer sr = nodeObj.AddComponent<SpriteRenderer>();
            sr.sprite = detectorSprite;
            if (detectorMat != null) sr.sharedMaterial = detectorMat;
            sr.sortingOrder = 3;

            BoxCollider2D col = nodeObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.1f, 2.4f);
            col.offset = new Vector2(0f, 0.6f);

            CapacitorPlateNode node = nodeObj.AddComponent<CapacitorPlateNode>();
            node.ConfigureNode(i, initialPolarities[i], i < 2, busPos);
            nodes.Add(node);
        }

        return nodes;
    }

    private void ConfigurePolarousBoss(List<CapacitorPlateNode> nodes, CurrentRod centralRod, FieldZone fieldZone)
    {
        // Reutilizar el sprite y material del prefab Emisor o Corruptor de la escena
        EnemyEmisor sceneEmisor = Object.FindAnyObjectByType<EnemyEmisor>();
        EnemyCorruptor sceneCorruptor = Object.FindAnyObjectByType<EnemyCorruptor>();

        Sprite bossSprite = GetFallbackSprite();
        Material bossMat = null;

        if (sceneEmisor != null)
        {
            if (sceneEmisor.TryGetComponent(out SpriteRenderer emSr))
            {
                if (emSr.sprite != null) bossSprite = emSr.sprite;
                bossMat = emSr.sharedMaterial;
            }
            Destroy(sceneEmisor.gameObject);
        }

        if (sceneCorruptor != null)
        {
            // Colocar al Corruptor patrullando el suelo inferior como obstáculo del sistema corrupto
            sceneCorruptor.transform.position = new Vector3(4.5f, -0.4f, 0f);
        }

        GameObject bossObj = new GameObject("POLAROUS_Boss");
        bossObj.transform.position = new Vector3(0f, 6.2f, 0f);
        bossObj.transform.localScale = new Vector3(2.2f, 2.2f, 1f);

        SpriteRenderer bossSr = bossObj.AddComponent<SpriteRenderer>();
        bossSr.sprite = bossSprite;
        if (bossMat != null) bossSr.sharedMaterial = bossMat;
        bossSr.color = new Color(0.3f, 0.75f, 1f, 1f);
        bossSr.sortingOrder = 6;

        CircleCollider2D bossCol = bossObj.AddComponent<CircleCollider2D>();
        bossCol.isTrigger = true;
        bossCol.radius = 0.45f;

        PolarousBossController bossCtrl = bossObj.AddComponent<PolarousBossController>();
        bossCtrl.InitializeBoss(nodes, new Vector2(0f, 5.6f), centralRod, fieldZone);
    }

    private static ChargedObject CreateFallbackChargeTerminal(string name, Vector2 pos, float chargeVal)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = new Vector3(pos.x, pos.y, 0f);
        obj.transform.localScale = new Vector3(1.1f, 1.1f, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = GetFallbackSprite();
        sr.sortingOrder = 3;

        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        obj.AddComponent<BoxCollider2D>();

        ChargedObject co = obj.AddComponent<ChargedObject>();
        co.charge = chargeVal;
        co.effectRadius = 8.5f;
        co.forceStrength = 55f;
        co.isConductor = true;
        co.isFixedSource = true;
        return co;
    }
}
