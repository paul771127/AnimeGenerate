package com.paul771127.spellduel;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;

import java.util.ArrayList;

/**
 * SpellDuel：用手機內建的語音辨識（android.speech.SpeechRecognizer）聽玩家喊技能名稱。
 *   語言 zh-TW、盡量用離線辨識、回報中途結果（partial），講完一句會自動重新開始聆聽（連續辨識）。
 *   SpeechRecognizer 規定只能在 UI 執行緒建立與呼叫，所以全部丟到 UI 執行緒執行。
 * Unity 每幀呼叫 poll() 取回新的辨識文字：每一筆前面加 "P:"（中途結果）或 "F:"（最後結果，可能有多個候選）。
 * 麥克風權限由 Unity（C#）端要求；這裡只檢查，沒有權限就回報錯誤。
 * 只用 Android 平台內建 API，不需要額外的 Gradle 相依套件。
 */
public class SpeechBridge {
    private static final String LANG = "zh-TW";
    private static final int MAX_QUEUE = 64;
    // Android 11 以後新增的錯誤碼（用數字，避免編譯時的 SDK 沒有這些常數）
    private static final int ERROR_TOO_MANY_REQUESTS = 10, ERROR_SERVER_DISCONNECTED = 11,
            ERROR_LANGUAGE_NOT_SUPPORTED = 12, ERROR_LANGUAGE_UNAVAILABLE = 13;

    private final Activity activity;
    private final Handler ui = new Handler(Looper.getMainLooper());
    private final ArrayList<String> queue = new ArrayList<String>();

    // 以下只在 UI 執行緒存取
    private SpeechRecognizer recognizer;
    private Listener listener;
    private int failures;            // 連續失敗次數（越多等越久再重試）
    private boolean offline = true;  // 離線辨識不支援這個語言時改用一般模式
    private String lastPartial = "";

    private volatile boolean wanted, listening;
    private volatile long lastEvent;
    private volatile String error = "";
    private volatile ArrayList<String> hints;

    public SpeechBridge() {
        activity = com.unity3d.player.UnityPlayer.currentActivity;
    }

    /** 這支手機有沒有可用的語音辨識服務 */
    public boolean isAvailable() {
        try {
            return activity != null && SpeechRecognizer.isRecognitionAvailable(activity);
        } catch (Throwable t) {
            error = "語音辨識檢查失敗：" + t.getMessage();
            return false;
        }
    }

    public boolean isListening() { return wanted && listening; }
    public String getError() { return error; }

    /** 提示辨識器可能出現的詞（技能名稱，以換行分隔）；Android 13 以上的部分辨識器會參考 */
    public void setHints(String lines) {
        ArrayList<String> list = new ArrayList<String>();
        if (lines != null)
            for (String s : lines.split("\n")) if (s.trim().length() > 0) list.add(s.trim());
        hints = list;
    }

