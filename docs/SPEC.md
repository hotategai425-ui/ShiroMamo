# ShiroMamo 現行技術仕様

更新日 2026-11-19 / 版 0.26

個体差実装の基点コミットは`382e965`。過去の調査時点の未コミット情報は履歴に保管している。

開発者向けの実装・保存値・APIの参照先。読み始めは[引き継ぎ](HANDOFF.md)、遊び方とゲーム内容は[ゲーム仕様書](GAME_SPEC.md)、過去の作業と検証ログの詳細は[履歴](CHANGELOG.md)を参照する。

この文書の「実装済み」はコード・シーン・アセットで存在を確認した範囲。実操作・通信での確認状況はHANDOFF.mdに記録する。数値は現在の調整値で、最終バランスを確定したものではない。変更前に対象ファイルの最新状態と照合する。

## 1. 技術構成とゲームシーン

| 項目 | 調査時の値 |
| --- | --- |
| Unity | 6000.0.46f1 |
| 描画 | Universal Render Pipeline 17.0.4 |
| 通信 | Netcode for GameObjects 2.13.3、Unity Transport、Relay |
| サービス | Authentication 3.8.0、Multiplayer Services 1.1.8 |
| 入力 | Input System 1.14.0。GameInputに新旧入力向けの分岐がある |
| ゲーム用シーン | `Assets/Scenes/gameplay.unity` |
| ゲーム用プレハブ | `Assets/Prefab/` |
| 敵データ | `Assets/MonsterDeta/`。現状のディレクトリ名を維持して記載 |
| 武器のデモデータ | `Assets/buki/CrystalSwordData.asset` |

`NetworkManager`が接続とプレイヤー生成を行う。`Menu`に`NetworkStartUI`があり、`GameSystems`にウェーブ、敵生成、ドロップ、アイテム一覧の管理がある。砦には`Fortress`と`NetworkObject`がある。Main Cameraはシーン直下から自分のプレイヤーを追従する。

ビルド設定には`SampleScene`、`gameplay`の順で両方が有効登録されている。配布ビルドの起動先と、利用するBuild Profileの確認が必要。これは設定上の確認事項で、起動失敗を再現したという意味ではない。

PC版の制作・UIの基準はFull HD（1920×1080・16:9）。ユーザーの指定を基礎作りから共有された。ProjectSettings/ProjectSettings.assetの現在値はdefaultScreenWidth=1024、defaultScreenHeight=768、resizableWindow=0。制作基準への設定反映は未着手で、ウィンドウ／フルスクリーン等のモードも未決定。担当と残作業はHANDOFF.mdのDISPLAY-01を参照する。

## 2. 1試合の進行

現行実装は `Lobby → Combat → Collect` を3ウェーブ繰り返し、最後の回収時間の終了で`Victory`になる。砦HPが0になると`Defeat`になる。

- ホストがRelay部屋を作り、参加コードを相手に伝える。
- 相手はコードを入力して参加する。上限はホストを含め2人。ホスト1人でも開始できる。
- ホストがStart Gameを押すと第1ウェーブの戦闘が始まる。
- 各戦闘は120秒、各回収は60秒。通算の進行時間は敗北しなければ約9分。
- 回収開始時に残存する敵はドロップを出さずに消える。
- 経験値と装備のドロップは戦闘中と回収中に取得できる。
- 次ウェーブの戦闘開始時に敵由来の拾い残しが消える。プレイヤーが落とした武器は残る。
- 砦の最大HPは1,000、敵が攻撃を始める際の砦半径は2.5。
- 勝敗後、ホストのRキーで全員のシーンを読み込み直す処理がある。最新機能を含めた再戦は未確認。

### ウェーブ設定

| Wave | 出現間隔・秒 | HP倍率 | 攻撃倍率 | 速度倍率 | 経験値倍率 | 装備ドロップ率倍率 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 3 | 1 | 1 | 1 | 1 | 1 |
| 2 | 2.5 | 1.6 | 1.4 | 1.1 | 1.3 | 1.2 |
| 3 | 2 | 2.5 | 2 | 1.2 | 1.6 | 1.4 |

敵生成に登録された地点は4か所。同時に存在できる敵の上限は40。人数に応じた敵数やHPの補正は現行コードにない。

## 3. 操作と攻撃

| 操作 | 現行の動作 |
| --- | --- |
| WASD・矢印キー | 視点の左右方向を基準に移動 |
| マウス移動 | 一人称の視点操作 |
| Space | 接地中にジャンプ |
| 左右どちらかのShift | 移動キーの入力方向へ水平ブリンク |
| 左クリック | 左手の攻撃。左手に武器が必要 |
| 右クリック | 右手の攻撃。武器がなければ素手攻撃 |
| I | 持ち物画面の開閉 |
| Esc・画面のClose | 持ち物画面を閉じる |
| R | 勝敗後にホストが再戦 |

クリックと手の左右を揃えることはユーザーの決定。現行コードは押した瞬間に1回攻撃を要求する。押しっぱなしの自動連打は行わない。

攻撃は戦闘中かつ生存中に可能。自分の入力をホストへ送り、ホストが攻撃可能状態と左右それぞれのクールダウンを判定する。武器の攻撃方式に応じて近接と球の発射を分岐する。近接武器の再使用間隔は各手0.5秒と武器の振り時間の長い方。魔法と素手は各手0.5秒。空振りでもクールダウンが発生する。

