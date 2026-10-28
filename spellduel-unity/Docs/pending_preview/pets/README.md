# 寵物 B2 接線與預覽

本批保留Pets.cs、Combat.Pets.cs、SoloBattle.Pets.cs及所有戰鬥、數值、存檔、網路功能。使用者定案的40%經驗、菁英HP1.5／攻擊1.3與各城誘餌名單都從既有資料讀取。

## 呼叫入口

- `PetDress.Apply(MinionRig rig, bool elite)`：SetVariant／SetDress後呼叫；重複Apply不重複加配件。換kind後配件會重綁。普通青綠織帶／身份扣／友方雙線環；菁英雙金羽與斷續金環。花用莖環、水母用傘緣緞結。遊戲中友方環沿WorldFrame貼地，死亡隱藏。
- `rig.SetEating(true/false)`：在原有動畫上疊加小幅前傾咀嚼，解除會平順還原；每次先清除上一幀疊加，不累積旋轉。受擊原動作保留，出手和死亡優先。這個入口不會讓怪停止攻擊、扣餌或擲收服；仍由Combat.Pets的狀態控制。
- **`PetArtFx.Capture(Vector3 pos, bool success)`**：場景座標，1.3秒局部繩結／爪印或鬆開碎光。需求的PetFx名稱已被Pets.cs技能效果enum使用，因此美術類改名PetArtFx，未改該enum。請將成功／失敗視覺事件接此入口，不把暫時馴服當成永久收藏。
- `SkillIcons.Get(id)`與`SupplyArt.Draw(rect,id)`可取新圖；均程序產生並快取。既有技能仍走原分支。

## 新圖示ID

| 內容 | ID |
|---|---|
| 誘餌 | bait_m0～bait_m47；19～22只有防呆查圖，不加入可收服資料 |
| 飼料 | food_basic／food_fine／food_star |
| 印片 | mat_tame_seal |
| 飛毯素材 | mat_cloud_silk／mat_wind_crystal／mat_sky_badge／mat_airship_compass |
| 精魄 | soul_sky_kite／soul_sky_eagle／soul_sky_glider／soul_sky_doll／soul_sky_captain／soul_sky_airship |
| 航裝 | skyweave_weapon／skyweave_head／skyweave_body／skyweave_boots／skyweave_trinket |
| 藥劑 | bp_tailwind |

公開CarpetPetArtIds供完整查表。共68個查圖ID；誘餌刻意按8種底形共用造型，菁英加雙金封蠟。未新增材料定義、精魄效果或套裝資料。

## 寵物分頁

CharacterPanel.Pets只改排版、文字高度和圖示，原買餌／餵食／技能替換／出戰／放生／調配的條件與操作保留。沒有空callback覆蓋已接好的功能。選中寵物用PetPanelArt產生192px模型預覽並按kind快取；僅首次選中渲染，沒有每幀建模或更動AR相機。預覽會保存並還原Unity亂數狀態。示意排版圖會明確標註是組圖，不冒充實機OnGUI截圖。

## 驗證範圍

icons.png來自實際SkillIcons.Render；68項在8／32／48／128尺寸與重繪一致檢查通過，81個既有技能圖示逐像素未變。static_validation.json記錄44種誘餌分類與現有Pets對應，以及24筆UI資料操作未變的比對。原生預覽涵蓋44種寵物吃餌與兩種收服特效，最終Unity與平台來源另附紀錄。所有資料／判定接線仍歸Claude；本輪沒有宣稱多人實機或AR手機效能已測。

## 對照目前已存在的邏輯訊號（供 Claude 接線）

- `Combat.Pets` 已有 `petcaught`／`lurefail`：在既有事件顯示分支呼叫 `PetArtFx.Capture(WorldFrame.FromWorld(at), true/false)`；成功選事件或 `OnPetCaught` 其中一處接，避免重複播兩次。`lurefail` 有時傳 Chest，高度請由畫面端對齊目標腳下，不修改事件位置或協定。
- 吃餌的顯示狀態讀既有 `Fighter.lureUntil`，並以現有戰鬥時間比較；走到餌旁才開始低頭。解除、死亡、移除時傳 `SetEating(false)`。本輪只提供入口，沒有新增邏輯計時器。
- 我方寵物可由既有 `petUid`／`petBody` 辨識，建立 MinionRig 並完成外觀設定後套 `PetDress.Apply`；不可把所有收服魔物代理都自動視為永久寵物。
- 本輪沒有改 `SoloBattle.Pets.cs` 的事件接法，也沒有新增網路訊息。寵物分頁樣式已直接套在既有功能畫面，其餘戰鬥演出等待 Claude 接入口。


## 最終驗收（2026-10-28）

驗證來源 `6a9762828f0e8f3b77a44cf97218e3726340759d`。Unity 原生 Snapshot、Android APK、iOS Xcode 匯出與未簽署 IPA 編譯皆通過；詳見 ../carpet/build_validation.json。原生圖共76張，另附檢查JSON；總覽裁去照片空白後等比例排列，原始PNG保持不變。寵物分頁圖片為明確標示的排版示意，並非手機OnGUI截圖。

目視確認：44種皆有織帶與雙線環，菁英有金羽／斷續金環；漂浮物的靜態地面環移到模型下方。技能、購買與培養規則沿用既有實作；各種族原始模型和菁英骨架未被重做。
