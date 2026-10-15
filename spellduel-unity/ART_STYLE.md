# 美術風格：日式賽璐珞（給 Codex／ChatGPT 調整用）

角色（玩家／對手）、小怪、Boss 的實心部位都用 `SpellDuel/Toon` 著色器：
兩階明暗（硬分界＋一點柔邊）＋邊緣亮光＋外框黑邊。
發光、半透明的特效部位（火焰、魔法陣、泡泡等）仍是 Sprites/Default 或粒子，不會描黑邊。

## 要調的地方

1. **全域風格參數**：`Assets/SpellDuel/Runtime/ToonStyle.cs`

   | 參數 | 預設 | 說明 |
   |---|---|---|
   | `OutlineColor` | 近黑 (0.04,0.03,0.05) | 黑邊顏色 |
   | `OutlineWidth` | 2.6 | 黑邊粗細，單位是「1000 像素高畫面上的像素」；手機實際寬度依螢幕高度等比放大 |
   | `OutlineFar` | 3.5 | 距離鏡頭超過幾公尺開始變細（避免遠處小怪糊成一團黑） |
   | `ShadeColor` | (0.62,0.58,0.78) | 暗部＝原色 × 這個顏色；偏藍紫比較有動畫感 |
   | `ShadeThreshold` | 0.05 | 亮暗分界（N·L），越大暗部越多 |
   | `ShadeSoftness` | 0.03 | 分界柔邊寬，0＝完全硬邊 |
   | `RimColor` | 白, a=0.22 | 亮部邊緣的亮光，a＝強度（0＝關掉） |
   | `Ambient` | 0.35 | 環境光量 |

   改完數字不用改其他程式（第一次取用著色器時會 `ToonStyle.Apply()`）。執行中改數字要再呼叫一次 `ToonStyle.Apply()`。

2. **各部位顏色**：還是在原本的 Rig 裡
   - 玩家／對手：`CharacterRig.cs`（職業色在 `Skills.cs` 的 `ClassDef.color`）
   - 小怪：`MinionRig.cs`（variant 0～18，熔岩 0–6、植物 7–12、海洋 13–18）
   - Boss：`BossRig.cs`（`RockAC/BarkAC/HideAC…` 等常數）
   賽璐珞風格建議：顏色彩度拉高、明度分明，少用接近黑的底色（不然跟黑邊糊在一起）。

3. **著色器本體**：`Assets/SpellDuel/Shaders/Toon.shader`（內建管線，兩個 Pass：TOON 本體、OUTLINE 黑邊）。

## 已知限制
- 黑邊是「背面沿法線外推」：用 Unity 內建 Cube 等**硬邊**（同一角有多個法線）的網格，轉角處黑邊會有小缺口。要漂亮的話把那個部位換成圓角／平滑法線的程序網格。
- 只有一盞主方向光會影響明暗（ForwardBase）；其他點光源不影響。
- 不支援這個著色器的裝置會自動退回 Legacy Diffuse（沒有黑邊）。
- 必須保留在建置裡：`Editor/BuildScript.cs` 已加 `IncludeShader("SpellDuel/Toon")`。