装備した剣はクリック直後に本人の端末で振り始め、カプセル形の刃と前回・今回のワールド姿勢からホストが接触を調べる。姿勢間の位置と回転を補間し、高さを含む刃の軌跡で検出する。壁への接触やプレイヤーから接触地点までの遮蔽物でダメージを遮断する。一振りは最大1対象に1回のダメージと既存ノックバックを与える。

ホストは攻撃ID、装備した個体番号、有効期限、姿勢と進行度の妥当性、握りと刃の両端の受付範囲を確認する。Attack Rangeの3は剣の姿勢受付範囲で、実際の命中範囲は刃の形と振り動作に従う。装備変更、持ち物画面、死亡などでは本人の振りを中断し、Cancel要求でホスト側の攻撃も終了する。素手は水平射程3・正面左右45度の距離判定に、敵の中心との高さの差1.5以内と遮蔽確認を加えている。魔法の球は上下を含む視点方向へ発射する。

### 剣の動作と判定データ

WeaponBladeがPCの振りと表示、MeleeHitResolverが入力方式に依存しない刃の姿勢間の接触検出、PlayerAttackがホストでの検証とダメージ確定を担当する。

CrystalSwordDataの調整用デモ値は、ローカル刃元(0, 0.2, 0)、刃先(0, 1.4, 0)、半径0.06、縮尺0.55、振り時間0.3秒。判定の有効区間は振りの進行度20〜70%。これらは担当が置いた初期値で、ユーザーによる固定値の指定ではない。

以前の自動攻撃は現行の手動攻撃へ置き換わっている。以前の「近づくと倒せた」という試遊報告を、最新のクリック攻撃の確認結果として扱わない。

### 敵への命中時のノックバック

ユーザーは命中したことを分かりやすくするため、ノックバックの追加を指示した。現行実装では、近接命中で生き残った敵をプレイヤーから敵へ向かう水平方向へ、魔法命中では球の進行方向を水平面へ投影した方向へ押し戻す。球が真上・真下へ進む場合は、敵の後方への水平押し戻しを使う。

ホストが敵のRigidbody速度を制御し、既存NetworkTransformで同期する。ノックバック中は追跡・攻撃を一時停止し、線形に減速してからAIを再開する。上下速度と重力は継続する。連続被弾では最後の攻撃方向へ変更して時間を更新し、速度を加算しない。壁や砦などの進路を遮る面の手前0.05mで停止する。致死ダメージでは死亡・ドロップ・消滅を優先する。

敵3種のMonsterDataにKnockback SpeedとKnockback Durationを追加し、担当が置いた調整用初期値は全種8m/s・0.2秒。各敵アセットのInspectorで調整でき、どちらかを0にすると無効となる。実行中に0にした場合も継続中のノックバックを終了する。方向を省略したTakeDamage呼び出しは、従来のダメージ処理だけを行う。

### ジャンプとブリンク

ユーザーはSpaceでジャンプ、Shiftでブリンク、ブリンク方向は入力中の移動キーの方向と指定した。その後、VALORANTのジェットのように経路上を短時間で高速移動する形への改善を指示した。本書ではこの現行方式をダッシュとも呼ぶ。ジャンプの高さ、ブリンク距離、基本ステータスはInspectorで調整できるようにすることもユーザーの指定。

現行実装では、ジャンプは接地中に可能で、空中の追加ジャンプを許可しない。ブリンクはShiftを押した瞬間に開始し、視点の左右方向を基準とした移動キーの方向へ高速移動する。開始時の方向を終了まで維持する。移動キーが無入力の場合は発動せず、斜め方向も設定距離は同じ。空中でも使用でき、上下速度と重力は継続する。

ブリンクは開始時と各物理更新で本体と子のコライダーを含めて進路を調べ、進行を遮る壁や砦などの手前0.05mで停止する。進路に平行な床の接触は除き、トリガーは通過する。死亡中と持ち物画面中のジャンプ・ブリンク入力は破棄し、進行中のダッシュも中断する。移動処理にはフェーズ制限がなく、現行ではロビーと勝敗後も移動・ジャンプ・ブリンクの入力処理が動く。これを今後の固定仕様とするかは未決定。

| PlayerStatsの調整項目 | 現在の初期値 | 決定の根拠 |
| --- | --- | --- |
| Jump Height | 1.5m | 基礎作り担当が置いた調整用初期値 |
| Blink Distance | 4m | 基礎作り担当が置いた調整用初期値 |
| Blink Duration | 0.15秒 | 基礎作り担当が置いた調整用初期値 |
| Blink Cooldown | 1.5秒 | 基礎作り担当が置いた調整用初期値 |

初期値、空中ブリンク、障害物停止の余白は現行実装の記録。ユーザーが数値や詳細挙動まで承認したものとして扱わない。HP・攻撃・防御・移動速度とレベル成長量も、PlayerプレハブのPlayerStatsで調整できる。ジャンプとブリンクは本人の端末で判定する。ダッシュは距離÷移動時間から速度を計算し、Rigidbody.linearVelocityで複数の物理ステップを通って進む。位置は通常のClientNetworkTransformで同期し、終了後は通常移動へ戻る。摩擦や衝突により設定距離と実移動距離には差が生じる。Blink Durationの実行時下限は0.02秒。

## 4. 成長と死亡

