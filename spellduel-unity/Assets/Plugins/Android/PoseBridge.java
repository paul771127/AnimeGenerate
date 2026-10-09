package com.paul771127.spellduel;

import android.graphics.Bitmap;
import android.graphics.PointF;

import com.google.android.gms.tasks.OnFailureListener;
import com.google.android.gms.tasks.OnSuccessListener;
import com.google.mlkit.vision.common.InputImage;
import com.google.mlkit.vision.pose.Pose;
import com.google.mlkit.vision.pose.PoseDetection;
import com.google.mlkit.vision.pose.PoseDetector;
import com.google.mlkit.vision.pose.PoseLandmark;
import com.google.mlkit.vision.pose.defaults.PoseDetectorOptions;

import java.nio.ByteBuffer;

/**
 * SpellDuel：用 Google ML Kit 偵測畫面中的人（攻擊方鏡頭輔助定位對手）。
 * Unity 送來 RGBA 影像（由上往下），非同步偵測；結果用 poll() 取回。
 * 結果格式：[序號, 有沒有找到人, 7 個關鍵點 ×（x, y, 可信度)]，x/y 為 0～1、左上為原點。
 */
public class PoseBridge {
    private static final int[] IDS = {
        PoseLandmark.NOSE,
        PoseLandmark.LEFT_SHOULDER, PoseLandmark.RIGHT_SHOULDER,
        PoseLandmark.LEFT_HIP, PoseLandmark.RIGHT_HIP,
        PoseLandmark.LEFT_ANKLE, PoseLandmark.RIGHT_ANKLE,
    };

    private final PoseDetector detector;
    private volatile boolean busy;
    private volatile float[] latest;
    private int seq;

    public PoseBridge() {
        PoseDetectorOptions options = new PoseDetectorOptions.Builder()
                .setDetectorMode(PoseDetectorOptions.STREAM_MODE)
                .build();
        detector = PoseDetection.getClient(options);
    }

    public boolean isBusy() { return busy; }

    public boolean submit(byte[] rgba, final int w, final int h) {
        if (busy) return false;
        busy = true;
        try {
            Bitmap bmp = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888);
            bmp.copyPixelsFromBuffer(ByteBuffer.wrap(rgba));
            InputImage image = InputImage.fromBitmap(bmp, 0);
            detector.process(image)
                    .addOnSuccessListener(new OnSuccessListener<Pose>() {
                        @Override public void onSuccess(Pose pose) { publish(pose, w, h); busy = false; }
                    })
                    .addOnFailureListener(new OnFailureListener() {
                        @Override public void onFailure(Exception e) { publish(null, w, h); busy = false; }
                    });
            return true;
        } catch (Throwable t) {
            busy = false;
            return false;
        }
    }

    private void publish(Pose pose, int w, int h) {
        float[] r = new float[2 + IDS.length * 3];
        r[0] = ++seq;
        if (pose != null && !pose.getAllPoseLandmarks().isEmpty()) {
            r[1] = 1f;
            for (int i = 0; i < IDS.length; i++) {
                PoseLandmark lm = pose.getPoseLandmark(IDS[i]);
                if (lm == null) continue;
                PointF p = lm.getPosition();
                r[2 + i * 3] = p.x / w;
                r[3 + i * 3] = p.y / h;
                r[4 + i * 3] = lm.getInFrameLikelihood();
            }
        }
        latest = r;
    }

    /** 取出最新結果；還沒有新結果時回傳空陣列 */
    public float[] poll() {
        float[] r = latest;
        latest = null;
        return r != null ? r : new float[0];
    }
}
