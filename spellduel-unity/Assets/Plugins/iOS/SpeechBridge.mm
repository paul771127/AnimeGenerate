// SpellDuel：用 Apple Speech 框架（SFSpeechRecognizer）聽玩家喊技能名稱。
//   語言 zh-TW（沒有就改用 zh-CN／zh-HK）；支援時強制在手機上辨識（不需要網路、沒有次數限制）。
//   麥克風聲音用 AVAudioEngine 取得，送進 SFSpeechAudioBufferRecognitionRequest，回報中途結果。
//   連續辨識：一句話講完（約 0.9 秒沒有新文字）、任務結束或出錯、或任務跑了 50 秒，就換一個新任務。
// Unity 每幀呼叫 sd_speech_poll 取回新的辨識文字：一行一筆、UTF-8，"P:"＝中途結果、"F:"＝最後結果。
// 需要連結 Speech.framework、AVFoundation.framework；Info.plist 需要 NSSpeechRecognitionUsageDescription
// 與 NSMicrophoneUsageDescription。
#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <Speech/Speech.h>
#include <pthread.h>
#include <string.h>
#include <string>

#if __has_feature(objc_arc)
#define SP_SET(var, val) do { var = (val); } while (0)
#define SP_AUTORELEASE(x) (x)
#define SP_MRC_RETAIN(x)
#define SP_MRC_RELEASE(x)
#else
#define SP_SET(var, val) do { id _sp_new = (val); [_sp_new retain]; [var release]; var = _sp_new; } while (0)
#define SP_AUTORELEASE(x) [(x) autorelease]
#define SP_MRC_RETAIN(x) [(x) retain]
#define SP_MRC_RELEASE(x) [(x) release]
#endif

#define SP_MAX_OUT 16384
#define SP_TASK_SECONDS 50.0      // 單一辨識任務最長時間（伺服器辨識限制約 1 分鐘）
#define SP_SILENCE_SECONDS 0.9    // 有文字後這麼久沒有變化 → 這句話講完了

static pthread_mutex_t sp_lock = PTHREAD_MUTEX_INITIALIZER;
static std::string sp_out;       // 待取回的辨識結果（加鎖）
static std::string sp_status;    // 狀態／錯誤文字（加鎖）
static SFSpeechAudioBufferRecognitionRequest* sp_req = nil;   // 音訊執行緒也會讀取（加鎖）

// 以下只在主執行緒存取
static SFSpeechRecognizer* sp_rec = nil;
static AVAudioEngine* sp_engine = nil;
static SFSpeechRecognitionTask* sp_task = nil;
static SFSpeechRecognitionTask* sp_oldTask = nil;   // 已 endAudio、等最後結果的前一個任務
static NSArray* sp_hints = nil;
static dispatch_source_t sp_timer = nil;
static int sp_wanted = 0;
static int sp_gen = 0;             // 任務代號：舊任務的回呼不再觸發重開
static int sp_failures = 0;
static int sp_restartPending = 0;
static double sp_taskStart = 0, sp_lastChange = 0, sp_lastEngineTry = 0;
static int sp_hasText = 0;
static int sp_everText = 0;        // 這次開始後有沒有辨識出任何文字
static int sp_noOnDevice = 0;      // 手機上的模型一直失敗 → 改用伺服器辨識
static std::string sp_lastPartial;

static double sp_now(void) { return [NSDate timeIntervalSinceReferenceDate]; }

static void sp_set_status(const char* s)
{
    pthread_mutex_lock(&sp_lock);
    sp_status = s ? s : "";
    pthread_mutex_unlock(&sp_lock);
}

static void sp_set_status_ns(NSString* prefix, NSError* err)
{
    NSString* s = err ? [NSString stringWithFormat:@"%@：%@", prefix, err.localizedDescription] : prefix;
    sp_set_status(s.UTF8String);
}

static void sp_push(const char* kind, NSString* text)
{
    if (text == nil || text.length == 0) return;
    NSString* t = [[text stringByReplacingOccurrencesOfString:@"\n" withString:@" "]
                   stringByReplacingOccurrencesOfString:@"\r" withString:@" "];
    const char* u = t.UTF8String;
    if (u == NULL || u[0] == 0) return;
    pthread_mutex_lock(&sp_lock);
    sp_out += kind;
    sp_out += u;
    sp_out += '\n';
    if (sp_out.size() > SP_MAX_OUT)   // 太久沒取：丟掉最舊的
    {
        size_t cut = sp_out.find('\n', sp_out.size() - SP_MAX_OUT);
        sp_out.erase(0, cut == std::string::npos ? sp_out.size() : cut + 1);
    }
    pthread_mutex_unlock(&sp_lock);
}

