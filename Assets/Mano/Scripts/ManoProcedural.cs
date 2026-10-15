using UnityEngine;

/// <summary>
/// Construye una mano 3D sencilla (palma + 5 dedos con falanges) usando
/// primitivas de Unity. No necesitas importar ningún modelo.
/// Agrégalo a un GameObject vacío; la mano se crea al darle Play
/// (o con clic derecho sobre el componente -> "Construir mano ahora").
/// </summary>
public class ManoProcedural : MonoBehaviour
{
    [Header("Apariencia")]
    public Color colorPiel = new Color(0.93f, 0.75f, 0.62f);
    [Tooltip("Mano derecha o izquierda")]
    public bool manoDerecha = true;
    [Tooltip("Escala general (1 = tamaño real aprox.)")]
    public float escala = 1f;

    [Header("Pose de los dedos")]
    [Range(0f, 90f)] public float curvaturaDedos = 12f;
    [Range(0f, 60f)] public float curvaturaPulgar = 15f;

    // Medidas en metros
    const float anchoPalma = 0.085f;
    const float largoPalma = 0.095f;
    const float grosorPalma = 0.028f;
    const float grosorDedo = 0.018f;

    Material materialPiel;

    void Awake()
    {
        if (transform.Find("Mano") == null) Construir();
    }

    [ContextMenu("Construir mano ahora")]
    public void Construir()
    {
        Transform vieja = transform.Find("Mano");
        if (vieja != null)
        {
            if (Application.isPlaying) Destroy(vieja.gameObject);
            else DestroyImmediate(vieja.gameObject);
        }

        materialPiel = CrearMaterial();
        float lado = manoDerecha ? 1f : -1f;

        Transform raiz = new GameObject("Mano").transform;
        raiz.SetParent(transform, false);
        raiz.localScale = Vector3.one * escala;

        // Muñeca
        Transform muneca = Pieza(PrimitiveType.Capsule, "Muneca", raiz,
            new Vector3(0, 0, -0.02f), new Vector3(0.06f, 0.025f, 0.05f),
            Quaternion.Euler(90, 0, 0));

        // Palma (la Z positiva apunta hacia los dedos)
        Pieza(PrimitiveType.Cube, "Palma", raiz,
            new Vector3(0, 0, largoPalma * 0.5f),
            new Vector3(anchoPalma, grosorPalma, largoPalma), Quaternion.identity);

        // Dedos: índice, medio, anular, meñique
        string[] nombres = { "Indice", "Medio", "Anular", "Menique" };
        float[] largos = { 0.085f, 0.095f, 0.088f, 0.068f };
        float[] posX = { 0.0315f, 0.0105f, -0.0105f, -0.0315f };
        for (int i = 0; i < 4; i++)
        {
            Vector3 baseDedo = new Vector3(posX[i] * lado, 0, largoPalma);
            CrearDedo(nombres[i], raiz, baseDedo, Quaternion.Euler(0, (i - 1.5f) * 3f * lado, 0),
                largos[i], 3, curvaturaDedos);
        }

        // Pulgar: sale del costado de la palma, girado hacia afuera
        Vector3 basePulgar = new Vector3(anchoPalma * 0.5f * lado, -0.004f, 0.025f);
        Quaternion rotPulgar = Quaternion.Euler(0, 45f * lado, -30f * lado);
        CrearDedo("Pulgar", raiz, basePulgar, rotPulgar, 0.07f, 2, curvaturaPulgar);
    }

    void CrearDedo(string nombre, Transform padre, Vector3 posicion, Quaternion rotacion,
                   float largoTotal, int falanges, float curva)
    {
        Transform articulacion = new GameObject(nombre).transform;
        articulacion.SetParent(padre, false);
        articulacion.localPosition = posicion;
        articulacion.localRotation = rotacion;

        float[] proporciones = falanges == 3 ? new[] { 0.45f, 0.30f, 0.25f } : new[] { 0.55f, 0.45f };

        for (int f = 0; f < falanges; f++)
        {
            float largo = largoTotal * proporciones[f];
            float grosor = grosorDedo * (1f - f * 0.1f);

            // Nudillo (articulación que se dobla)
            articulacion.localRotation *= Quaternion.Euler(curva, 0, 0);

            // Falange: cápsula orientada hacia +Z
            Pieza(PrimitiveType.Capsule, nombre + "_F" + (f + 1), articulacion,
                new Vector3(0, 0, largo * 0.5f),
                new Vector3(grosor, largo * 0.5f + grosor * 0.25f, grosor),
                Quaternion.Euler(90, 0, 0));

            // Siguiente articulación al final de esta falange
            Transform siguiente = new GameObject(nombre + "_Art" + (f + 2)).transform;
            siguiente.SetParent(articulacion, false);
            siguiente.localPosition = new Vector3(0, 0, largo);
            articulacion = siguiente;
        }
    }

    Transform Pieza(PrimitiveType tipo, string nombre, Transform padre,
                    Vector3 posicion, Vector3 tamano, Quaternion rotacion)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nombre;
        // Quitamos el collider para que la mano no choque con el jugador
        Collider c = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);

        go.transform.SetParent(padre, false);
        go.transform.localPosition = posicion;
        go.transform.localRotation = rotacion;
        go.transform.localScale = tamano;
        go.GetComponent<Renderer>().sharedMaterial = materialPiel;
        return go.transform;
    }

    Material CrearMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        Material m = new Material(s) { name = "PielMano" };
        m.color = colorPiel;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colorPiel);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
        return m;
    }
}
