# Village2D 美術交付

依 `VILLAGE_2D_BRIEF.md` 分批交付素材與版面圖；不修改 Runtime、功能流程或數值。

## 第一批：炎之里地圖與居民

- `Assets/SpellDuel/Resources/Village2D/map_flame.png`：2048×3072，沒有文字、名牌或人物烘焙在圖裡。
- `map_flame_fit.png`：1080×1920，完整地圖等比縮為 1080×1620，置於 y=150；沒有裁切或拉伸。
- `map_flame.json`：9 個設施加晨鐘塔、村長家的點擊矩形與名牌中心；居民老焰、小鐵、米菈的腳底位置。
- `npc_laoyan.png`、`npc_xiaotie.png`、`npc_mila.png`：160×240 RGBA，全身、腳底完整的獨立小圖。
- [標籤與居民合成示意](batch1/map_flame_labels.jpg)、[點擊範圍示意](batch1/map_flame_hotspots.jpg)。示意圖不是 Unity 實機截圖，正式地圖不含這些標籤。

### 座標與接線

JSON 全部使用 2048×3072 的像素座標，原點左上、x 向右、y 向下。建築 `x,y,w,h` 是點擊矩形，`label_x,label_y` 是名牌中心；`npc[id].x,y` 是 160×240 圖的**下緣中心／腳底錨點**。貼圖小圖內容腳底約在自身 y=232，其餘 8px 留白。

fit 版本的轉換為 `screenX = mapX × 0.52734375`、`screenY = mapY × 0.52734375 + 150`。若使用可拖曳／縮放的大圖，先做 viewport 的平移縮放反變換，再測試原圖矩形。Unity bottom-left 座標要先轉成上述 top-left 座標；本次不實作接線程式。

`smith/shop/guild/dungeon/training/bounty/watch/warp/gate` 保持文件的 key；`belltower` 與 `mayorhome` 為額外的裝飾／居民對話 key，不能當作新戰鬥功能。名牌文字由程式畫，完整文字與 emoji 見預覽及後續 layout JSON。

PNG 與 JSON 同名時，請用 `Resources.Load<Texture2D>`、`Resources.Load<TextAsset>` 明確指定型別。大圖 meta 最大 4096，以保留高 3072；fit 與居民最大 2048，均關 mipmap、`nPOTScale: 0`。原始 image_gen 地圖渲染為 1024×1536，正式大圖以高品質重採樣至指定尺寸；不宣稱為原生 2K 細節。

使用內建 image_gen，參考已確認的 Story2D village 與對應居民立繪；沒有用 3D 投影替代。來源尺寸、檔案雜湊與提示詞見 [assets.json](assets.json)、[prompts.json](prompts.json)。

## 全部批次已完成

2. 鐵匠鋪：背景、六影格鐵砧、小鐵／師父表情、台詞與版面。
3. 道具店與職業工會。
4. 副本、訓練、討伐、觀戰、傳送、村口與共用 UI 框。
5. 其他四座城鎮與居民。使用者已確認第五張 `map_snow` 沿用**風沙驛黃土山道**，檔名仍為 snow，不改成雪景。

其餘地圖與點擊範圍預覽在 [maps](maps/)，九個設施分別在 [batch2](batch2/README.md)、[batch3](batch3/README.md)、[batch4](batch4/README.md)。人物表情總覽在 [staff](staff/)。所有設施共用炎之里背景是本次 brief 允許的交付範圍。

全套共 69 張正式圖片：五張大圖、五張 fit、15 位居民、九張設施背景、六格鐵砧、七張 UI 框，以及 22 張新增人物／鐵匠姿勢。公會長及五職業立繪沿用 Story2D 現有素材。鐵匠 focus／proud／awkward 是姿勢變體；五位設施 NPC 的 normal／smile／surprised 則鎖定臉外像素。

`map_forest`＝森之里、`map_harbor`＝潮騷港、`map_desert`＝赤砂城、`map_snow`＝風沙驛。每張 JSON 的矩形都依該張圖重新量測，不直接複製炎之里座標；全部點擊矩形互不重疊。背景不烘焙人物或名牌。

執行 `python spellduel-unity/Tools/check_village2d.py` 進行完整檢查，必須 0 pending；涵蓋尺寸、透明、匯入設定、五張地圖座標、九個版面、表情臉外像素一致、鐵砧下半部固定和台詞組數。`check_story2d.py` 仍保留原 31 位劇情角色的檢查；新增設施人物獨立計入 villageStaffBytes，避免誤算成原劇情 40 MiB 預算。

本次交付是圖片、文字、JSON 與離線工具，未修改任何 Runtime C#；遊戲接線與真機驗收由 brief 指定的程式端處理。沒有將 mockup 冒稱實機畫面，也未在此環境宣稱 Unity iOS／Android 編譯通過。
