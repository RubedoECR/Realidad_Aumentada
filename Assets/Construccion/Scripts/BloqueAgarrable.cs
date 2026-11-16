using UnityEngine;

/// <summary>
/// Marca un objeto como bloque que se puede agarrar, soltar y apilar.
/// Necesita un Rigidbody y un Collider (se agregan solos si faltan).
/// Lo usa ManipuladorBloques; también lo puedes poner en cualquier objeto
/// de la escena (por ejemplo los CSE_BrickStack) para poder agarrarlo.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BloqueAgarrable : MonoBehaviour
{
    [Tooltip("Color con el que se ilumina cuando lo apuntas")]
    public Color colorResaltado = new Color(1f, 0.85f, 0.3f);

    [Tooltip("Medidas del bloque en metros (se calculan solas)")]
    public Vector3 tamano;
    [Tooltip("Centro del bloque respecto a su origen, en metros (se calcula solo)")]
    public Vector3 centroLocal;

    public Rigidbody Cuerpo { get; private set; }

    /// <summary>Centro del bloque en el mundo.</summary>
    public Vector3 CentroMundo => transform.position + transform.rotation * centroLocal;
    public bool Sostenido { get; set; }

    Renderer[] renderers;
    MaterialPropertyBlock bloque;
    bool resaltado;

    void Awake()
    {
        Cuerpo = GetComponent<Rigidbody>();
        Cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
        if (GetComponentInChildren<Collider>() == null)
            gameObject.AddComponent<BoxCollider>();

        renderers = GetComponentsInChildren<Renderer>();
        bloque = new MaterialPropertyBlock();

        // Si nadie le dio medidas, las tomamos de su BoxCollider
        if (tamano == Vector3.zero)
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null)
            {
                tamano = Vector3.Scale(box.size, transform.lossyScale);
                centroLocal = Vector3.Scale(box.center, transform.lossyScale);
            }
            else tamano = Vector3.one * 0.25f;
        }
    }


    public void Resaltar(bool activo)
    {
        if (activo == resaltado) return;
        resaltado = activo;

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            if (!activo)
            {
                r.SetPropertyBlock(null);
                continue;
            }
            r.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", colorResaltado); // URP
            bloque.SetColor("_Color", colorResaltado);     // Built-in
            r.SetPropertyBlock(bloque);
        }
    }
}
