package com.utp.colorquest.grabacion;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.IBinder;

/**
 * Servicio en primer plano de tipo mediaProjection: Android 10+ no deja capturar la
 * pantalla sin él. Mientras graba muestra una notificación fija.
 */
public class ServicioGrabacion extends Service {
    static final String EXTRA_CODIGO = "codigo";
    static final String EXTRA_DATOS = "datos";
    private static final String CANAL = "colorquest_grabacion";
    private static final int ID_NOTIFICACION = 4711;

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (intent == null) { stopSelf(); return START_NOT_STICKY; }

        try {
            Notification notificacion = crearNotificacion();
            if (Build.VERSION.SDK_INT >= 29)
                startForeground(ID_NOTIFICACION, notificacion, ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION);
            else
                startForeground(ID_NOTIFICACION, notificacion);
        } catch (Exception e) {
            GrabadorPantalla.fallo("startForeground: " + e);
            stopSelf();
            return START_NOT_STICKY;
        }

        int codigo = intent.getIntExtra(EXTRA_CODIGO, 0);
        Intent datos = intent.getParcelableExtra(EXTRA_DATOS);
        GrabadorPantalla.iniciar(this, codigo, datos);
        if (!GrabadorPantalla.GRABANDO.equals(GrabadorPantalla.estado())) stopSelf();
        return START_NOT_STICKY;
    }

    // Deslizar la app fuera de Recientes: cerrar el MP4 antes de que maten el proceso
    @Override
    public void onTaskRemoved(Intent rootIntent) {
        GrabadorPantalla.detener();
        super.onTaskRemoved(rootIntent);
    }

    @Override
    public void onDestroy() {
        GrabadorPantalla.detener();
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) { return null; }

    private Notification crearNotificacion() {
        Notification.Builder b;
        if (Build.VERSION.SDK_INT >= 26) {
            NotificationManager nm = (NotificationManager) getSystemService(NOTIFICATION_SERVICE);
            nm.createNotificationChannel(new NotificationChannel(
                    CANAL, "Grabación de la partida", NotificationManager.IMPORTANCE_LOW));
            b = new Notification.Builder(this, CANAL);
        } else {
            b = new Notification.Builder(this);
        }
        return b.setContentTitle("ColorQuest")
                .setContentText("Grabando la pantalla de la partida")
                .setSmallIcon(android.R.drawable.presence_video_online)
                .setOngoing(true)
                .build();
    }
}
