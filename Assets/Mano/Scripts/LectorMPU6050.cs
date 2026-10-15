using System;
using System.Globalization;
using System.Threading;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if NET_4_6
using System.IO.Ports;
#endif

/// <summary>
/// Lee "pitch,roll,yaw" del Arduino por el puerto serial y gira este objeto.
///
/// REQUISITO: Edit > Project Settings > Player > Other Settings >
///            Api Compatibility Level = .NET Framework
/// (con .NET Standard no existe System.IO.Ports y el script se queda en modo prueba).
///
/// Teclas:
///   R          -> recentrar (la posición actual de tu mano pasa a ser "al frente")
///   Flechas/Q/E -> mover la mano con el teclado (modo prueba, sin Arduino)
/// </summary>
public class LectorMPU6050 : MonoBehaviour
{
    [Header("Puerto serial")]
    [Tooltip("Mira cuál es en el IDE de Arduino: Herramientas > Puerto (ej. COM3)")]
    public string puerto = "COM3";
    public int baudios = 115200;
    public bool conectarAlIniciar = true;

    [Header("Ejes (ajusta si la mano gira al revés)")]
    public bool invertirPitch = false;
    public bool invertirRoll = false;
    public bool invertirYaw = false;
    [Tooltip("Rotación extra según cómo pegaste el sensor en tu mano")]
    public Vector3 rotacionMontaje = Vector3.zero;

    [Header("Movimiento")]
    [Tooltip("Mayor = más rápido pero con más temblor")]
    [Range(1f, 30f)] public float suavizado = 12f;

    [Header("Posición frente a la cámara")]
    public bool seguirCamara = true;
    public Vector3 desplazamientoCamara = new Vector3(0.18f, -0.22f, 0.45f);

    [Header("Estado (solo lectura)")]
    [SerializeField] bool conectado;
    [SerializeField] Vector3 angulosRecibidos;

    // Datos compartidos entre el hilo de lectura y Unity
    readonly object candado = new object();
    float pitch, roll, yaw;
    bool hayDatos;

    Thread hilo;
    volatile bool leyendo;
    Quaternion referencia = Quaternion.identity;
    Quaternion rotacionPrueba = Quaternion.identity;
    bool pedirRecentrado;

#if NET_4_6
    SerialPort serial;
#endif

    void Start()
    {
        if (seguirCamara && Camera.main != null)
        {
            transform.SetParent(Camera.main.transform, false);
            transform.localPosition = desplazamientoCamara;
            transform.localRotation = Quaternion.identity;
        }
        if (conectarAlIniciar) Conectar();
    }

    public void Conectar()
    {
#if NET_4_6
        try
        {
            serial = new SerialPort(puerto, baudios)
            {
                ReadTimeout = 200,
                NewLine = "\n",
                DtrEnable = true
            };
            serial.Open();
            leyendo = true;
            hilo = new Thread(LeerSerial) { IsBackground = true };
            hilo.Start();
            conectado = true;
            pedirRecentrado = true;
            Debug.Log($"[MPU6050] Conectado a {puerto}");
        }
        catch (Exception e)
        {
            conectado = false;
            Debug.LogWarning($"[MPU6050] No se pudo abrir {puerto}: {e.Message}. " +
                             "¿Está cerrado el Monitor Serie de Arduino? Usando modo prueba con teclado.");
        }
#else
        Debug.LogWarning("[MPU6050] Cambia Api Compatibility Level a .NET Framework " +
                         "(Project Settings > Player) para leer el Arduino. Usando modo prueba con teclado.");
#endif
    }

#if NET_4_6
    void LeerSerial()
    {
        while (leyendo)
        {
            try
            {
                string linea = serial.ReadLine().Trim();
                string[] partes = linea.Split(',');
                if (partes.Length != 3) continue;   // ignora "Calibrando...", "LISTO", etc.

                float p = float.Parse(partes[0], CultureInfo.InvariantCulture);
                float r = float.Parse(partes[1], CultureInfo.InvariantCulture);
                float y = float.Parse(partes[2], CultureInfo.InvariantCulture);
                lock (candado) { pitch = p; roll = r; yaw = y; hayDatos = true; }
            }
            catch (TimeoutException) { }
            catch (FormatException) { }
            catch (Exception) { if (!leyendo) break; Thread.Sleep(50); }
        }
    }
#endif

    void Update()
    {
        LeerTeclado();

        Quaternion objetivo;
        if (conectado)
        {
            float p, r, y; bool datos;
            lock (candado) { p = pitch; r = roll; y = yaw; datos = hayDatos; }
            if (!datos) return;
            angulosRecibidos = new Vector3(p, r, y);

            // Sensor -> Unity: pitch = eje X, yaw = eje Y, roll = eje Z
            Quaternion sensor = Quaternion.Euler(
                invertirPitch ? -p : p,
                invertirYaw ? y : -y,
                invertirRoll ? r : -r);
            sensor = Quaternion.Euler(rotacionMontaje) * sensor;

            if (pedirRecentrado)
            {
                referencia = Quaternion.Inverse(sensor);
                pedirRecentrado = false;
            }
            objetivo = referencia * sensor;
        }
        else
        {
            objetivo = rotacionPrueba;
        }

        transform.localRotation = Quaternion.Slerp(transform.localRotation, objetivo,
            1f - Mathf.Exp(-suavizado * Time.deltaTime));
    }

    void LeerTeclado()
    {
        float vel = 90f * Time.deltaTime;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return;
        if (k.rKey.wasPressedThisFrame) Recentrar();
        if (conectado) return;
        if (k.upArrowKey.isPressed) rotacionPrueba *= Quaternion.Euler(-vel, 0, 0);
        if (k.downArrowKey.isPressed) rotacionPrueba *= Quaternion.Euler(vel, 0, 0);
        if (k.leftArrowKey.isPressed) rotacionPrueba *= Quaternion.Euler(0, -vel, 0);
        if (k.rightArrowKey.isPressed) rotacionPrueba *= Quaternion.Euler(0, vel, 0);
        if (k.qKey.isPressed) rotacionPrueba *= Quaternion.Euler(0, 0, vel);
        if (k.eKey.isPressed) rotacionPrueba *= Quaternion.Euler(0, 0, -vel);
#else
        if (Input.GetKeyDown(KeyCode.R)) Recentrar();
        if (conectado) return;
        if (Input.GetKey(KeyCode.UpArrow)) rotacionPrueba *= Quaternion.Euler(-vel, 0, 0);
        if (Input.GetKey(KeyCode.DownArrow)) rotacionPrueba *= Quaternion.Euler(vel, 0, 0);
        if (Input.GetKey(KeyCode.LeftArrow)) rotacionPrueba *= Quaternion.Euler(0, -vel, 0);
        if (Input.GetKey(KeyCode.RightArrow)) rotacionPrueba *= Quaternion.Euler(0, vel, 0);
        if (Input.GetKey(KeyCode.Q)) rotacionPrueba *= Quaternion.Euler(0, 0, vel);
        if (Input.GetKey(KeyCode.E)) rotacionPrueba *= Quaternion.Euler(0, 0, -vel);
#endif
    }

    public void Recentrar()
    {
        pedirRecentrado = true;
        rotacionPrueba = Quaternion.identity;
#if NET_4_6
        try { if (serial != null && serial.IsOpen) serial.Write("r"); } catch { }
#endif
    }

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
