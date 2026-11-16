using System;
using System.Globalization;
using System.Threading;
using UnityEngine;
#if NET_4_6
using System.IO.Ports;
#endif

/// <summary>
/// Brazo completo (brazo, antebrazo y mano) controlado por 3 BNO085.
///
/// Arduino: sketch BrazoBNO085.ino (manda "Q,w,x,y,z,..." de los 3 sensores).
///
/// MONTAJE DE LOS SENSORES (importante):
///   Pega los 3 sensores con la flecha / eje X del módulo apuntando HACIA LA MANO,
///   del lado de afuera del brazo. Si tu módulo no tiene flechas, elige en
///   "Eje Hacia La Mano" el eje que apunta hacia la mano.
///
/// CALIBRACIÓN (tecla C, y automática al empezar):
///   Estira el brazo AL FRENTE, recto, con la PALMA HACIA ABAJO, y quédate quieto
///   durante la cuenta regresiva.
///
/// Uso: crea un objeto vacío "Brazo" en la escena (NO dentro de la cámara) y
/// agrégale este componente. El brazo y la mano se crean solos al dar Play.
/// </summary>
public class BrazoBNO085 : MonoBehaviour
{
    public enum EjeSensor { X, Y, Z, MenosX, MenosY, MenosZ }

    [Header("Puerto serial")]
    [Tooltip("El mismo COM que usa el Mega en el IDE de Arduino")]
    public string puerto = "COM3";
    public int baudios = 115200;

    [Header("Jugador")]
    [Tooltip("Cámara del jugador (vacío = Camera.main)")]
    public Camera camara;
    [Tooltip("Cuerpo del jugador que gira con el mouse (vacío = se busca el FirstPersonController)")]
    public Transform cuerpo;
    [Tooltip("Dónde está el hombro respecto a la cámara (derecha, arriba, adelante)")]
    public Vector3 posicionHombro = new Vector3(0.20f, -0.28f, 0.0f);
    public bool brazoDerecho = true;

    [Header("Medidas del brazo (metros)")]
    public float largoBrazo = 0.28f;
    public float largoAntebrazo = 0.26f;
    public float grosorBrazo = 0.075f;
    public float grosorAntebrazo = 0.06f;
    public Color colorPiel = new Color(0.93f, 0.75f, 0.62f);

    [Header("Sensores")]
    [Tooltip("Qué eje del módulo apunta hacia la mano cuando está pegado al brazo")]
    public EjeSensor ejeHaciaLaMano = EjeSensor.X;
    public KeyCode teclaCalibrar = KeyCode.C;
    public float segundosCalibracion = 3f;
    [Tooltip("Mayor = más rápido pero con más temblor")]
    [Range(1f, 30f)] public float suavizado = 15f;

    [Tooltip("Muestra en pantalla los ángulos de cada parte del brazo")]
    public bool mostrarDiagnostico = true;

    [Header("Cerrar la mano (FSR)")]
    [Tooltip("Los 2 sensores deben pasar este valor para cerrar la mano (0 a 1023)")]
    public int umbralCerrar = 600;
    [Tooltip("Si cualquiera de los 2 baja de este valor, la mano se abre")]
    public int umbralAbrir = 300;
    [Tooltip("Mantener esta tecla simula apretar los sensores (para probar sin FSR)")]
    public KeyCode teclaCerrarPrueba = KeyCode.G;
    [Tooltip("Qué tan rápido se cierran los dedos")]
    public float velocidadCierre = 8f;

    [Header("Estado (solo lectura)")]
    [SerializeField] int[] presion = new int[2];
    [SerializeField] bool manoCerrada;
    [SerializeField] bool conectado;
    [SerializeField] bool calibrado;
    [SerializeField] int lineasRecibidas;
    [SerializeField] string ultimaLinea = "";
    [SerializeField] bool[] sensorActivo = new bool[3];

    /// <summary>La mano creada por el brazo (para usarla con ManipuladorBloques en modo Mano).</summary>
    public Transform Mano { get; private set; }

