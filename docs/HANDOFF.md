# ShiroMamo 引き継ぎ

更新日 2026-11-20

## 最初に把握すること

最大2人で砦を守る一人称のPCゲーム。将来はPC接続型VRを目指す。2人で接続・開始・プレイできたことはユーザー確認済みだが、その報告は現在の武器機能より前のもの。面白さについてはユーザーから「まだなにもおもしろくない」と報告され、戦闘と装備の基礎を制作中。

最新の実装は、剣の軌跡判定、本の魔法球、盾バッシュ、ジャンプ・ダッシュ、敵ノックバック、ダメージ数字、個体別レア度・攻撃倍率・HPと盾軽減。本人防御と武器加算攻撃はEQUIP-02で廃止した。実操作と実表示は受入確認待ち。

DEV-01の制作者モードとPROG-01の共有経験値・各自3択強化も実装・隔離検証の完了報告済み。会心や攻撃速度等の現行7強化はレベルアップ選択の個人効果であり、武器個体の抽選候補を8種類へ増やしたものではない。実操作の受入確認は残る。

ユーザーが片手だけ空の場合も自動装備するよう追加承認し、基礎作りが実装。新規取得時に右手が空なら右手、右手が装備中で左手が空なら左手へ装備する。両手空なら右手優先、両手装備中なら保管のみ。個体の重複拒否と既存装備の保持を維持する。

**直近の確認事項は、新しい持ち物比較表の実戦拾得・右手操作・通信での受入。** UI担当がレア度順の比較表と選択式の固定操作へ変更。合成個体14個で実表示・選択・左手装備・落とす・スクロールを確認したと報告。物理拾得・2人通信の確認とは区別する。

## 読むファイル

| ファイル | 役割 |
| --- | --- |
| [共通ルール](../AGENTS.md) | 全チャットの作業・決定・報告のルール |
| この文書 | 現在地、担当、次の仕事、確認状況 |
| [ゲーム仕様書](GAME_SPEC.md) | 人間向けのゲーム内容と遊び方 |
| [技術仕様](SPEC.md) | 現行実装、数値、データAPI、参照コード |
| [作業・検証履歴](CHANGELOG.md) | 過去の変更、調査基点、成功ログと検証の詳細 |

## 担当と権限

| 担当 | 役割・状態 |
| --- | --- |
| ユーザー | 最終意思決定。仕様変更と作業範囲を承認 |
| Codex司令塔 | このチャット。ID `01a1c3f0-571e-7700-97d3-a8a6cd7dd387`。方針・優先順位・担当整理・共通文書の更新。実装相談は担当へ案内 |
| ChatGPT司令塔 | 企画・方針の検討。チャットIDは未共有 |
| 基礎作り | ID `01a1c3f6-e34f-7443-a593-1bcb3a0bb7f8`。移動・戦闘判定・敵行動・内部データ・同期。直近の個体差第1段階は実装完了報告済み |
| UI担当 | ID `01a1ce17-1a45-77b3-a138-924e74957557`。承認された仮戦闘UIと日本語化をHUD.cs・日本語フォント素材へ実装。実表示の確認待ち。今後のUI全体をCodexへ全面委任するか人間が制作するかは未決定 |
| 共同制作者 | Claudeを利用。現在の作業範囲は未共有 |

基礎作りからDROP-01の実装・隔離環境検証の完了報告を受領した。実操作と実際のプレイヤー位置同期を含む受入確認は残る。ユーザー指定のタイミングまでコミット・pushは待つ。

DROP-01限定の基礎作りによるHUD行編集は完了し、今回のHUD.cs編集予約を解除する。UIを再編集する際は、実装済みの「落とす」ボタンと個体ID要求の変更を保持する。UI全体の制作委任は引き続き未決定。

今回の変更はPlayerEquipment、DropManager、Pickup、ItemPickup、PlayerAttack、HUDの6ファイル。装備要求のint互換APIを保ち、個体ID版とRPC照合を追加した。詳細はSPEC.mdを参照する。

ユーザーは「自分で落とした武器は消さずに残す」と承認したと担当から報告。プレイヤー品を次ウェーブ掃除から除外し、敵ドロップの掃除を維持する実装を確認。シーン再起動では消える。最終検証はホスト→参加者→ホストの往復まで成功したが、テスト内でプレイヤー位置同期を無効化した制約がある。

## 未完了・次の候補

### SKILL-01 初期3効果・個体抽選同期・表示（承認済み、基礎実装/UI準備を依頼済み）

ユーザーは司令塔提案の毒/貫通/反射と共通候補を承認。SPEC 0.33に確定内容・未指定事項を記録した。目的は武器を拾い、効果と重複を見て選ぶ遊びを試すこと。着手前git statusはクリーン、両担当の最新ターンは完了で新しい作業報告なし（チャット状態notLoaded）。

基礎作り：ItemData.cs、ItemInstance.cs、ItemDatabase.cs、PlayerEquipment.cs、PlayerAttack.cs、MagicProjectile.cs、Monster.cs、PlayerStats.cs、および新規スキル定義/管理/毒状態/ダメージ文脈用スクリプトとmetaを担当。武器3種のCrystalSwordData.asset/BookData.asset/ShieldData.assetへのスキル設定、新規独立スキル設定assetとmetaも割当。HUD/LevelUpChoiceUI/CreatorModeUI/DamageNumber系、シーン・既存プレハブ・既存モデル・共通文書は対象外。追加ファイルが必要なら変更前に司令塔へ調整する。

第1段階でデータ契約と未指定の毒管理・再帰防止・帰属の設計案を報告し、司令塔が確認する。確定範囲の抽選/同期を進めつつ、判断が必要な効果は確定まで実装しない。公開APIと実ファイルを確認後にUIへ実装依頼する。

UI担当：今回の編集予約はHUD.csと必要なら新規独立スキル表示ヘルパー/metaのみ。準備報告後、司令塔が実APIとデータ検証ログを照合してUI実装依頼を送信した。個体の固有/共通と重複数、固定効果を選択武器で確認できる表示を既存比較・選択ID・ソート・装備操作を保って追加する。既存列/操作の全面改変、独自抽選・戦闘計算・仮契約は行わない。

基礎は倍率/HP・レア度/個体ID保持とスキル同期往復/途中参加/制作者、毒の量/時間/重複/更新/停止復帰、貫通の距離順衝突・同敵重複/複数Collider/壁/出現時重なり、反射の複数盾・軽減前・致死/帰属/非再帰を検証する。MELEE-02・通常ダメージ通知・攻撃停止・ドロップも回帰確認する。実操作/隔離API/通信/別PCを区別して報告。共有文書とGitは司令塔が担当し、次の実行指示まで未コミット保持。