以下はPlayerプレハブの設定とPlayerStatsの計算式に基づく。

| 能力 | Lv1 | レベルが1上がるごとの増加 |
| --- | --- | --- |
| 最大HP | 100 | 15 |
| 基礎攻撃力 | 12 | 2 |
| 基礎防御力 | 0 | 0.5 |
| 移動速度 | 6 | 0.1 |

- 次のレベルまでの必要経験値は `30 × 1.4^(現在レベル - 1)`。
- 共有レベルアップ時に生存者のHPは全回復する。戦闘不能者を復活させない。
- 各手の攻撃力は基礎攻撃力、レベル補正、その手の武器の攻撃補正、防具の攻撃補正の合計。反対の手の武器の攻撃補正は加算しない。
- 最大HPと防御力には、左右両方の武器と防具の補正を加算する。
- プレイヤーの被ダメージは `max(1, 敵の攻撃力 - 防御力)`。
- HPが0になると死亡し、3秒後に初期位置へ戻って全回復する。死亡中は移動・攻撃・ドロップ取得ができない。
- 経験値とレベルはチーム共有。持ち物と選択した強化は個人別。永続セーブは現行の調査対象コードにない。

### 共有経験値と各自の3択強化

ホストの試合単位TeamProgressionへ経験値を一度加算し、共有Lv・Exp・選択停止状態を各PlayerStatsへ同じスナップショットで同期する。既存の自動成長は維持する。各レベルアップで各自に重複しない能力種類の3択を提示し、個人の強化値へ適用する。提示した能力と量はスナップショットで固定し、本人のticketと選択番号だけをホストで検証する。古い・二重・範囲外・非所有者の要求は拒否する。

各端末のTime.timeScaleを0にし、全員の全選択が終わるまで待つ。NPC・弾・復活待ち、回収・出現と操作要求も停止し、通信と選択UIは続く。WaveManagerはNetworkTime上の停止時間を補正して時計を保持する。攻撃の待ち時間と受付期限はscaled Time.timeAsDouble基準とする。選択後はTime.timeScaleを1へ戻す。

複数レベル分は順に提示する。死亡中も選択でき、切断者を待ち対象から除外する。途中参加者は共有進捗へ合流し、過去のLv2以降の分を自分で選ぶ。再戦で進捗と個人強化を初期化する。会心率100%の候補は以降除外し、無効なカタログでは有効候補だけを最大3件提示、全候補無効ならログを出してその回をスキップする。これらの境界条件は担当の実装判断。

| 強化 | 標準カタログの仮値 |
| --- | --- |
| 攻撃 | +2 |
| 最大HP | +15 |
| 防御 | +1 |
| 会心率 | +0.05（5ポイント） |
| 会心倍率 | +0.25 |
| 攻撃速度係数 | +0.1 |
| 移動速度 | +0.5m/s |
| ジャンプ高さ | +0.25m |

会心率の基礎は0、倍率1.5、攻撃速度係数1。ホストが剣・素手・本の会心を判定する。攻撃速度は再使用間隔と剣の振り時間に適用し、振り下限0.1秒、その他間隔下限0.02秒。生存中の最大HP選択では増加分のHPも補充する。武器個体の左右適用ルールは維持する。

カタログは`Assets/Resources/Progression/LevelUpCatalog.asset`。安定ID・表示名・能力種類・量・重みをInspectorで編集する。PlayerStatsのLevel Up Catalogを指定しなければ標準Resourcesを使う。項目追加と並べ替えは安定IDで扱うが、新しい効果種類はUpgradeStat・PlayerUpgradeBonusesとPlayerStats.Progressionの適用・計算を追加する。

公開APIはIsTeamLevelUpPaused、CurrentLevelUpOffer、PendingLevelUps、UpgradeBonuses、UpgradeCatalog、ChooseLevelUp(ulong ticket, int index)、CriticalChance、CriticalMultiplier、AttackSpeedMultiplier、GetAttackDamage(bool)、GetSwingDuration(ItemData)、GetAttackInterval(float, ItemData)。既存Level・Exp・ExpToNextは互換名を維持。LevelUpChoiceUIは独立画面で選択・相棒待ちを表示し、閉じる操作はない。HUDは既存APIから共有Lv/Expを表示するが、個人強化詳細の追加表示は未対応。

## 5. 敵とドロップ

| 敵アセット | 初登場Wave | HP | 速度 | 攻撃力 | 攻撃間隔・秒 | 射程 | プレイヤー索敵距離 | 出現重み | 経験値 | 基本装備ドロップ率 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Goblin | 1 | 30 | 3 | 5 | 1.5 | 1.5 | 10 | 3 | 10 | 15% |
| Orc | 2 | 50 | 2 | 7 | 4 | 2 | 3 | 2 | 30 | 50% |
| Witch | 2 | 30 | 1 | 10 | 5 | 15 | 15 | 1 | 30 | 50% |

値はウェーブ倍率適用前。敵は索敵距離内の最も近い生存プレイヤーを優先し、いなければ砦へ直進する。射程に達すると停止して攻撃する。Witchは射程が長いが、現在は弾を飛ばす処理を持たない。

倒した敵は経験値オーブを生成し、確率で装備を1個落とす。現在は3種すべてのドロップ表とシーンのItemDatabaseにCrystal SwordとMagic Bookが登録されている。装備が出る確率は上表とウェーブ補正を維持し、出る装備は剣と本を重み1対1で抽選する。比率は基礎作り担当が置いたデモ設定。以前の「ドロップ表が空」という調査結果は更新済み。

