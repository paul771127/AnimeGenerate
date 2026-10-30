# Codex 任務：探索特效影片的「第一格／最後一格」圖片

故事探索的特效影片由使用者的**影片生成機**用 ComfyUI MiniMax H3 做成 mp4（見 `Docs/explore_fx/README.md`、`Docs/explore_fx/shots.json`）。純文字生成的話，鑰匙、碎片、森林之靈、任務標記會和遊戲裡 Codex 做的模型長得不一樣。

所以請 Codex 先**從遊戲裡的模型算出一張圖**，生成機再用「圖生影片」：這張圖當影片的第一格（循環的也當最後一格），或當最後一格（`_end`）。這樣影片裡的東西就跟遊戲裡一模一樣。

**前提**：這些模型來自 `CODEX_TASK_EXPLORE_JUNGLE.md`（`ExploreArt` 的 CreateSwitch、CreateHerb、DressNpc spirit、QuestMark、祭壇的古樹之心）。那一單還沒做完的，先做那些模型再出圖。

## 規格

- **960×544 PNG**，放 `spellduel-unity/Docs/explore_fx/keyframes/`，檔名照下表。
- 用 Unity 編輯器腳本（例如 `Assets/SpellDuel/Editor/ExploreFxKeyframes.cs`，選單一鍵輸出）從實際的 prefab／程式產生的模型拍：正交或小 FOV 相機、平光（遊戲的卡通著色＋描邊照常）、**沒有地面、沒有天空盒、沒有霧**。
- 背景：純黑 `#000000`，或綠幕 `#00FF00`（整片平均、沒有陰影、沒有反光溢色），照下表。
- 主體在畫面中間，約佔畫面高度 45%（地名橫幅除外），四邊至少留 15% 空白，因為影片裡東西還會飄、會轉。
- 不要有字。不要在圖上畫特效粒子（粒子由影片生成）。

## 要的圖（16 張）

| 檔名 | 背景 | 內容 |
|---|---|---|
| `fx_A5_socket_pulse.png` | 純黑 | 翠玉碎片（和 CreateSwitch 的 shard 同一個模型），正面，不發光的狀態 |
| `fx_B1_rune_idle.png` | 純黑 | 石雕機關壓板上的綠色符文特寫（只要符文，黑底），中等亮度 |
| `fx_B1_activate.png` | 純黑 | 同 fx_B1_rune_idle 的符文，中等亮度 |
| `fx_B2_key_idle.png` | 綠幕 #00FF00 | 古銅鑰匙（CreateSwitch key 的模型），斜 3/4 視角，綠幕 |
| `fx_B2_shard_idle.png` | 純黑 | 翠玉碎片（CreateSwitch shard 的模型），斜 3/4 視角，發綠光 |
| `fx_B2_pickup.png` | 純黑 | 翠玉碎片，同 fx_B2_shard_idle 但小一點 |
| `fx_C3_herb.png` | 綠幕 #00FF00 | 月光草（CreateHerb 的模型），正側面，綠幕 |
| `fx_C4_heart_rise_end.png` | 純黑 | 古樹之心（祭壇上的子物件），發綠光，畫面中間偏上 |
| `fx_C4_heart_idle.png` | 純黑 | 同 fx_C4_heart_rise 的結束畫面 |
| `fx_C4_heart_take.png` | 純黑 | 同 fx_C4_heart_rise 的結束畫面 |
| `fx_D3_fire.png` | 純黑 | 篝火的火焰（只要火，不要柴堆），黑底 |
| `fx_E1_spirit.png` | 純黑 | 森林之靈（DressNpc spirit 的外型），正面，發綠光 |
| `fx_E2_alert_end.png` | 純黑 | 紅色粗「！」（遊戲裡怪物頭上的樣子），畫面中間 |
| `fx_E3_exclaim.png` | 純黑 | 金色「❗」任務標記（QuestMark 1 的樣子），正面 |
| `fx_E3_question.png` | 純黑 | 金色「❓」任務標記（QuestMark 2 的樣子），正面 |
| `fx_E4_banner_end.png` | 純黑 | 地名橫幅的框和空緞帶全部展開的樣子（沒有字），畫面上方 1/3 |

其他 9 支（爆光、閃光點、孢子、聲波、湖面）只有粒子或光，不需要圖。

## 完成後

- 推到 `claude/mobile-camera-mic-battle-game-v5nhfr`（Android、iOS 都要編譯通過；編輯器腳本放 `Editor/` 資料夾就不會進 build）。
- 在 `CODEX_PENDING_BRIEF.md` 的「Codex 實作紀錄」寫一行：哪幾張已出、用哪個選單重出。
- 模型之後有改，重跑選單覆蓋同檔名即可；生成機會用新圖重生該支影片。
