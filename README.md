# 3D 砦防衛ゲーム 基本スクリプト

Unity 6 向け(`rb.linearVelocity` を使用)。Unity 2022 以前なら `linearVelocity` → `velocity` に置換してください。
※ 作成環境で Unity を動かせないため、コンパイルは未確認です。エラーが出たらメッセージを教えてください。

## ゲームの流れ
- WAVE 1〜3(各ウェーブ: 戦闘 2:00 → 回収 1:00)
- 戦闘中、複数種のモンスターが砦へ向かって進軍。プレイヤーが射程内の敵を自動攻撃
- 回収フェーズ開始時に残った敵は撤退(ドロップなし)。ドロップ品は戦闘中・回収中どちらでも拾える
- 次のウェーブ開始時に拾い忘れたドロップは消える
- 全ウェーブ終了でクリア / 砦のHPが0でゲームオーバー(R でリスタート)
- 敵は倒すと経験値オーブ+一定確率で装備をドロップ。装備は「今より良ければ自動装備、悪ければ経験値に変換」
- 敵はウェーブごとに HP・攻撃・速度・経験値・ドロップ率が倍率で強化される(WaveManager の Waves)

## スクリプト一覧
| ファイル | 役割 |
|---|---|
| WaveManager | ウェーブ/フェーズ進行、強化倍率 |
| MonsterSpawner | 戦闘中のモンスター出現(重み付き抽選) |
| Monster | 移動・攻撃・被ダメ・死亡時ドロップ |
| MonsterData (SO) | モンスター種類ごとのパラメータとドロップ表 |
| Fortress | 砦のHP |
| PlayerController3D | Rigidbody 移動(WASD、カメラ基準) |
| PlayerStats | Lv/EXP/HP、ステータス計算、リスポーン |
| PlayerEquipment | 武器・防具スロット |
| PlayerAttack | 自動攻撃 |
| ItemData (SO) | 装備データ |
| Pickup / ExpOrb / ItemPickup | ドロップ品(距離判定で吸い寄せ) |
| DropManager | ドロップ品の生成(プレハブ未設定なら仮の球・立方体) |
| CameraFollow | 見下ろし追従カメラ |
| HUD | 画面表示(TextMeshPro) |
| GameInput / GameLog | 入力ヘルパー / 画面メッセージ |

## シーンのセットアップ
1. **Ground**: Plane (Scale 10,1,10 くらい)
2. **Fortress**: Cylinder を原点に置き(Scale 4,1,4)、`Fortress` を付ける(Radius は 2.5 前後)
3. **Player**: Capsule (位置 5,1,0)
   - `PlayerController3D` を付ける(Rigidbody / PlayerStats / PlayerEquipment が自動で付く)
   - `PlayerAttack` を付ける
4. **SpawnPoint**: 空のオブジェクトを砦から 25m くらい離して 3〜4 個配置
5. **GameSystems**: 空のオブジェクトに `WaveManager` / `MonsterSpawner` / `DropManager` を付ける
   - MonsterSpawner の Monster Types と Spawn Points を割り当てる
6. **Main Camera**: `CameraFollow` を付けて Target に Player を指定
7. **UI**: Canvas に TextMeshPro テキストを複数作り、空のオブジェクトに付けた `HUD` へ割り当て
   (初回は "Import TMP Essentials" を押す)

## データ作成(Project ウィンドウ右クリック > Create > TD)
### MonsterData の例(HP などはウェーブ1基準)
| 名前 | maxHp | moveSpeed | attackDamage | spawnWeight | firstWave | expReward | itemDropChance | tintColor |
|---|---|---|---|---|---|---|---|---|
| Goblin | 20 | 4.5 | 3 | 3 | 1 | 8 | 0.25 | 緑 |
| Orc | 50 | 3 | 8 | 2 | 1 | 15 | 0.35 | 赤 |
| Golem | 150 | 1.8 | 20 | 1 | 2 | 40 | 0.6 | 灰 |

各 MonsterData の Drop Table に ItemData を入れる(強い敵ほどレア装備を重めに)。

### ItemData の例
| 名前 | slot | rarity | attackBonus | defenseBonus | maxHpBonus |
|---|---|---|---|---|---|
| Iron Sword | Weapon | Common | 5 | 0 | 0 |
| Steel Sword | Weapon | Rare | 12 | 0 | 0 |
| Flame Blade | Weapon | Epic | 25 | 0 | 0 |
| Leather Armor | Armor | Common | 0 | 2 | 20 |
| Chain Mail | Armor | Rare | 0 | 5 | 50 |
| Knight Armor | Armor | Epic | 0 | 10 | 100 |

## Blender / Maya モデルに差し替えるとき
- モンスター: モデルに Rigidbody + Capsule Collider + `Monster` を付けてプレハブ化し、MonsterData の Prefab に指定。`Apply Tint` はオフに
- プレイヤー: カプセルの子にモデルを置き、カプセルの MeshRenderer を無効化
- ドロップ品: ExpOrb / ItemPickup を付けたプレハブを DropManager に指定(コライダー不要)

## 注意
- TextMeshPro の標準フォントは日本語が表示できないため、画面表示は英語にしています。日本語にするなら日本語フォントの TMP Font Asset を作成してください
- バランス(敵の数、HP、砦のHP、経験値など)は全てインスペクターで調整できます。最初は maxAlive や spawnInterval を触ると手応えが変わります

## 次に作りやすい拡張
- 砦を守るタレット(タワー)の設置
- 敵のHPバー、攻撃エフェクト、アニメーション
- インベントリ画面と手動装備、武器ごとの攻撃範囲・攻撃速度
- ボス敵、ウェーブ数の増加
