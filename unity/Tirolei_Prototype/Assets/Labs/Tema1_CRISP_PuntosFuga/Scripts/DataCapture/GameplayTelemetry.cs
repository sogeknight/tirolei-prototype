using System.IO;
using UnityEngine;
using System;
using System.Globalization;

public class GameplayTelemetry : MonoBehaviour
{
    public static GameplayTelemetry Instance { get; private set; }

    private string sessionId;
    private string filePath;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ID de sesión corto
        sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);

        // Ruta del CSV
        filePath = Path.Combine(Application.persistentDataPath, "telemetry_gameplay.csv");

        // Cabecera limpia y coherente
        if (!File.Exists(filePath))
        {
            string header = "sessionId,time_ms,eventType,posX,posY,extra";
            File.WriteAllText(filePath, header + Environment.NewLine);
        }

        Debug.Log("[Telemetry] CSV path: " + filePath);
    }

    public void LogEvent(string eventType, Vector2 position, string extra = "")
    {
        try
        {
            long timeMs = (long)(Time.time * 1000f);

            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5}",
                sessionId,
                timeMs,
                eventType,
                Mathf.RoundToInt(position.x * 100f),
                Mathf.RoundToInt(position.y * 100f),
                Sanitize(extra)
            );

            File.AppendAllText(filePath, line + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogError("[Telemetry] Error writing event: " + e.Message);
        }
    }

    private string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        // Sin comas ni saltos de línea para no romper el CSV
        return text.Replace(",", " ").Replace("\n", " ").Replace("\r", " ");
    }
}
