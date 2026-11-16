using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Poner, agarrar, mover, girar, soltar y borrar bloques.
///
/// Controles con mouse (modo de prueba):
///   Mantener clic IZQUIERDO -> agarrar el bloque que apuntas (soltar = lo sueltas)
///   Clic DERECHO            -> poner un bloque nuevo donde apuntas
///   1, 2, 3...              -> elegir el tipo de bloque (block, ladrillo...)
///   Rueda del mouse         -> acercar / alejar el bloque que sostienes
///   T                       -> girar 90° el bloque que sostienes
///   Clic CENTRAL o X        -> borrar el bloque que apuntas
///
/// Con el guante: asigna el objeto Brazo en "Brazo". Al apretar los 2 FSR
/// (o mantener G) la mano se cierra y agarra el bloque que esté junto a la palma;
/// al soltar, el bloque se suelta.
/// </summary>
public class ManipuladorBloques : MonoBehaviour
{
    public enum ModoAgarre { Mira, Mano }

    [System.Serializable]
    public class TipoBloque
    {
        public string nombre = "Bloque";
        [Tooltip("Modelo (FBX o prefab)")]
        public GameObject modelo;
        [Tooltip("Textura de color base (opcional). Si la dejas vacía usa el material del modelo")]
        public Texture2D textura;
        public float masa = 2f;

        [System.NonSerialized] public Material material;
    }

    [Header("Referencias")]
    [Tooltip("Cámara desde la que se apunta (vacío = Camera.main)")]
    public Camera camara;
    [Tooltip("Solo para modo Mano: el objeto de la mano (ManoJugador)")]
    public Transform mano;
    public ModoAgarre modo = ModoAgarre.Mira;
    [Tooltip("Brazo con sensores. Si lo asignas, se agarra con la mano y los FSR")]
    public BrazoBNO085 brazo;

    [Header("Tipos de bloque (teclas 1, 2, 3...)")]
    public TipoBloque[] tipos = new TipoBloque[0];
    public int tipoActual = 0;

    [Header("Colocación")]
    [Tooltip("Paso de la cuadrícula en el piso, en metros")]
    public float cuadricula = 0.05f;
    [Tooltip("Al apilar, permite desplazar medio bloque (como en un muro de ladrillos)")]
    public bool permitirMedioBloque = true;
    [Tooltip("Tamaño del cubo si no hay ningún tipo de bloque")]
    public float tamanoCubo = 0.25f;

    [Header("Agarre")]
    public float alcance = 4f;
    [Tooltip("Radio para agarrar en modo Mano")]
    public float radioMano = 0.15f;
    public float distanciaInicial = 1.2f;
    public float distanciaMin = 0.6f;
    public float distanciaMax = 3f;
    [Tooltip("Qué tan fuerte sigue el bloque al punto de agarre")]
    public float fuerzaSeguir = 20f;
    public float fuerzaGiro = 15f;
    [Tooltip("Si el bloque queda atorado más lejos que esto, se suelta")]
    public float distanciaRuptura = 1.2f;
    public float velocidadMaxSoltar = 6f;

    [Header("Controles de mouse")]
    public bool usarMouse = true;
    public bool mostrarAyuda = true;

    public bool Sosteniendo => sostenido != null;

    BloqueAgarrable sostenido;
    BloqueAgarrable apuntado;
    float distancia;
    Quaternion rotacionRelativa;
    Collider[] collidersJugador = new Collider[0];
    readonly List<Collider> collidersSostenido = new List<Collider>();
    CollisionDetectionMode deteccionOriginal;
    Material materialCubo;
    bool manoCerradaAntes;
    Vector3 offsetAgarre;   // dónde quedó el bloque respecto a la palma al agarrarlo

    void Start()
    {
        if (camara == null) camara = Camera.main;
        distancia = distanciaInicial;

        // El controlador usa clic derecho para zoom; lo apagamos para poner bloques
        var fpc = FindAnyObjectByType<FirstPersonController>();
        if (fpc != null)
        {
            if (usarMouse) fpc.enableZoom = false;
            collidersJugador = fpc.GetComponentsInChildren<Collider>();
        }
    }