    /// <summary>true mientras los 2 FSR están apretados (o la tecla de prueba).</summary>
    public bool ManoCerrada => manoCerrada;

    /// <summary>Valores crudos de los 2 FSR (0 a 1023).</summary>
    public int[] Presion => presion;

    ManoProcedural manoProcedural;

    // Segmentos visuales (el pivote está en la articulación de inicio)
    Transform segBrazo, segAntebrazo, articulacionCodo;

    // Datos del hilo serial
    readonly object candado = new object();
    readonly Quaternion[] qHilo = new Quaternion[3];
    readonly bool[] validoHilo = new bool[3];
    string lineaHilo = "";
    readonly int[] presionHilo = new int[2];
    bool hayPresionHilo;
    int contadorHilo;
    Thread hilo;
    volatile bool leyendo;
#if NET_4_6
    SerialPort serial;
#endif

    // Orientaciones
    readonly Quaternion[] qSensor = new Quaternion[3];       // último dato (ya en ejes de Unity)
    readonly bool[] qValido = new bool[3];
    readonly Quaternion[] alinear = new Quaternion[3];       // giro horizontal del mundo del sensor
    readonly Quaternion[] montaje = new Quaternion[3];       // cómo está pegado el sensor
    readonly Quaternion[] local = new Quaternion[3];         // rotación actual de cada segmento (suavizada)

    float giroCalibracion;   // hacia dónde mirabas (respecto al cuerpo) al calibrar
    float cuentaRegresiva = -1f;
    bool autoCalibrado;

    // Pose antes de calibrar: brazo colgando un poco hacia adelante
    static readonly Quaternion[] poseReposo =
    {
        Quaternion.Euler(70, 0, 0),
        Quaternion.Euler(20, 0, 0),
        Quaternion.Euler(20, 0, 0)
    };

    // ---------------------------------------------------------------- Inicio

    void Start()
    {
        BuscarCamara();

        for (int i = 0; i < 3; i++)
        {
            local[i] = poseReposo[i];
            alinear[i] = Quaternion.identity;
            montaje[i] = Quaternion.identity;
        }

        ConstruirBrazo();
        Conectar();
    }

    void BuscarCamara()
    {
        // Si lo asignado está apagado (por ejemplo el FirstPersonController en modo VR), buscar otro
        if (camara != null && !camara.isActiveAndEnabled) camara = null;
        if (cuerpo != null && !cuerpo.gameObject.activeInHierarchy) cuerpo = null;

        var fpc = FindAnyObjectByType<FirstPersonController>();   // solo encuentra los activos
        if (cuerpo == null && fpc != null) cuerpo = fpc.transform;
        if (camara == null && fpc != null && fpc.playerCamera != null && fpc.playerCamera.isActiveAndEnabled)
            camara = fpc.playerCamera;
        if (camara == null) camara = Camera.main;
        if (camara == null) camara = FindAnyObjectByType<Camera>();

        // En VR el "cuerpo" es el XR Origin, que es la raíz de la cámara del visor
        if (cuerpo == null && camara != null && camara.transform.root != camara.transform)
            cuerpo = camara.transform.root;
        if (camara != null)
            Debug.Log($"[Brazo] Cámara: '{camara.name}'  Cuerpo: '{(cuerpo != null ? cuerpo.name : "ninguno")}'");
        if (camara == null)
            Debug.LogWarning("[Brazo] No encontré ninguna cámara. Arrastra PlayerCamera al campo 'Camara'.");
    }

