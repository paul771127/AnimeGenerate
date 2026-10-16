# 角色造型改成日式漫畫風（給 Codex）

目前 `Runtime/CharacterRig.cs` 的角色是用 Unity 內建的球、膠囊、方塊拼成的，看起來像積木人偶，太陽春了。
請改成**日式動漫／漫畫風**（像《原神》《鬥陣特攻》Q 版前的動畫角色，或日系手遊的 3D 角色）。

目前的樣子：Actions →「SpellDuel snapshots (character renders)」的 artifact（`all_classes.png` 等），程式在 `Editor/Snapshot.cs`。

## 這些角色用在哪
- 單人模式的電腦對手（對戰 AI）。
- **遠端連線**時的隊友／對手（不在同一個地方的人，以他的職業角色出現在你的場地）。
- 進階職業（聖騎士、狂戰士…共 10 個）目前沿用基本職業的外觀，**希望各自有區別**（換配色、加配件即可，例如聖騎士金色盔甲＋披風、狂戰士紅色＋雙手斧、忍者面罩＋圍巾）。

## 風格要求
1. **比例**：頭身約 1:5～1:6（偏日系、不要寫實 1:7.5），頭稍大、四肢修長、腰細。
2. **臉**：大眼睛（有黑眼珠＋白色高光＋上眼線）、小鼻子（可省略）、小嘴；眉毛。表情可以只有一種。
3. **頭髮**：要有髮型（瀏海＋側髮＋後髮，尖角狀的髮束），髮色依職業：法師銀白、獵人棕、刺客黑、劍士金、牧師淡粉或淡金。不要再用一顆球當頭髮。
4. **服裝**：有層次的衣服（領子、腰帶、裙擺／披風、護甲片、靴子），邊緣有金色或白色鑲邊。保留每個職業目前的武器與識別物（法杖、弓、雙匕首、劍盾、權杖光環）。
5. **著色**：沿用 `SpellDuel/Toon`（兩階明暗＋黑色外框，見 `ART_STYLE.md`）。用**平滑法線的程式網格**（不要內建 Cube 的硬邊，外框會有缺口）。

## 技術限制
- 仍然用**程式生成網格**（不能匯入 FBX／VRM 等模型檔，也不能用 Asset Store 素材）；可以自己寫網格產生器（lathe、loft、髮束的錐形網格等）。
- **不要改動作系統的介面**：`CharacterRig.Create(classId)`、`SetPose`、`MoveSpeed`、`Charge`、`PlayCast`、`PlayHit`、`SetDead`、`SetVisible`、`Guarding` 都要保留，走路、詠唱、出招、被打中、倒地的動作照樣能動。
- 新增 `CharacterRig.Create(classId, advId)`（advId 可為 null）來套進階職業外觀；呼叫端由 Claude 接。
- 手機效能：每個角色三角形數量控制在約 8,000 以內。
- 不要動 `Battle`／`Fighter`／網路相關程式。

## 驗收
- 跑一次「SpellDuel snapshots」工作流程（推送 `Editor/Snapshot.cs` 或在 Actions 手動執行），把新的截圖放在 PR／commit 說明；可以順便讓 Snapshot 也拍 10 個進階職業。
- iOS／Android 都能編譯（Claude 會再檢查與建置）。