static SFSpeechRecognizer* sp_make_recognizer(void)
{
    NSArray* ids = @[ @"zh-TW", @"zh_TW", @"zh-Hant-TW", @"zh-CN", @"zh-HK" ];
    for (NSString* lid in ids)
    {
        SFSpeechRecognizer* r = [[SFSpeechRecognizer alloc] initWithLocale:[NSLocale localeWithLocaleIdentifier:lid]];
        if (r != nil) return SP_AUTORELEASE(r);
    }
    return nil;
}

static void sp_start_task(void);
static void sp_teardown(void);

static void sp_schedule_task(double delay)
{
    if (sp_restartPending) return;
    sp_restartPending = 1;
    int gen = sp_gen;
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(delay * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        sp_restartPending = 0;
        if (sp_wanted && gen == sp_gen) sp_start_task();
    });
}

// 開一個新的辨識任務（麥克風繼續錄，聲音改送進新的 request）
static void sp_start_task(void)
{
    if (!sp_wanted || sp_rec == nil) return;
    int gen = ++sp_gen;

    // 前一個任務：結束送音，讓它回傳最後結果
    pthread_mutex_lock(&sp_lock);
    SFSpeechAudioBufferRecognitionRequest* prev = sp_req;
    SP_MRC_RETAIN(prev);
    pthread_mutex_unlock(&sp_lock);
    [prev endAudio];
    SP_MRC_RELEASE(prev);
    if (sp_oldTask != nil) [sp_oldTask cancel];
    SP_SET(sp_oldTask, sp_task);
    SP_SET(sp_task, nil);

    SFSpeechAudioBufferRecognitionRequest* req = SP_AUTORELEASE([[SFSpeechAudioBufferRecognitionRequest alloc] init]);
    req.shouldReportPartialResults = YES;
    req.taskHint = SFSpeechRecognitionTaskHintSearch;   // 短詞
    if (sp_hints != nil) req.contextualStrings = sp_hints;
    if (@available(iOS 13.0, *))
    {
        if (sp_rec.supportsOnDeviceRecognition && !sp_noOnDevice) req.requiresOnDeviceRecognition = YES;
    }

    pthread_mutex_lock(&sp_lock);
    SP_SET(sp_req, req);
    pthread_mutex_unlock(&sp_lock);

    sp_taskStart = sp_now();
    sp_lastChange = sp_taskStart;
    sp_hasText = 0;
    sp_lastPartial.clear();

    SFSpeechRecognitionTask* task = [sp_rec recognitionTaskWithRequest:req resultHandler:^(SFSpeechRecognitionResult* result, NSError* error) {
        // 回呼在 sp_rec.queue（預設為主執行緒）
        BOOL fin = NO;
        if (result != nil)
        {
            fin = result.isFinal;
            NSString* text = result.bestTranscription.formattedString;
            if (text.length > 0)
            {
                sp_everText = 1;
                if (fin) sp_push("F:", text);
                else if (gen == sp_gen)
                {
                    std::string u = text.UTF8String ? text.UTF8String : "";
                    if (u != sp_lastPartial)
                    {
                        sp_lastPartial = u;
                        sp_push("P:", text);
                        sp_hasText = 1;
                        sp_lastChange = sp_now();
                    }
                }
                else sp_push("P:", text);
            }
        }
        if (gen != sp_gen || !sp_wanted) return;
        if (fin)
        {
            sp_failures = 0;
            sp_set_status("聆聽中");
            sp_schedule_task(0.05);
        }
        else if (error != nil)
        {
            // 沒講話而逾時（kAFAssistantErrorDomain 1110 等）也會走到這裡：換新任務繼續聽
            if (++sp_failures >= 3) sp_set_status_ns(@"語音辨識錯誤", error);
            // 一直失敗、從沒辨識出文字：可能是手機上沒有中文模型 → 改用伺服器辨識（需要網路）
            if (sp_failures >= 3 && !sp_everText && !sp_noOnDevice) { sp_noOnDevice = 1; sp_failures = 0; }
            sp_schedule_task(sp_failures < 3 ? 0.2 : MIN(3.0, 0.5 * sp_failures));
        }
    }];
    SP_SET(sp_task, task);
}