    void ConstruirBrazo()
    {
        Material piel = new Material(ShaderLit()) { name = "PielBrazo" };
        piel.color = colorPiel;
        if (piel.HasProperty("_BaseColor")) piel.SetColor("_BaseColor", colorPiel);

        segBrazo = CrearSegmento("BrazoSuperior", largoBrazo, grosorBrazo, piel);
        segAntebrazo = CrearSegmento("Antebrazo", largoAntebrazo, grosorAntebrazo, piel);

        // Bolita en el codo para que no se vea el hueco
        articulacionCodo = Pieza(PrimitiveType.Sphere, "Codo", transform,
                                 Vector3.zero, Vector3.one * grosorBrazo * 0.95f, piel);

        // Mano: usamos ManoProcedural (dedos hacia +Z, palma hacia abajo)
        GameObject goMano = new GameObject("ManoBrazo");
        goMano.transform.SetParent(transform, false);
        var mp = goMano.AddComponent<ManoProcedural>();
        mp.colorPiel = colorPiel;
        // Con la palma hacia abajo, el pulgar de la mano derecha queda a la izquierda
        mp.manoDerecha = !brazoDerecho;
        mp.Construir();
        Mano = goMano.transform;
        manoProcedural = mp;
    }

    Transform CrearSegmento(string nombre, float largo, float grosor, Material mat)
    {
        Transform pivote = new GameObject(nombre).transform;
        pivote.SetParent(transform, false);
        // Cápsula acostada a lo largo de +Z, empezando en el pivote
        Transform cap = Pieza(PrimitiveType.Capsule, nombre + "_Malla", pivote,
                              new Vector3(0, 0, largo * 0.5f),
                              new Vector3(grosor, largo * 0.5f + grosor * 0.25f, grosor), mat);
        cap.localRotation = Quaternion.Euler(90, 0, 0);
        return pivote;
    }

    static Transform Pieza(PrimitiveType tipo, string nombre, Transform padre,
                           Vector3 pos, Vector3 escala, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        Destroy(go.GetComponent<Collider>());   // sin colisiones: no estorba a los bloques
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Shader ShaderLit()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        return s != null ? s : Shader.Find("Standard");
    }

    // ---------------------------------------------------------------- Serial

    void Conectar()
    {
#if NET_4_6
        try
        {
            serial = new SerialPort(puerto, baudios)
            {
                ReadTimeout = 200,
                NewLine = "\n",
                DtrEnable = true,
                RtsEnable = true
            };
            serial.Open();
            leyendo = true;
            hilo = new Thread(LeerSerial) { IsBackground = true };
            hilo.Start();
            conectado = true;
            Debug.Log($"[Brazo] Conectado a {puerto}");
        }
        catch (Exception e)
        {
            conectado = false;
            Debug.LogWarning($"[Brazo] No se pudo abrir {puerto}: {e.Message}. " +
                             "Revisa el COM, cierra el Monitor Serie y desactiva otros scripts que usen el puerto (LectorMPU6050).");
        }
#else
        Debug.LogWarning("[Brazo] Cambia Api Compatibility Level a .NET Framework (Project Settings > Player).");
#endif
    }

#if NET_4_6
    void LeerSerial()
    {
        float[] v = new float[12];
        while (leyendo)
        {
            try
            {
                string linea = serial.ReadLine().Trim();
                lock (candado) { lineaHilo = linea; contadorHilo++; }
                if (!linea.StartsWith("Q,")) continue;

                string[] p = linea.Split(',');
                if (p.Length != 13 && p.Length != 15) continue;
                bool ok = true;
                for (int k = 0; k < 12 && ok; k++)
                    ok = float.TryParse(p[k + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v[k]);
                if (!ok) continue;

                lock (candado)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float w = v[i * 4], x = v[i * 4 + 1], y = v[i * 4 + 2], z = v[i * 4 + 3];
                        float norma = w * w + x * x + y * y + z * z;
                        validoHilo[i] = norma > 0.5f;           // 0,0,0,0 = desconectado
                        if (validoHilo[i]) qHilo[i] = DeBNO(w, x, y, z);
                    }
                    if (p.Length == 15 &&
                        int.TryParse(p[13], NumberStyles.Integer, CultureInfo.InvariantCulture, out int f0) &&
                        int.TryParse(p[14], NumberStyles.Integer, CultureInfo.InvariantCulture, out int f1))
                    {
                        presionHilo[0] = f0;
                        presionHilo[1] = f1;
                        hayPresionHilo = true;
                    }
                }
            }
            catch (TimeoutException) { }
            catch (Exception) { if (!leyendo) break; Thread.Sleep(100); }
        }
    }
