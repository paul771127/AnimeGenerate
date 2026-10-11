// 手機震動（Taptic Engine）：0 輕、1 中、2 重、3 成功、4 警告
#import <UIKit/UIKit.h>

extern "C" void sd_haptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (kind == 3 || kind == 4)
        {
            UINotificationFeedbackGenerator* g = [[UINotificationFeedbackGenerator alloc] init];
            [g notificationOccurred:(kind == 3 ? UINotificationFeedbackTypeSuccess : UINotificationFeedbackTypeWarning)];
#if !__has_feature(objc_arc)
            [g release];
#endif
            return;
        }
        UIImpactFeedbackStyle st = kind == 0 ? UIImpactFeedbackStyleLight : kind == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy;
        UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:st];
        [g impactOccurred];
#if !__has_feature(objc_arc)
        [g release];
#endif
    });
}