基礎への実装依頼・UIへの準備依頼：両チャットへ送信成功。基礎の契約報告を確認後にUI実装へ進む。

### MELEE-02完了報告と後続スキル計画

2026-11-19、ユーザーは共有文書更新と基礎作りへの近接複数命中依頼を承認。着手前に基礎・UI両チャットがidle、作業ツリーに変更なしを確認した。今回の目的は、敵の群れに攻撃を当てた手応えを改善すること。

MELEE-02は剣・盾バッシュとも複数敵へ減衰なし100%、同じ敵には一振り1回。素手と魔法は現状維持。基礎作りがPlayerAttack.csとMeleeHitResolver.csだけを変更し完了報告。今回の2ファイルの編集予約を解除する。WeaponBlade/Monster等の追加編集が必要なら先に司令塔へ報告。HUD、LevelUpChoiceUI、DamageNumber系、ItemData/ItemInstance、シーン・プレハブ・アセット・共有文書は編集しない。UI担当には今回の新規実装割当なし。

ホスト側の攻撃ID/所有権/期限/姿勢/距離/装備/進行度検証、有効判定区間、遮蔽物接触での停止、敵ごとの遮蔽を維持する。敵自身による遮蔽と地形遮断を区別する。振り開始時の会心結果を維持し、敵の複数Collider・連続姿勢・同じRPC再送でも1体1回、左右と次の振りでは再び命中できることを検証する。多体の致死Despawn中も収集・反復を壊さず、本人へのダメージ通知を敵ごとに1回維持する。

完了報告には変更ファイル、承認仕様との差、コンパイルと検証環境・手順・成功ログ・未確認を添える。実入力/目視・隔離試験・通信試験を区別。今回の編集予約を返却し、未コミットのまま司令塔へ引き継ぐ。送信状況は下記に記録する。

後続SKILL-01は2026-11-20の追加承認で初期3効果の実装へ進む。最新仕様と担当は直下のSKILL-01節およびSPEC 0.33を参照。MELEE-02はc50e03bへコミット・push済み。

MELEE-02は依頼送信後に完了報告を受領。製品差分2ファイルと成功ログを司令塔で照合した。EQUIP-02の実表示・実操作受入は残し、新機能の検証で確認済みへ読み替えない。

担当ログLogs/CodexMeleeMultiVerified.log:2543のMELEE MULTI FULL DAMAGE DEDUP WALL VALIDATION CRITICAL BARE MAGIC HOST REMOTE CHECK PASSED checks=129と2555のMELEE MULTI CLEANUP PASSEDを確認。Unity6000.0.46f1隔離nographics、C#/Netcode後処理を含む129 assertions。剣/盾の2体満額・重複Collider/姿勢/再送拒否・左右/次振り独立・複数致死・保存会心/通知・0.2/0.7境界・壁前後/接触後打切り・姿勢/距離/期限/装備/キャンセル/非Owner拒否を検証したとの報告。素手は実BeginAttackの最寄り1体、魔法は実生成弾への合成Impactで1体と再送拒否を確認。2 NetworkManagerの127.0.0.1:18219通信で参加者から剣/盾のRPCを確認。製品2ソースと試験コピーのSHA256一致は担当確認。司令塔はUnity再実行なし。

制約はAPI/Reflection/合成姿勢、移動とNetworkTransform無効化・位置固定、制作者停止NPCに検証用2Collider、参加者複製のMonster/Collider無効化。同一プロセスstatic通知イベントの回数等の確認で、端末別画面や数字UIの検証ではない。隔離フォント不足のPresenterエラーは試験内Error Pause解除で進めたためUI成功と扱わない。実クリック・目視・通常移動同期込み戦闘・別プロセス/別PC/Relay/ビルド/XRは未確認。製品2ファイルと司令塔文書3ファイルは、ユーザーのコミット・push指示を受けて今回の保存対象とした。以後の変更も別途実行指示を待つ。
### UI-07 レベルアップ選択の連打持越し防止

ユーザーの攻撃連打からの誤選択報告に対し、UI担当がLevelUpChoiceUI.csだけを変更して完了報告。今回の編集予約を解除する。基礎の選択RPC・成長・カーソル制御・共有停止処理は変更していない。

新チケット・表示再開・フォーカス復帰ごとに、左右マウスとReturn/KeypadEnter/Spaceをすべて離して連続0.35秒待つ。途中の押下は待ち直す。解除フレームより後の新しい左クリックか決定キー入力を要求し、古いMouseUpだけでは送信しない。複数レベルでも毎回再適用する。0.35秒はInspector調整用初期値で、カーソル位置は変更しない。

Logs/CodexLevelUpInputGateVerified.txtの29件PASSとLEVEL UP CONTINUOUS RELEASE NEW GESTURE MULTI OFFER PAUSED CLOCK AND RPC CHECK PASSEDを確認。担当は新旧入力分岐のコンパイル、隔離nographicsで連打・押下持越し・チケット切替・フォーカス復帰・二重送信拒否を検証したと報告。実AddExpから2選択を発生させ、停止中の実時間待ちと選択RPC、選択後の再開も確認。司令塔はコードとログの照合のみ。

内部メソッドと合成GUIイベントを使い、自動Update/OnGUIは停止、フォーカスを検証用に指定した。実マウス連打・中央ボタンの誤選択解消・Tab/決定キー操作・実表示、2人の相棒待ち・別PC/Relay・ビルドは未確認。旧入力分岐はコンパイルのみ。実操作の受入は残る。UI-07はユーザーの実行指示により240bad5へコミット・push済み。

### EQUIP-02 現行状態（実装・隔離検証完了、実環境受入待ち）

本人攻撃力×使用手の武器倍率へ変更し、本人防御・防御強化候補・武器加算攻撃を廃止。HP補正は剣/本/盾に維持し、倍率とHPを個体生成時に抽選する。盾は常時軽減と小ダメージ＋ノックバックのバッシュ。剣/本/盾のドロップ重みは1対1対1、DBは剣0・本1・盾2。詳細数値とAPIはSPEC.mdを参照。

両担当完了報告とソース・実アセット・ログを照合済み。前半はa960e3c、盾モデル・データ・DB/敵表とHUD簡略化は7115908、仕様整理は240bad5へユーザー指示でコミット・push済み。EQUIP-02の編集予約は終了。HUDは剣/本/素手の計算済み攻撃力、盾1枚の率を防御力%と表示し、本人攻撃力・合成軽減率の行を省略する。防御の内部計算を復活させない。

