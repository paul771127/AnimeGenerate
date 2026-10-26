# 玩家頭頸、髮型與 2D 對照

玩家造型由 Astra 對照既有 16 張職業立繪優化，保留同一位褐髮冒險者的臉與眼睛。程式僅產生 Mesh／頂點色；本資料夾的圖片是驗收用圖，不是遊戲的模型貼圖。

## 預覽

- [16 職業 2D／3D 對照](all_16_2d_3d.jpg)
- [16 職業頭肩總覽](all_16_heads.jpg)
- [初心者、法師、主教修改前後](before_after.jpg)
- `{id}_views.jpg`：每職業九視角等比例拼圖。
- `neck_{id}_{front,side,45,overhead,back,low}.png`：原生頭肩圖。
- `neck_{id}_{full_front,full_45,gear_overhead}.png`：全身與穿裝備檢查。
- `after_{novice,mage,bishop}_cast.png`：原生施法動作。

## 檢查範圍

`NeckHairAudit` 檢查上頸 16 點是否位於實際下顎內、領口內緣是否在頸面外、來源髮束頭皮距離，以及最終變形後髮面是否覆蓋背顱。後者是針對第一次原生背面圖發現露頭皮而新增，不能只拿來源橢球的距離代替。每職業有 120 個背顱取樣。

`PlayerMatch2DAudit` 延用原有換裝、顯隱、走動、施法、受擊、倒地與快取檢查。Astra 同時修正主教白金冠前片朝向、胸扣懸浮、髮殼與領口面朝向；頭骨下移後，以既有髮型作固定身高基準，不重新縮放臉。

`neck_hair_audit.json` 是原生 Unity 結果；`offline_geometry_audit.json` 是較早的 .NET 契約幾何量測，兩者不可混稱。`native_source.json` 記錄原生來源提交與 Actions。前後比較的 before 來自 03e9233 的實際渲染；該舊流程之後在編輯模式音效測試失敗，並非 before 圖未產生。

總覽與視角表是 `Tools/assemble_pending_previews.py` 對原生 PNG 等比例拼接，未修改人物像素。Linux 與 iOS 編譯結果另見 `../pending_preview/ci_validation.json`。此流程沒有量測手機 AR 的實際 FPS。