// 設定音訊、開啟麥克風、開始第一個任務
static void sp_begin(void)
{
    if (!sp_wanted) return;
    sp_lastEngineTry = sp_now();
    if (sp_rec == nil) SP_SET(sp_rec, sp_make_recognizer());
    if (sp_rec == nil) { sp_set_status("這支手機的語音辨識不支援中文"); return; }

    NSError* err = nil;
    AVAudioSession* session = [AVAudioSession sharedInstance];
    // 錄音＋播放並存：聲音照常從喇叭出來、可和其他聲音混音、可用藍牙耳機
    AVAudioSessionCategoryOptions opts = AVAudioSessionCategoryOptionDefaultToSpeaker
                                       | AVAudioSessionCategoryOptionMixWithOthers
                                       | AVAudioSessionCategoryOptionAllowBluetooth;
    if (![session.category isEqualToString:AVAudioSessionCategoryPlayAndRecord] || (session.categoryOptions & opts) != opts)
    {
        if (![session setCategory:AVAudioSessionCategoryPlayAndRecord mode:AVAudioSessionModeDefault options:opts error:&err])
        {
            sp_set_status_ns(@"音訊設定失敗", err);
            err = nil;
        }
    }
    if (![session setActive:YES error:&err]) { sp_set_status_ns(@"音訊啟動失敗", err); err = nil; }

    sp_teardown();
    SP_SET(sp_engine, SP_AUTORELEASE([[AVAudioEngine alloc] init]));
    AVAudioInputNode* input = sp_engine.inputNode;
    AVAudioFormat* fmt = [input outputFormatForBus:0];
    if (fmt == nil || fmt.sampleRate <= 0 || fmt.channelCount == 0)
    {
        sp_set_status("麥克風無法使用");
        SP_SET(sp_engine, nil);
        return;
    }
    [input installTapOnBus:0 bufferSize:1024 format:fmt block:^(AVAudioPCMBuffer* buffer, AVAudioTime* when) {
        // 音訊執行緒：送進目前的 request
        pthread_mutex_lock(&sp_lock);
        SFSpeechAudioBufferRecognitionRequest* r = sp_req;
        SP_MRC_RETAIN(r);
        pthread_mutex_unlock(&sp_lock);
        [r appendAudioPCMBuffer:buffer];
        SP_MRC_RELEASE(r);
    }];
    [sp_engine prepare];
    if (![sp_engine startAndReturnError:&err])
    {
        sp_set_status_ns(@"麥克風啟動失敗", err);
        [input removeTapOnBus:0];
        SP_SET(sp_engine, nil);
        return;
    }
    sp_set_status("聆聽中");
    sp_start_task();
}

// 停止麥克風與所有任務（不關閉 AVAudioSession，以免影響 Unity 的聲音）
static void sp_teardown(void)
{
    sp_gen++;
    if (sp_engine != nil)
    {
        [sp_engine stop];
        [sp_engine.inputNode removeTapOnBus:0];
        SP_SET(sp_engine, nil);
    }
    pthread_mutex_lock(&sp_lock);
    SFSpeechAudioBufferRecognitionRequest* r = sp_req;
    SP_MRC_RETAIN(r);
    SP_SET(sp_req, nil);
    pthread_mutex_unlock(&sp_lock);
    [r endAudio];
    SP_MRC_RELEASE(r);
    if (sp_task != nil) [sp_task cancel];
    if (sp_oldTask != nil) [sp_oldTask cancel];
    SP_SET(sp_task, nil);
    SP_SET(sp_oldTask, nil);
}

// 每 0.25 秒：斷句、任務輪替、麥克風被中斷（來電、耳機插拔）時重開
static void sp_tick(void)
{
    if (!sp_wanted) return;
    double now = sp_now();
    if (sp_engine == nil || !sp_engine.isRunning)
    {
        if (now - sp_lastEngineTry > 1.5) sp_begin();
        return;
    }
    if (sp_restartPending) return;
    if ((sp_hasText && now - sp_lastChange > SP_SILENCE_SECONDS) || now - sp_taskStart > SP_TASK_SECONDS)
        sp_start_task();
}

static void sp_ensure_timer(void)
{
    if (sp_timer != nil) return;
    sp_timer = dispatch_source_create(DISPATCH_SOURCE_TYPE_TIMER, 0, 0, dispatch_get_main_queue());
    dispatch_source_set_timer(sp_timer, dispatch_time(DISPATCH_TIME_NOW, 250 * NSEC_PER_MSEC), 250 * NSEC_PER_MSEC, 50 * NSEC_PER_MSEC);
    dispatch_source_set_event_handler(sp_timer, ^{ sp_tick(); });
    dispatch_resume(sp_timer);
}