担当のLogs/CodexShieldBuild.logのSHIELD PREFAB BUILD PASSED、Logs/CodexShieldEquipmentVerified3.logのSHIELD FBX MULTIPLIER DEFENSE REMOVAL SEQUENTIAL REDUCTION BASH DROPS CREATOR CHOICES AND NETWORK CHECK PASSEDとSHIELD EQUIPMENT CLEANUP PASSEDを照合。隔離Unity/nographicsのC#/Netcode IL後処理、1536個体、両手20%盾で100→64、0.5→0.4、実RequestAttackからバッシュ・HP/通知・ノックバック、実Pickup往復の個体維持、途中参加・制作者・7強化を同一プロセス2 NetworkManagerで確認したとの報告。移動/位置同期固定、クライアント敵Collider検証用無効化、NPC停止等の制約がある。

HUDの旧28項目成功ログは合成個体の表示状態・ソート・API装備等の検証。後の表示簡略化はコンパイルまでで、旧文言の確認を新表示へ読み替えない。通常敵死亡からの盾ドロップ、実モデルの保持/向き/左右視界/突き出し、実クリック、移動同期込み戦闘・交換、別PC/Relay/ビルドは未確認。司令塔はUnityを再実行していない。

### DAMAGE-01 承認事項と担当

ユーザーが「提案通りでそれぞれに仕事を振ってください」と承認。武器スキル追加より先に、本人が与えたホスト確定の会心込みダメージを敵の頭上付近へ表示する。残HP5へ17の攻撃なら17を表示。少し浮上して薄く消え、撃破しても表示は最後まで残す。壁越しには見せない。通常は白、会心は黄色で少し大きくする。位置・サイズ・時間はInspectorで調整できる仮値。毒等の将来の区別は未決定。

着手前確認で両担当とも待機中。基礎作りはMonster.cs、PlayerAttack.cs、MagicProjectile.cs、PlayerStats.csと新規DamageFeedback.cs（meta含む）を担当。既存GetAttackDamage等の互換APIを維持し、攻撃者・会心情報をホスト側で保持、Monsterで受理した正の有限ダメージを致死消滅前に一度通知する。魔法は発射時の攻撃者・会心・ダメージを保持し、命中前には表示しない。持続ダメージは今回追加しない。

UI担当は新規DamageNumberPresenter.cs・必要な表示スクリプト/プレハブ/Resources設定とmetaを担当。既存フォントを再利用し、HUD・シーン・プレハブ保存値・基礎の担当コードは編集しない。起動と購読はUI側で自動初期化する。表示は敵の寿命から独立し、各端末のカメラと遮蔽で制御する。

共通契約は基礎担当が作るpublic struct DamageFeedbackDataのpublicフィールドAmount(float)、WorldPosition(Vector3)、IsCritical(bool)と、public static class DamageFeedbackのpublic static event System.Action<DamageFeedbackData> DamageConfirmed。WorldPositionはホストが採った敵頭上の基準位置で、UIが高さ等の仮オフセットを適用する。攻撃者の所有プレイヤー等の存続するNetworkObjectからOwner対象RPCを送り、本人端末でイベントを一度発火する。ホスト本人も二重発火させない。UI側からのダメージ再計算やネットワークオブジェクトの生成は行わない。

UIは契約ファイルが揃うまで同名の仮定義を作らず、準備可能な表示部から進める。数値書式・演出時間等の仮値は担当が報告し、全体停止中は既存ゲーム時間に合わせて浮上・消滅も止める構成を初期案とする。致死、壁越し、近接/素手/魔法、会心、連打、ホスト/参加者、自分以外の非表示と一度だけの通知を両担当の境界で確認する。ゲームコードと独立表示の編集範囲を分離し、API変更が必要なら司令塔へ報告する。

UI側はDamageNumberPresenter・DamageNumberView・DamageNumberSettingsと各meta、Resourcesの設定assetとmetaの8ファイルを追加し、完了報告を受領した。HUD・基礎コード・契約・既存素材は変更していない。仮値はSPEC.mdのDAMAGE-01へ集約。検証用の一時Editorメニューは削除されたことを確認。

Logs/CodexDamageNumberUiVerified.logのDAMAGE NUMBER SYNTHETIC UI CHECK PASSEDを司令塔で確認。担当はnographics隔離Playで合成イベントからの17/白・24.5/黄・文字サイズ、1イベント1表示、上昇フェード、対象破棄後保持、壁/壁内カメラ/Trigger、背面画面外/別カメラ、無効値、96上限、disable購読解除、実PlayフレームのtimeScale0固定と復帰消滅の17確認を実施したと報告。C#コンパイルも成功。ピクセルの目視確認ではなく、実命中・Owner通信の統合検証でもない。実画面・操作・別PC/Relay・ビルド等は未確認。

基礎担当も実装・隔離通信検証の完了報告を受領。変更はMonster、PlayerAttack、MagicProjectile、PlayerStats、新規DamageFeedbackとmetaの6件。契約は変更なし。AttackDamageResultとRollAttackDamageの保存結果、旧float API互換、頭上Bounds、Server限定Owner RPCと再送拒否を司令塔で照合した。

Logs/CodexDamageFeedbackVerified6.logのDAMAGE FEEDBACK HOST REMOTE OWNER BARE MELEE MAGIC CRITICAL OVERKILL LETHAL INVALID AND REPLAY CHECK PASSEDとSHARED PROGRESSION CLEANUP PASSEDを確認。担当は同一Unity内ループバックで両者の素手/剣/本の6経路、保存済み会心値、残HP5へ17の致死、敵消滅後の位置、無効値・旧API・再送・非所有呼出・壁/範囲外の非通知を検証。8有効通知が本人へ各1回、相棒0回であることを検証用受信ログで照合したと報告。

基礎検証はプレイヤー移動/位置同期を停止し、NPCを制作者モードで停止、クライアント敵Colliderを検証用に無効化した条件。UI Presenterは入れず通知だけを検証した。基礎とUIを合わせた実命中の画面・本人限定表示、通常移動同期、別プロセス/別PC/Relay/ビルドは引き続き未確認。司令塔でUnityは再実行していない。

