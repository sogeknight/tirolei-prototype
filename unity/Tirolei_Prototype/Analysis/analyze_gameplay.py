import pandas as pd

# Cargar el merged con la ruta REAL que tienes
df = pd.read_csv("Data/Telemetry/Processed/gameplay_merged001.csv")

print("FILAS:", len(df))
print("SESIONES:", df["sessionId"].nunique())
print("\nCOLUMNAS:")
print(df.columns.tolist())
print("\nPRIMERAS FILAS:")
print(df.head())

# --- MÉTRICAS POR SECCIÓN ---
print("\n=== CONTEOS POR SECCIÓN Y EVENTO ===")
counts = pd.crosstab(df["section"], df["eventType"])
print(counts)

print("\n=== RATIO DE MUERTES POR ENTRADA DE SECCIÓN ===")
death_ratio = counts["DEATH_HAZARD"] / counts["SECTION_ENTER"]
death_ratio = (death_ratio * 100).round(1)
print(death_ratio)

# --- TIEMPO POR SECCIÓN ---
print("\n=== DURACIÓN POR SECCIÓN (segundos) ===")
sec_durations = (
    df.groupby(["sessionId", "section"])["time_ms"]
      .agg(lambda s: s.max() - s.min())
      .reset_index()
)

sec_stats = (sec_durations.groupby("section")["time_ms"]
             .agg(["mean", "median", "min", "max"]) / 1000).round(1)

print(sec_stats)

# --- HOTSPOTS ---
print("\n=== HOTSPOTS DE MUERTE ===")
deaths = df[df["eventType"] == "DEATH_HAZARD"].copy()
deaths["posX_round"] = (deaths["posX"] // 10) * 10
deaths["posY_round"] = (deaths["posY"] // 10) * 10

hotspots = (
    deaths.groupby(["section", "posX_round", "posY_round"])
    .size()
    .reset_index(name="count")
    .sort_values("count", ascending=False)
)

print(hotspots.head(15))