    public void start() {
        if (activity == null) { error = "找不到 Activity"; return; }
        wanted = true;
        error = "";
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                failures = 0;
                ui.removeCallbacks(restartTask);
                ui.removeCallbacks(watchdog);
                ui.postDelayed(watchdog, 1000);
                listen(true);
            }
        });
    }

    public void stop() {
        wanted = false;
        listening = false;
        if (activity == null) return;
        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                ui.removeCallbacks(restartTask);
                ui.removeCallbacks(watchdog);
                destroy();
            }
        });
    }

    /** 取出上次 poll 之後的新辨識結果（"P:文字" 或 "F:文字"） */
    public String[] poll() {
        synchronized (queue) {
            String[] r = queue.toArray(new String[0]);
            queue.clear();
            return r;
        }
    }

    // ------------------------------------------------------------ 以下在 UI 執行緒執行

    private void push(String s) {
        synchronized (queue) {
            if (queue.size() >= MAX_QUEUE) queue.remove(0);
            queue.add(s);
        }
    }

    private final Runnable restartTask = new Runnable() {
        @Override public void run() { listen(false); }
    };

    // 有些手機的辨識服務偶爾會卡住、不再回呼：太久沒有任何事件就整個重建
    private final Runnable watchdog = new Runnable() {
        @Override public void run() {
            if (!wanted) return;
            if (SystemClock.elapsedRealtime() - lastEvent > 10000) {
                destroy();
                listen(true);
            }
            ui.postDelayed(this, 1000);
        }
    };

    private void scheduleRestart(long delayMs) {
        listening = false;
        ui.removeCallbacks(restartTask);
        if (wanted) ui.postDelayed(restartTask, delayMs);
    }

    private void destroy() {
        listening = false;
        if (recognizer != null) {
            try { recognizer.cancel(); } catch (Throwable ignored) { }
            try { recognizer.destroy(); } catch (Throwable ignored) { }
        }
        recognizer = null;
        listener = null;
    }

    private Intent buildIntent() {
        Intent it = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        it.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        it.putExtra(RecognizerIntent.EXTRA_LANGUAGE, LANG);
        it.putExtra(RecognizerIntent.EXTRA_LANGUAGE_PREFERENCE, LANG);
        it.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
        it.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 5);
        it.putExtra(RecognizerIntent.EXTRA_CALLING_PACKAGE, activity.getPackageName());
        if (offline) it.putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, true);
        // 技能名稱都很短：講完停頓一下就結束這一句（不是每個辨識服務都會採用）
        it.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS, 700L);
        it.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS, 500L);
        ArrayList<String> h = hints;
        if (h != null && !h.isEmpty()) it.putStringArrayListExtra("android.speech.extra.BIASING_STRINGS", h);   // Android 13+
        return it;
    }

    private void listen(boolean fresh) {
        if (!wanted) return;
        lastEvent = SystemClock.elapsedRealtime();
        if (activity.checkSelfPermission("android.permission.RECORD_AUDIO") != PackageManager.PERMISSION_GRANTED) {
            error = "沒有麥克風權限";
            wanted = false;
            destroy();
            return;
        }
        try {
            if (fresh || recognizer == null) {
                destroy();
                recognizer = SpeechRecognizer.createSpeechRecognizer(activity);
                listener = new Listener();
                recognizer.setRecognitionListener(listener);
            } else {
                recognizer.cancel();
            }
            lastPartial = "";
            recognizer.startListening(buildIntent());
        } catch (Throwable t) {
            error = "語音辨識啟動失敗：" + t.getMessage();
            failures++;
            destroy();
            scheduleRestart(Math.min(5000, 500L * failures));
        }
    }

    private void emit(Bundle b, boolean fin) {
        if (b == null) return;
        ArrayList<String> list = b.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
        if (list == null) return;
        if (!fin) {
            // 中途結果只取第一個，內容沒變就不重複送
            if (list.isEmpty()) return;
            String s = clean(list.get(0));
            if (s.length() == 0 || s.equals(lastPartial)) return;
            lastPartial = s;
            push("P:" + s);
            return;
        }
        for (String r : list) {
            String s = clean(r);
            if (s.length() > 0) push("F:" + s);
        }
    }

    private static String clean(String s) {
        return s == null ? "" : s.replace('\n', ' ').replace('\r', ' ').trim();
    }

    private class Listener implements RecognitionListener {
        private boolean current() { return listener == this && wanted; }

        @Override public void onReadyForSpeech(Bundle params) {
            if (!current()) return;
            lastEvent = SystemClock.elapsedRealtime();
            listening = true;
        }
        @Override public void onBeginningOfSpeech() { if (current()) lastEvent = SystemClock.elapsedRealtime(); }
        @Override public void onRmsChanged(float rmsdB) { if (current()) lastEvent = SystemClock.elapsedRealtime(); }
        @Override public void onBufferReceived(byte[] buffer) { }
        @Override public void onEndOfSpeech() { if (current()) lastEvent = SystemClock.elapsedRealtime(); }

        @Override public void onPartialResults(Bundle partialResults) {
            if (!current()) return;
            lastEvent = SystemClock.elapsedRealtime();
            emit(partialResults, false);
        }

        @Override public void onResults(Bundle results) {
            if (!current()) return;
            lastEvent = SystemClock.elapsedRealtime();
            emit(results, true);
            failures = 0;
            error = "";
            scheduleRestart(30);
        }

        @Override public void onError(int code) {
            if (!current()) return;
            lastEvent = SystemClock.elapsedRealtime();
            switch (code) {
                case SpeechRecognizer.ERROR_NO_MATCH:
                case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                    // 沒聽到話／聽不懂：正常情況，馬上再聽
                    failures = 0;
                    scheduleRestart(30);
                    return;
                case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                case SpeechRecognizer.ERROR_CLIENT:
                case ERROR_SERVER_DISCONNECTED:
                    // 辨識服務還在忙（或狀態亂了）：整個重建
                    failures++;
                    destroy();
                    ui.removeCallbacks(restartTask);
                    if (wanted) ui.postDelayed(new Runnable() {
                        @Override public void run() { listen(true); }
                    }, Math.min(3000, 250L * failures));
                    return;
                case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                    error = "沒有麥克風權限";
                    wanted = false;
                    destroy();
                    return;
                case ERROR_LANGUAGE_NOT_SUPPORTED:
                case ERROR_LANGUAGE_UNAVAILABLE:
                    // 離線模型沒有中文：改用一般（可能需要網路）模式
                    if (offline) { offline = false; scheduleRestart(100); return; }
                    error = "這支手機的語音辨識不支援中文（zh-TW）";
                    break;
                case SpeechRecognizer.ERROR_NETWORK:
                case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                case SpeechRecognizer.ERROR_SERVER:
                    error = "語音辨識需要網路（沒有離線中文模型）";
                    break;
                case SpeechRecognizer.ERROR_AUDIO:
                    error = "麥克風錄音失敗（可能被其他程式占用）";
                    break;
                case ERROR_TOO_MANY_REQUESTS:
                    error = "語音辨識請求太頻繁";
                    break;
                default:
                    error = "語音辨識錯誤（" + code + "）";
                    break;
            }
            failures++;
            scheduleRestart(Math.min(5000, 400L * failures));
        }

        @Override public void onEvent(int eventType, Bundle params) { }
    }
}
