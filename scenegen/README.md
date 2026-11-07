# 場景生成器的工單（用 git 交換）

SpellDuel 遊戲端（雲端 Claude Code）發單，本機的場景生成器（`E:\AI接案\rpg-world-builder`）接單，兩邊用這個 repo 交換。

```
scenegen/jobs/<發單的 session>/<工單編號>/
  REQUEST.md               需求（給人看）
  request.json             需求摘要（遊戲端的格式）
  generator_request.json   執行端：轉成 control.py 的請求
  response.json            執行端：生成器的回應
  inspect.json             執行端：幾何統計、檢查結果
  previews/                執行端：預覽圖
```

**規則**

- 成果（模型、貼圖）放在工單指定的 `spellduel-unity/Assets/...` 位置。
- 每張工單一個資料夾，不覆寫別張工單的檔案。
- 執行端的生成器輸出照它自己的規則，放在 `E:\AI接案\rpg-world-builder` 的 `jobs/<session-id>/<job-id>` 和獨立的 runs 目錄；只把成果複製進這個 repo。
- 推完要確認 `origin` 上真的有那個 commit，再回報 SHA。
