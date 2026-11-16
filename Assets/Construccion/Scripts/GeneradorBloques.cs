using System.Collections;
using UnityEngine;

/// <summary>
/// Llena el mapa con bloques para recoger y apilar:
///   - Una MESA DE MATERIALES frente al jugador, con bloques encima
///     (a la altura de la mano, para alcanzarlos con el brazo).
///   - Una MESA DE CONSTRUCCIÓN vacía a un lado, para apilar ahí.
///   - Algunas PILAS de bloques en el piso alrededor.
///
/// Uso: crea un objeto vacío "GeneradorBloques" y agrégale este componente.
/// Usa los tipos de bloque (Block, Ladrillo) del ManipuladorBloques.
/// Clic derecho en el componente -> "Regenerar bloques" para volver a crearlos (en Play).
/// </summary>
public class GeneradorBloques : MonoBehaviour
{
    [Header("Referencias (vacío = se buscan solas)")]
    public ManipuladorBloques manipulador;
    [Tooltip("Jugador o cámara: las mesas se ponen frente a él")]
    public Transform jugador;

    [Header("Mesas")]
    [Tooltip("Pone la mesa a una altura que alcances con la mano según tu estatura")]
    public bool alturaAutomatica = true;
    [Tooltip("Altura de la mesa si no es automática (metros)")]
    public float alturaMesa = 0.85f;
    [Tooltip("Largo y fondo de cada mesa (metros)")]
    public Vector2 tamanoMesa = new Vector2(1.2f, 0.5f);
    [Tooltip("Qué tan lejos queda el borde de la mesa frente a ti")]
    public float distanciaAlFrente = 0.55f;
    public bool crearMesaConstruccion = true;
    [Tooltip("La mesa de construcción va a tu derecha (si no, a la izquierda)")]
    public bool construccionALaDerecha = true;
    public Color colorMesa = new Color(0.55f, 0.38f, 0.22f);

    [Header("Bloques en la mesa de materiales")]
    [Tooltip("Cuántos de cada tipo (en el orden de Tipos del ManipuladorBloques)")]
    public int[] cantidadPorTipo = { 4, 8 };
    public float separacion = 0.03f;

    [Header("Pilas en el piso")]
    public int pilasEnPiso = 4;
    public int bloquesPorPila = 6;
    [Tooltip("Distancia mínima y máxima de las pilas al jugador")]
    public Vector2 distanciaPilas = new Vector2(2.5f, 5f);

    Transform contenedor;
    Material materialMesa;

    IEnumerator Start()
    {
        // Esperar un cuadro: que el jugador y la cámara (o el visor) ya estén en su lugar
        yield return null;
        yield return null;
        Generar();
    }

