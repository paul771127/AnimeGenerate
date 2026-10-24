# 選角職業頭像：完整 16 職業

使用者已確認第一批 novice、swordsman、mage 的主角長相與畫風，本批完成其餘 13 張。第一批三張 PNG 的 SHA-256 與確認版本完全相同，沒有重新生成或改色。

- [16 職業並排總覽](all_classes.jpg)
- 完整立繪與實際小頭像裁切：[初心者／劍士／獵人／法師](portraits_1.jpg)、[刺客／牧師／聖騎士／劍仙](portraits_2.jpg)、[神射手／遊俠／大魔導士／召喚師](portraits_3.jpg)、[忍者／影舞者／主教／審判者](portraits_4.jpg)。
- [新增資源與雜湊](assets.json)、[本批提示詞](prompts.json)、[職業構圖設定](designs.json)、[16 張檢查結果](validation.json)。第一批提示詞仍在 `../story2d_classes_stage1/prompts.json`。

正式資源均位於 `Assets/SpellDuel/Resources/Story2D/classes/{id}.png`，768×1152、四通道 RGBA、真正透明背景。meta 沿用 chars，關閉 mipmap、最大 2048、保留 alpha、`nPOTScale: 0`，新資源 GUID 獨立。

## 主角、裝備與構圖

使用內建 image_gen 的 identity-preserve 編輯，參照已確認的初心者母圖及劍士／法師完成圖，保持年輕冒險者的短栗髮、側分瀏海、琥珀眼、臉型、膚色與朝右前方的角度。只變換職業服裝、武器、姿勢與光效。沿用相同縮放、定位和色盤匯出；主教另調垂直位置以容納完整高冠，確保臉仍在指定裁切內。預覽下排直接採用寬 30%～70%、高 2%～30% 的範圍，非另畫的頭像。

| id | 主要辨識元素 |
|---|---|
| archer | 綠色皮甲、長弓、拉箭姿勢 |
| assassin | 深色交叉綁帶、雙匕首、紅色刃光 |
| priest | 白金法衣、權杖、手掌聖光 |
| paladin | 白金重甲、大盾、握持長劍 |
| berserker | 劍仙白色交領袍、紅白劍氣、握劍與飛劍；內部 id 保留 |
| marksman | 藍金狙擊裝、長弓、外翻單眼瞄準鏡，兩眼可見 |
| ranger | 翠綠斗篷、手臂獵鷹、握持木製長弓 |
| archmage | 深紫華麗法袍、握杖、浮空魔導書、雷光 |
| summoner | 橘色契約服、短杖、火靈與小石像 |
| ninja | 灰藍忍裝、環柄苦無、另一手結印 |
| shadowdancer | 紫色短戰裝、雙毒刃、身後半透明分身 |
| bishop | 完整高冠、聖典、權杖、金色障壁 |
| inquisitor | 橘金審判裝、握持長槍、側下方光柱 |

遊俠初稿誤持劍，已用 image_gen 定點修改為長弓。主教初稿高冠頂端遭裁切，已重做完整高冠構圖。兩張都保持同一位主角身份，未用程式補畫裝備或五官。所有武器都有手握持；法書、飛劍、召喚物與分身依職業設計懸浮／陪伴。光效不遮擋主要臉部。

這些預覽為資源排版示意，不是 Unity 或手機實機截圖。

## 容量與檢查

16 張職業圖共 **10,819,896 bytes（10.82 MB／10.32 MiB）**。既有對話立繪與背景仍是 39,273,419 bytes；兩類合計 **50,093,315 bytes（50.09 MB／47.77 MiB）**，超過原劇情批次提出的 40 MB 軟目標。本批保留明確指定的 768×1152 與已確認首批的像素，不以降低舊立繪品質來騰出容量。

`check_story2d.py` 現在分別列出對話圖與新增職業圖的容量，保留對原對話圖的 40 MiB 檢查，並明確回報合併後超標。職業圖完整性由 `check_story2d_classes.py --all` 檢查，沒有隱藏額外檔案或把超標記錄為未超標。

```sh
python spellduel-unity/Tools/check_story2d_classes.py --all
python spellduel-unity/Tools/check_story2d.py
python spellduel-unity/Tools/check_dungeons.py
```

16 張職業圖尺寸、RGBA、alpha、裁切有內容、meta、唯一 GUID、首批檔案不變與預覽齊全檢查通過；既有 31 位對話角色的 150 張表情、18 張背景和非臉部像素一致性檢查通過；7 個副本 0 個錯誤。沒有修改任何 Runtime、流程、戰鬥或數值程式。

UNITY_ANDROID／UNITY_IOS 的實際編譯以這次提交的 GitHub Actions 為準。
