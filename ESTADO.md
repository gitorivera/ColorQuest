# ESTADO: versión Android tablet de ColorQuest

Última actualización: 2026-10-13. Rama actual al guardar: `MRBranchLSL` (limpia). **No se ha implementado nada todavía**; el plan está aprobado y se iba a empezar por el paso 1.

## Contexto
ColorQuest (RM en Meta Quest) debe tener una versión táctil para tablet Android, con mecánicas lo más idénticas posible, para compararlas en el estudio. El código del proyecto son 6 scripts (~2.000 líneas); casi todo (GameController, EventLogger, LSLMarkerSender, SyncFlash, prefabs, niveles) no depende de XR. Lo dependiente de Quest: `Blaster.cs` (OVRInput + rayo del control), la háptica en `GameController.PutRedGridAlert()`, el rig/escena Meta, el manifest y los paquetes.

## Decisión: nueva rama `TabletBranch` (desde `MRBranchLSL`), no un proyecto nuevo
- Un proyecto nuevo duplicaría la lógica; las versiones podrían divergir y eso invalidaría la comparación.
- La rama conserva historial y permite `merge`/`cherry-pick` de arreglos de mecánicas.
- Divergirán solo ProjectSettings, `Packages/manifest.json` y el manifest Android. La escena será separada (`TabletScene.unity`) para evitar merges de YAML.
- Alternativa descartada por ahora: dos proyectos con un paquete local compartido.

## Pasos (marcar al completar)
- [ ] 1. Limpieza en `MRBranchLSL`: dejar de trackear `Colorquest-0.1.0-v1-IL2CPP.symbols.zip` (`git rm --cached`); añadir a `.gitignore`: `*.apk`, `*.symbols.zip`, `*_BackUpThisFolder_*`, `*_BurstDebugInformation_*`. Commit. (Opcional `git gc`; .git pesa 697 MB.)
- [ ] 2. `git checkout -b TabletBranch`.
- [ ] 3. Escena: duplicar `DefaultScene` → `Assets/Scenes/TabletScene.unity`; quitar Camera Rig, Controller Tracking, Passthrough, ISDK_PokeInteraction, MRUK, Effect Mesh, ButtonPanel Meta. Añadir cámara estándar (tag `MainCamera`) encuadrando el tablero (centro 0,1,1, ancho 1 m) con fondo/piso. Ajustar FOV/distancia para que el tamaño angular de las esferas se parezca al de RM y documentarlo.
- [ ] 4. Input: crear `TouchShooter` (o `#if` en `Blaster.cs`) con Input System (`Pointer`/`Touchscreen`) y `Camera.ScreenPointToRay` contra `capaImpacto`. Reutilizar `Shoot()` sin cambios (orden de logs `TriggerPull` → `EsferaClickeada`, explosión, destrucción, impacto en rejilla, sonido). Botón de inicio = collider 3D o botón UI que llame `GameController.IniciarJuego()`.
- [ ] 5. Háptica: envolver `OVRInput.SetControllerVibration` en `#if` y usar `Handheld.Vibrate()` en tablet; mantener el evento `HapticFeedback` en el log.
- [ ] 6. Logger/LSL: `controllerHand` de `"RTouch"` a `"Touch"`/`"tablet"`; `TriggerPull` registra posición de pantalla además del rayo. Cambiar `source_id` de `JuegoEventos` (`juego-rm-quest` → `juego-tablet`). Mantener `ColorQuestMarkers` y los códigos idénticos. El multicast/wifi lock y `liblsl.so` ya sirven en cualquier Android.
- [ ] 7. SyncFlash: usa `Camera.main` con tamaño angular (8° / marco 1° / 10° arriba). Decidir (GAME-SPECS 6.2) entre mantenerlo o un cuadrado en pantalla de proporción equivalente; verificar con grabación de pantalla y marcador `sync_1`.
- [ ] 8. Settings: desactivar XR Plug-in Management/OpenXR en Android en esta rama; quitar defines Meta (`ISDK_OPENXR_HAND;USE_INPUT_SYSTEM_POSE_CONTROL;USE_STICK_CONTROL_THUMBSTICKS;OVR_DISABLE_HAND_PINCH_BUTTON_MAPPING;APP_UI_EDITOR_ONLY`); quitar `com.meta.xr.sdk.*` y `com.unity.xr.meta-openxr` (tras quitar referencias); reemplazar `Assets/Plugins/Android/AndroidManifest.xml` por uno normal (sin oculus/horizonos/headtracking/VR category; conservar INTERNET, ACCESS_NETWORK_STATE, ACCESS_WIFI_STATE, CHANGE_WIFI_MULTICAST_STATE); landscape fijo; application id distinto (p. ej. `com.UTP.ColorQuestTablet`); minSdk acorde a la tablet.
- [ ] 9. Build APK a la tablet y validar.

## Archivos críticos
- `Assets/scripts/Blaster.cs`, `Assets/scripts/GameController.cs` (solo háptica), `Assets/scripts/EventLogger.cs`, `Assets/scripts/LSL/LSLMarkerSender.cs`, `Assets/scripts/LSL/SyncFlash.cs`
- `Assets/Scenes/DefaultScene.unity` → `TabletScene.unity`
- `Assets/Plugins/Android/AndroidManifest.xml`, `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`, `.gitignore`

## Hallazgos de la exploración
- Unity 6000.4.0f1, URP 17.4.0, IL2CPP ARM64, Input System nuevo únicamente (`Input.*` desactivado), app id actual `com.UTP.ColorQuest`, minSDK 32 / target 34.
- Paquetes XR: Meta XR SDK 203.0.0, `com.unity.xr.meta-openxr` 2.4.0, OpenXR loader. No hay XR Interaction Toolkit ni AR Foundation. LSL4Unity es un paquete embebido en `Packages/`.
- Niveles (`GameController.niveles`): 2x2/3 bolas, 3x3/4, 4x4/5, 5x5/6, 6x6/7; 20 s por nivel; secuencia visible 3 s + 1 s de descenso; tablero 5 s; error 2,5 s con rejilla roja; +10 puntos por secuencia de 3 aciertos; 10 s de confeti final.
- Interacción actual: disparo por rayo desde el control (gatillo índice), no poke ni grab. Todo lo posterior al rayo es Unity estándar.
- Git: ramas `main` (muy atrás), `MRBranchLSL` (la más actual), `MRBranch`; remoto `git@github.com:gitorivera/ColorQuest.git`. `Library/` no está trackeado. El `.apk` (106 MB) y carpetas `*_BackUpThisFolder_*`/`*_BurstDebugInformation_*` están sin trackear pero sin ignorar; el `symbols.zip` (40 MB) sí está trackeado.

## Pendiente de confirmar en el editor
- Build target activo (se infiere Android; `Library/EditorUserBuildSettings.asset` no se pudo leer).
- Si el poke de ISDK también invoca `IniciarJuego()` o solo el disparo del blaster.

## Verificación final
- Partida completa en tablet: 5 niveles y todos los tiempos anteriores.
- Comparar NDJSON y marcadores LSL (`Tools/lsl_markers_listener.py`, `Tools/lsl_receiver.py`) de una sesión tablet vs una Quest: misma secuencia de eventos y códigos.
- Recepción de ambos streams LSL por Wi-Fi y alineación de `sync_1` con la grabación.
- `MRBranchLSL` sigue compilando y generando el APK de Quest sin cambios.

Plan original: `C:\Users\gitor\.claude\plans\la-creaci-n-de-una-virtual-balloon.md`