    [ContextMenu("Regenerar bloques")]
    public void Generar()
    {
        if (!Application.isPlaying) { Debug.Log("[Generador] Dale Play para generar los bloques."); return; }

        if (manipulador == null) manipulador = FindAnyObjectByType<ManipuladorBloques>();
        if (manipulador == null)
        {
            Debug.LogWarning("[Generador] Falta un ManipuladorBloques en la escena (ponlo en la cámara).");
            return;
        }
        if (jugador == null)
        {
            var fpc = FindAnyObjectByType<FirstPersonController>();
            if (fpc != null) jugador = fpc.playerCamera != null ? fpc.playerCamera.transform : fpc.transform;
            else if (Camera.main != null) jugador = Camera.main.transform;
        }
        if (jugador == null)
        {
            Debug.LogWarning("[Generador] No encontré al jugador ni la cámara.");
            return;
        }

        if (contenedor != null) Destroy(contenedor.gameObject);
        contenedor = new GameObject("BloquesGenerados").transform;

        materialMesa = new Material(ShaderLit()) { name = "Mesa", color = colorMesa };
        if (materialMesa.HasProperty("_BaseColor")) materialMesa.SetColor("_BaseColor", colorMesa);

        // Frente horizontal del jugador
        Vector3 frente = jugador.forward; frente.y = 0f;
        if (frente.sqrMagnitude < 1e-4f) frente = Vector3.forward;
        frente.Normalize();
        Vector3 derecha = Vector3.Cross(Vector3.up, frente);
        Quaternion giro = Quaternion.LookRotation(frente, Vector3.up);

        Vector3 pies = jugador.position;
        float piso = AlturaPiso(pies, pies.y);
        float altura = alturaMesa;
        if (alturaAutomatica)
        {
            // Mesa unos 70 cm abajo de los ojos: los bloques quedan al alcance
            // de la mano con el brazo estirado hacia abajo y al frente
            float ojos = jugador.position.y - piso;
            altura = Mathf.Clamp(ojos - 0.7f, 0.6f, 1.15f);
        }

        // Mesa de materiales al frente
        Vector3 centroMat = pies + frente * (distanciaAlFrente + tamanoMesa.y * 0.5f);
        Transform mesaMat = CrearMesa("MesaMateriales", centroMat, giro, altura);
        LlenarMesa(mesaMat, altura);

        // Mesa de construcción a un lado
        if (crearMesaConstruccion)
        {
            Vector3 lado = construccionALaDerecha ? derecha : -derecha;
            Vector3 centroCon = centroMat + lado * (tamanoMesa.x + 0.4f);
            CrearMesa("MesaConstruccion", centroCon, giro, altura);
        }

        // Pilas en el piso
        for (int i = 0; i < pilasEnPiso; i++) CrearPila(pies, i);

        Debug.Log($"[Generador] Listo: mesas a {altura:0.00} m de altura y {pilasEnPiso} pilas en el piso.");
    }

    // ---------------------------------------------------------------- Mesas

    Transform CrearMesa(string nombre, Vector3 centro, Quaternion giro, float altura)
    {
        float piso = AlturaPiso(centro, centro.y);
        Transform mesa = new GameObject(nombre).transform;
        mesa.SetParent(contenedor, false);
        mesa.SetPositionAndRotation(new Vector3(centro.x, piso, centro.z), giro);

        float grosor = 0.04f;
        float largo = tamanoMesa.x, fondo = tamanoMesa.y;

        Cubo("Cubierta", mesa, new Vector3(0, altura - grosor * 0.5f, 0), new Vector3(largo, grosor, fondo));

        float pata = 0.05f;
        float alto = altura - grosor;
        float px = largo * 0.5f - pata, pz = fondo * 0.5f - pata;
        Cubo("Pata1", mesa, new Vector3(px, alto * 0.5f, pz), new Vector3(pata, alto, pata));
        Cubo("Pata2", mesa, new Vector3(-px, alto * 0.5f, pz), new Vector3(pata, alto, pata));
        Cubo("Pata3", mesa, new Vector3(px, alto * 0.5f, -pz), new Vector3(pata, alto, pata));
        Cubo("Pata4", mesa, new Vector3(-px, alto * 0.5f, -pz), new Vector3(pata, alto, pata));
        return mesa;
    }

