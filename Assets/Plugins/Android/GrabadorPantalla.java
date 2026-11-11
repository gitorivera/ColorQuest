package com.utp.colorquest.grabacion;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.hardware.display.DisplayManager;
import android.hardware.display.VirtualDisplay;
import android.media.MediaRecorder;
import android.media.projection.MediaProjection;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.DisplayMetrics;
import android.util.Log;
import android.view.Display;

/**
 * Grabación de la pantalla de la tablet durante la partida (GAME-SPECS §6.2).
 *
 * Unity llama a solicitar() al arrancar: ConsentimientoGrabacion pide el permiso de
 * captura del sistema y, si se acepta, ServicioGrabacion (servicio en primer plano,
 * obligatorio para MediaProjection desde Android 10) llama a iniciar(), que graba la
 * pantalla en un MP4 H.264 sin audio. detener() cierra el MP4; si no se llama (la app
 * muere sin pasar por aquí) el archivo queda sin índice y no se puede reproducir.
 *
 * Unity consulta estado() y, para traducir el inicio a tiempo LSL, inicioNanos() y
 * ahoraNanos() (System.nanoTime, reloj monotónico del sistema).
 */
public final class GrabadorPantalla {
    private static final String TAG = "GrabadorPantalla";

    public static final String INACTIVO = "inactivo";
    public static final String ESPERANDO = "esperando_permiso";
    public static final String GRABANDO = "grabando";
    public static final String RECHAZADO = "rechazado";
    public static final String DETENIDO = "detenido";
    public static final String ERROR = "error";

    // H.264 nivel 4.1 admite hasta 8192 macrobloques (~1920x1080) por cuadro; la
    // pantalla de 2000x1200 de la Tab S6 Lite se pasa, así que se reduce.
    private static final int MAX_PIXELES = 1920 * 1080;

    private static final Object candado = new Object();

    private static volatile String estado = INACTIVO;
    private static volatile String error = "";
    private static volatile String ruta = "";
    private static volatile long inicioNanos = 0;
    private static volatile long finNanos = 0;
    private static volatile int ancho, alto, dpi;
    private static volatile int fps = 30;
    private static volatile int bitrate = 6_000_000;

    private static Context contexto;
    private static MediaProjection proyeccion;
    private static VirtualDisplay pantallaVirtual;
    private static MediaRecorder grabador;

    private GrabadorPantalla() { }

    // ── API para Unity ──────────────────────────────────────────────────────

    /** Pide el permiso de captura y, si se concede, empieza a grabar en rutaArchivo. */
    public static void solicitar(Activity actividad, String rutaArchivo, int cuadrosPorSegundo, int bitsPorSegundo) {
        synchronized (candado) {
            if (ESPERANDO.equals(estado) || GRABANDO.equals(estado)) return;
            ruta = rutaArchivo;
            fps = cuadrosPorSegundo;
            bitrate = bitsPorSegundo;
            error = "";
            inicioNanos = 0;
            finNanos = 0;
            contexto = actividad.getApplicationContext();
            estado = ESPERANDO;
        }
        Intent intent = new Intent(actividad, ConsentimientoGrabacion.class);
        intent.addFlags(Intent.FLAG_ACTIVITY_NO_ANIMATION);
        actividad.startActivity(intent);
    }

    /** Detiene la grabación y cierra el MP4. Se puede llamar varias veces. */
    public static void detener() {
        detener(null);
    }

    public static String estado() { return estado; }
    public static String error() { return error; }
    public static String ruta() { return ruta; }
    public static long inicioNanos() { return inicioNanos; }
    public static long finNanos() { return finNanos; }
    public static long ahoraNanos() { return System.nanoTime(); }
    public static int ancho() { return ancho; }
    public static int alto() { return alto; }
    public static int fps() { return fps; }
    public static int bitrate() { return bitrate; }

    // ── Llamadas internas (ConsentimientoGrabacion / ServicioGrabacion) ─────

    static void rechazado() {
        synchronized (candado) {
            if (ESPERANDO.equals(estado)) estado = RECHAZADO;
        }
        Log.w(TAG, "Permiso de captura rechazado");
    }

    static void fallo(String mensaje) {
        synchronized (candado) {
            error = mensaje;
            estado = ERROR;
        }
        Log.e(TAG, mensaje);
    }

