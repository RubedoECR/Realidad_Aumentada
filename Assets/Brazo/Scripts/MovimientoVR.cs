using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Caminar en VR con la palanca del control IZQUIERDO del Meta Quest,
/// hacia donde mira el visor. El brazo derecho sigue con los sensores.
///
///   Palanca izquierda  -> caminar (adelante, atrás, de lado)
///   Botón X            -> girar a la izquierda
///   Botón Y            -> girar a la derecha
///   (Sin control: WASD o flechas del teclado, para probar)
///
/// Uso: agrégalo al objeto XR Origin (VR).
/// </summary>
public class MovimientoVR : MonoBehaviour
{
    [Header("Referencias (vacío = se buscan solas)")]
    [Tooltip("El objeto que se mueve (XR Origin). Vacío = este mismo objeto")]
    public Transform origen;
    [Tooltip("La cámara del visor (Main Camera dentro de XR Origin)")]
    public Transform cabeza;

    [Header("Control")]
    public XRNode manoDelControl = XRNode.LeftHand;
    [Tooltip("Metros por segundo")]
    public float velocidad = 1.5f;
    [Range(0f, 0.5f)] public float zonaMuerta = 0.15f;
    public bool usarTeclado = true;

    [Header("Giro")]
    [Tooltip("Grados por cada vez que presionas X o Y")]
    public float anguloGiro = 45f;

    [Header("Choques y terreno")]
    [Tooltip("No atravesar mesas, paredes ni objetos fijos")]
    public bool chocarConObjetos = true;
    public float radioCuerpo = 0.25f;
    [Tooltip("Subir y bajar con el terreno")]
    public bool seguirTerreno = true;

    InputDevice control;
    bool xAntes, yAntes;

    void Start()
    {
        if (origen == null) origen = transform;
        if (cabeza == null && Camera.main != null) cabeza = Camera.main.transform;
    }

    void Update()
    {
        if (cabeza == null)
        {
            if (Camera.main != null) cabeza = Camera.main.transform;
            else return;
        }
        if (!control.isValid) control = InputDevices.GetDeviceAtXRNode(manoDelControl);

        // ---- Leer palanca y botones
        Vector2 palanca = Vector2.zero;
        bool botonX = false, botonY = false;
        if (control.isValid)
        {
            control.TryGetFeatureValue(CommonUsages.primary2DAxis, out palanca);
            control.TryGetFeatureValue(CommonUsages.primaryButton, out botonX);    // X en el control izquierdo
            control.TryGetFeatureValue(CommonUsages.secondaryButton, out botonY);  // Y en el control izquierdo
        }
        if (usarTeclado)
        {
            palanca.x += Input.GetAxisRaw("Horizontal");
            palanca.y += Input.GetAxisRaw("Vertical");
            botonX |= Input.GetKey(KeyCode.Q);
            botonY |= Input.GetKey(KeyCode.E);
        }
        palanca = Vector2.ClampMagnitude(palanca, 1f);
        if (palanca.magnitude < zonaMuerta) palanca = Vector2.zero;

        // ---- Girar (una vez por cada presión)
        if (botonX && !xAntes) Girar(-anguloGiro);
        if (botonY && !yAntes) Girar(anguloGiro);
        xAntes = botonX;
        yAntes = botonY;

        // ---- Caminar hacia donde mira el visor
        if (palanca != Vector2.zero)
        {
            Vector3 adelante = cabeza.forward; adelante.y = 0f;
            if (adelante.sqrMagnitude < 1e-4f) adelante = cabeza.up;   // mirando recto abajo
            adelante.y = 0f;
            adelante.Normalize();
            Vector3 derecha = Vector3.Cross(Vector3.up, adelante);

            Vector3 paso = (adelante * palanca.y + derecha * palanca.x) * velocidad * Time.deltaTime;
            if (!chocarConObjetos || CaminoLibre(paso))
                origen.position += paso;
        }

        if (seguirTerreno) AjustarAlTerreno();
    }

    void Girar(float grados)
    {
        // Girar alrededor de la cabeza, para no "deslizarse" al girar
        origen.RotateAround(cabeza.position, Vector3.up, grados);
    }

    bool CaminoLibre(Vector3 paso)
    {
        float piso = origen.position.y;
        Vector3 cuerpo = new Vector3(cabeza.position.x, piso, cabeza.position.z);
        Vector3 abajo = cuerpo + Vector3.up * (radioCuerpo + 0.3f);   // sobre escalones pequeños
        Vector3 arriba = cuerpo + Vector3.up * 1.4f;

        RaycastHit[] hits = Physics.CapsuleCastAll(abajo, arriba, radioCuerpo, paso.normalized,
                                                   paso.magnitude + 0.02f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            // Los bloques (con física) no estorban; las mesas y objetos fijos sí
            if (h.collider.attachedRigidbody != null) continue;
            if (h.collider is TerrainCollider) continue;
            return false;
        }
        return true;
    }

    void AjustarAlTerreno()
    {
        Vector3 desde = new Vector3(cabeza.position.x, cabeza.position.y + 1f, cabeza.position.z);
        RaycastHit[] hits = Physics.RaycastAll(desde, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            // Solo el terreno: así no te "subes" a las mesas
            if (h.collider is TerrainCollider)
            {
                Vector3 p = origen.position;
                p.y = Mathf.Lerp(p.y, h.point.y, 10f * Time.deltaTime);
                origen.position = p;
                return;
            }
        }
    }
}