追加のUI統合報告を受領。Logs/CodexDamageNumberIntegratedVerified2.logのDAMAGE NUMBER REAL HIT OWNER RPC LETHAL CRITICAL AND LEVEL UP PAUSE INTEGRATION CHECK PASSEDとSHARED PROGRESSION CLEANUP PASSEDを確認。ホストBeginAttack→素手命中→HP減少→Owner RPC→Presenterで通常17/白36pxと残HP5への会心17/黄45px、実敵Despawn後の保持を検証したと報告。実AddExpと両OwnerのChooseLevelUpでレベルアップ停止中0.7秒以上のage/位置/alpha固定、解除後消滅も確認。合成Publishなし、製品8ソースはSHA256一致との報告。

統合試験はnographicsで、移動/位置同期固定・敵停止・クライアント敵Collider無効化。同一プロセスのイベント/Presenter共有のため参加者端末だけの表示分離は証明しない。剣/魔法とのUI統合、実入力・目視・壁際の実命中、通常移動同期、別PC/Relay/ビルドは未確認。今回の製品ファイル追加変更はなし。司令塔はログ照合のみ。

### 共有経験値とレベルアップ選択の着手

基礎作りから、ユーザーが経験値・レベルの2人共有と、レベルアップごとに各自が3択から自己強化する変更を依頼したと共有された。既存の自動成長を残す、2人とも選び終わるまでゲーム全体を止める、攻撃・最大HP・防御・会心率・会心ダメージ・攻撃速度・移動速度・ジャンプ力の8候補からランダム重複なし3つ、強化量はInspector調整可能な仮値とすることも承認済みとの報告。

PROG-01の実装・隔離検証完了報告を受領。独立LevelUpChoiceUIとLevelUpCatalogを含め今回限定で基礎作りが実装。既存HUD・シーン・プレハブは変更していない。新規はLevelUpCatalog、ProgressionState、TeamProgression、PlayerStats.Progression、LevelUpChoiceUIとmeta、Resourcesの標準カタログ。既存10ソースを更新し、前回DEV-01とUIの未コミット変更を保持した。

死亡中選択、切断者待ち除外、途中参加の過去分選択、無効カタログのスキップ、制作者レベルを下げた際の選択済み強化保持は担当の実装判断。カタログ仮値は攻撃2・HP15・防御1・会心率0.05・会心倍率0.25・速度係数0.1・移動0.5・ジャンプ0.25。ユーザーが固定値を指定したものではない。現行仕様とAPIはSPEC.mdへ反映済み。

最終ログLogs/CodexSharedProgressionVerified5.logのSHARED EXPERIENCE INDEPENDENT THREE CHOICES GLOBAL PAUSE STATS CRITICAL NETWORK LATE JOIN AND RESTART CHECK PASSEDとSHARED PROGRESSION CLEANUP PASSEDを確認。担当が同一Unity内ループバックと内部APIで、実オーブからの共有経験値、各自選択、ticket拒否、停止中通信・時計固定と再開、8効果と会心/速度・魔法ダメージ、複数レベル、死亡・切断・途中参加、再戦・制作者レベル・1人時を検証したと報告。

試験はプレイヤー移動・位置同期を固定し、同一物理ワールドのクライアント敵Colliderを検証用に無効化している。WaveManagerは一時コピーの登録プレハブで実行。実キー/クリック・日本語/狭幅/既存HUDの重なり、通常移動同期込みの停止復帰、高速振り、実120秒境界、別プロセス/別PC/Relay/ビルドは未確認。司令塔はコード・カタログ・成功ログの照合のみで再実行していない。個人強化値・会心・攻撃速度のHUD追加表示はUI担当へ検討を引き継ぐ候補。

### 制作者モードの完了状況

ユーザーの検証用モード依頼と追加回答により、レベルの数値指定、選んだ武器の持ち物への直接追加、敵の移動・攻撃・追加出現の停止と再開を制作する。敵停止中もプレイヤーは攻撃できる。基礎作りから着手共有を受領した。

今回限定で新規CreatorMode.cs（管理）とCreatorModeUI.cs（仮OnGUI画面）を基礎作りへ割り当てる。既存のHUD.cs・持ち物比較表・日本語フォント・共通文書の編集は含めない。シーン・プレハブ保存値も変更しない予定。

変更は新規CreatorMode・CreatorModeUIとmeta4件、既存PlayerStats、PlayerEquipment、ItemDatabase、ItemInstance、Monster、MonsterSpawner、GameInput、CameraFollow、PlayerController3D、PlayerAttack、WeaponBladeの11件。HUD・共有文書・シーン・プレハブ・武器アセットは基礎作り担当が変更していない。

ユーザーが追加指示したレア度選択を含め、実装・隔離検証の完了報告を受領。通常抽選・コモン・レア・エピックに対応し、該当設定なしを拒否、旧固定武器は元レア度のみ許可する。共通設定と通常ドロップは維持。APIと挙動はSPEC.mdへ反映した。

最終ログ`Logs/CodexCreatorModeRarityVerified.log`の`CREATOR MODE RARITY HOST AUTHORITY LEVEL WEAPON NETWORK AND ENEMY PAUSE CHECK PASSED`と`ITEM INSTANCE CLEANUP PASSED`を司令塔で確認。旧基本機能ログと区別して扱う。

F2で開くホスト専用画面、Editor/Development Build限定。PROG-01でレベル指定はチーム共有へ変更し、上げた差分の選択を発生させる。下げる際は未選択を消して選択済み個人強化を検証用に保持する。選択中は制作者画面を閉じる。敵停止中も時計とフェーズ進行を継続する設計だが、通常ウェーブ遷移を跨ぐ試験は未実施。これらは担当の実装判断で、ユーザーからの個別指定とは区別する。