    void Update()
    {
        // En VR la cámara del FirstPersonController se apaga: usar la del visor
        if (camara == null || !camara.isActiveAndEnabled) camara = Camera.main;
        if (camara == null) return;

        // Guante: cerrar la mano = agarrar, abrirla = soltar
        if (brazo != null)
        {
            if (brazo.Mano != null)
            {
                mano = brazo.Mano;
                modo = ModoAgarre.Mano;
            }
            bool cerrada = brazo.ManoCerrada;
            if (cerrada && !manoCerradaAntes) Agarrar();
            else if (!cerrada && manoCerradaAntes) Soltar();
            manoCerradaAntes = cerrada;
        }

        ActualizarApuntado();
        if (usarMouse) LeerMouse();
    }

    // ---------------------------------------------------------------- Entrada

    void LeerMouse()
    {
        if (Input.GetMouseButtonDown(0)) Agarrar();
        if (Input.GetMouseButtonUp(0)) Soltar();

        if (Input.GetMouseButtonDown(1) && !Sosteniendo) PonerBloque();

        if (Input.GetMouseButtonDown(2) || Input.GetKeyDown(KeyCode.X)) BorrarApuntado();

        for (int i = 0; i < Mathf.Min(tipos.Length, 9); i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) tipoActual = i;

        if (Sosteniendo)
        {
            float rueda = Input.mouseScrollDelta.y;
            if (Mathf.Abs(rueda) > 0.01f)
                distancia = Mathf.Clamp(distancia + rueda * 0.15f, distanciaMin, distanciaMax);

            if (Input.GetKeyDown(KeyCode.T))
                rotacionRelativa = Quaternion.Euler(0, 90, 0) * rotacionRelativa;
        }
    }

    // ---------------------------------------------------------------- API pública

    /// <summary>Agarra el bloque apuntado (modo Mira) o el más cercano a la mano (modo Mano).</summary>
    public void Agarrar()
    {
        if (Sosteniendo) return;

        BloqueAgarrable objetivo = modo == ModoAgarre.Mano ? BuscarCercaDeMano() : apuntado;
        if (objetivo == null) return;

        sostenido = objetivo;
        sostenido.Sostenido = true;
        sostenido.Resaltar(false);

        Rigidbody rb = sostenido.Cuerpo;
        rb.useGravity = false;
        rb.linearDamping = 5f;
        rb.angularDamping = 5f;
        deteccionOriginal = rb.collisionDetectionMode;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.WakeUp();

        if (modo == ModoAgarre.Mira)
            distancia = Mathf.Clamp(Vector3.Distance(camara.transform.position, sostenido.CentroMundo),
                                    distanciaMin, distanciaMax);

        rotacionRelativa = Quaternion.Inverse(ReferenciaRotacion().rotation) * rb.rotation;
        if (modo == ModoAgarre.Mano && mano != null)
            offsetAgarre = Quaternion.Inverse(mano.rotation) * (sostenido.CentroMundo - PuntoPalma());

        // Que el bloque no empuje al jugador mientras lo cargas
        collidersSostenido.Clear();
        collidersSostenido.AddRange(sostenido.GetComponentsInChildren<Collider>());
        IgnorarJugador(true);
    }

    /// <summary>Suelta el bloque que se está sosteniendo.</summary>
    public void Soltar()
    {
        if (!Sosteniendo) return;

        Rigidbody rb = sostenido.Cuerpo;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.collisionDetectionMode = deteccionOriginal;
        rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, velocidadMaxSoltar);