#endif

    /// <summary>
    /// Convierte un cuaternión del BNO085 (mano derecha, Z arriba)
    /// a Unity (mano izquierda, Y arriba): se intercambian Y y Z.
    /// </summary>
    static Quaternion DeBNO(float w, float x, float y, float z)
    {
        return new Quaternion(-x, -z, -y, w);
    }

    Vector3 EjeEnUnity()
    {
        // Mismo intercambio Y<->Z que en DeBNO
        switch (ejeHaciaLaMano)
        {
            case EjeSensor.X: return Vector3.right;
            case EjeSensor.Y: return Vector3.forward;
            case EjeSensor.Z: return Vector3.up;
            case EjeSensor.MenosX: return Vector3.left;
            case EjeSensor.MenosY: return Vector3.back;
            default: return Vector3.down;
        }
    }

    // ---------------------------------------------------------------- Cada cuadro

    void Update()
    {
        // Copiar datos del hilo
        lock (candado)
        {
            for (int i = 0; i < 3; i++)
            {
                qValido[i] = validoHilo[i];
                if (validoHilo[i]) qSensor[i] = qHilo[i];
                sensorActivo[i] = validoHilo[i];
            }
            ultimaLinea = lineaHilo;
            lineasRecibidas = contadorHilo;
            if (hayPresionHilo)
            {
                presion[0] = presionHilo[0];
                presion[1] = presionHilo[1];
            }
        }

        ActualizarCierre();

        // Calibración automática la primera vez que llegan los 3 sensores
        if (!autoCalibrado && qValido[0] && qValido[1] && qValido[2])
        {
            autoCalibrado = true;
            cuentaRegresiva = segundosCalibracion;
        }

        if (Input.GetKeyDown(teclaCalibrar)) cuentaRegresiva = segundosCalibracion;

        if (cuentaRegresiva > 0f)
        {
            cuentaRegresiva -= Time.deltaTime;
            if (cuentaRegresiva <= 0f) Calibrar();
        }
    }

    void ActualizarCierre()
    {
        bool tecla = Input.GetKey(teclaCerrarPrueba);
        bool ambosApretados = presion[0] >= umbralCerrar && presion[1] >= umbralCerrar;
        bool algunoSuelto = presion[0] < umbralAbrir || presion[1] < umbralAbrir;

        // Dos umbrales: así no "parpadea" cuando la presión queda en medio
        if (!manoCerrada && (ambosApretados || tecla)) manoCerrada = true;
        else if (manoCerrada && algunoSuelto && !tecla) manoCerrada = false;

        if (manoProcedural != null)
            manoProcedural.cierre = Mathf.MoveTowards(manoProcedural.cierre,
                manoCerrada ? 1f : 0f, velocidadCierre * Time.deltaTime);
    }

    void Calibrar()
    {
        Vector3 eje = EjeEnUnity();
        int ok = 0;
        for (int i = 0; i < 3; i++)
        {
            if (!qValido[i]) continue;
            Quaternion qc = qSensor[i];

            // Hacia dónde apunta el brazo según el sensor (solo la parte horizontal)
            Vector3 dir = qc * eje;
            dir.y = 0f;
            float giro = dir.sqrMagnitude > 1e-4f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : 0f;

            alinear[i] = Quaternion.Euler(0f, -giro, 0f);             // ese rumbo pasa a ser "al frente"
            montaje[i] = Quaternion.Inverse(alinear[i] * qc);          // en esta pose el segmento = al frente
            ok++;
        }
        // "Al frente" = hacia donde mira la cabeza en este momento (importante en VR)
        if (camara != null)
        {
            float yawCuerpo = cuerpo != null ? cuerpo.eulerAngles.y : camara.transform.eulerAngles.y;
            giroCalibracion = Mathf.DeltaAngle(yawCuerpo, camara.transform.eulerAngles.y);
        }
        calibrado = ok > 0;
        Debug.Log($"[Brazo] Calibrado ({ok}/3 sensores)");
    }

    void LateUpdate()
    {
        if (camara == null || !camara.isActiveAndEnabled) { BuscarCamara(); if (camara == null) return; }

        // Base: solo el giro horizontal del jugador (mirar arriba/abajo no mueve el hombro)
        // Orientación del brazo: el cuerpo (FirstPersonController o XR Origin)
        float yawCuerpo = (cuerpo != null ? cuerpo : camara.transform).eulerAngles.y;
        Quaternion baseCuerpo = Quaternion.Euler(0f, yawCuerpo + (calibrado ? giroCalibracion : 0f), 0f);

        // Posición del hombro: sigue el giro horizontal de la cabeza
        Quaternion giroCabeza = Quaternion.Euler(0f, camara.transform.eulerAngles.y, 0f);
        Vector3 offset = posicionHombro;
        if (!brazoDerecho) offset.x = -offset.x;
        Vector3 hombro = camara.transform.position + giroCabeza * offset;

        float t = 1f - Mathf.Exp(-suavizado * Time.deltaTime);
        for (int i = 0; i < 3; i++)
        {
            Quaternion objetivo = (calibrado && qValido[i])
                ? alinear[i] * qSensor[i] * montaje[i]
                : (calibrado ? local[i] : poseReposo[i]);
            local[i] = Quaternion.Slerp(local[i], objetivo, t);
        }

        Quaternion rBrazo = baseCuerpo * local[0];
        Quaternion rAntebrazo = baseCuerpo * local[1];
        Quaternion rMano = baseCuerpo * local[2];

        Vector3 codo = hombro + rBrazo * Vector3.forward * largoBrazo;
        Vector3 muneca = codo + rAntebrazo * Vector3.forward * largoAntebrazo;

        segBrazo.SetPositionAndRotation(hombro, rBrazo);
        segAntebrazo.SetPositionAndRotation(codo, rAntebrazo);
        articulacionCodo.position = codo;
        Mano.SetPositionAndRotation(muneca, rMano);
    }

    // ---------------------------------------------------------------- Pantalla

    void OnGUI()
    {
        var estilo = new GUIStyle(GUI.skin.label) { fontSize = 16 };
        string s = conectado ? $"Brazo: {puerto}  " : "Brazo: SIN CONEXION  ";
        s += $"Sensores: {(sensorActivo[0] ? "H" : "-")}{(sensorActivo[1] ? "C" : "-")}{(sensorActivo[2] ? "M" : "-")}  ";
        s += calibrado ? "(C = recalibrar)  " : "(sin calibrar)  ";
        s += $"FSR: {presion[0]} / {presion[1]}  " + (manoCerrada ? "MANO CERRADA" : "mano abierta");
        GUI.Label(new Rect(12, Screen.height - 34, 1100, 30), s, estilo);

        if (mostrarDiagnostico)
        {
            string d = "";
            string[] nombres = { "Brazo", "Antebrazo", "Mano" };
            for (int i = 0; i < 3; i++)
            {
                Vector3 e = local[i].eulerAngles;
                d += $"{nombres[i]}: {e.x:0} {e.y:0} {e.z:0}   ";
            }
            if (segBrazo != null)
                d += $"\nHombro en {segBrazo.position}  Mano en {Mano.position}";
            GUI.Label(new Rect(12, Screen.height - 80, 900, 46), d, estilo);
        }

        if (cuentaRegresiva > 0f)
        {
            var grande = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 90),
                $"Estira el brazo AL FRENTE, palma ABAJO\nCalibrando en {Mathf.CeilToInt(cuentaRegresiva)}...",
                grande);
        }
    }

    // ---------------------------------------------------------------- Cierre

    void OnDisable() => Cerrar();
    void OnApplicationQuit() => Cerrar();

    void Cerrar()
    {
        leyendo = false;
#if NET_4_6
        try { if (serial != null && serial.IsOpen) serial.Close(); } catch { }
#endif
        if (hilo != null && hilo.IsAlive) hilo.Join(300);
        conectado = false;
    }
}
