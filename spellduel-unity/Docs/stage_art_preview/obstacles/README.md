# 第一大關：手繪障礙、陷阱、脫出點

依 CODEX_TASK_OBSTACLE_ART.md 製作，僅熔岩主題。素材由內建 image_gen 生成，提示詞與母圖尺寸記錄在 generation_manifest.json。沒有修改 Runtime、玩法、碰撞或連線；由 Claude 接線。

## 交付清單
- volcano_jump_0/1/2：岩脊、倒下黑曜石柱、冷卻熔岩堆，2048×512。
- volcano_duck_0/1/2：岩樑、岩拱、焦木，2048×1024。
- volcano_side_0/1/2：巨石堆、傾倒石柱、崩塌石牆，1024×1024。
- volcano_vent_idle/warn：同位置噴口待機／預警，512×512。
- volcano_vent_erupt：1024×1024，左到右 4 格，每格 256×1024；初噴、最高、最高、回落。
- volcano_rock_fall / volcano_rock_rubble：512×512 落石與地面碎石。
- exit_circle：1024×1024 藍色脫出法陣。
- exit_pillar：256×1024，黑色 RGB 背景，供加亮混合；不是一般透明混合。

共 16 張 PNG 與 16 份 .meta，PNG 約 10.75 MiB。Default、sRGB、Alpha Is Transparency、Clamp、Bilinear、maxTextureSize 2048、不開 mipmap、nPOTScale 0。

## 尺寸基準
跳障礙落地線 y=492／512，實體高度最多 210px（對應約 0.41m），上方留空。
蹲障礙中央橫樑底線 y≈563／1024（圖頂往下 55%）；地面 y≈983。下方中央透空。
側步障礙採自然碎石收尾，可以左右翻轉，不含文字。
噴口兩態沿用母圖完整畫布與中心，不各自裁切；透明輪廓 IoU 0.9929。
噴發四格基底全部落在 y=1024，第二、三格明顯高於第一、四格。
光柱由下亮上淡，預覽使用加亮混合。

## 預覽
- all_assets.jpg：16 張素材總表。
- player_view.jpg：熔岩地面、近處跳障礙、中間蹲障礙、遠處側步巨石、噴口、脫出點與 arena_imp 比例尺。
  這是資產投影合成，非 Unity 實機截圖；巨石向側邊錯開以避免被石拱完全擋住，並非實際關卡擺放資料。
- vent_warning.gif：待機／預警切換檢查。
- vent_eruption.gif：四格噴發播放檢查。

## 驗證
執行 python spellduel-unity/Tools/check_obstacle_art.py。
16 張尺寸、透明背景、匯入設定、跳障礙高度、蹲行開口、橫樑底線、噴發格子底線與高度變化檢查皆通過，0 錯誤。
已人工查看總表與玩家視角。沒有執行 Unity／Android／iOS 建置；本批僅新增圖檔、匯入設定與離線驗證工具，不宣稱遊戲接線已完成。
