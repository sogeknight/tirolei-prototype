using System.IO;
using UnityEngine;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    [Header("Nombre base del fichero")]
    public string filePrefix = "lab_puntosfuga";

    private string filePath;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TelemetryManager] Ya existe una instancia, destruyendo este objeto.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // opcional, pero no molesta

        InitFile();
    }

    private void InitFile()
    {
        string sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{filePrefix}_{sessionId}.csv";
        filePath = Path.Combine(Application.persistentDataPath, fileName);

        // Cabecera del CSV
        if (!File.Exists(filePath))
        {
            string header = "time_sec;event_id;pos_x;pos_y";
            File.WriteAllText(filePath, header + "\n");
            Debug.Log($"[TelemetryManager] Creado fichero de telemetría: {filePath}");
        }
    }

    public void LogEvent(string eventId, Vector3 worldPos)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[TelemetryManager] filePath vacío. ¿InitFile falló?");
            return;
        }

        float t = Time.time;

        string line = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0:0.000};{1};{2:0.000};{3:0.000}",
            t, eventId, worldPos.x, worldPos.y
        );

        File.AppendAllText(filePath, line + "\n");
        Debug.Log("[TELEMETRY] " + line);
    }
}
