# 角色生成器工單（SpellDuel）

跟 `scenegen/` 一樣用 git 交換：

1. 遊戲端（雲端 session）在 `chargen/jobs/spellduel-cloud/NNN-名稱/` 放 `REQUEST.md`（給人看）和 `request.json`（給程式讀）。
2. 角色生成器 pull 分支 `claude/mobile-camera-mic-battle-game-v5nhfr`，照工單做，產出放到工單指定的 Unity 資料夾，並在工單資料夾寫 `response.json`、預覽圖放 `previews/`。
3. commit（訊息不要加 `[skip ci]`）、push 到同一個分支，回報完整的 commit SHA。