ドロップは水平距離で最も近い生存プレイヤーへ吸い寄せられる。武器は取得した本人へ、経験値はチーム共有へ加算する。Pickupクラスの既定値は吸引距離6、速度10、取得距離1.2で、プレハブの保存値が優先される。レベルアップ選択による全体停止中は回収しない。

## 6. 武器・防具・持ち物

### ユーザーが決めた方向

- 左右に1本ずつ持つ二刀流を使えるようにする。
- 様々な性能の武器を落とし、自分の意思で持つ武器を変更したい。
- 拾った武器を持ち物に保管し、画面から左右の手に装備する。
- Crystal Swordは片手剣のデモ版として扱う。
- 追加したbookを魔法用の武器として使い、仮の球を発射できるようにする。

### 現行実装

- Crystal Swordは攻撃力補正+5、防御力補正0、最大HP補正0。外見には追加された剣プレハブを使う。
- Magic Bookは左右どちらにも装備できる片手武器。装備した手のクリックで仮の球を発射し、反対の手に剣を持てる。
- 武器は拾った順に保管し、同じ種類も1本ずつ別の持ち物として保持する。新規取得時に右手が空なら右手へ、右手が装備中で左手が空なら左手へ、その個体を自動装備する。両手とも空なら右手を優先する。両手装備中なら保管のみで既存装備を置換しない。武器の経験値への自動変換は行わない。重複した個体IDは保管・自動装備の前に拒否する。
- Iキーで持ち物一覧を開き、攻撃・防御・最大HPの補正とレアリティを見て左右の装備を選ぶ。
- 装備解除後も武器は持ち物に残る。同じ1本を同時に両手へ装備できない。反対の手で装備中の1本を選ぶと、両手の装備が入れ替わる。
- 同種を2本所有していれば同種の二刀流が可能。
- 持ち物画面を開いている本人の移動・視点・攻撃入力は停止する。戦闘時間と敵の行動は進み続ける。
- 武器モデルは自分の画面ではカメラの左右に配置する。相手の近接武器は待機中はキャラクターの左右、振り中は受理されたワールド姿勢に追従する。近接武器の縮尺は本人・相手ともItemData.meleeScaleを使う。実際の手の追跡は行わない。
- 防具は従来の自動比較を維持する。スコアが高ければ自動装備、同等以下は経験値へ変換する。スコアは `攻撃補正×2 + 防御補正×2 + 最大HP補正×0.2`。
- 持ち物はNetworkList<ItemInstance>で個体のID・レア度・性能を保管する。モデルと攻撃方式は共通ItemDataを参照する。攻撃方式はMeleeとProjectileで分岐し、既存剣は既定値Meleeを使う。

### 武器を落として味方へ渡す

持ち物の各行の「落とす」から、装備中・未装備の武器を地面へ戻せる。所有者が個体IDをホストへ送り、ホストが所持・生存・ゲーム終了状態を確認する。現在位置のYから0.5下に同じItemInstanceを生成し、生成成功後に所持一覧から削除する。落とした手を解除し、一覧の後続番号と他の装備を補正してHPを新上限へClampする。削除前の近接攻撃状態は無効化する。

落とした本人は、アイテムとの水平距離がmax(吸引距離, 取得距離)の外へ一度離れるまで回収対象外。味方はすぐ通常の吸引・取得の対象になり、取得時は空き手への自動装備を使う。個体ID・レア度・性能は再抽選せず保持する。プレイヤーが落とした武器は次ウェーブの掃除対象外だが、シーン再起動では消える。投擲・地面への落下演出・永続保存は追加していない。

| API | 用途 |
| --- | --- |
| PlayerEquipment.DropWeapon(ulong instanceId) | 所有する武器を落とす要求 |
| PlayerEquipment.EquipWeapon(ulong instanceId, bool leftHand) | 個体IDで装備要求。RPCも個体IDで照合 |
| PlayerEquipment.EquipWeapon(int inventoryIndex, bool leftHand) | 互換API。送信前に個体IDへ解決。-1は解除 |
| DropManager.TryDropItem(Vector3, ItemInstance, ulong) | 同じ個体の生成成功をboolで返す |
| ItemPickup.InitDropped(ItemInstance, ulong) | 落とした本人の一時除外とウェーブ間保持を設定 |
| Pickup.CanCollect / ClearOnWaveStart | 回収可否と掃除対象の拡張口 |

HUDはLayout時の個体IDを装備・ドロップ要求に使い、一覧削除後の古い行から別個体を操作しないようにする。今回の対象は武器の所持一覧で、防具等の受け渡しは追加していない。

### Magic Bookと仮の球の設定

本のデータは`Assets/buki/BookData.asset`、外見は`Assets/buki/book.prefab`、球は`Assets/Prefab/MagicProjectile.prefab`。担当が置いた共通デモ値は攻撃補正+5、防御・最大HP補正0、球の速度20m/s、寿命2秒、判定半径0.15m。個体抽選前の固定値ならLv1で17ダメージとなり、現在の抽選個体では個体の攻撃補正に従って変わる。数値はBookDataと球プレハブで調整できる。