| ID | 仕事 | 担当・状態 | 完了とする条件 |
| --- | --- | --- | --- |
| MELEE-02 | 剣・盾バッシュの複数命中 | 基礎作り完了・編集予約解除・隔離129項目成功 | 隔離検証済み。実クリック・目視・通常移動同期・別PC/Relay/ビルドの受入待ち |
| SKILL-01 | 固有・共通スキル | 初期3効果承認・基礎実装/UI準備を依頼済み | 公開API照合後にUI実装。抽選同期・毒/貫通/反射と実操作の確認範囲を区別 |
| UI-07 | レベルアップ時の誤選択防止 | UI担当完了・編集予約解除 | 内部29項目と選択RPC検証済み。実連打・実クリック・2人通信の受入待ち |
| EQUIP-02 | 武器倍率・防御廃止・盾軽減 | 基礎とUIの実装・隔離検証済み。実画面と実環境受入待ち | 盾モデル/保持/バッシュ/左右視界、通常敵死亡のドロップ、実クリックと移動同期込み戦闘・交換、HUD実表示、別PC/Relay/ビルドを確認 |
| DAMAGE-01 | 被弾した敵の付近へダメージ数字 | 個別検証とホスト素手→Owner RPC→表示・致死・実レベルアップ停止の隔離統合済み。実画面/端末別受入待ち | 剣/魔法のUI統合、参加者攻撃の表示分離、見やすさ・壁際・連打、通常移動同期・別PC/Relay/ビルドを確認 |
| PROG-01 | 共有経験値と各自の3択強化 | 実装・隔離API/通信検証済み、実操作受入待ち | 独立3択UIの日本語・実クリック・フォーカス・HUD重なり、移動同期込み停止復帰、実120秒境界、別PC/Relay/ビルドを確認 |
| DEV-01 | 制作者モード | 実装と隔離内部API検証済み・実操作受入待ち | F2・キー/マウス、日本語配置・スクロール・小解像度・カーソルと既存HUDの重なり、Release/Development実ビルド、別PC/Relay、停止を跨ぐ通常ウェーブ時計・遷移を確認 |
| DROP-01 | 武器を落として味方へ渡す | 実装と隔離環境検証済み・実操作受入待ち。HUD編集予約解除 | 実クリック、歩行・ジャンプ・ダッシュ中の取得と位置同期、攻撃中の削除、地形と空中の見た目、別PC・Relayで確認。個体維持・重複拒否等の検証済み範囲は下表参照 |
| UI-01 | 持ち物一覧の個体別性能・レア度表示 | 合成14個体の表示と左手装備・落とすを確認したと担当報告。実戦受入待ち | 物理拾得後の値・整列・選択保持、右手装備・交換・解除、2人通信で一致を確認 |
| UI-02 | 日本語の仮戦闘UI | コード対応済み・Unityインポートと実表示待ち | 固定OTFとTMP生成、日本語字形、各欄・HPバー・照準・通知・勝敗を確認。持ち物操作と拾得中の一覧も確認 |
| UI-03 | 自動装備の新通知とレア度色 | UI担当がコード対応済み・受入待ち | 左右の装備通知が日本語になり、個体の白・青・紫と一致する。一般通知が白へ戻ることも確認 |
| UI-04 | 残り時間の表示消失 | 原因候補へのコード対処済み・実画面確認待ち | 戦闘・回収の両方で右上に非負の分秒が表示される。360×80・省略解除・参照補完の実表示を確認 |
| UI-05 | 持ち物の比較表と日本語表示 | 新比較表を実装。Full HDの合成個体で列揃え・固定欄・一覧スクロールを確認したと担当報告 | 実戦拾得、長い名前、他解像度、最終ホバー色補正の再Playを確認。旧全体スクロール・狭幅縦並びは現行仕様ではない |
| UI-06 | 能力列の見出しで降順ソート | ユーザー承認・コード対応済み。担当がC#コンパイル成功と報告 | 攻撃力・防御力・HPの降順、同値時のレア度と取得順、レア度順への復帰、↓表示、スクロール先頭と選択ID維持を実クリックで確認。実拾得・通信・ビルドも未確認 |
| QA-01 | 実キー・マウスで最新機能を試す | 未割り当て・確認候補 | 剣／本の拾得、Iで装備・交換・解除、左右攻撃、ジャンプ・ダッシュの操作と見た目を確認して結果を記録 |
| QA-02 | 別PC・Relayで最新機能を確認 | 未割り当て・確認候補 | 個体の値、2人の所持品の独立、装備・剣姿勢・球・敵の同期を確認。勝敗・再戦も個別に記録 |
| BUILD-01 | 配布ビルドの起動経路 | 未割り当て・確認候補 | 実際のBuild Profileと起動シーンを確認し、接続から開始まで検証。現在はSampleSceneがgameplayより前に有効登録 |
| DISPLAY-01 | Full HDの制作基準とゲーム設定の対応 | HUDの実行時Canvas設定は対応済み。ゲーム設定の担当・着手調整待ち | 1920×1080・16:9をUIの基準とする。設定変更前に画面モードと反映範囲を確認し、実表示を検証する |
| DESIGN-01 | 武器能力の次段階 | 設計待ち | 能力ごとの適用・抽選・重複・発動条件と着手範囲をユーザーが承認 |

表の確認候補は実装開始の許可ではない。着手時は担当と編集対象をこの表に記録し、仕様変更があれば承認内容も残す。最新の実行結果が来たら、完了した項目を履歴へ移し、この表から外す。

## UIへのデータ引き継ぎ

- DROP-01の`DropWeapon(ulong instanceId)`と`EquipWeapon(ulong instanceId, bool)`は実装済み。HUDのLayoutスナップショットから個体IDを送る。再編集時に行番号による要求へ戻さない。

- `GetInventoryInstance(index)`で持ち物、`GetWeaponInstance(leftHand)`で装備中の個体を取得する。
- `ItemInstance`の`Rarity`、`AttackBonus`、`DefenseBonus`、`MaxHpBonus`が個体の値。補正値は基本値を含む合計なので二重加算しない。
- 名前・モデル・攻撃方式は`instance.Definition`の共通データを使う。色は`ItemData.GetRarityColor(instance.Rarity)`を使える。
- `GetInventoryWeapon`・`GetWeapon`は共通ItemDataを返す互換API。個体の性能やレア度表示には使わない。
- HUDの`GetAttack`による最終攻撃力と、持ち物一覧・左右装備欄の個体レア度・補正値のコード対応が済んだ。実表示と個体APIの照合は受入確認待ち。
- APIの詳細は[技術仕様](SPEC.md)に集約する。

## 検証の現在地

