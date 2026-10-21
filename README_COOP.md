# オンライン協力(2人)版 セットアップ

Unity 6 + Netcode for GameObjects 2.x 向け。ホスト(1人目)とクライアント(2人目)の構成です。
※ 作成環境で Unity を動かせないため、コンパイル・動作は未確認です。エラーはそのまま教えてください。

## 1. ファイルの扱い
### 新規(Assets/Scripts に追加)
ClientNetworkTransform.cs / ItemDatabase.cs / NetworkMenu.cs
Editor/NetworkSetupTool.cs → **必ず Assets/Scripts/Editor フォルダ**(無ければ作る)に入れる

### 上書き(同名ファイルを丸ごと置き換え)
WaveManager / Fortress / Monster / MonsterSpawner / DropManager / Pickup / ExpOrb / ItemPickup /
PlayerStats / PlayerEquipment / PlayerController3D / PlayerAttack / CameraFollow / HUD

### そのまま(変更なし)
GameInput / GameLog / ItemData / MonsterData

### 削除してOK
- 最初の2D版の4本(PlayerController2D / GameManager / Coin / Enemy)
- FirstPersonCamera.cs(CameraFollow に統合済みで、もし残っているなら)

## 2. パッケージ導入
Window > Package Manager > Unity Registry で **Netcode for GameObjects** を Install
(Unity Transport は自動で入る)

## 3. プレハブ作成(自動)
メニュー **Tools > TD > Create Network Prefabs** を実行
→ Assets/Prefabs に Player / Monster / ExpOrb / ItemPickup ができる

## 4. シーンの設定
1. **古いプレイヤーのカプセルを削除**(Player はプレハブから自動で出現するようになる)
2. 空のオブジェクト **NetworkManager** を作り、以下を付ける
   - `NetworkManager` コンポーネント(Add Component)
     - Inspector の Network Transport で **UnityTransport** を選ぶ
     - **Player Prefab** に Assets/Prefabs/Player を入れる
     - Network Prefabs Lists に DefaultNetworkPrefabs が入っているか確認
       (Monster / ExpOrb / ItemPickup / Player が載っていなければ、そのリストに4つドラッグして追加)
   - `NetworkMenu`(接続メニュー)
3. **Fortress** のオブジェクトに `NetworkObject` を追加
4. **GameSystems** のオブジェクトに `NetworkObject` を追加(WaveManager / MonsterSpawner / DropManager と同居)
   - `ItemDatabase` を追加し、Items に **全部の ItemData** を入れる(ドロップ表に使うもの全て)
   - MonsterSpawner の **Default Monster Prefab** に Assets/Prefabs/Monster
   - DropManager の **Exp Orb Prefab / Item Pickup Prefab** に ExpOrb / ItemPickup
5. **Main Camera** に `CameraFollow`(Target の設定は不要。シーン直下に置く)
6. HUD に、相棒の状態を出す欄 Team Text が増えた(使うなら TMP を1つ作って入れる。空でも可)
7. File > Build Profiles(Build Settings)で、**このシーンを Scenes In Build に追加**
   (NetworkManager のシーン同期に必要)

## 5. 遊び方
- **ホスト**: Host ボタン → 相手が入ったら **Start Game**(1人でも開始できる=テスト用)
- **クライアント**: ホストの IP を入れて Join
- 勝敗後は **ホストが R** でリスタート(全員のシーンを読み込み直す)

## 6. 試し方(1台で2人分)
- 方法A: ビルドして実行ファイルを起動(クライアント)+ エディタで Play(ホスト)。IP は 127.0.0.1
- 方法B: Window > Multiplayer > Multiplayer Play Mode で仮想プレイヤーを追加

## 7. 友達とインターネット越しで遊ぶ
### 簡単: VPN
Tailscale / ZeroTier / Radmin VPN などで同じ仮想LANに入り、ホストの VPN 側 IP を Join に入れる

### 本格: Unity Relay(ポート開放不要)
1. Package Manager から **Authentication** と **Relay**(Services Core は自動)を Install
2. Project Settings > Services で Unity アカウントのプロジェクトにリンク
3. Project Settings > Player > Scripting Define Symbols に **TD_RELAY** を追加
4. メニューに Relay の欄が出る。ホストが Host via Relay → 表示された Join code を相手に伝える
※ Relay 部分は特に未確認。コンパイルエラーが出たら教えてください

## 8. 仕組みのメモ(あとで改造するとき)
- 数値の判定(ダメージ、レベル、ドロップ、ウェーブ進行)は**ホストだけ**が行い、結果が全員に同期される
- プレイヤーの移動は本人が決める(ClientNetworkTransform)。協力ゲームなので不正対策はしていない
- 敵・ドロップ品は NetworkObject 付きのプレハブから生成される。Blender/Maya の敵モデルに替えるときは、
  Monster プレハブのカプセルの見た目だけを差し替える(Rigidbody / NetworkObject / NetworkTransform / Monster は残す)
- ドロップ品はホストが「一番近い生きているプレイヤー」に吸い寄せ、触れた人のものになる(早い者勝ち)
- 2人プレイ用の難易度調整はまだ無い(モンスターの数・HPは1人のときと同じ)