    void Cubo(string nombre, Transform padre, Vector3 pos, Vector3 escala)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        go.GetComponent<Renderer>().sharedMaterial = materialMesa;
    }

    /// <summary>Acomoda los bloques en filas sobre la mesa; si no caben, hace otra capa encima.</summary>
    void LlenarMesa(Transform mesa, float altura)
    {
        float margen = 0.04f;
        float minX = -tamanoMesa.x * 0.5f + margen, maxX = tamanoMesa.x * 0.5f - margen;
        float minZ = -tamanoMesa.y * 0.5f + margen, maxZ = tamanoMesa.y * 0.5f - margen;

        float x = minX, z = minZ, fondoFila = 0f, y = altura, altoCapa = 0f;
        int tipos = manipulador.tipos != null ? Mathf.Max(manipulador.tipos.Length, 1) : 1;

        for (int t = 0; t < cantidadPorTipo.Length && t < tipos; t++)
        {
            Vector3 tam = manipulador.MedidasDeTipo(t);
            for (int n = 0; n < cantidadPorTipo[t]; n++)
            {
                // ¿Cabe en esta fila? si no, siguiente fila; si no, siguiente capa
                if (x + tam.x > maxX)
                {
                    x = minX; z += fondoFila + separacion; fondoFila = 0f;
                }
                if (z + tam.z > maxZ)
                {
                    x = minX; z = minZ; fondoFila = 0f;
                    y += altoCapa + 0.005f; altoCapa = 0f;
                }

                Vector3 local = new Vector3(x + tam.x * 0.5f, y + tam.y * 0.5f + 0.003f, z + tam.z * 0.5f);
                manipulador.CrearBloqueEn(t, mesa.TransformPoint(local), mesa.rotation)
                           .transform.SetParent(contenedor, true);

                x += tam.x + separacion;
                fondoFila = Mathf.Max(fondoFila, tam.z);
                altoCapa = Mathf.Max(altoCapa, tam.y);
            }
        }
    }

    // ---------------------------------------------------------------- Pilas en el piso

    void CrearPila(Vector3 alrededorDe, int indice)
    {
        int tipos = manipulador.tipos != null ? Mathf.Max(manipulador.tipos.Length, 1) : 1;
        int tipo = indice % tipos;
        Vector3 tam = manipulador.MedidasDeTipo(tipo);

        // Buscar un lugar libre (hasta 15 intentos)
        for (int intento = 0; intento < 15; intento++)
        {
            float angulo = Random.Range(0f, 360f);
            float dist = Random.Range(distanciaPilas.x, distanciaPilas.y);
            Vector3 c = alrededorDe + Quaternion.Euler(0, angulo, 0) * Vector3.forward * dist;
            float piso = AlturaPiso(c, alrededorDe.y);
            Quaternion giro = Quaternion.Euler(0, Mathf.Round(Random.Range(0f, 360f) / 90f) * 90f, 0);

            // Pila de 2 x 2 por capa
            Vector3 huella = new Vector3(tam.x * 2f + 0.02f, 0.5f, tam.z * 2f + 0.02f);
            Vector3 centroPrueba = new Vector3(c.x, piso + 0.3f, c.z);
            if (Physics.CheckBox(centroPrueba, huella * 0.5f, giro, ~0, QueryTriggerInteraction.Ignore))
                continue;

            for (int n = 0; n < bloquesPorPila; n++)
            {
                int capa = n / 4, pos = n % 4;
                float ox = (pos % 2 == 0 ? -0.5f : 0.5f) * (tam.x + 0.01f);
                float oz = (pos < 2 ? -0.5f : 0.5f) * (tam.z + 0.01f);
                Vector3 centro = new Vector3(c.x, piso + tam.y * (capa + 0.5f) + 0.003f * (capa + 1), c.z)
                               + giro * new Vector3(ox, 0, oz);
                manipulador.CrearBloqueEn(tipo, centro, giro).transform.SetParent(contenedor, true);
            }
            return;
        }
    }

    // ---------------------------------------------------------------- Auxiliares

    /// <summary>Altura del piso (terreno u objeto) debajo de un punto.</summary>
    static float AlturaPiso(Vector3 punto, float siNoHay)
    {
        Vector3 desde = new Vector3(punto.x, punto.y + 20f, punto.z);
        RaycastHit[] hits = Physics.RaycastAll(desde, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.NegativeInfinity;
        foreach (RaycastHit h in hits)
        {
            // Ignorar al jugador y a otros cuerpos con física (bloques)
            if (h.collider.attachedRigidbody != null) continue;
            if (h.collider is TerrainCollider) return h.point.y;
            if (h.point.y > mejor) mejor = h.point.y;
        }
        return float.IsNegativeInfinity(mejor) ? siNoHay - 1.6f : mejor;
    }

    static Shader ShaderLit()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        return s != null ? s : Shader.Find("Standard");
    }
}
