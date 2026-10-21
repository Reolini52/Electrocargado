using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Constructor y configurador del Nivel 3 — Potencial Eléctrico, Gradientes y Puzzles con Llaves Dinámicas.
/// Utiliza todos los prefabs originales de la escena (player.prefab animado, Grid.prefab Tilemap,
/// Billboard, DialogueSystem, ChargedObject, DynamicChargedObject, ChargeDetector, Door,
/// Corruptor, Emisor y Gandalf) y construye las 3 secciones pedagógicas:
/// 1. Exposición (Diálogos + Billboards sobre Potencial Eléctrico V = kQ/r y Capacitancia).
/// 2. Experimentación (Gradientes de Potencial que afectan la velocidad, Puzzles con Llaves Dinámicas en Receptores ⊕/⊖, y combate contra Corruptor + Emisor).
/// 3. Verificación (Firewall Gandalf evaluando zonas de mayor/menor potencial eléctrico hacia Level 4).
/// </summary>
public class Level3Builder : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterLevel3Listener()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        CheckAndBuildIfLevel3(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndBuildIfLevel3(scene);
    }

    private static void CheckAndBuildIfLevel3(Scene scene)
    {
        if (scene.name != "Level 3" && scene.name != "Level3") return;
        if (Object.FindAnyObjectByType<Level3Builder>() != null) return;

        GameObject builderObj = new GameObject("[Level3_PotentialAndPuzzles_Builder]");
        builderObj.AddComponent<Level3Builder>().BuildLevel3();
    }

    public void BuildLevel3()
    {
        // 1. Pintar el mapa del Nivel 3 en el Tilemap real (Grid.prefab)
        PaintLevel3Tilemap();

        // 2. Configurar a THEIA (player.prefab original animado) y la Cámara
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerObj.transform.position = new Vector3(-9.5f, -0.4f, 0f);
            if (playerObj.TryGetComponent(out ChargeResource cr))
            {
                cr.charge = 0.7f;
            }
        }

        Camera mainCam = Camera.main;
        if (mainCam != null && playerObj != null)
        {
            mainCam.orthographicSize = 5.0f;
            if (mainCam.TryGetComponent(out CameraFollow follow))
            {
                follow.target = playerObj.transform;
            }
        }

        // 3. Configurar Sección 1: Exposición (Diálogos y Billboards de Potencial Eléctrico y Capacitancia)
        SetupExpositionDialoguesAndBillboards();

        // 4. Configurar Fuentes de Carga Fijas (+Q y -Q) a lo largo del nivel
        SetupChargeSources();

        // 5. Configurar Gradientes de Potencial Eléctrico visibles en el mapa (ElectricPotentialZone)
        SetupPotentialGradients();

        // 6. Configurar Puzzles de Receptores (⊕ y ⊖) que requieren Objetos Dinámicos como Llaves
        SetupReceptorKeyPuzzles();

        // 7. Configurar Ambos Tipos de Enemigos (Corruptor + Emisor) y Peligros (VoidKill / Spikes)
        SetupEnemiesAndHazards();

        // 8. Configurar el Firewall Gandalf de Cierre (Evaluación de Mayor/Menor Potencial -> Level 4)
        SetupClosingGandalf();
    }

    private void PaintLevel3Tilemap()
    {
        Tilemap tilemap = Object.FindAnyObjectByType<Tilemap>();
        if (tilemap == null) return;

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

        // Pared inicial izquierda (x = -13..-12)
        for (int y = -4; y <= 6; y++)
        {
            tilemap.SetTile(new Vector3Int(-13, y, 0), topCenter);
            tilemap.SetTile(new Vector3Int(-12, y, 0), topCenter);
        }

        // Tramo 1: Zona de Exposición + Puzzle 1 (x = -11 a 21, suelo en y = -2)
        PaintGroundSection(tilemap, -11, 21, -2, topLeft, topCenter, topRight, fillLeft, fillCenter, fillRight);

        // Techo/Marco sobre Puerta 1 (x = 18..20, y = 2..5) para que no se pueda saltar por encima sin abrir la puerta
        for (int x = 18; x <= 20; x++)
        {
            for (int y = 2; y <= 6; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), topCenter);
            }
        }

        // Tramo 2: Foso de Combate y Plataformas con Gradiente (x = 22 a 36)
        // Suelo bajo del foso (x = 22 a 30, y = -5)
        PaintGroundSection(tilemap, 22, 30, -5, topLeft, topCenter, topRight, fillLeft, fillCenter, fillRight);
        // Plataforma media 1 (Corruptor) en x = 23..28, y = 0
        PaintPlatform(tilemap, 23, 28, 0, topLeft, topCenter, topRight);
        // Plataforma alta (Emisor + Fuente -Q) en x = 30..36, y = 2
        PaintPlatform(tilemap, 30, 36, 2, topLeft, topCenter, topRight);

        // Tramo 3: Puzzle 2 con Gradiente + Salida de Gandalf (x = 35 a 58, suelo en y = -2)
        PaintGroundSection(tilemap, 35, 58, -2, topLeft, topCenter, topRight, fillLeft, fillCenter, fillRight);

        // Techo/Marco sobre Puerta 2 (x = 48..50, y = 2..6)
        for (int x = 48; x <= 50; x++)
        {
            for (int y = 2; y <= 6; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), topCenter);
            }
        }

        // Pared final derecha detrás de Gandalf (x = 58..59)
        for (int y = -4; y <= 6; y++)
        {
            tilemap.SetTile(new Vector3Int(58, y, 0), topCenter);
            tilemap.SetTile(new Vector3Int(59, y, 0), topCenter);
        }

        tilemap.RefreshAllTiles();

        if (tilemap.TryGetComponent(out CompositeCollider2D composite))
        {
            composite.GenerateGeometry();
        }
        Physics2D.SyncTransforms();
    }

    private static void PaintPlatform(Tilemap map, int xMin, int xMax, int y, TileBase left, TileBase center, TileBase right)
    {
        for (int x = xMin; x <= xMax; x++)
        {
            TileBase t = (x == xMin) ? left : (x == xMax ? right : center);
            map.SetTile(new Vector3Int(x, y, 0), t);
        }
    }

    private static void PaintGroundSection(
        Tilemap map, int xMin, int xMax, int yTop,
        TileBase topLeft, TileBase topCenter, TileBase topRight,
        TileBase fillLeft, TileBase fillCenter, TileBase fillRight)
    {
        PaintPlatform(map, xMin, xMax, yTop, topLeft, topCenter, topRight);
        for (int x = xMin; x <= xMax; x += 2)
        {
            TileBase fill = (x == xMin) ? fillLeft : (x >= xMax - 1 ? fillRight : fillCenter);
            map.SetTile(new Vector3Int(x, yTop - 2, 0), fill);
            map.SetTile(new Vector3Int(x, yTop - 4, 0), fill);
        }
    }

    private void SetupExpositionDialoguesAndBillboards()
    {
        // Configurar Diálogos (DialogueSystem)
        DialogueSystem[] dialogues = Object.FindObjectsByType<DialogueSystem>();
        if (dialogues.Length > 0)
        {
            DialogueSystem d1 = dialogues[0];
            d1.transform.position = new Vector3(-9.0f, -0.4f, 0f);
            d1.triggerRadius = 3.2f;
            d1.triggerOnce = true;
            d1.speakerName = "SYSTEM";
            d1.lines = new string[]
            {
                "SECTOR 3: Potencial Eléctrico (V = k·Q/r) y Capacitancia.",
                "Cerca de una carga positiva (+Q, Azul) el Potencial es MAYOR (+V). Cerca de una negativa (-Q, Roja) el Potencial es MENOR (-V).",
                "Los gradientes visibles en el mapa aceleran o frenan a THEIA según su carga. ¡Inspecciona los paneles con [F]!"
            };

            // Segundo diálogo junto al primer puzzle de llave dinámica
            DialogueSystem d2 = dialogues.Length > 1 ? dialogues[1] : Instantiate(d1);
            d2.name = "DialogueSystem_PuzzleKey";
            d2.transform.position = new Vector3(8.5f, -0.4f, 0f);
            d2.triggerRadius = 3.2f;
            d2.triggerOnce = true;
            d2.lines = new string[]
            {
                "PUZZLE DE LLAVE DINÁMICA: Este receptor ⊕ solo mantiene abierta la compuerta si dejas una esfera cargada encima.",
                "Empuja la esfera móvil con tu rayo [Click Izq] o inyéctale carga [+Q] con [Q + Click Der] hasta posicionarla en el receptor ⊕."
            };

            // Tercer diálogo antes del segundo puzzle y Gandalf
            DialogueSystem d3 = dialogues.Length > 2 ? dialogues[2] : Instantiate(d1);
            d3.name = "DialogueSystem_PreGandalf";
            d3.transform.position = new Vector3(38.0f, -0.4f, 0f);
            d3.triggerRadius = 3.2f;
            d3.triggerOnce = true;
            d3.lines = new string[]
            {
                "ÚLTIMO RECEPTOR ⊖: Transfiere carga negativa (-Q) a la esfera dinámica y llévala al receptor ⊖ contra el gradiente de potencial.",
                "Más adelante te espera el Firewall Gandalf para evaluar dónde es mayor y menor el potencial eléctrico."
            };
        }

        // Configurar Billboards educativos ([F] Inspect)
        Billboard[] billboards = Object.FindObjectsByType<Billboard>();
        if (billboards.Length > 0)
        {
            Billboard b1 = billboards[0];
            b1.transform.position = new Vector3(-5.0f, -0.3f, 0f);
            b1.caption = "POTENCIAL ELÉCTRICO (V = k·Q/r): Mayor potencial (+V) cerca de +Q y Menor potencial (-V) cerca de -Q.";

            Billboard b2 = billboards.Length > 1 ? billboards[1] : Instantiate(b1);
            b2.name = "Billboard_GradientAndKeys";
            b2.transform.position = new Vector3(0.5f, -0.3f, 0f);
            b2.caption = "GRADIENTES Y LLAVES DINÁMICAS: Una carga +q gana velocidad al ir de +V a -V. Usa las esferas móviles como llaves en ⊕ y ⊖.";

            Billboard b3 = billboards.Length > 2 ? billboards[2] : Instantiate(b1);
            b3.name = "Billboard_GandalfReview";
            b3.transform.position = new Vector3(40.0f, -0.3f, 0f);
            b3.caption = "RESUMEN FIREWALL: En un sistema de cargas, la zona de MAYOR potencial rodea a +Q y la de MENOR potencial rodea a -Q.";
        }
    }

    private void SetupChargeSources()
    {
        ChargedObject[] existing = Object.FindObjectsByType<ChargedObject>();
        if (existing.Length == 0) return;

        ChargedObject template = existing[0];

        Vector2[] positions = new Vector2[]
        {
            new Vector2(-2.5f, 1.8f),  // [0] +Q en Zona de Exposición
            new Vector2(9.5f, 2.0f),   // [1] +Q para Puzzle 1 (Llave +Q)
            new Vector2(14.5f, 2.4f),  // [2] -Q para impulsar/atraer en Puzzle 1
            new Vector2(22.5f, 1.8f),  // [3] +Q antes del foso de combate
            new Vector2(31.5f, 4.4f),  // [4] -Q sobre plataforma alta del Emisor
            new Vector2(41.5f, 2.2f),  // [5] -Q para cargar la Llave -Q del Puzzle 2
            new Vector2(45.5f, 2.2f)   // [6] +Q en el extremo alto del gradiente 2
        };
        float[] charges = new float[] { +1f, +1f, -1f, +1f, -1f, -1f, +1f };

        for (int i = 0; i < positions.Length; i++)
        {
            ChargedObject src = i < existing.Length ? existing[i] : Instantiate(template);
            src.name = charges[i] > 0 ? $"Source_Positive_{i}" : $"Source_Negative_{i}";
            src.transform.position = new Vector3(positions[i].x, positions[i].y, 0f);
            src.transform.localRotation = Quaternion.identity;
            src.charge = charges[i];
            src.isConductor = true;
            src.isFixedSource = true;
            src.effectRadius = 7.5f;
            src.forceStrength = 50f;
            src.UpdateVisual();
        }
    }

    private void SetupPotentialGradients()
    {
        FieldZone[] existingFields = Object.FindObjectsByType<FieldZone>();
        Sprite fieldSprite = null;
        Material fieldMat = null;

        if (existingFields.Length > 0)
        {
            if (existingFields[0].TryGetComponent(out SpriteRenderer sr))
            {
                fieldSprite = sr.sprite;
                fieldMat = sr.sharedMaterial;
            }
            for (int i = 0; i < existingFields.Length; i++)
            {
                existingFields[i].gameObject.SetActive(false);
            }
        }

        // Gradiente 1 (Exposición): de +20V (izquierda) a -20V (derecha) -> Acelera cargas +Q hacia la derecha
        CreatePotentialGradientZone(
            "PotentialGradient_1_Exposition",
            new Vector2(3.5f, 0.2f),
            new Vector2(6.5f, 3.2f),
            Vector2.right,
            20f,
            16f,
            fieldSprite,
            fieldMat
        );

        // Gradiente 2 (Combate sobre plataformas): de +25V (derecha) a -25V (izquierda) -> Empuja cargas +Q a la izquierda y acelera -Q a la derecha
        CreatePotentialGradientZone(
            "PotentialGradient_2_CombatBridge",
            new Vector2(28.5f, 2.6f),
            new Vector2(7.5f, 3.8f),
            Vector2.left,
            25f,
            18f,
            fieldSprite,
            fieldMat
        );

        // Gradiente 3 (Puzzle 2): de +30V (derecha) a -30V (izquierda) -> Ayuda a llevar la llave -Q hacia el receptor ⊖
        CreatePotentialGradientZone(
            "PotentialGradient_3_KeyPuzzle2",
            new Vector2(44.2f, 0.2f),
            new Vector2(6.5f, 3.2f),
            Vector2.left,
            30f,
            18f,
            fieldSprite,
            fieldMat
        );
    }

    private static ElectricPotentialZone CreatePotentialGradientZone(
        string name, Vector2 pos, Vector2 size, Vector2 highToLowDir,
        float volts, float force, Sprite spr, Material mat)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = new Vector3(pos.x, pos.y, 0f);
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        if (spr != null)
        {
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            if (mat != null) sr.sharedMaterial = mat;
            sr.color = new Color(0.2f, 0.4f, 0.8f, 0.12f);
            sr.sortingOrder = 0;
        }

        BoxCollider2D box = obj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;

        ElectricPotentialZone zone = obj.AddComponent<ElectricPotentialZone>();
        zone.highToLowPotentialDir = highToLowDir;
        zone.potentialDifferenceVolts = volts;
        zone.speedGradientForce = force;
        zone.equipotentialLineCount = 7;

        return zone;
    }

    private void SetupReceptorKeyPuzzles()
    {
        DynamicChargedObject[] dynObjs = Object.FindObjectsByType<DynamicChargedObject>();
        ChargeDetector[] detectors = Object.FindObjectsByType<ChargeDetector>();

        if (dynObjs.Length == 0 || detectors.Length == 0) return;

        ChargeDetector det1 = detectors[0];
        GameObject door1 = det1.door != null ? det1.door : GameObject.Find("Door");
        if (door1 == null) return;

        // --- PUZZLE 1: Llave Dinámica Positiva (+Q) en Receptor ⊕ abre Door 1 ---
        DynamicChargedObject key1 = dynObjs[0];
        key1.name = "DynamicKey_1_Positive";
        key1.transform.position = new Vector3(11.8f, 0.2f, 0f);
        key1.charge = 0.85f;
        key1.UpdateVisual();

        door1.name = "Door_Puzzle1";
        door1.transform.position = new Vector3(19.0f, 0.1f, 0f);
        door1.transform.localScale = new Vector3(0.45f, 3.2f, 1f);
        door1.SetActive(true);

        det1.name = "Detector_Key1_Positive";
        det1.transform.position = new Vector3(16.2f, -1.32f, 0f);
        det1.requiredCharge = +1f;
        det1.chargeThreshold = 0.25f;
        det1.requireDynamicObjectKey = true;
        det1.door = door1;

        // --- PUZZLE 2: Llave Dinámica Negativa (-Q) en Receptor ⊖ abre Door 2 ---
        DynamicChargedObject key2 = dynObjs.Length > 1 ? dynObjs[1] : Instantiate(key1);
        key2.name = "DynamicKey_2_Negative";
        key2.transform.position = new Vector3(42.0f, 0.2f, 0f);
        key2.charge = -0.85f;
        key2.UpdateVisual();

        GameObject door2 = Instantiate(door1);
        door2.name = "Door_Puzzle2";
        door2.transform.position = new Vector3(49.0f, 0.1f, 0f);
        door2.transform.localScale = new Vector3(0.45f, 3.2f, 1f);
        door2.SetActive(true);

        ChargeDetector det2 = detectors.Length > 1 ? detectors[1] : Instantiate(det1);
        det2.name = "Detector_Key2_Negative";
        det2.transform.position = new Vector3(46.5f, -1.32f, 0f);
        det2.requiredCharge = -1f;
        det2.chargeThreshold = 0.25f;
        det2.requireDynamicObjectKey = true;
        det2.door = door2;
    }

    private void SetupEnemiesAndHazards()
    {
        // 1. Enemigo Corruptor en la plataforma media de la sección de combate
        EnemyCorruptor corruptor = Object.FindAnyObjectByType<EnemyCorruptor>();
        if (corruptor != null)
        {
            corruptor.transform.position = new Vector3(25.5f, 1.2f, 0f);
        }

        // 2. Enemigo Emisor en la plataforma alta de la sección de combate
        EnemyEmisor emisor = Object.FindAnyObjectByType<EnemyEmisor>();
        if (emisor != null)
        {
            emisor.transform.position = new Vector3(33.5f, 3.3f, 0f);
        }
    }

    private void SetupClosingGandalf()
    {
        GandalfNPC gandalf = Object.FindAnyObjectByType<GandalfNPC>();
        if (gandalf == null) return;

        gandalf.transform.position = new Vector3(54.0f, 0.35f, 0f);
        gandalf.triggerRadius = 3.2f;
        gandalf.nextSceneName = "Level 4";

        gandalf.questions = new TrialQuestion[]
        {
            new TrialQuestion
            {
                question = "1. Dado un sistema con una carga positiva (+Q) y una negativa (-Q), ¿dónde se ubica la zona de MAYOR potencial eléctrico (+V)?",
                options = new string[]
                {
                    "A) En la región más cercana a la carga negativa (-Q)",
                    "B) En la región más cercana a la carga positiva (+Q)",
                    "C) Exactamente a mitad de camino entre ambas cargas",
                    "D) El potencial es cero en todo el mapa"
                },
                correctAnswer = 1,
                pedagogicalHint = "Recuerda que V = k·Q/r: una carga fuente positiva (+Q) genera potencial positivo (+V) que crece al acercarte a ella."
            },
            new TrialQuestion
            {
                question = "2. En ese mismo sistema de cargas, ¿dónde se encuentra la zona de MENOR potencial eléctrico (-V)?",
                options = new string[]
                {
                    "A) Alrededor de la carga positiva (+Q)",
                    "B) Sobre cualquier plataforma aislante",
                    "C) En la región más cercana a la carga negativa (-Q)",
                    "D) Donde no existen cargas eléctricas"
                },
                correctAnswer = 2,
                pedagogicalHint = "Como V = k·Q/r conserva el signo de la carga Q, las zonas cercanas a una carga negativa (-Q) tienen el menor potencial (-V)."
            },
            new TrialQuestion
            {
                question = "3. Si THEIA tiene carga positiva (+q) y recorre un gradiente desde Mayor Potencial (+V) hacia Menor Potencial (-V), ¿qué le ocurre?",
                options = new string[]
                {
                    "A) El campo eléctrico realiza trabajo positivo y aumenta su velocidad",
                    "B) Pierde toda su velocidad y queda inmóvil",
                    "C) Su carga cambia de signo espontáneamente",
                    "D) La capacitancia del suelo se anula"
                },
                correctAnswer = 0,
                pedagogicalHint = "El campo eléctrico apunta de mayor a menor potencial (E = -∇V), acelerando a las cargas positivas (+q) en esa dirección."
            }
        };
    }
}
