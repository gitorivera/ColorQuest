# ColorQuest

[![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.23373465.svg)](https://doi.org/10.5281/zenodo.23373465)

[English](README.md) | **Español**

ColorQuest es un juego de realidad mixta para entrenar la atención en Meta Quest, desarrollado en Unity como instrumento de investigación sobre TDAH. Registra eventos conductuales y transmite marcadores con sello de tiempo mediante [Lab Streaming Layer (LSL)](https://labstreaminglayer.org/), de modo que los eventos del juego puedan alinearse con EEG y otras señales biológicas.

Desarrollado en la Universidad Tecnológica de Pereira (UTP), Colombia.

## Jugabilidad

Aparece una cuadrícula de esferas de colores en el tablero. El jugador debe dispararles con el control siguiendo el orden de 3 colores que muestra un panel de referencia. Un orden incorrecto produce un destello rojo en la cuadrícula, vibración del control y una pausa de 2,5 s; una secuencia correcta otorga puntos y confeti.

El juego tiene cinco niveles de dificultad creciente (unos 20 s cada uno, con avance automático):

| Nivel | Cuadrícula | Longitud de la secuencia |
|---|---|---|
| 1 | 2×2 | 3 |
| 2 | 3×3 | 4 |
| 3 | 4×4 | 5 |
| 4 | 5×5 | 6 |
| 5 | 6×6 | 7 |

## Instrumentación para investigación

- **`EventLogger.cs`** – guarda los eventos en formato NDJSON en `Application.persistentDataPath` para análisis posterior. Los identificadores de participante están seudonimizados (sin datos personales).
- **`LSLMarkerSender.cs`** – ejecuta liblsl de forma nativa en la Quest y publica el stream `ColorQuestMarkers` (tipo `Markers`, source id `colorquest-adhd-v1`). Los marcadores son cadenas con el formato `código|etiqueta|extra`.
- **`SyncFlash.cs`** – muestra un destello blanco fijo a la cabeza con un marcador LSL simultáneo (`sync_k`), para alinear las grabaciones de pantalla con el tiempo LSL. Ver [GAME-SPECS.md](GAME-SPECS.md).
- **`Tools/`** – utilidades en Python para recibir, grabar e inspeccionar el stream en un PC. Ver [Tools/README.md](Tools/README.md).

## Requisitos

- Unity **6000.4.0f1** con Android Build Support
- Meta XR SDK 203.0.0 y OpenXR (ver `Packages/manifest.json`)
- Meta Quest 3 (o compatible) con modo desarrollador activado
- Para recolectar datos: un PC en la **misma subred** que el visor, con Python y `pylsl`

## Primeros pasos

1. Clona el repositorio y abre la carpeta con Unity 6000.4.0f1.
2. Abre `Assets/Scenes/DefaultScene.unity`.
3. Cambia la plataforma de compilación a Android, conecta la Quest y usa *Build And Run*.
4. En el PC, recibe los marcadores:

   ```bash
   pip install pylsl
   python Tools/lsl_receiver.py
   ```

Notas sobre LSL por Wi-Fi: muchas redes universitarias bloquean multicast. Si no se descubre el stream, agrega la IP del visor en un archivo `lsl_api.cfg` (`[lab]` → `KnownPeers = {<ip-quest>}`) y apunta la variable de entorno `LSLAPICFG` a ese archivo. Mantén el visor puesto durante las pruebas; de lo contrario el sensor de proximidad suspende la aplicación.

## Estructura del repositorio

| Ruta | Contenido |
|---|---|
| `Assets/scripts/` | Lógica del juego (`GameController`, `Blaster`, `EventLogger`) |
| `Assets/scripts/LSL/` | Emisor de marcadores LSL y destello de sincronización |
| `Assets/Plugins/Android/` | `liblsl.so` para Android y manifest (permisos de red) |
| `Tools/` | Herramientas en Python para el stream LSL |
| `documentation/` | Documentación adicional |
| `GAME-SPECS.md` | Especificación de sincronización juego ↔ TET/EEG |

## Licencia

Actualmente no se concede ninguna licencia de código abierto para el código y la documentación propios del proyecto. Copyright (c) 2026 Jorge Rivera, Universidad Tecnológica de Pereira. Todos los derechos reservados.

Los componentes de terceros conservan sus propias licencias: el Meta XR SDK está sujeto a los términos de Meta, y LSL4Unity / liblsl tienen licencia MIT. Unity está sujeto a los términos de Unity.

## Cómo citar

Si usas ColorQuest en tu investigación, por favor cítalo. Ver [CITATION.cff](CITATION.cff) o usa el DOI: [10.5281/zenodo.23373465](https://doi.org/10.5281/zenodo.23373465).

## Autor

Jorge Rivera – Universidad Tecnológica de Pereira (j.rivera@utp.edu.co)
