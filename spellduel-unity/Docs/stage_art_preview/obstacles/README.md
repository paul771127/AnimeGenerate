# 噴口、落石、出口：火山賽璐璐重畫

本次依更新後 `CODEX_TASK_CEL_RESTYLE.md` 重畫 **7 張 PNG**：

| 素材 | 尺寸與規則 |
|---|---|
| volcano_vent_idle／warn | 512×512 RGBA，同一裁切與縮放矩陣，俯視噴口 |
| volcano_vent_erupt | 1024×1024 RGBA，4 格橫排，每格 256×1024 |
| volcano_rock_fall／rubble | 512×512 RGBA，沿用舊圖實體範圍與比例 |
| exit_circle | 1024×1024 RGBA，透明中央與外圍的藍色法陣 |
| exit_pillar | 256×1024 RGB，黑底加亮混合，上暗下亮 |

9 張 volcano_jump／duck／side 圖不屬於本次重畫，原檔及 `.meta` 均保留。`validation.json` 仍檢查現有全部 16 張，以確保未重畫素材沒有遭到破壞。

冷色玄武岩、紫灰火山灰、左上暖光與少量分岔熔岩脈，配合 SceneKit/volcano_cel_v3。全部重畫素材由內建 image_gen 生成；提示詞與母圖資訊見 `generation_manifest.json`。

## 驗證

噴口兩態透明輪廓 IoU = 0.989744；共用裁切／縮放／位移，沒有各自對齊。四格噴發底線皆 y=1024，第二／三格高度高於第一／四格 1.25 倍以上。光柱仍為不透明黑色 RGB，頂端足夠暗，供既有加亮材質使用。

尺寸、alpha、匯入設定、噴發相位與基準線檢查 0 錯誤。沒有改動 `.meta`、Runtime 或碰撞。

## 檢視

- `all_assets.jpg`：7 張重畫素材；不是包含舊跳／蹲／側步的 16 張總表。
- `player_view.jpg`：新場景與新噴口、落石、出口的資產合成。
- `vent_warning.gif`：待機／預警對位播放。
- `vent_eruption.gif`：四格噴發播放。
- `../cel_match.png`：工單指定的既有 Unity 走道截圖加上新 2D 素材。

合成預覽用來檢查比例與美術，並非新遊戲執行截圖。
