import pandas as pd
import glob
import os

# Ruta donde están los CSV
DATA_DIR = "./"

# Definimos manualmente el tipo de cada sesión
session_map = {
    "normalsesion001": "NORMAL",
    "normalsesion002": "NORMAL",
    "normalsesion003": "NORMAL",
    "failsesion001": "FAIL",
    "failsesion002": "FAIL",
    "exoloresesion001": "EXPLORE",
    "exoloresesion002": "EXPLORE",
}

# Unimos todos los csv
all_files = glob.glob(os.path.join(DATA_DIR, "*.csv"))
df_list = []

for file in all_files:
    filename = os.path.splitext(os.path.basename(file))[0]
    if filename not in session_map:
        print(f"[WARNING] {filename} no está en la tabla de sesiones, lo salto")
        continue

    session_type = session_map[filename]
    df = pd.read_csv(file)
    df["session_type"] = session_type
    df["session_name"] = filename
    df_list.append(df)

merged = pd.concat(df_list, ignore_index=True)

# Guardamos
merged.to_csv("gameplay_merged.csv", index=False)
print("[OK] Dataset combinado guardado como gameplay_merged.csv")