    /** Lo llama el servicio ya en primer plano (requisito de Android 14 para MediaProjection). */
    static void iniciar(Context ctx, int codigoResultado, Intent datos) {
        synchronized (candado) {
            if (!ESPERANDO.equals(estado)) return;
            try {
                MediaProjectionManager gestor =
                        (MediaProjectionManager) ctx.getSystemService(Context.MEDIA_PROJECTION_SERVICE);
                proyeccion = gestor.getMediaProjection(codigoResultado, datos);
                if (proyeccion == null) throw new IllegalStateException("getMediaProjection devolvió null");

                // Android 14 exige registrar el callback antes de crear la pantalla virtual.
                // onStop llega si el usuario detiene la captura desde el sistema.
                proyeccion.registerCallback(new MediaProjection.Callback() {
                    @Override public void onStop() {
                        if (GRABANDO.equals(estado)) detener("captura detenida por el sistema");
                    }
                }, new Handler(Looper.getMainLooper()));

                calcularTamano(ctx);

                grabador = Build.VERSION.SDK_INT >= 31 ? new MediaRecorder(ctx) : new MediaRecorder();
                grabador.setVideoSource(MediaRecorder.VideoSource.SURFACE);
                grabador.setOutputFormat(MediaRecorder.OutputFormat.MPEG_4);
                grabador.setOutputFile(ruta);
                grabador.setVideoEncoder(MediaRecorder.VideoEncoder.H264);
                grabador.setVideoSize(ancho, alto);
                grabador.setVideoFrameRate(fps);
                grabador.setVideoEncodingBitRate(bitrate);
                grabador.prepare();

                pantallaVirtual = proyeccion.createVirtualDisplay("ColorQuestGrabacion",
                        ancho, alto, dpi, DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                        grabador.getSurface(), null, null);

                grabador.start();
                inicioNanos = System.nanoTime();
                estado = GRABANDO;
                Log.i(TAG, "Grabando " + ancho + "x" + alto + " a " + fps + " fps en " + ruta);
            } catch (Exception e) {
                liberar();
                error = "iniciar: " + e;
                estado = ERROR;
                Log.e(TAG, error, e);
            }
        }
    }

    // ── Privado ─────────────────────────────────────────────────────────────

    private static void detener(String motivoError) {
        Context ctx;
        synchronized (candado) {
            ctx = contexto;
            if (GRABANDO.equals(estado)) {
                try {
                    grabador.stop();
                    estado = motivoError == null ? DETENIDO : ERROR;
                    if (motivoError != null) error = motivoError;
                } catch (RuntimeException e) {
                    // stop() falla si no llegó ningún cuadro: el archivo no sirve
                    error = "detener: " + e;
                    estado = ERROR;
                    Log.e(TAG, error, e);
                }
                finNanos = System.nanoTime();
            }
            liberar();
        }
        if (ctx != null) ctx.stopService(new Intent(ctx, ServicioGrabacion.class));
    }

    private static void liberar() {
        if (grabador != null) {
            try { grabador.reset(); } catch (Exception ignorada) { }
            grabador.release();
            grabador = null;
        }
        if (pantallaVirtual != null) {
            pantallaVirtual.release();
            pantallaVirtual = null;
        }
        if (proyeccion != null) {
            MediaProjection p = proyeccion;
            proyeccion = null;   // antes de stop(): onStop no debe volver a entrar
            try { p.stop(); } catch (Exception ignorada) { }
        }
    }

    // Tamaño del video: la pantalla real, reducida si pasa de MAX_PIXELES, con lados
    // múltiplos de 16 (lo piden los codificadores) y, si se puede, la misma proporción.
    private static void calcularTamano(Context ctx) {
        DisplayManager dm = (DisplayManager) ctx.getSystemService(Context.DISPLAY_SERVICE);
        Display d = dm.getDisplay(Display.DEFAULT_DISPLAY);
        DisplayMetrics m = new DisplayMetrics();
        d.getRealMetrics(m);
        // El juego es horizontal fijo: el lado largo es el ancho aunque el diálogo del
        // permiso se haya mostrado en vertical
        int w = Math.max(m.widthPixels, m.heightPixels), h = Math.min(m.widthPixels, m.heightPixels);
        dpi = m.densityDpi;

        double escala = Math.min(1.0, Math.sqrt((double) MAX_PIXELES / ((double) w * h)));
        int g = mcd(w, h);
        int pasoW = 16 * (w / g), pasoH = 16 * (h / g);
        int k = (int) Math.floor(w * escala / pasoW);
        if (k >= 1 && pasoW <= 1920) {
            // Proporción exacta: 2000x1200 (5:3) → 1840x1104
            ancho = k * pasoW;
            alto = k * pasoH;
        } else {
            ancho = Math.max(16, (int) (w * escala) / 16 * 16);
            alto = Math.max(16, (int) (h * escala) / 16 * 16);
        }
    }

    private static int mcd(int a, int b) {
        while (b != 0) { int t = a % b; a = b; b = t; }
        return a;
    }
}