球はプレイヤーの頭付近の左右から視点方向へ発射する。ホストが生成・移動・命中を処理し、NetworkObjectとNetworkTransformで同期する。初期位置の重なりと移動経路を球の半径で検査し、敵への命中時に1回ダメージを与えて消える。壁・地面などの非トリガー障害物への命中、寿命終了、戦闘フェーズ終了でも消える。自分と相棒のコライダーは除外する。

モデルの大きさ・向きの調整として、担当はbook.prefabを30倍、ローカル位置Y0.5、回転X-50度へ変更したと報告している。元のbook.fbxは変更していないとの報告。モデルがカメラの視野内に入ることは幾何学的に検証されているが、実画面での目視調整は未確認。

盾、左右の枠を両方使う大型武器はユーザーの将来希望。ガード、両手占有、魔法の追加種類や演出、各武器の最終性能は別途決める。持ち物の上限、売却・廃棄、永続保存も未決定。

能力拡張の決定事項・候補と未決定事項は[引き継ぎ](HANDOFF.md)を参照する。

### 個体差の承認事項と実装済みデータ契約

基礎作りから、ユーザーが「攻撃能力はその武器だけ、装備者の能力は左右合算」という適用ルールを承認したと共有された。この分類に沿って、まず個体データ・ホスト抽選・同期の土台を作り、既存の攻撃力・防御力・最大HPへ接続する。残りの能力の分類詳細、発動仕様、数値は後続の設計対象。

以下は完了報告と現行コードを照合したAPI。検証環境と未確認事項は[引き継ぎ](HANDOFF.md)の検証状況を参照する。

| データ・API | 内容 |
| --- | --- |
| ItemInstance | INetworkSerializable・IEquatableを実装した値型。InstanceIdはulong、DefinitionIndexは共通データの番号。Rarity、AttackBonus・DefenseBonus・MaxHpBonusを保持 |
| PlayerEquipment.GetInventoryInstance(index) | 持ち物の個体情報 |
| PlayerEquipment.GetWeaponInstance(leftHand) | 左右に装備した個体情報 |
| ItemPickup.Instance | 地面のドロップの個体情報 |
| GetInventoryWeapon / GetWeapon / LeftWeapon / RightWeapon | 共通ItemDataを返す互換APIを維持。個体の表示値には使わない |
| ItemInstance.IsValid / Definition | 有効性と共通ItemDataへの参照 |
| ItemDatabase.CreateItem(ItemData) | ホストのみ個体を生成 |
| ItemData.GetRarityColor(ItemRarity) | 個体のレア度を渡して色を取得 |

個体データはItemInstance、生成はItemDatabaseとDropManager、取得はItemPickup、所持・装備はPlayerEquipment、ステータス計算はPlayerStatsが担当する。

HUDの持ち物一覧はItemInstanceのキャッシュを使い、個体のRarityとAttackBonus・DefenseBonus・MaxHpBonusを表示する。左右の装備欄も個体APIを使う。名前はDefinitionを参照し、戦闘HUDの攻撃力はGetAttackによる基礎値込みの最終値を表示する。コード対応済みで、実表示の受入確認は未完了。担当と対応状況は[引き継ぎ](HANDOFF.md)で管理する。

個体は世界に落とす前にホストが生成し、ItemPickupのNetworkVariableから所持一覧へ同じ値を渡す。AttackBonus・DefenseBonus・MaxHpBonusは基本値を含む、その1本の補正の合計。取得・装備・解除・左右交換で再抽選せず、同じInstanceIdの重複取得を拒否する。既存の防具は固定値と従来処理を維持する。

### 現在のデモ抽選設定

剣と本に同じ仮設定を置いている。ItemDataのWeaponRarityRoll配列から重み付きでレア度を選び、攻撃・防御・最大HPのうち重複しない能力を選んで0.1刻みで基本値へ加算する。範囲の両端が0の能力は候補外。設定が空または重み合計0なら共通データの固定値とレア度を使う。

| レア度 | 重み | 加算能力数 | 攻撃加算範囲 | 防御加算範囲 | 最大HP加算範囲 |
| --- | --- | --- | --- | --- | --- |
| Common | 70 | 1 | 1〜3 | 1〜2 | 5〜15 |
| Rare | 25 | 2 | 2〜5 | 2〜4 | 10〜25 |
| Epic | 5 | 3 | 4〜8 | 3〜6 | 20〜40 |

上表は担当が置いた調整可能なデモ値であり、最終バランスの承認値ではない。抽選後に共通設定を変更しても、既存個体の数値は保持する。

## 7. 通信とUI

現行の通信はホスト1人と参加側1人。サービスを初期化して匿名ログインし、Relayの参加コードで接続する。ホストがウェーブ、敵、ドロップ、HP、経験値、装備状態と武器の所持一覧を管理する。プレイヤーの移動位置は本人が決め、ClientNetworkTransformで同期する。

持ち物はNetworkList<ItemInstance>、地面の個体はNetworkVariable<ItemInstance>で同期し、左右の装備枠は持ち物内の番号を参照する。装備変更要求は所有者からホストへ送る。装備の見た目は同期された装備情報から各端末で生成する。頭・左右の手を独立追跡して同期するVR用処理は未実装。

近接の動作中はPCの武器姿勢を最大約50Hzでホストへ送り、受理された姿勢をRPCで他端末へ渡す。表示側は装備個体番号の不一致や古い攻撃IDを無視する。2人接続での動作表示と遅延は未確認。ホストの現在位置による判定とクライアント表示の差に対する遅延補償は未検証。

