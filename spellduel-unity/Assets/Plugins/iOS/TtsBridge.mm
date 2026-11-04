// 文字轉語音（iOS：AVSpeechSynthesizer，台灣中文的聲音）：背誦試煉、快問快答時電腦唸題目（Tts.cs）。
// 聲音走 SpeechBridge 設好的 PlayAndRecord＋DefaultToSpeaker，所以從喇叭出來；辨識那邊由 C# 在唸的期間忽略聽到的字。
#import <AVFoundation/AVFoundation.h>

static AVSpeechSynthesizer* sdSynth = nil;

extern "C" {

void sd_tts_speak(const char* utf8, float rate)
{
    NSString* s = [NSString stringWithUTF8String:(utf8 ? utf8 : "")];
    if (s.length == 0) return;
    if (sdSynth == nil) sdSynth = [[AVSpeechSynthesizer alloc] init];
    [sdSynth stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
    AVSpeechUtterance* u = [AVSpeechUtterance speechUtteranceWithString:s];
    AVSpeechSynthesisVoice* v = [AVSpeechSynthesisVoice voiceWithLanguage:@"zh-TW"];
    if (v == nil) v = [AVSpeechSynthesisVoice voiceWithLanguage:@"zh-CN"];
    u.voice = v;
    u.rate = rate > 0 ? rate : AVSpeechUtteranceDefaultSpeechRate;
    [sdSynth speakUtterance:u];
}

void sd_tts_stop()
{
    if (sdSynth != nil) [sdSynth stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
}

int sd_tts_speaking()
{
    return (sdSynth != nil && sdSynth.speaking) ? 1 : 0;
}

}
