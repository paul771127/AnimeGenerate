"""Validate the chapter expansion and preserve the four original boss conversations."""
from pathlib import Path
import hashlib,json,re
ROOT=Path(__file__).resolve().parents[1]
def main():
 s=(ROOT/'Assets/SpellDuel/Resources/story.txt').read_text(encoding='utf-8-sig')
 record=json.loads((ROOT/'Docs/minions/story_expansion.json').read_text(encoding='utf-8-sig'))
 digest=lambda text:hashlib.sha256(text.encode()).hexdigest()
 assert digest(s[:s.index('[chapter 2]')])==record['chapter1Sha256'],'Chapter 1 changed'
 matches=list(re.finditer(r'^\[stage ([\d-]+)\][\s\S]*?(?=^\[(?:stage|chapter) |\Z)',s,re.M))
 stages={m[1]:m[0] for m in matches};assert len(matches)==len(stages)==28
 previous='none'
 for ch in range(1,6):
  end=4 if ch==1 else 6
  for n in range(1,end+1):
   key=f'{ch}-{n}';block=stages[key];settings=dict(re.findall(r'^(theme|waves|xp|req)\s*=\s*(.+)$',block,re.M))
   assert settings['req']==previous,(key,'prerequisite',settings['req'],previous)
   assert (settings['waves']=='10')==(n==end),(key,'boss wave')
   assert '<before>' in block and '<after>' in block,key
   previous=key
 for ch in range(2,6):
  key=f'{ch}-6';block=stages[key]
  assert digest(block[block.index('<before>'):].strip())==record['bossDialogueSha256'][key],(key,'boss dialogue changed')
  sample=dict(re.findall(r'^(theme|waves|xp)\s*=\s*(.+)$',stages[f'{ch}-3'],re.M))
  for n in (4,5):
   key=f'{ch}-{n}';block=stages[key]
   assert dict(re.findall(r'^(theme|waves|xp)\s*=\s*(.+)$',block,re.M))==sample,(key,'temporary tuning')
   before,after=block.split('<before>',1)[1].split('<after>')
   for section in (before,after):
    lines=[l for l in section.splitlines() if '：' in l and not l.startswith('背景：')]
    assert 5<=len(lines)<=8,(key,'dialogue count',len(lines))
    assert all(len(l.split('：',1)[1])<=60 for l in lines),(key,'long dialogue')
 print('28 stages; 8 new stages; chapter 1 and 4 original boss conversations preserved; prerequisites and dialogue lengths OK')
if __name__=='__main__':main()
