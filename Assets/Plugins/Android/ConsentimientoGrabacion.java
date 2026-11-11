package com.utp.colorquest.grabacion;

import android.app.Activity;
import android.content.Intent;
import android.media.projection.MediaProjectionManager;
import android.os.Build;
import android.os.Bundle;

/**
 * Actividad transparente que muestra el diálogo del sistema «¿Empezar a grabar?»
 * (la actividad de Unity no recibe onActivityResult desde un plugin) y, si se
 * acepta, arranca ServicioGrabacion con el permiso concedido.
 */
public class ConsentimientoGrabacion extends Activity {
    private static final int PEDIDO_CAPTURA = 4710;

    @Override
    protected void onCreate(Bundle estadoGuardado) {
        super.onCreate(estadoGuardado);
        if (estadoGuardado != null) return;   // recreada: el diálogo ya se pidió
        MediaProjectionManager gestor =
                (MediaProjectionManager) getSystemService(MEDIA_PROJECTION_SERVICE);
        try {
            startActivityForResult(gestor.createScreenCaptureIntent(), PEDIDO_CAPTURA);
        } catch (Exception e) {
            GrabadorPantalla.fallo("no se pudo pedir el permiso: " + e);
            terminar();
        }
    }

    @Override
    protected void onActivityResult(int pedido, int resultado, Intent datos) {
        super.onActivityResult(pedido, resultado, datos);
        if (pedido != PEDIDO_CAPTURA) return;

        if (resultado == RESULT_OK && datos != null) {
            Intent servicio = new Intent(this, ServicioGrabacion.class);
            servicio.putExtra(ServicioGrabacion.EXTRA_CODIGO, resultado);
            servicio.putExtra(ServicioGrabacion.EXTRA_DATOS, datos);
            try {
                if (Build.VERSION.SDK_INT >= 26) startForegroundService(servicio);
                else startService(servicio);
            } catch (Exception e) {
                GrabadorPantalla.fallo("no se pudo iniciar el servicio: " + e);
            }
        } else {
            GrabadorPantalla.rechazado();
        }
        terminar();
    }

    private void terminar() {
        finish();
        overridePendingTransition(0, 0);
    }
}