        IgnorarJugador(false);
        sostenido.Sostenido = false;
        sostenido = null;
    }

    /// <summary>Pone un bloque nuevo del tipo actual donde apunta la cámara.</summary>
    public void PonerBloque()
    {
        if (!Physics.Raycast(camara.transform.position, camara.transform.forward,
                             out RaycastHit hit, alcance, ~0, QueryTriggerInteraction.Ignore))
        {
            Debug.Log($"[Bloques] No apuntas a nada dentro de {alcance} m");
            return;
        }

        // Crear el bloque primero (desactivado) para conocer sus medidas
        GameObject go = CrearBloque(tipoActual);
        BloqueAgarrable nuevo = go.GetComponent<BloqueAgarrable>();
        Vector3 tam = nuevo.tamano;

        Vector3 centro;
        Quaternion rotacion;

        BloqueAgarrable otro = hit.collider.GetComponentInParent<BloqueAgarrable>();
        if (otro != null && !otro.Sostenido)
        {
            // Pegado a la cara del bloque que apuntas, con su misma orientación
            Transform t = otro.transform;
            rotacion = t.rotation;
            Vector3 n = RedondearAEje(Quaternion.Inverse(rotacion) * hit.normal);
            Vector3 separacion = Vector3.Scale(n, (otro.tamano + tam) * 0.5f);

            // Desplazamiento sobre la cara (medio bloque, como un muro de ladrillos)
            Vector3 desplazamiento = Vector3.zero;
            if (permitirMedioBloque)
            {
                Vector3 local = Quaternion.Inverse(rotacion) * (hit.point - otro.CentroMundo);
                for (int eje = 0; eje < 3; eje++)
                {
                    if (Mathf.Abs(n[eje]) > 0.5f) continue;
                    float paso = tam[eje] * 0.5f;
                    float valor = Mathf.Round(local[eje] / paso) * paso;
                    desplazamiento[eje] = Mathf.Clamp(valor, -paso, paso);
                }
            }
            centro = otro.CentroMundo + rotacion * (separacion + desplazamiento);
        }
        else
        {
            // Sobre el piso o cualquier superficie
            float giro = Mathf.Round(camara.transform.eulerAngles.y / 90f) * 90f;
            rotacion = Quaternion.Euler(0, giro, 0);
            centro = hit.point + Vector3.up * (tam.y * 0.5f + 0.002f);
            if (cuadricula > 0.001f)
            {
                centro.x = Mathf.Round(centro.x / cuadricula) * cuadricula;
                centro.z = Mathf.Round(centro.z / cuadricula) * cuadricula;
            }
        }

        // ¿Hay espacio libre?
        Vector3 medio = Vector3.Max(tam * 0.5f - Vector3.one * 0.01f, Vector3.one * 0.005f);
        Collider[] choques = Physics.OverlapBox(centro, medio, rotacion, ~0, QueryTriggerInteraction.Ignore);
        if (choques.Length > 0)
        {
            Debug.Log($"[Bloques] No hay espacio para '{go.name}' ({tam} m): choca con '{choques[0].name}'");
            Destroy(go);
            return;
        }

        go.transform.SetPositionAndRotation(centro - rotacion * nuevo.centroLocal, rotacion);
        go.SetActive(true);
        Debug.Log($"[Bloques] Puesto '{go.name}' de {tam.x:0.000} x {tam.y:0.000} x {tam.z:0.000} m " +
                  $"en {centro} (apuntando a '{hit.collider.name}')");
    }

    public void BorrarApuntado()
    {
        if (apuntado == null || apuntado.Sostenido) return;
        Destroy(apuntado.gameObject);
        apuntado = null;
    }

    // ---------------------------------------------------------------- Física

    void FixedUpdate()
    {
        if (!Sosteniendo) return;

        Rigidbody rb = sostenido.Cuerpo;
        Transform referencia = ReferenciaRotacion();

        Vector3 punto = modo == ModoAgarre.Mano && mano != null
            ? PuntoPalma() + mano.rotation * offsetAgarre
            : camara.transform.position + camara.transform.forward * distancia;

        // Posición: el centro del bloque va hacia el punto de agarre
        Vector3 diferencia = punto - sostenido.CentroMundo;
        if (diferencia.magnitude > distanciaRuptura)
        {
            Soltar();   // se atoró en algo
            return;
        }
        rb.linearVelocity = diferencia * fuerzaSeguir;

        // Rotación: girar hacia la rotación objetivo
        Quaternion objetivo = referencia.rotation * rotacionRelativa;
        Quaternion delta = objetivo * Quaternion.Inverse(rb.rotation);
        delta.ToAngleAxis(out float angulo, out Vector3 eje);
        if (angulo > 180f) angulo -= 360f;
        if (Mathf.Abs(angulo) > 0.01f && !float.IsNaN(eje.x))
            rb.angularVelocity = eje.normalized * (angulo * Mathf.Deg2Rad * fuerzaGiro);
        else
            rb.angularVelocity = Vector3.zero;
    }

    // ---------------------------------------------------------------- Creación

    /// <summary>
    /// Crea un bloque del tipo indicado y lo deja listo en esa posición.
    /// La usa GeneradorBloques para llenar el mapa.
    /// </summary>
    public GameObject CrearBloqueEn(int indiceTipo, Vector3 centro, Quaternion rotacion)
    {
        GameObject go = CrearBloque(indiceTipo);
        BloqueAgarrable ba = go.GetComponent<BloqueAgarrable>();
        go.transform.SetPositionAndRotation(centro - rotacion * ba.centroLocal, rotacion);
        go.SetActive(true);
        return go;
    }

    /// <summary>Medidas (largo, alto, ancho) de un tipo de bloque, en metros.</summary>
    public Vector3 MedidasDeTipo(int indiceTipo)
    {
        GameObject go = CrearBloque(indiceTipo);
        Vector3 tam = go.GetComponent<BloqueAgarrable>().tamano;
        Destroy(go);
        return tam;
    }

    /// <summary>Crea un bloque del tipo indicado, DESACTIVADO, en el origen.</summary>
    GameObject CrearBloque(int indiceTipo)
    {
        TipoBloque tipo = (tipos.Length > 0)
            ? tipos[Mathf.Clamp(indiceTipo, 0, tipos.Length - 1)]
            : null;

        GameObject go;
        float masa = 2f;
        Vector3 tamano, centro;

        if (tipo != null && tipo.modelo != null)
        {
            // Raíz limpia (escala 1, sin rotación) y el modelo adentro tal como viene
            // del FBX, para respetar la rotación y escala que trae de Blender.
            go = new GameObject(tipo.nombre);
            go.SetActive(false);
            GameObject modelo = Instantiate(tipo.modelo, go.transform, false);
            modelo.name = "Modelo";
            masa = tipo.masa;

            // Material con la textura (uno compartido por tipo)
            if (tipo.textura != null)
            {
                if (tipo.material == null)
                {
                    tipo.material = new Material(ShaderLit()) { name = tipo.nombre };
                    tipo.material.mainTexture = tipo.textura;
                    tipo.material.color = Color.white;
                }
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = tipo.material;
                    r.sharedMaterials = mats;
                }
            }

            // Quitar colliders que traiga el modelo y poner una caja a su medida
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            Bounds b = MedirLocal(go);
            if (b.size.magnitude > 5f || b.size.magnitude < 0.01f)
                Debug.LogWarning($"[Bloques] '{tipo.nombre}' mide {b.size} m, ¿está bien la escala del modelo?");
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
            tamano = b.size;
            centro = b.center;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.SetActive(false);
            go.name = "Bloque";
            go.transform.localScale = Vector3.one * tamanoCubo;
            if (materialCubo == null)
                materialCubo = new Material(ShaderLit()) { name = "Cubo", color = new Color(0.72f, 0.33f, 0.22f) };
            go.GetComponent<Renderer>().sharedMaterial = materialCubo;
            tamano = Vector3.one * tamanoCubo;
            centro = Vector3.zero;
        }

        Rigidbody rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = masa;
        BloqueAgarrable ba = go.GetComponent<BloqueAgarrable>();
        if (ba == null) ba = go.AddComponent<BloqueAgarrable>();
        // El objeto está desactivado (su Awake aún no corre), así que damos las medidas aquí
        ba.tamano = tamano;
        ba.centroLocal = centro;
        return go;
    }

    /// <summary>Caja que envuelve todas las mallas, en coordenadas locales del objeto raíz.</summary>
    static Bounds MedirLocal(GameObject go)
    {
        Transform raiz = go.transform;
        bool hay = false;
        Bounds total = new Bounds();
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds m = mf.sharedMesh.bounds;
            // 8 esquinas de la malla llevadas al espacio de la raíz
            for (int i = 0; i < 8; i++)
            {
                Vector3 esquina = m.center + Vector3.Scale(m.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = raiz.InverseTransformPoint(mf.transform.TransformPoint(esquina));
                if (!hay) { total = new Bounds(p, Vector3.zero); hay = true; }
                else total.Encapsulate(p);
            }
        }
        if (!hay) total = new Bounds(Vector3.zero, Vector3.one * 0.25f);
        return total;
    }

    static Shader ShaderLit()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        return s != null ? s : Shader.Find("Standard");
    }

    // ---------------------------------------------------------------- Auxiliares

    void ActualizarApuntado()
    {
        BloqueAgarrable nuevo = null;
        if (!Sosteniendo && modo == ModoAgarre.Mira &&
            Physics.Raycast(camara.transform.position, camara.transform.forward,
                            out RaycastHit hit, alcance, ~0, QueryTriggerInteraction.Ignore))
        {
            nuevo = hit.collider.GetComponentInParent<BloqueAgarrable>();
        }
        else if (!Sosteniendo && modo == ModoAgarre.Mano)
        {
            nuevo = BuscarCercaDeMano();
        }

        if (nuevo != apuntado)
        {
            if (apuntado != null) apuntado.Resaltar(false);
            apuntado = nuevo;
            if (apuntado != null) apuntado.Resaltar(true);
        }
    }

    BloqueAgarrable BuscarCercaDeMano()
    {
        if (mano == null) return null;
        Vector3 palma = PuntoPalma();
        Collider[] cerca = Physics.OverlapSphere(palma, radioMano, ~0, QueryTriggerInteraction.Ignore);
        BloqueAgarrable mejor = null;
        float mejorDist = float.MaxValue;
        foreach (Collider c in cerca)
        {
            BloqueAgarrable b = c.GetComponentInParent<BloqueAgarrable>();
            if (b == null || b.Sostenido) continue;
            float d = (c.ClosestPoint(palma) - palma).sqrMagnitude;
            if (d < mejorDist) { mejorDist = d; mejor = b; }
        }
        return mejor;
    }

    /// <summary>Centro de la palma (un poco abajo de la mano, que tiene la palma hacia abajo).</summary>
    Vector3 PuntoPalma()
    {
        return mano.TransformPoint(new Vector3(0f, -0.03f, 0.05f));
    }

    Transform ReferenciaRotacion()
    {
        return modo == ModoAgarre.Mano && mano != null ? mano : camara.transform;
    }

    void IgnorarJugador(bool ignorar)
    {
        foreach (Collider j in collidersJugador)
            foreach (Collider b in collidersSostenido)
                if (j != null && b != null) Physics.IgnoreCollision(j, b, ignorar);
    }

    static Vector3 RedondearAEje(Vector3 v)
    {
        Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0, 0);
        if (a.y >= a.z) return new Vector3(0, Mathf.Sign(v.y), 0);
        return new Vector3(0, 0, Mathf.Sign(v.z));
    }

    void OnGUI()
    {
        if (!mostrarAyuda || !usarMouse) return;
        string nombreTipo = tipos.Length > 0
            ? tipos[Mathf.Clamp(tipoActual, 0, tipos.Length - 1)].nombre
            : "Cubo";
        string teclas = "";
        for (int i = 0; i < Mathf.Min(tipos.Length, 9); i++)
            teclas += (i + 1) + ": " + tipos[i].nombre + "   ";

        string lineaMano = brazo != null
            ? "Mano: aprieta los 2 sensores (o mantén G) junto a un bloque para agarrarlo\n"
            : "";
        GUI.Label(new Rect(12, 12, 700, 140),
            lineaMano +
            "Clic izq. (mantener): agarrar  |  Clic der.: poner bloque\n" +
            "Rueda: acercar/alejar  |  T: girar 90°  |  X o clic central: borrar\n" +
            teclas + "\n" +
            "Bloque actual: " + nombreTipo +
            (Sosteniendo ? "   (sosteniendo)" : ""));
    }

#if UNITY_EDITOR
    // Llena la lista con los modelos de Assets/modelos si está vacía
    void OnValidate()
    {
        if (tipos != null && tipos.Length > 0) return;
        CargarModelosDeCarpeta();
    }

    [ContextMenu("Cargar bloques de Assets/modelos")]
    void CargarModelosDeCarpeta()
    {
        var lista = new List<TipoBloque>();
        void Agregar(string nombre, string modelo, string textura, float masa)
        {
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelo);
            if (go == null) return;
            lista.Add(new TipoBloque
            {
                nombre = nombre,
                modelo = go,
                textura = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(textura),
                masa = masa
            });
        }
        Agregar("Block", "Assets/modelos/Cinder_Block.fbx", "Assets/modelos/Cinder_Block_Base_Color.png", 3f);
        Agregar("Ladrillo", "Assets/modelos/Red_Holey_Brick.fbx", "Assets/modelos/Red_Holey_Brick_Base_Color.png", 1.5f);

        if (lista.Count > 0)
        {
            tipos = lista.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