HUDはStart時に既存TMP欄を再配置し、相棒・左右武器・案内・照準・HPバーを補う。シーンのteamTextは未割り当てだが実行時に生成する。CanvasScalerを実行時だけ1920×1080基準、ScaleWithScreenSize、match 0.5にする。ProjectSettingsとシーン保存値は変更していない。

配置は上中央にウェーブと砦HP・バー、右上に時間、左上に相棒、左下に自分の状態・HPバー、下部に左右武器、右下に持ち物案内、中央に照準、中央下に通知、勝敗時に中央へ結果を表示。ロビーとローカルプレイヤー不在時は戦闘HUDを隠し、持ち物表示中・死亡中・勝敗後は照準を隠す。

戦闘欄、持ち物画面、既知の剣・本の名前、レア度、通知・勝敗をHUDで日本語化する。持ち物はOnGUIで描画する。既知英文通知の置換であり、汎用ローカライズ基盤ではない。接続メニューは今回のUI変更範囲に含まれない。

持ち物画面は不透明な比較表、表示窓上限1120×820。最小表示寸法基準でGUI全体を画面内へ縮小する。上に左右装備と解除、固定列見出しは装備・武器名・レア度・攻撃力・防御力・HP。数値は個体のAttackBonus・DefenseBonus・MaxHpBonusであり、プレイヤーの最終能力値ではない旨を画面にも記す。中央の一覧だけスクロールし、下に選択個体名と固定の左手装備・右手装備・落とす、最下段に戦闘継続注意とI/Esc案内を置く。

表示用スナップショットだけを並べ替える。初期はエピック→レア→コモン、同レア度は取得順。攻撃力・防御力・HPの見出しクリックで該当する個体補正の降順へ変更し、同値はレア度降順、その後は取得順。レア度見出しでレア度順へ戻す。並べ替え列に↓を表示し、クリックでスクロールを先頭に戻し、次Layoutで反映する。NetworkListは並べ替えない。装備中は白枠と左手・右手ラベル、選択中は青灰色の背景で区別する。選択はInstanceIdで維持し、個体がなくなれば解除して隣の個体を自動選択しない。選択なしでは操作ボタンを無効にする。武器名・レア度は白青紫、補正数値は白。個体IDによる装備・ドロップ要求と解除互換APIを維持する。

列名変更と見出しクリックの並べ替えは、担当がC#コンパイルと差分チェックを確認した段階。実画面・クリック・拾得・通信・ビルドは未確認で、以前の14個体検証をこの新操作の確認結果として扱わない。

UI担当はFull HDのGameビュー0.82倍、ローカルStartHostで合成ItemInstanceをAPIから14個投入し、並び順・列揃え・固定欄・スクロールを目視確認したと報告。実マウスで選択、左手装備、装備枠更新、スクロール後の選択保持、落とすによる14→13・左手解除・選択解除・操作無効化を確認。持ち物は検証メニューで開いており、I入力や物理拾得の確認ではない。最後のホバー色補正はコンパイル・静的確認のみ。実戦拾得、右手装備・左右交換・解除の新画面での実クリック、2人通信、他解像度、ビルドは未確認。

時間欄は右上360×80、文字34、折り返しなし・Overflowで表示し、参照がなければ実行時に生成して有効化する。TimeRemainingを非負の分秒として表示する。ユーザーの時間欄消失報告に対し、日本語フォントの行高と省略設定を原因候補として対処したが、実画面での原因確定と修正確認は未実施。

個体のレア度に応じ、コモンは純白、レアは青、エピックは紫で文字を表示する。対象は左右武器HUD、持ち物の武器名・レア度と装備欄、既知の拾得・左右自動装備通知。その他の通知は白へ戻す。青・紫は既存ItemData.GetRarityColorを使い、コモンはHUD側でColor.whiteを使う。左右自動装備通知の日本語化も対応済み。実画面での一致確認は未完了。

日本語フォントは`Assets/Resources/Fonts/NotoSansJP.otf`。Noto Sans CJK JP Regular固定OTFをローカル名で保存し、SIL Open Font License 1.1を同ディレクトリのOFL.txtに同梱する。担当の取得元は[公式フォント](https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/OTF/Japanese/NotoSansCJKjp-Regular.otf)と[ライセンス](https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE)。TMPはResourcesから実行時に動的フォントを生成し複数アトラスを有効にする。OnGUIは描画中だけ専用GUISkinに切り替える。固定OTFのUnityインポートと実表示は未確認。

## 8. PC VRの位置づけ

ユーザーは将来のVR方式としてPC接続型を選択した。現在はマウス・キーボードで操作するPC版を制作している。

現時点のPackages/manifest.jsonにOpenXR、XR Interaction Toolkit、XR Plug-in Managementの登録はない。Unity標準モジュールのVR/XRという名前だけで、VR対応済みとは判断しない。

以下は未決定で、今後の試作前に決める。

- 対象ヘッドセットと実機検証環境。
- PCの通常画面のプレイヤーとVRプレイヤーが同じ部屋で遊べるか。
- VRの移動と旋回方式。
- 剣を振って当てる方式か、トリガーで攻撃する方式か。
- 魔法の狙い方、VR内の装備変更、UIの配置。
- 必要なPC性能、目標フレームレート、対応機器の範囲。

