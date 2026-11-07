# 日式賽璐璐火山零件風格修訂

此交付來自本機使用者最新的風格指示，不冒用雲端工單 004 編號。

> 再換一下風格，像原神那種，賽璐璐風格的關卡風格就行。

成果是原創表面貼圖、實際 3D FBX 零件與渲染預覽。只參考使用者附圖的環境風格，沒有散布原遊戲的角色、介面或材質。

- 零件：`spellduel-unity/Assets/SpellDuel/Resources/SceneKit/volcano_cel_v1/`
- 控制器：`scenegen/tools/anime_cel_v1/cel_kit_control.py`，獨立 API v1 extension；不更改原 `control.py` 的整張地圖 API。
- 20 個邏輯零件，24 個 FBX（含獨立門框／門板），15,331 個不重複三角面；尺寸、腳底原點、Y-up、+Z-forward、4m 格與命名沿用 v3。
- 明暗／AO 烘焙；主圖手機匯入上限 2048，遠景 1024，其他 512，ASTC 6x6、mipmap、關閉 Read/Write。詳見 `mobile-import-settings.json`。
- Unity 2022.3.62f2 Built-in 已實際匯入核對全部零件尺寸、面數、門子物件及 shader；未測手機 FPS。
- 原 v3 形狀仍保留，本次主要換貼圖、著色與範例配置；舊 2D 障礙物尚未換畫風。
- 原有煙雲動畫接口維持原生成器的管線；本獨立範例天空／熔岩仍是靜態。

## 遊戲端切換

本次只新增資產、控制工具和交接資料，遊戲端仍由原 session 選擇切換。
目前遊戲的 StageKit.cs 將素材路徑與三個貼圖名稱寫死為 v3，並非讀 kit.json 自動選版本。
已附 `StageKit-style-switch.patch`，以本次 pull 到的遊戲程式為基底，只修改資產路徑與貼圖名稱，沒有更動玩法。
想啟用可在 repo 根目錄先執行：

```
git apply --check scenegen/jobs/codex-style/volcano-cel-v1/StageKit-style-switch.patch
git apply scenegen/jobs/codex-style/volcano-cel-v1/StageKit-style-switch.patch
```

既有 SpellDuel/StageKit shader 可沿用（Unlit + emission mask + distance haze + instancing）。
新版 emission 是線性遮罩，匯入時取消 sRGB。模型 Normals=Import、不要產生 Animator。
Unity 自行產生 .meta；本生成器沒有交付 .meta、.mat 或 URP 材質。

## 另一個 session 呼叫生成器

先安裝 Blender 4.5，設定 BLENDER_PATH 或在請求加 blender 完整路徑。
`portable_request.json` 是可攜樣例；每次換新的 requestId/outputFolder，outputFolder 放在生成器獨立 runs，別放遊戲 repo。
actions 支援 capabilities → plan → validate → generate → inspect；請逐一檢查 ok。
style.json 的 sourceKit/surfaceAtlas 路徑以該設定檔目錄為基準；請求 configFile 以請求檔目錄為基準。
控制器不會呼叫 AI 產圖；surfaceAtlas 由呼叫端提供。完整原始提示詞和內建 imagegen 來源見 source/。