| 対象 | 確認できていること | 残る確認 |
| --- | --- | --- |
| 基本のオンラインプレイ | ユーザーが2人で接続・開始・プレイ | 最新機能の確認を含むとは扱わない。完走・再戦も個別報告待ち |
| 持ち物・剣・本・ダッシュ・ノックバック | 基礎作りがUnity一時コピーのローカルホストで検証。詳細ログは履歴 | 実操作、目視、別PCのRelay、ビルド |
| 剣の新判定と外部姿勢入力 | ローカルホストで形状・回転軌跡・遮蔽・高さ・重複拒否等を検証 | 2人の姿勢表示・遅延、実操作・目視、ビルド、VR実機 |
| 武器の個体差 | 同一Unityプロセス内の別NetworkManager／Transportをループバック接続し、ホスト・クライアント間の生成・取得・装備・途中参加・差分同期・権限を検証 | 別プロセス・別PC・Relay、実操作と一覧表示、ビルド |
| 仮戦闘UI・日本語・個体表示 | 担当がUnity付属Roslynと既存応答ファイルでC#コンパイル成功。司令塔はコード・フォント・ライセンス・生成物の存在と差分を照合 | Unityインポート・Play・TMP生成・実表示・操作・2人通信・ビルドは未確認。C#コンパイルはIL後処理やビルド成功を意味しない |
| 時間欄とレア度色の修正 | UI担当がUnity付属C#コンパイラ・既存Bee応答ファイルでコンパイル終了0を報告。司令塔がHUD差分を照合しdiff --checkを確認 | 時間消失の原因確定・実表示、白青紫の拾得通知・左右武器・持ち物の一致、一般通知の白復帰、実操作・2人通信・ビルドは未確認 |
| 空いている手への自動装備 | 担当が隔離UnityコピーでC#・Netcode IL後処理と同一プロセス内ループバック通信を確認。`Logs/CodexEmptyHandAutoEquipVerified.log`に`AUTO EQUIP AND ITEM INSTANCE NETWORK CHECK PASSED`と`ITEM INSTANCE CLEANUP PASSED`。両手空→右、右装備中→左、左装備中→右、両手装備中保管、装備保持、重複拒否、解除後の取得、参加者の初回自動装備と既存個体通信を検証 | 受取処理は直接呼出。歩いて拾う実操作・目視、日本語通知、参加側の2本目取得を独立した通信ケースとしての確認、別プロセス・別PC・Relay・ビルドは未確認。司令塔は差分とログ照合のみ |
| VR | 共通判定へ外部姿勢を渡す入口 | XR導入、追跡入力、実機検証は未実装 |
| DROP-01 | 隔離UnityでC#・Netcode IL後処理、同一プロセス内ループバック通信を検証したと担当が報告。`Logs/CodexPlayerDropVerified6.log`の`PLAYER DROP TRANSFER NETWORK AND PHYSICAL PICKUP CHECK PASSED`と`ITEM INSTANCE CLEANUP PASSED`を司令塔で確認。実Pickup.Updateの往復、個体維持・差分同期・装備整合・重複/権限拒否・生成不可時保持・HP Clamp・再取得抑止と解除・ウェーブ掃除を検証 | DropWeaponは直接呼出。テスト内で移動制御とプレイヤーNetworkTransformを無効化し座標を固定。実クリック・日本語・横幅・移動/攻撃中・高低差と空中の見た目・別プロセス/別PC/Relay/ビルドは未確認。司令塔で再実行はしていない |
| 持ち物の日本語見切れ対策 | 担当がUnity付属RoslynでC#コンパイル成功と報告。実UnityのFull HD・Gameビュー0.62倍で、持ち物0個の日本語見出し・説明・装備なし・空欄案内・閉じるを目視確認。DROP-01の個体ID要求を保持 | 開始・Iキーはユーザー操作の可能性あり。武器あり・装備/落とす・スクロール・狭い解像度・2人通信・ビルドは未確認。司令塔はコード照合のみ |
| 新持ち物比較表 | 担当が最終C#コンパイル成功と報告。Full HD・Gameビュー0.82倍、ローカルホストへ合成14個体をAPI投入し、レア度降順と同レア取得順、3補正列、固定欄、一覧スクロール、実マウス選択・左手装備・白枠表示・選択保持・落とす14→13と選択解除を確認 | 物理拾得・I入力ではない。最終ホバー補正はコンパイルのみ。実戦拾得、右手装備・交換・解除の実クリック、2人通信・他解像度・ビルドは未確認。司令塔は差分照合のみ |
| 列クリックソート | UI担当がUnity付属Roslynと既存Bee応答ファイルでC#コンパイル終了0、差分チェック成功と報告。司令塔が個体値によるソートと同値順のコードを照合 | 新しい列クリック操作・実表示・拾得・2人通信・ビルドは未確認。前回14個体の目視・クリック確認から範囲を広げない |
| DEV-01 | 担当が隔離Unityでコンパイル・Netcode IL後処理、同一プロセス内ループバック通信と内部APIを検証。最終成功ログを司令塔で照合。ホスト限定権限、レベル上げ下げ/不正値/死亡拒否、剣・本×3レア度×4個体、設定保持と未対応拒否、個体/Lv/HP同期、敵物理と追加出現の停止再開・被弾撃破・破棄時復元を確認 | プレイヤー移動と位置同期はテスト内で固定。Spawner条件は非Spawnのテスト用WaveManager内部値を使用。通常時計と遷移の実行確認ではない。F2実操作・目視・実ビルド・別プロセス/別PC/Relayは未確認。司令塔で再実行していない |

検証は担当報告と保存ログに基づく。司令塔は今回Unityを再実行していない。古い成功ログを最新変更の実操作確認へ読み替えない。

## 承認済みの方向と保留事項

- 最終判断はユーザー。チャットを必要最小限にし、仕様・担当・結果をGitの共有ファイルへ戻す。
- 左クリックは左手、右クリックは右手。左右に別々の片手武器を装備する。
- 拾った武器を保管し、ユーザーが左右への装備を選ぶ。空いている手への自動装備を行い、両手空なら右手優先。同種・同レア度でも個体差で選べるようにする。
- 攻撃能力はその武器だけへ適用。装備者の能力は左右合算。現在つながっている効果は攻撃・防御・最大HP。
- 武器動作と戦闘ルールを分け、武器の形・軌跡を使い、ホストがダメージを確定する方向。
- 将来はPC接続型VR。機種、PC版との混在、VRの移動・攻撃開始・装備UI、必要性能は未決定。
- PC版の制作・UIの基準はFull HD（1920×1080・16:9）。ユーザーの「fullHDサイズ」という指定をこの標準寸法として記録する。ウィンドウ／フルスクリーン、他解像度への対応範囲は未決定。現在のProjectSettings保存値は1024×768、ウィンドウサイズ変更は無効で、設定への反映は未着手。
- ユーザーが装飾なしの仮戦闘UIの配置案と実装、その後の日本語化を承認。持ち物の個体API対応は当初の表示目的に沿ってUI担当が合わせて実装し、日本語化とは別の再承認は取っていないと報告している。
- ユーザーが時間表示の消失を報告し、武器のレア度ごとの文字色を白・青・紫の順で指定。UI担当が時間欄の対処と色分けを実装したが、実表示は確認待ち。
- 防具を今後制作するかは保留。既存の防具コードは残る。
- 盾、両手武器、持ち物上限、売却・廃棄、永続セーブ、残りの能力の具体仕様と優先順位は未決定。