基礎作りから、ユーザーが「VRでは武器の形で命中候補を調べ、ホストがダメージを確定する。入力・武器の動きと戦闘ルールを分ける」という方向を承認したと共有された。PCでの武器の振りと将来のVR追跡姿勢を共通の判定処理へ渡す構成で進める。VR入力やXRパッケージの導入は今回の着手範囲に含めない。

現行コードには外部姿勢用のBeginAttack(bool, Vector3)、SubmitMeleePose(bool, int, Vector3, Quaternion, float)、CancelMeleeAttack(bool, int)がある。攻撃IDを取得して握りのワールド位置・回転・動作進行度を送り、PCの振り開始用RequestAttackとは分けて利用できる。VRの追跡入力、自動スイングの開始条件、振動、実機向け操作は未実装。

## 敵付近のダメージ数字（DAMAGE-01）

承認仕様は、攻撃した本人だけに、ホストが確定した会心込みの攻撃ダメージを表示すること。残HPを超える場合も攻撃の確定値を使う。頭上付近から浮上・フェードし、敵の撃破後も表示を残す。通常白、会心黄色で拡大し、壁越しには見せない。基礎通知・UI単体に加え、ホスト素手命中からOwner RPCと表示状態までの隔離統合検証が完了。実画面と端末別の統合受入は未完了。

PlayerStats.RollAttackDamage(bool)はAttackDamageResultのAmount・IsCriticalを一度抽選し、近接は振り開始、魔法は発射時に保持する。素手は命中時に抽選。GetAttackDamage(bool)はfloat互換ラッパーとして維持する。Monster.TakeDamage(AttackDamageResult, PlayerStats, Vector3)はサーバー上の生存・Spawn済み敵と正の有限ダメージを確認してHPから引き、致死Despawn前に攻撃者へ通知する。旧float版はHP処理を維持するが、攻撃者を捏造して数字を出さない。

頭上基準は敵ルートのXZと非Trigger Collider群の上端Y。ColliderなしはRenderer上端、どちらもなければルート。攻撃者が同じNetworkManagerのサーバー管理下であることを確認し、PlayerStats.NotifyDamageConfirmedからServer invoke限定のOwner RPCを通してDamageFeedback.DamageConfirmedを一度発火する。ホスト本人への別通知は追加しない。魔法の新InitはAttackDamageResultとPlayerStatsを受け、旧float版も維持する。攻撃者が死亡してもPlayerObject存続中は通知でき、切断で消滅していれば通知しない。

左右それぞれの受理済み攻撃IDで再送を拒否し、近接のConsumedと魔法のimpactedで重複を防ぐ。無効値や不受理命中ではHPと通知を変更しない。

DamageNumberPresenterがDamageFeedback.DamageConfirmedを購読し、Amount・WorldPosition・IsCriticalだけを使う。自動初期化とsceneLoadedで表示用Canvasを生成、シーン再開始やdisableで旧表示と購読を消す。敵Transform・親子関係・寿命を参照せず、受信位置を保持する。数字ごとのNetworkObjectは作らない。

独立したScreenSpaceOverlay Canvasへワールド位置をローカルカメラで毎フレーム投影する。表示は固定HUDの位置へ貼り付かない。sortingOrderは-10。背面、near/far外、画面外、環境Colliderに遮られた場合は隠す。RaycastAllとカメラ位置のOverlapSphereを使い、敵・プレイヤー・拾得品・魔法弾・Triggerを遮蔽物から除く。横ずらし後の位置も調べる。Colliderのない壁は遮蔽対象にならない。

設定は`Assets/Resources/DamageNumberSettings.asset`。既存のNotoSansJPとライセンスを再利用し、表示用プレハブの追加はない。

| 仮設定 | 現在値 |
| --- | --- |
| 数字の書式 | InvariantCultureの0.#。表示だけ小数1桁まで、末尾0省略 |
| 基準位置への加算 | 上へ0.15m |
| 寿命・上昇・フェード開始 | 0.9秒・0.65m・寿命の30% |
| Full HD基準文字 | 36px、会心1.25倍（45px） |
| 色 | 通常純白、会心純黄 |
| 文字領域 | 240×90 |
| 横ずらし | 40px間隔の5位置 |
| 同時上限 | 96。超過時は最古表示を再利用 |
| 環境Mask・終点余白 | 全レイヤー・0.02m |

Time.deltaTimeで寿命・浮上・フェードを更新し、全体停止中はこれらも停止する。カメラ投影と遮蔽は停止中も更新する。0.05未満等の小さい正値は書式で0と表示される場合があり、将来の微小ダメージ書式は未決定。上限時の最古表示再利用は負荷対策による担当判断で、すべての数字が常に寿命いっぱい残る保証とは区別する。

UI単体のnographics隔離Playは合成イベントのTMP状態・投影・物理遮蔽・寿命・停止復帰を検証。追加の隔離統合試験ではBeginAttackから通常の素手命中・HP減少・Owner RPC・Presenterへ通し、通常17/白36px、残HP5への会心17/黄45pxと敵Despawn後の表示保持を確認したと担当報告。AddExpで実TeamProgressionの停止を発生させ、timeScale0中のage・位置・alpha保持、両者のChooseLevelUp後の寿命終了も確認した。製品8ソースは元ファイルとSHA256一致との報告で、合成Publishは使っていない。

