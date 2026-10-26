# 用 Whisper 把每句配音聽寫回來，和 emotion_script.json 的 tts_text 逐字比對拼音，找出可能念錯的字。
# 在 narration/ 底下執行：python ../tools/narration_check.py [whisper 模型，預設 large-v3 或已下載的最大模型] [ONLY=檔名,...]
# 比對方式：兩邊都轉成帶聲調的拼音（tts_text 裡 [h][ái] 這種標音直接當成該字的讀音），用 difflib 對齊，
# 列出「取代」的位置（Whisper 聽到的音 ≠ 預期的音）。怪物聲（巨像、樹王）經過降音與回聲，Whisper 本身也會聽錯，要人工再確認。
import difflib, json, os, re, sys
from pathlib import Path
import whisper
from pypinyin import pinyin, Style

narr = Path('.').resolve()
script = json.loads((narr / 'emotion_script.json').read_text(encoding='utf-8'))
only = {s for s in os.environ.get('ONLY', '').split(',') if s}
model_name = sys.argv[1] if len(sys.argv) > 1 else 'large-v3'
model = whisper.load_model(model_name)

TAG = re.compile(r'\[([a-zü]*)\]\[([^\]]+)\]')   # [聲母][帶調韻母]

def expected_syllables(tts_text):
    out = []
    pos = 0
    for m in TAG.finditer(tts_text):
        for ch in tts_text[pos:m.start()]:
            if '一' <= ch <= '鿿':
                out.append((ch, pinyin(ch, style=Style.TONE)[0][0]))
        out.append(('□', m.group(1) + m.group(2)))   # 標了音的字：用標的音
        pos = m.end()
    for ch in tts_text[pos:]:
        if '一' <= ch <= '鿿':
            out.append((ch, pinyin(ch, style=Style.TONE)[0][0]))
    return out

def heard_syllables(text):
    return [(ch, pinyin(ch, style=Style.TONE)[0][0]) for ch in text if '一' <= ch <= '鿿']

def strip_tone(s): return re.sub(r'[āáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜ]', lambda m: 'aeiouü'['āáǎàēéěèīíǐìōóǒòūúǔùǖǘǚǜ'.index(m.group(0)) // 4], s)

report = []
for video, items in script['videos'].items():
    for it in items:
        stem = it['file'][:-4]
        if only and stem not in only: continue
        tts_text = it.get('tts_text', it['text'])
        plain = TAG.sub(lambda m: '□', tts_text)
        # 提示只給語言風格，不能放原句（放了 Whisper 會照抄提示而不是聽音）
        res = model.transcribe(str(narr / it['file']), language='zh', initial_prompt='以下是简体中文的游戏旁白。', fp16=True, temperature=0)
        heard = res['text'].strip()
        exp = expected_syllables(tts_text); got = heard_syllables(heard)
        a = [strip_tone(p) for _, p in exp]; b = [strip_tone(p) for _, p in got]
        diffs = []
        for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(a=a, b=b, autojunk=False).get_opcodes():
            if tag == 'replace':
                diffs.append(f"預期「{''.join(c for c, _ in exp[i1:i2])}」({' '.join(p for _, p in exp[i1:i2])}) 聽到「{''.join(c for c, _ in got[j1:j2])}」({' '.join(p for _, p in got[j1:j2])})")
            elif tag == 'delete':
                diffs.append(f"沒聽到「{''.join(c for c, _ in exp[i1:i2])}」")
            elif tag == 'insert':
                diffs.append(f"多出「{''.join(c for c, _ in got[j1:j2])}」")
        flag = '❌' if diffs else '✅'
        line = f"{flag} {it['file']} [{it['speaker']}]\n   原文：{it['text']}\n   聽到：{heard}" + ''.join(f"\n   - {x}" for x in diffs)
        print(line, flush=True)
        report.append({'file': it['file'], 'speaker': it['speaker'], 'heard': heard, 'diffs': diffs})
(narr / 'check_report.json').write_text(json.dumps(report, ensure_ascii=False, indent=1), encoding='utf-8')