## 引き継ぎ時の作業状態

ブランチはmain。武器個体差の第1段階は`382e965`にコミットした。作業前にgit statusと最新の履歴を取り直す。

2026-11-17のユーザーのコミット・push指示により、リモートの追加2コミットを615df77まで早送りで取り込んだ。追加対象はgameplay.unityとui.psdで、DROP-01とUIの未コミット変更とはファイルが重ならなかった。統合後のUnity実行確認は未実施。

同コミットはDropManager、ItemData、ItemDatabase、ItemPickup、PlayerEquipment、PlayerStats、CrystalSwordData、BookData、新規ItemInstanceとmetaを含む。

2026-11-17のユーザー指示により、コミットとGitHubへのpushはユーザーが指定したタイミングでのみ実行する。以前の包括的な委任による自動実行は終了。担当の完了報告後も差分照合と共有文書更新まで進め、未コミットで実行指示を待つ。コミットだけの指示をpushの許可として扱わない。通常の履歴と検証結果は、指示を受けてコミットする際にメッセージへ集約し、CHANGELOG.mdは既存記録の保管先とする。

## 文書の更新方法

新しい報告を受けたら、ユーザー承認・実装・検証を照合する。現行挙動はSPEC.md、人間向けの説明はGAME_SPEC.md、担当と残課題はこの文書へ反映し、完了作業と検証結果はコミットへ記録する。過去の着手報告を現在の進行中タスクとして残し続けない。

## 選択された能力候補

次の34項目は候補として選ばれたもの。全項目への着手や詳細仕様の承認を意味しない。現在の実装済み効果は攻撃・防御・最大HP。

| 候補番号 | 能力 |
| --- | --- |
| 1・2・3・4 | 攻撃力、攻撃速度、会心率、会心ダメージ |
| 9・10・11・12・13 | 最大HP、防御力、HP自然回復、撃破時回復、吸血 |
| 15・16 | 最大HP連動攻撃、ガード性能 |
| 17・18・19・20・21 | 移動速度、ジャンプ力、ダッシュ距離、ダッシュ再使用短縮、ノックバック強化 |
| 23・24 | 武器の長さ、複数体攻撃 |
| 25・26・28・29・30・31・32 | 弾速、射程、貫通数、発射数、爆発範囲、連鎖数、跳弾数 |
| 33・34・35・36・37・38 | 炎上、毒、鈍足、凍結/気絶、弱体化、被ダメージ増加 |
| 39・40・41 | 経験値獲得量、回収範囲、レア装備発見率 |

### SKILL-01 データ契約の中間確認（2026-11-20）

ItemInstance.Skills（WeaponSkillLoadout: Innate/CommonFirst/CommonSecond、CommonCount/GetCommon/Count）、WeaponSkillId None0/Poison1/Pierce2/Reflect3、WeaponSkillSettings.Currentとstatic GetDisplayName/GetDescription、PlayerEquipment.GetWeaponSkills/EquippedReflectCountを実ファイルで確認。担当のLogs/CodexWeaponSkillDataVerified2.log:449のWEAPON SKILL DATA RARITY CANDIDATES DUPLICATES WIRE EQUALITY CREATOR AND LEGACY CHECK PASSED samples=5400、:510のCLEANUP PASSEDを照合。コンパイル/Netcodeと抽選・wire等値・旧None等の試験報告であり、効果実装や実通信の全体成功ではない。途中参加/実Pickup往復の回帰は担当進行中。

UIへ公開契約での実装依頼を送信済み。合計個数と基礎GetDescriptionの1個あたり説明を使い、合算式をUIへ重複実装しない。毒の人別管理/弱い再命中での最新値置換/tick維持/本人死亡・切断/制作者停止/毒と反射の通常白・追加ノックバックなしはユーザーへ提案して回答待ち。未承認の細部を効果・表示に確定として入れない。

### SKILL-01 第1段階の検証報告（効果は未実装）

基礎担当は12製品ファイル（ItemData/ItemInstance/PlayerEquipment、WeaponSkillLoadout/Settings各csとmeta、Resources設定assetとmeta、武器3種asset）を変更。全12ファイルの隔離コピーSHA256一致は担当確認。HUDはUI担当の作業で基礎側未編集。基礎編集予約は効果実装待ちとして継続。

Logs/CodexSkillTransportVerified2.log:648のSKILL INDIVIDUAL DATA LATE JOIN PHYSICAL TRANSFER CREATOR AND BASELINE REGRESSION CHECK PASSED、:660のSKILL TRANSPORT CLEANUP PASSEDを司令塔で確認。担当はUnity6000.0.46f1隔離batchmode/nographics終了0、同一プロセス2 NetworkManager/127.0.0.1:18219でSkills込みEqualsによる途中参加時の所持/装備一致、実DropWeapon/実Pickupのホスト→参加者→ホスト往復、制作者Epic・既存盾バッシュ/軽減/HP/通知/共有成長を検証したと報告。先の5400個体試験と併せて個体データ段階の成功で、毒/貫通/反射の発動成功ではない。

制約は合成個体/API/Reflection、プレイヤー移動とNetworkTransform固定、停止NPC、参加者NPCのCollider/Monster無効化、共有static通知イベント。隔離日本語フォント不足のError Pauseを試験内解除、UI検証なし。実入力・目視・UI新表示・通常移動同期・別プロセス/別PC/Relay/ビルド/XRは未確認。毒/貫通/反射効果、発射/振り開始時のスキル保存、効果の非再帰と停止復帰は未実装・未検証。旧wire混在と永続移行は対応せず、同版新試合の生成が基準。ユーザーへ提示済みの未指定効果案の回答を待つ。コミット・pushなし。
### SKILL-01 UI表示段階の完了（効果本体は未実装）

UI担当がHUD.csと新規WeaponSkillDisplay.cs/metaの3ファイルを変更して完了報告。今回のHUD/表示ヘルパー編集予約を解除する。基礎ファイル・武器asset・共有文書等は未編集。ItemInstance.Skillsを使い固有・共通と使用枠数・合計個数を表示、名前と1個あたり説明は基礎APIから取得し、UIで効果量を再計算しない。Noneは未設定、不正IDは未対応IDとして区別。反射説明の盾枚数とスキル個数の誤読を避ける補足を表示側だけで行う。

