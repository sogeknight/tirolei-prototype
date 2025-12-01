# telemetry_plots.R
# Versión base R mejorada:
# - Siempre muestra todas las secciones (S00..S04) aunque no haya muertes
# - Eje Y = tasa de muertes (%)
# - Texto sobre cada barra: muertes/entradas

# 1) CONFIGURACIÓN BÁSICA -----------------------------------------------

setwd("C:/Users/sogek/OneDrive/my_virtual_way/GRADO/Tirolei/unity/Tirolei_Prototype")

telemetry_dir <- "Data/Telemetry"

# Carpeta de salida para las gráficas
output_dir <- file.path("Analysis", "Rplots_rates")
dir.create(output_dir, recursive = TRUE, showWarnings = FALSE)

# Secciones que queremos ver SIEMPRE, en este orden
sections_all <- c(
  "S00_START",
  "S01_EASY_ENTER",
  "S02_MID_ENTER",
  "S03_HARD_ENTER",
  "S04_SUPERHARD_ENTER"
)

axis_labels <- c("S00", "S01", "S02", "S03", "S04")

# 2) LISTAR CSV DE TELEMETRÍA -------------------------------------------

files <- list.files(
  telemetry_dir,
  pattern = "\\.csv$",
  full.names = TRUE
)

files <- files[!grepl("merged", files, ignore.case = TRUE)]

cat("Archivos de telemetría encontrados:\n")
print(basename(files))

# 3) FUNCIÓN AUXILIAR PARA NOMBRE SEGURO --------------------------------

make_safe_name <- function(x) {
  x <- gsub("[^A-Za-z0-9_-]", "_", x)
  x
}

# 4) LOOP PRINCIPAL: UNA GRÁFICA POR CSV --------------------------------

for (f in files) {
  cat("\nProcesando:", f, "\n")
  
  df <- tryCatch(
    read.csv(f, stringsAsFactors = FALSE),
    error = function(e) {
      cat("   !! Error leyendo", f, ":", conditionMessage(e), "\n")
      return(NULL)
    }
  )
  if (is.null(df)) next
  
  if (!all(c("eventType", "section") %in% names(df))) {
    cat("   !! Falta 'eventType' o 'section'. Se omite.\n")
    next
  }
  
  # Nombre de sesión
  if ("session_name" %in% names(df)) {
    session_label <- paste(unique(df$session_name), collapse = ", ")
  } else {
    session_label <- tools::file_path_sans_ext(basename(f))
  }
  session_safe <- make_safe_name(session_label)
  if (nchar(session_safe) == 0) {
    session_safe <- tools::file_path_sans_ext(basename(f))
  }
  
  # 4.1 Contar muertes y entradas por sección ---------------------------
  deaths_df  <- df[df$eventType == "DEATH_HAZARD", , drop = FALSE]
  enters_df  <- df[df$eventType == "SECTION_ENTER", , drop = FALSE]
  
  deaths_tab <- table(deaths_df$section)
  enters_tab <- table(enters_df$section)
  
  deaths_vec <- numeric(length(sections_all))
  enters_vec <- numeric(length(sections_all))
  rate_vec   <- numeric(length(sections_all))
  
  for (i in seq_along(sections_all)) {
    sec <- sections_all[i]
    
    d <- if (sec %in% names(deaths_tab)) as.numeric(deaths_tab[sec]) else 0
    e <- if (sec %in% names(enters_tab)) as.numeric(enters_tab[sec]) else 0
    
    deaths_vec[i] <- d
    enters_vec[i] <- e
    rate_vec[i]   <- if (e > 0) (d / e) * 100 else 0  # porcentaje
  }
  
  # 4.2 Gráfica ----------------------------------------------------------
  out_file <- file.path(output_dir, paste0("rates_", session_safe, ".png"))
  
  png(filename = out_file, width = 1200, height = 800, res = 150)
  
  par(mar = c(6, 5, 5, 2))  # márgenes: abajo, izquierda, arriba, derecha
  
  # Limite Y de 0 a 100 %
  bar_pos <- barplot(
    rate_vec,
    names.arg = axis_labels,
    ylim = c(0, 100),
    main = paste("Tasa de muertes por sección –", session_label),
    xlab = "Sección",
    ylab = "Tasa de muertes (%)"
  )
  
  # Texto encima de cada barra: "muertes/entradas"
  labels_counts <- paste0(deaths_vec, "/", enters_vec)
  text(
    x = bar_pos,
    y = rate_vec + 3,  # un poco por encima de la barra
    labels = labels_counts,
    cex = 0.9
  )
  
  dev.off()
  
  cat("   -> guardado:", out_file, "\n")
}

cat("\nListo. Revisa la carpeta:", output_dir, "\n")
