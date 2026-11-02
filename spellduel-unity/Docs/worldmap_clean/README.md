# 世界地圖底圖：拿掉「該動卻沒動」的元素（影片生成機做）

這份是給**影片生成機**（裝 ComfyUI 的那台）上的 Claude Code 工作階段看的。

## 背景

使用者回報：世界地圖的底圖（`Assets/SpellDuel/Resources/WorldMap/region_1～5.jpg`）裡有很多本來應該會動的東西，例如煙、海浪、漩渦、瀑布，畫在圖上卻不會動，看起來很假。

要把這些元素從底圖裡**拿掉**（補成乾淨的背景），遊戲再用程式畫會動的版本疊上去。遊戲端 Claude 已經做好了：
- 有乾淨版的圖，就自動改用乾淨版，並打開那個區域的程式動畫（煙柱、煙囪、瀑布、噴泉、漩渦、浪花、風車）。
- 沒有乾淨版，照舊用原圖。

Claude 在雲端試過用 OpenCV 自動補，補出來是一大片抹開的色塊，不能用，所以要用 AI 的圖片修補（inpainting）。

## 新工作階段怎麼開

1. 在生成機上進入 AnimeGenerate 資料夾：
   ```
   git checkout claude/mobile-camera-mic-battle-game-v5nhfr
   git pull
   claude
   ```
2. 開 ComfyUI。
3. 對新工作階段貼這段：

   > 你是 SpellDuel 的影片生成機。讀 `spellduel-unity/Docs/worldmap_clean/README.md`，用 ComfyUI 的 inpainting 把世界地圖底圖裡會動的元素拿掉，照 README 的「交貨」推上去。先做 `region_1`（三柱火山煙）給我看。

## 要拿掉的東西

遮罩在 `masks/`，大小跟底圖一樣（2160×1920），**白色＝要重畫的地方**。

| 底圖 | 遮罩 | 拿掉什麼 | 補成什麼 |
|---|---|---|---|
| region_1（火山） | `region_1_mask.png` | 三柱火山的大煙柱；左邊建築的煙囪煙 | 天空、雲、火山頂（火山口保留，煙拿掉） |
| region_2（森林） | `region_2_mask.png` | 上方的瀑布、右邊的噴泉水柱、下方河道的兩段瀑布、左邊村子的兩縷煙囪煙 | 瀑布：岩壁、平靜的水面或濕的岩石<br>噴泉：水池<br>煙囪：屋頂、樹 |
| region_3（海港） | `region_3_mask.png` | 漩渦 | 平靜的深藍海面 |
| region_3（海港） | `region_3_foam_zone.png` | 這個範圍裡岩石周圍的**白色浪花、白沫** | 平靜的海面，岩石邊緣只留一點點淡淡的白邊 |
| region_5（峽谷） | `region_5_mask.png` | 風車的扇葉 | 只留風車的屋子和中間的軸，扇葉由遊戲畫 |

region_4（角鬥場）不用改。

**注意**

- **遮罩只是位置參考**：煙、浪花沒蓋完，可以自己把遮罩擴大一點；遮罩外面的東西**一律不要動**。
- **`region_3_foam_zone.png` 是一個大範圍**，不要整片重畫。在這個範圍裡只把白色浪花挑出來重畫：
  - 自己做精細的遮罩，例如用亮度或白色挑出浪花再擴大幾像素。
  - 岩石、島、燈塔、船、棧橋、建築都要保留。
- **畫風要和原圖一致**：日式動畫風、飽和的顏色、細緻的筆觸。可以先用原圖的局部當參考圖（IP-Adapter 或 image prompt），補出來的地方不能糊、不能有明顯的接縫。
- **地圖上的路、地點的建築、城鎮的位置不能移動**：遊戲裡的地點標記是照原圖的座標放的。

### 建議的做法

- 模型用 SDXL inpaint 或 Flux Fill（看機器上有哪個）。
- denoise 大約 0.75～0.9（煙、漩渦要整個換掉）。
- 遮罩邊緣羽化 8～16px。
- 提示詞例如 `clear sky with soft clouds, anime background art, detailed, vibrant`，或 `calm deep blue sea water, anime background art`。
- 負面提示詞寫 `smoke, waterfall, foam, whirlpool, text, blurry`。
- 一次修一個元素（局部裁切大約 768～1024px 修補，再貼回原圖），比整張一起修好。

## 交貨

1. 輸出成 **`spellduel-unity/Assets/SpellDuel/Resources/WorldMap/region_N_clean.jpg`**：
   - 大小 2160×1920，JPEG 品質 90，不要覆蓋原本的 `region_N.jpg`。
   - 遊戲看到有 `_clean` 版本就會自動用。
2. 第三張的漩渦和浪花可以分兩次做，最後交一張 `region_3_clean.jpg`。
3. 檢查用的對照圖（原圖、乾淨版並排）放 `spellduel-unity/Docs/worldmap_clean/preview/`。
4. commit 寫做了哪幾張，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`，推完跟使用者說一聲，Claude 會檢查程式動畫的位置對不對。