// 確認麥克風權限後開始
static void sp_after_speech_auth(void)
{
    if (!sp_wanted) return;
    AVAudioSession* session = [AVAudioSession sharedInstance];
    AVAudioSessionRecordPermission p = session.recordPermission;
    if (p == AVAudioSessionRecordPermissionGranted) { sp_begin(); sp_ensure_timer(); return; }
    if (p == AVAudioSessionRecordPermissionDenied) { sp_set_status("麥克風未授權（請到「設定」開啟）"); sp_wanted = 0; return; }
    sp_set_status("等待麥克風授權");
    [session requestRecordPermission:^(BOOL granted) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (!granted) { sp_set_status("麥克風未授權（請到「設定」開啟）"); sp_wanted = 0; return; }
            if (sp_wanted) { sp_begin(); sp_ensure_timer(); }
        });
    }];
}

static void sp_handle_auth(SFSpeechRecognizerAuthorizationStatus st)
{
    if (!sp_wanted) return;
    if (st == SFSpeechRecognizerAuthorizationStatusAuthorized) sp_after_speech_auth();
    else if (st == SFSpeechRecognizerAuthorizationStatusRestricted) { sp_set_status("這支手機限制使用語音辨識"); sp_wanted = 0; }
    else { sp_set_status("語音辨識未授權（請到「設定」開啟）"); sp_wanted = 0; }
}

extern "C" {

int sd_speech_available(void)
{
    if (@available(iOS 10.0, *)) {} else return 0;
    if (sp_rec == nil) SP_SET(sp_rec, sp_make_recognizer());
    return sp_rec != nil ? 1 : 0;
}

void sd_speech_start(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (sp_wanted) return;
        sp_wanted = 1;
        sp_failures = 0;
        sp_everText = 0;
        if (sp_rec == nil) SP_SET(sp_rec, sp_make_recognizer());
        if (sp_rec == nil) { sp_set_status("這支手機的語音辨識不支援中文"); sp_wanted = 0; return; }
        SFSpeechRecognizerAuthorizationStatus st = [SFSpeechRecognizer authorizationStatus];
        if (st != SFSpeechRecognizerAuthorizationStatusNotDetermined) { sp_handle_auth(st); return; }
        sp_set_status("等待語音辨識授權");
        [SFSpeechRecognizer requestAuthorization:^(SFSpeechRecognizerAuthorizationStatus s2) {
            dispatch_async(dispatch_get_main_queue(), ^{ sp_handle_auth(s2); });
        }];
    });
}

void sd_speech_stop(void)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        sp_wanted = 0;
        sp_teardown();
        sp_set_status("已停止");
    });
}

// 技能名稱提示（以換行分隔，UTF-8），下一個任務開始生效
void sd_speech_set_hints(const char* lines)
{
    NSString* s = lines ? [NSString stringWithUTF8String:lines] : nil;
    NSMutableArray* arr = [NSMutableArray array];
    for (NSString* part in [s componentsSeparatedByString:@"\n"])
    {
        NSString* t = [part stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
        if (t.length > 0) [arr addObject:t];
    }
    NSArray* copy = [NSArray arrayWithArray:arr];
    dispatch_async(dispatch_get_main_queue(), ^{ SP_SET(sp_hints, copy); });
}

// 取出新的辨識結果（多行 "P:文字"／"F:文字"，UTF-8、以 '\0' 結尾）；回傳寫入的位元組數，沒有就回傳 0
int sd_speech_poll(char* buf, int len)
{
    if (buf == NULL || len <= 1) return 0;
    int n = 0;
    pthread_mutex_lock(&sp_lock);
    if (!sp_out.empty())
    {
        // 只取完整的行（不切斷 UTF-8 字元）
        size_t end = sp_out.size() <= (size_t)(len - 1) ? sp_out.size() : sp_out.rfind('\n', (size_t)(len - 2));
        if (end == std::string::npos || end == 0)
        {
            // 單行就超過緩衝區：丟掉這一行
            size_t nl = sp_out.find('\n');
            sp_out.erase(0, nl == std::string::npos ? sp_out.size() : nl + 1);
        }
        else
        {
            if (end < sp_out.size()) end += 1;   // 含換行
            memcpy(buf, sp_out.data(), end);
            sp_out.erase(0, end);
            n = (int)end;
        }
    }
    pthread_mutex_unlock(&sp_lock);
    buf[n] = 0;
    return n;
}

// 狀態／錯誤文字（UTF-8、以 '\0' 結尾）；回傳位元組數
int sd_speech_status(char* buf, int len)
{
    if (buf == NULL || len <= 1) return 0;
    pthread_mutex_lock(&sp_lock);
    size_t n = sp_status.size();
    if (n > (size_t)(len - 1))
    {
        n = (size_t)(len - 1);
        while (n > 0 && (((unsigned char)sp_status[n]) & 0xC0) == 0x80) n--;   // 不切斷 UTF-8 字元
    }
    memcpy(buf, sp_status.data(), n);
    pthread_mutex_unlock(&sp_lock);
    buf[n] = 0;
    return (int)n;
}

}
