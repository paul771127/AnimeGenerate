# 工單 002：大魔導師的左右移動、後退動作（＋法杖施法）

## 目的
第三人稱時角色一直面向準心（攝影機的方向）。現在只有往前走／跑的動作，往左、往右、往後移動時看起來像在滑。
請在**工單 001 的同一個模型**（`spellduel-unity/Assets/SpellDuel/Resources/CharKit/archmage_v1/archmage.fbx`）上加動作。

## 要加的動作（原地播放，不要根位移；跟 001 一樣 30fps）
| 名字 | 內容 | 循環 |
|---|---|---|
| `walk_back` | 面向前方、往後退著走 | 是 |
| `walk_left` | 面向前方、往左橫移（側步） | 是 |
| `walk_right` | 面向前方、往右橫移 | 是 |
| `run_back` | 往後小跑 | 是 |
| `run_left` | 往左側跑 | 是 |
| `run_right` | 往右側跑 | 是 |
| `cast_staff` | **右手的法杖往前推出施法**（法杖頂端朝前、法球發光），左手自然收在身側；約 0.8 秒 | 否 |
| `cast_staff_loop` | 詠唱中：右手把法杖舉到前上方聚光 | 是 |

- 使用者回報：001 的 `cast` 是空著的左手在施法，請用法杖。
- 走路類跟 001 的 `walk`、`run` 一樣的步調（walk 約 1.3 秒一循環、run 約 0.67 秒），法杖一直拿在右手，魔法書跟著左邊浮動。

## 交貨
- 直接更新 `archmage.fbx`（舊的 10 個動作保留、名字不變）；或另外放 `archmage@動作名.fbx`。
- 不要放 .meta、不要改 `Runtime/` 程式。
- `response.json`：新增的動作名稱、長度；`previews/`：每個新動作一張。
- push 到 `claude/mobile-camera-mic-battle-game-v5nhfr`（commit 訊息不要加 `[skip ci]`），回報完整 commit SHA。
