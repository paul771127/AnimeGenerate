// SpellDuel：用 Apple Vision（iOS 14 以上）偵測畫面中的人（攻擊方鏡頭輔助定位對手）。
// Unity 送來 RGBA 影像（由上往下），在背景執行緒偵測；結果用 sd_pose_poll 取回。
// 結果格式：[序號, 有沒有找到人, 7 個關鍵點 ×（x, y, 可信度)]，x/y 為 0～1、左上為原點。
#import <Foundation/Foundation.h>
#import <Vision/Vision.h>
#import <CoreGraphics/CoreGraphics.h>
#include <pthread.h>
#include <stdlib.h>
#include <string.h>

#define SD_COUNT 7
#define SD_LEN (2 + SD_COUNT * 3)

static volatile int sd_busy = 0;
static float sd_latest[SD_LEN];
static int sd_has_new = 0;
static int sd_seq = 0;
static pthread_mutex_t sd_lock = PTHREAD_MUTEX_INITIALIZER;
static dispatch_queue_t sd_queue = nil;

static void sd_detect(unsigned char* rgba, int w, int h, float* r) API_AVAILABLE(ios(14.0));
static void sd_detect(unsigned char* rgba, int w, int h, float* r)
{
    CGColorSpaceRef cs = CGColorSpaceCreateDeviceRGB();
    CGContextRef ctx = CGBitmapContextCreate(rgba, w, h, 8, (size_t)w * 4, cs,
                                             kCGImageAlphaNoneSkipLast | kCGBitmapByteOrder32Big);
    CGImageRef img = ctx ? CGBitmapContextCreateImage(ctx) : NULL;
    if (img)
    {
        VNImageRequestHandler* handler = [[VNImageRequestHandler alloc] initWithCGImage:img options:@{}];
        VNDetectHumanBodyPoseRequest* req = [[VNDetectHumanBodyPoseRequest alloc] init];
        NSError* err = nil;
        if ([handler performRequests:@[req] error:&err] && req.results.count > 0)
        {
            // 畫面裡有好幾個人時，取可信度最高的
            VNHumanBodyPoseObservation* best = nil;
            for (VNHumanBodyPoseObservation* o in req.results)
                if (best == nil || o.confidence > best.confidence) best = o;
            NSArray<VNHumanBodyPoseObservationJointName>* names = @[
                VNHumanBodyPoseObservationJointNameNose,
                VNHumanBodyPoseObservationJointNameLeftShoulder, VNHumanBodyPoseObservationJointNameRightShoulder,
                VNHumanBodyPoseObservationJointNameLeftHip, VNHumanBodyPoseObservationJointNameRightHip,
                VNHumanBodyPoseObservationJointNameLeftAnkle, VNHumanBodyPoseObservationJointNameRightAnkle,
            ];
            r[1] = 1.f;
            for (int i = 0; i < SD_COUNT; i++)
            {
                NSError* e2 = nil;
                VNRecognizedPoint* p = [best recognizedPointForJointName:names[i] error:&e2];
                if (p == nil) continue;
                r[2 + i * 3] = (float)p.location.x;
                r[3 + i * 3] = 1.f - (float)p.location.y;   // Vision 以左下為原點 → 改成左上
                r[4 + i * 3] = p.confidence;
            }
        }
#if !__has_feature(objc_arc)
        [handler release];
        [req release];
#endif
        CGImageRelease(img);
    }
    if (ctx) CGContextRelease(ctx);
    CGColorSpaceRelease(cs);
}

extern "C" {

int sd_pose_available(void)
{
    if (@available(iOS 14.0, *)) return 1;
    return 0;
}

int sd_pose_submit(const unsigned char* rgba, int w, int h)
{
    if (sd_busy || rgba == NULL || w <= 0 || h <= 0) return 0;
    if (@available(iOS 14.0, *)) {} else return 0;
    sd_busy = 1;
    size_t n = (size_t)w * h * 4;
    unsigned char* copy = (unsigned char*)malloc(n);
    if (copy == NULL) { sd_busy = 0; return 0; }
    memcpy(copy, rgba, n);
    if (sd_queue == nil) sd_queue = dispatch_queue_create("spellduel.pose", DISPATCH_QUEUE_SERIAL);
    dispatch_async(sd_queue, ^{
        float r[SD_LEN];
        memset(r, 0, sizeof(r));
        @autoreleasepool {
            if (@available(iOS 14.0, *)) sd_detect(copy, w, h, r);
        }
        free(copy);
        pthread_mutex_lock(&sd_lock);
        r[0] = (float)(++sd_seq);
        memcpy(sd_latest, r, sizeof(r));
        sd_has_new = 1;
        pthread_mutex_unlock(&sd_lock);
        sd_busy = 0;
    });
    return 1;
}

// 有新結果時填入 result 並回傳 1；否則回傳 0
int sd_pose_poll(float* result, int n)
{
    int got = 0;
    pthread_mutex_lock(&sd_lock);
    if (sd_has_new && result != NULL && n >= SD_LEN)
    {
        memcpy(result, sd_latest, sizeof(float) * SD_LEN);
        sd_has_new = 0;
        got = 1;
    }
    pthread_mutex_unlock(&sd_lock);
    return got;
}

}
