# 副本大地圖：六套主題美術

## 檢視方式

每套包含 `dungeon_<theme>_top.png`（俯瞰）和 `dungeon_<theme>_first.png`（1.65m 眼高）。圖片由 Unity 的實際程式網格、材質與地圖產生，不是概念圖。

| 主題 | 牆與景物 | 鋪面 |
|---|---|---|
| volcano | 黑曜岩、發光裂縫、礦車、短鐵軌、坩堝 | 焦灰碎石／房間石板 |
| jungle | 沿用翠蔭迷林樹叢、薄霧、房間燈籠 | 踩實泥土小徑／區域框線 |
| sea | 礁岩、分岔珊瑚、船骸、帆、海草、貝殼 | 濕沙、淺水漣漪 |
| arena | 木樁、分層石看台、旗幟、兵器架 | 沙地／石板 |
| canyon | 紅褐岩層、風蝕柱、枯樹 | 黃土碎石／石板 |
| clockwork | 黃銅管線、齒輪、鉚釘板、蒸汽 | 金屬格柵 |

營地、寶箱台、王座放在房間邊緣的原有牆格；特殊事件用貼地符文。地板邊框標示房間範圍。門讀取 `GateOpen`，清除後上升打開；離開再走回來不會重播。小地圖保留皇冠／寶箱符號，右下加完成勾記，未清除的當前區域閃亮。

## 範圍與資料

- 正式副本資料、隨機種子、難度、敵人、碰撞及清除條件不變。天穹飛毯維持原場景。
- 混合主題逐房讀取 `ThemeOf(area.node)`；千機迴廊以 `nodeAreas` 使用機械裝飾，地板、符文和小地圖仍讀各房間 theme 作為配色，不把機械外觀當成玩法主題。
- 實心景物限定在原有牆格；不新增走道障礙、Collider 或 `AddProp`。
- 維持附近正負 6 格串流；共用網格、程序貼圖與材質。地圖主題查詢只在首次建立時算好。
- 角鬥場和峽谷目前只出現在千機迴廊的機械房，因此其非機械美術預覽使用 Editor 專用雙房測試資料，不寫回正式副本。
- 本交付不含特效影片；影片製作依 `Docs/dungeon_fx/` 另行進行。

## 畫面接線

`SoloBattle.Explore.cs` 僅更換原有繪製入口：副本使用主題底層地面、`CreateDungeonGate`、`CreateDungeonArena`，小地圖加入 `DrawDungeonAreaMap`。非副本仍走原本的翠蔭迷林工廠。房間邊框、地板與主題景物由 `CreatePath`／`CreateWall` 的 map-aware 版本自動選用。

門動畫的計時放在美術快取內，觀察 `GateOpen` 而不寫回區域狀態；牆面依走道轉向，圓角網格的快取鍵也包含方向。原有全圖底層每次建場仍會建立一份網格與材質，原生快取檢查明確排除這兩份既有配置；串流格子重建不增加快取網格、材質或貼圖。

## 驗證

[Unity 原生驗證 41261756349](https://github.com/paul771127/AnimeGenerate/actions/runs/41261756349) 通過（Unity 2022.3.62f2，程式版次 `6e481c5`）：8 個正式副本、60 個區域、59 條連接，全部能通行；新增美術前後地圖資料相同、固定種子相同、無新增碰撞器、牆格界限與快取、開門中間／完成狀態都通過。

每個主題的示範串流視窗為 66～77 格、10,798～32,044 三角形，含既有全圖底層，不含角色與戰鬥特效；這是示範視窗的統計，並非所有位置的最壞值。詳細數字在 `dungeon_maps_audit.json`，12 張原生圖與兩張總覽已附。

[雙平台建置 41261758184](https://github.com/paul771127/AnimeGenerate/actions/runs/41261758184) 全部成功：Android APK、iOS Unity 匯出、Xcode 未簽署 IPA。證據存於 `platform_ci.json`，與原生預覽使用同一個 `6e481c5`；`check_dungeon_map_art.py --native --require-mobile` 已比對本次美術程式內容並通過。`check_dungeons.py` 亦為 9 個副本、0 個格式錯誤。手機實機幀率／AR 合成效果須另行實測，CI 的原生畫面不代表已通過手機效能測試。

### 額外舊場景回歸檢查

既有 `spellduel-explore-art` 工作流程在 [41260209703](https://github.com/paul771127/AnimeGenerate/actions/runs/41260209703) 回報 `Arena scenery on open path 3`。失敗來自 `ExploreScenePreview` 的翠蔭迷林第三競技場擺設檢查；`CreateArena` 本體與 `Explore.cs` 相對本單起點 `836e9f9` 完全未變。本單新增的副本使用 `CreateDungeonArena`，另以副本專用路線、邊界及快取檢查驗收。舊場景問題留待對應場景修正，不宣稱舊預覽全部通過。
