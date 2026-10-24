package com.paul771127.spellduel;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.PointF;

import com.google.android.gms.tasks.Tasks;
import com.google.mediapipe.framework.image.BitmapImageBuilder;
import com.google.mediapipe.framework.image.MPImage;
import com.google.mediapipe.tasks.components.containers.NormalizedLandmark;
import com.google.mediapipe.tasks.core.BaseOptions;
import com.google.mediapipe.tasks.core.Delegate;
import com.google.mediapipe.tasks.vision.core.RunningMode;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarker;
import com.google.mediapipe.tasks.vision.handlandmarker.HandLandmarkerResult;
import com.google.mlkit.vision.common.InputImage;
import com.google.mlkit.vision.pose.Pose;
import com.google.mlkit.vision.pose.PoseDetection;
import com.google.mlkit.vision.pose.PoseDetector;
import com.google.mlkit.vision.pose.PoseLandmark;
import com.google.mlkit.vision.pose.defaults.PoseDetectorOptions;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * SpellDuel：手機上的影像辨識（在背景執行緒執行，不卡畫面）。
 *   人體（對手的位置）：Google ML Kit Pose Detection
 *   手（自己的手勢）：Google MediaPipe Hand Landmarker（模型 hand_landmarker.task 放在 StreamingAssets）
 * Unity 送來 RGBA 影像（由上往下），結果用 poll() 取回。
 * 結果格式：[序號, 有沒有人, 7 個身體關鍵點 ×（x, y, 可信度), 有沒有手, 21 個手部關鍵點 ×（x, y, 可信度)]
 * x/y 為 0～1、左上為原點。flags：1＝偵測人體、2＝偵測手。
 */
public class PoseBridge {
    private static final int[] IDS = {
        PoseLandmark.NOSE,
        PoseLandmark.LEFT_SHOULDER, PoseLandmark.RIGHT_SHOULDER,
        PoseLandmark.LEFT_HIP, PoseLandmark.RIGHT_HIP,
        PoseLandmark.LEFT_ANKLE, PoseLandmark.RIGHT_ANKLE,
    };
    private static final int BODY = 7, HAND = 21;
    private static final int LEN = 2 + BODY * 3 + 1 + HAND * 3;

    private final PoseDetector detector;
    private HandLandmarker hands;
    private String handError = "";
    private final ExecutorService worker = Executors.newSingleThreadExecutor();
    private volatile boolean busy;
    private volatile float[] latest;
    private int seq;
    private Bitmap bmp;            // 重複使用（以前每張都新建一張 Bitmap）
    private long lastTs;           // VIDEO 模式的時間戳記要一直遞增
    private String handMode = "";

    public PoseBridge() {
        PoseDetectorOptions options = new PoseDetectorOptions.Builder()
                .setDetectorMode(PoseDetectorOptions.STREAM_MODE)
                .build();
        detector = PoseDetection.getClient(options);
    }

    public boolean isBusy() { return busy; }
    public String getHandError() { return handError; }

    // 第一次需要偵測手時才載入模型（約 8 MB）
    private void ensureHands() {
        if (hands != null || !handError.isEmpty()) return;
        try {
            Context ctx = com.unity3d.player.UnityPlayer.currentActivity;
            InputStream in = ctx.getAssets().open("hand_landmarker.task");
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] b = new byte[65536];
            int n;
            while ((n = in.read(b)) > 0) out.write(b, 0, n);
            in.close();
            byte[] model = out.toByteArray();
            // 用 CPU：GPU 在部分手機會直接閃退（原生層，接不到例外）；VIDEO 模式的追蹤已經夠快
            hands = create(ctx, model, Delegate.CPU); handMode = "CPU";
        } catch (Throwable t) {
            handError = String.valueOf(t.getMessage());
        }
    }

    // VIDEO 模式：上一張找到手，下一張直接追蹤（不用每張重新跑「找手」的模型），延遲少很多
    private static HandLandmarker create(Context ctx, byte[] model, Delegate d) {
        ByteBuffer buf = ByteBuffer.allocateDirect(model.length).order(ByteOrder.nativeOrder());
        buf.put(model);
        buf.rewind();
        HandLandmarker.HandLandmarkerOptions opts = HandLandmarker.HandLandmarkerOptions.builder()
                .setBaseOptions(BaseOptions.builder().setModelAssetBuffer(buf).setDelegate(d).build())
                .setRunningMode(RunningMode.VIDEO)
                .setNumHands(2)   // 畫面裡可能有別人的手：最多找 2 隻，取最大的
                .setMinHandDetectionConfidence(0.5f)
                .setMinHandPresenceConfidence(0.5f)
                .setMinTrackingConfidence(0.4f)
                .build();
        return HandLandmarker.createFromOptions(ctx, opts);
    }

    public String getHandMode() { return handMode; }

    public boolean submit(final byte[] rgba, final int w, final int h, final int flags) {
        if (busy) return false;
        busy = true;
        try {
            worker.execute(new Runnable() {
                @Override public void run() {
                    float[] r = new float[LEN];
                    try {
                        if (bmp == null || bmp.getWidth() != w || bmp.getHeight() != h) bmp = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888);
                        bmp.copyPixelsFromBuffer(ByteBuffer.wrap(rgba));
                        if ((flags & 1) != 0) {
                            try {
                                Pose pose = Tasks.await(detector.process(InputImage.fromBitmap(bmp, 0)));
                                fillBody(r, pose, w, h);
                            } catch (Throwable ignored) { }
                        }
                        if ((flags & 2) != 0) {
                            ensureHands();
                            if (hands != null) {
                                try {
                                    MPImage img = new BitmapImageBuilder(bmp).build();
                                    long ts = Math.max(lastTs + 1, android.os.SystemClock.uptimeMillis());
                                    lastTs = ts;
                                    fillHand(r, hands.detectForVideo(img, ts), w, h);
                                } catch (Throwable ignored) { }
                            }
                        }
                    } catch (Throwable ignored) { }
                    r[0] = ++seq;
                    latest = r;
                    busy = false;
                }
            });
            return true;
        } catch (Throwable t) {
            busy = false;
            return false;
        }
    }

    private static void fillBody(float[] r, Pose pose, int w, int h) {
        if (pose == null || pose.getAllPoseLandmarks().isEmpty()) return;
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

    private static void fillHand(float[] r, HandLandmarkerResult res, int w, int h) {
        if (res == null) return;
        List<List<NormalizedLandmark>> all = res.landmarks();
        if (all == null || all.isEmpty()) return;
        // 取畫面上最大的手（手腕→中指根部最長）：自己伸到鏡頭前的手，不是遠處對手的手
        List<NormalizedLandmark> lm = null; float bestLen = -1f;
        for (List<NormalizedLandmark> c : all) {
            if (c == null || c.size() < 10) continue;
            float dx = (c.get(0).x() - c.get(9).x()) * w, dy = (c.get(0).y() - c.get(9).y()) * h;
            float len = (float) Math.sqrt(dx * dx + dy * dy);
            if (len > bestLen) { bestLen = len; lm = c; }
        }
        if (lm == null) return;
        int o = 2 + BODY * 3;
        r[o] = 1f;
        for (int i = 0; i < HAND && i < lm.size(); i++) {
            r[o + 1 + i * 3] = lm.get(i).x();
            r[o + 2 + i * 3] = lm.get(i).y();
            r[o + 3 + i * 3] = 1f;
        }
    }

    /** 取出最新結果；還沒有新結果時回傳空陣列 */
    public float[] poll() {
        float[] r = latest;
        latest = null;
        return r != null ? r : new float[0];
    }
}