同一プロセスの静的イベントとPresenterを共有する試験なので、参加者画面だけの表示分離を証明するものではない。プレイヤー位置同期は固定し、NPC停止とクライアント敵Collider無効化を使用。実入力・目視、剣/魔法とUIの統合、参加者攻撃の端末分離、実命中時の壁際、通常移動同期、別PC/Relay/ビルド/XRは未確認。

## 制作者モード

検証用の独立画面。Unity EditorまたはDevelopment Buildでホスト本人のPlayerStats生成時にCreatorModeとCreatorModeUIを追加する。F2で開閉、Esc・閉じるでも閉じる。参加クライアントと通常の非Developmentビルドでは使えない。画面を開くと本人の移動・視点・ジャンプ・ダッシュ・攻撃を抑止し、カーソルを解放する。持ち物との同時表示を防ぐ。画面を閉じても敵停止は維持し、再開ボタンまたはモード破棄で解除する。

ホストがチームの共有レベルを1以上の整数へ上げ下げし、経験値を0にする。上げた差分の3択を全員に提示する。下げた場合は未選択キューを消すが、既に選んだ個人強化は検証用として保持する。必要経験値の計算が無効となる値は拒否する。選択・待機中は制作者画面を閉じ、F2と持ち物を開けない。現ホストのLocalClient.PlayerObjectからモードを取得し、再戦後も操作できるようにする。

ItemDatabase登録済みの武器を直接所持品へ追加する。通常抽選・コモン・レア・エピックを選べる。指定時はそのレア度のWeaponRarityRollで抽選し、共通アセットのレア度・重みは書き換えない。該当設定がなければ拒否し、旧固定武器は元レア度のみ許可する。通常ドロップは従来どおり。固有IDと個体同期、空き手への自動装備を利用する。

敵停止はホストの敵Rigidbodyをキネマティックにし、移動・攻撃・ノックバック進行・追加出現と出現待ち時間を止める。停止中に生成した敵も止まる。プレイヤーによる被ダメージ・撃破・ドロップは有効。再開時は元の物理状態・速度を戻す。Time.timeScaleは変更せず、ウェーブ時計と通常フェーズ遷移・回収時の敵撤退は続ける設計。時計と通常遷移を跨ぐ実行検証は未確認。

| API | 用途 |
| --- | --- |
| CreatorMode.Instance / SupportedBuild / CanUse | 有効環境とホスト本人の権限 |
| IsMenuOpen / MonstersPaused / SetMenuOpen(bool) | 画面と敵停止の状態 |
| TrySetLevel(int) / TryGiveWeapon(int, ItemRarity? = null) / SetMonstersPaused(bool) | 検証操作 |
| PlayerStats.TrySetCreatorLevel(int) | モード権限を再確認して本人のレベルを変更 |
| PlayerEquipment.IsControlBlocked | 持ち物・制作者画面による操作抑止 |
| ItemDatabase.Count / CreateItemForCreator(ItemData, ItemRarity) | 登録一覧と権限付きレア度指定生成 |
| Monster.SetCreatorPaused(bool) | 敵の物理・AI停止と復元 |

F2・ホスト専用・有効ビルド・経験値/HP・時計の扱いは担当の実装判断で、ユーザーの個別指定とは区別する。内部APIによる隔離検証は成功報告を受領。F2実操作、日本語UIと既存HUDとの重なり、実ビルド、別PC/Relay、通常ウェーブ遷移は未確認。詳細はHANDOFF.mdを参照する。

## 実ファイルの参照先

| 確認する内容 | 主な参照先 |
| --- | --- |
| シーン内の値と参照 | `Assets/Scenes/gameplay.unity` |
| 接続と人数 | `Assets/Scripts/NetworkStartUI.cs` |
| 進行と勝敗 | `Assets/Scripts/WaveManager.cs`、`Assets/Scripts/Fortress.cs` |
| 操作・攻撃 | `Assets/Scripts/GameInput.cs`、`Assets/Scripts/PlayerController3D.cs`、`Assets/Scripts/CameraFollow.cs`、`Assets/Scripts/PlayerAttack.cs` |
| 成長と装備 | `Assets/Scripts/PlayerStats.cs`、`Assets/Scripts/PlayerEquipment.cs`、`Assets/Scripts/ItemData.cs` |
| 敵とドロップ | `Assets/Scripts/Monster.cs`、`Assets/Scripts/MonsterSpawner.cs`、`Assets/Scripts/MonsterData.cs`、`Assets/Scripts/Pickup.cs`、`Assets/Scripts/DropManager.cs`、`Assets/Scripts/ItemPickup.cs` |
| 画面 | `Assets/Scripts/HUD.cs` |
| 実データ | `Assets/Prefab/Player.prefab`、`Assets/MonsterDeta/*.asset`、`Assets/buki/CrystalSwordData.asset` |
| 魔法武器と球 | `Assets/buki/BookData.asset`、`Assets/buki/book.prefab`、`Assets/Scripts/MagicProjectile.cs`、`Assets/Prefab/MagicProjectile.prefab`、`Assets/DefaultNetworkPrefabs.asset` |
| 剣の動作と接触検出 | `Assets/Scripts/WeaponBlade.cs`、`Assets/Scripts/MeleeHitResolver.cs`、`Assets/Scripts/PlayerAttack.cs`、`Assets/buki/CrystalSwordData.asset` |