詳細には固定文「スキル効果は未実装です。以下は予定の効果です。」がある。実装状態APIによる自動判定ではないため、効果実装の完了確認後に司令塔からUI担当へ削除/更新を依頼する必要がある。未承認の毒の細部は説明へ追加しない。

窓高さ上限960、既存比較列/行高64/個体ID操作を維持し下部に独立詳細スクロールを追加。選択ID変更/消失/所持者変更で詳細先頭へ、同個体のソートでは維持する。Logs/CodexSkillHudVerified.txtのSKILL HUD INNATE COMMON TOTAL DESCRIPTION SORT SCROLL EQUIP DROP CHECK PASSEDを司令塔で照合。担当はUnity6000.0.46f1付属Roslynコンパイルと隔離nographicsで31項目、合成6個体の文字列・状態・4窓高幾何・実Host装備/ドロップAPI・個体スキル保持を確認したと報告。2ソースの試験コピーSHA256一致は担当確認。

実ピクセル/長文欠け/小画面の読みやすさ・ホイール・GUI.Buttonへの実クリック・物理拾得・2人通信・別PC/Relay/ビルドは未確認。毒/貫通/反射の実効果試験ではない。司令塔は差分・ログ照合のみ。未コミット保持、基礎の効果予約とユーザーへの毒等細部の確認待ちは継続。
### SKILL-01 効果本体の続行承認（2026-11-20）

ユーザーの「実装しよう」で、司令塔が提示した毒の人別・最新値置換・tick維持・死亡/切断/停止条件・通常白/追加ノックバックなし・非再帰の案を承認し、効果本体へ続行する。正式内容はSPEC0.34を参照。過去節の回答待ちはこの承認で解消。基礎の既存予約内で毒/貫通/反射を実装・検証し、他担当のHUD/WeaponSkillDisplay/LevelUpChoiceUI等の差分は触らない。現在LevelUpChoiceUIにも未コミット差分があり今回基礎の対象外。

基礎の実装・検証完了後に司令塔が照合し、UI担当へ固定の「スキル効果は未実装」注意文の更新を依頼する。完了前には消さない。実クリック・目視・通常移動同期・別PC/Relay等の未確認を引き続き区別。コミット・pushは新しい実行指示まで行わない。基礎への続行指示は送信成功。実装・検証結果の報告を待つ。
### SKILL-01 基礎完了報告の照合（2026-11-20）

基礎の20製品ファイルは、第1段階12件にPlayerAttack/MagicProjectile/Monster/PlayerStats、DamageContext.cs/meta、MonsterPoisonState.cs/metaを加えたもの。ItemDatabase/Resolver/UI/文書/シーン等は基礎側無変更。差分とログを司令塔で照合し、基礎の今回の予約を解除する。全20件の隔離コピーSHA256一致は担当確認。コミットpushなし。

Logs/CodexWeaponSkillEffectsFinalVerified2.log:1971のWEAPON SKILL POISON PIERCE REFLECTION SNAPSHOT OWNER NETWORK AND REAL TEAM PAUSE CHECK PASSED checks=93、:1983 CLEANUPを確認。毒の更新/時刻/人別/死亡/切断/終了、非再帰/白/ノックバックなし、スキル保存、実飛翔貫通の上限/壁/順序/重複/致死、両盾/致死/AIの攻撃元からの反射、毒致死の実ドロップ、参加者Owner通知、実共有停止1.2秒と再開/制作者停止中の毒進行を試験したとの報告。

Logs/CodexSkillMeleeRegressionVerified3.log:2543のSKILL LEGACY MAGIC AND MELEE MULTI FULL DAMAGE DEDUP WALL VALIDATION HOST REMOTE REGRESSION PASSED checks=129、:2555 CLEANUPと、Logs/CodexSkillEffectsTransportVerified.log:615のSKILL INDIVIDUAL DATA LATE JOIN PHYSICAL TRANSFER CREATOR AND BASELINE REGRESSION CHECK PASSED、:627 CLEANUPを確認。後者は1536抽選・途中参加/実Pickup往復・制作者/既存成長/軽減等。最後の0ダメージ弾停止修正は回帰2件の後で、効果93項目を最終再実行して成功。

試験はUnity6000.0.46f1隔離batchmode/nographics。合成個体/姿勢/API/Reflection、毒境界はAdvanceへ合成時刻、移動/NetworkTransform固定、NPC停止・Collider検証形状置換、参加者複製NPC無効化、同プロセスWaveManager指定・共有通知イベント。AI1ケースで非サーバープレイヤー一覧を試験用に除外復元。隔離フォント不足はError Pause解除で進めUI成功には含めない。実画面/入力/移動同期込み戦闘・端末別数字・別PC/Relay/ビルド/XRは未確認。司令塔は再実行せず照合のみ。

UI仕上げ：WeaponSkillDisplayの固定「効果未実装/予定」注意文の削除を依頼し、完了報告を照合した。今回の表示ヘルパー編集予約を解除する。基礎の効果未実装という過去節は当時の中間状態であり、現在の状態は本節とSPEC0.35を優先する。LevelUpChoiceUI差分は別作業で、今回の基礎完了へ混ぜて検証済みと扱わない。

### SKILL-01 UI仕上げの確認

WeaponSkillDisplay.csの旧未実装注意文1行のみを削除。固有/共通/合計・1個あたり説明・None/不明ID等は維持。Logs/CodexSkillDisplayFinalVerified.txtのSKILL DISPLAY CURRENT DESCRIPTION NONE UNKNOWN DUPLICATE CHECK PASSED checks=30を司令塔で照合。担当はUnity6000.0.46f1付属Roslynコンパイル、隔離batchmode/nographicsのEditMode文字列30項目、helperの試験コピーSHA256一致を確認したと報告。実画面/入力/スクロール・通信・戦闘の試験ではなく、LevelUpChoiceUI別差分も含めない。SKILL-01の基礎とUIの今回編集予約は終了。未コミットでユーザー指示待ち、実プレイ受入が次の確認事項。


### 今回のGit保存範囲

ユーザーのコミット・push指示によりSKILL-01基礎20ファイル・UI3ファイル・司令塔文書を保存対象とする。別作業のLevelUpChoiceUI.csはUI担当の完了報告と差分を照合し、選択肢を中央より下へ移す変更として別コミットにする。高さ上限440・中央下24pxから配置し0.35秒ゲートを維持。コンパイル/差分確認までで、実画面・連打操作感は未確認。今回のスキル効果の検証済み範囲へ混ぜない。
