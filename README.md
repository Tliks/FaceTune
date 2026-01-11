FaceTune
====
A modular tool for avatar emotion expression.

Dependencies
- [NDMF](https://github.com/bdunderscore/ndmf) >= 1.8.0
- [Modular Avatar](https://github.com/bdunderscore/modular-avatar) >= 1.13.0

## FaceTuneとは
FaceTuneはモジュール的に設計された、アバター向けの表情編集ツールです。

柔軟な制御設計、軽量な編集エディタ、高度なプレビューシステムなどを特徴としています。

## 導入方法
[Add to VCC or ALCOM](https://tliks.github.io/facetune-release/)

## クイックスタート
1.  テンプレートの追加:
    - Hierarchyからアバターを右クリックし、メニューから `FaceTune` > `Template` を選択します。
    - `FaceTune Template` GameObjectが追加されます。このルートに `FaceTune Assistant` コンポーネントがあります。
2.  表情制御の追加:
    - `FaceTune Assistant`のInspectorに表示される「表情制御を追加」セクションで、作りたい表情制御の種類を選びます。
        - ハンドジェスチャー: 片手制御、基本的な両手制御、両手で表情をブレンドする表情制御などが作成できます。
        - メニュー: メニューによりオンオフできる表情制御などが作成できます。
        - アニメーターコントローラー: FXレイヤーなどを解析して、近い動作の表情制御が作成できます。
    - 「追加」ボタンを押すと、選択した制御が子オブジェクトとして生成されます。
        - これには `Condition` コンポーネントや`Expression`コンポーネントなどが含まれています。
3. 表情の設定
    - `Expression`コンポーネントとそれに紐づく`Expression Data`コンポーネントから表情用のブレンドシェイプを設定します。
    - Animation Clipの割り当て、手動での設定、またはその併用により表情が編集できます。
    - 表情以外のアニメーションも割り当てできます。

### ビルドとアップロード
設定に基づいて必要なAnimator ControllerやAnimation Clipなどがビルド時に生成され、アバターに非破壊的に適用されます。

### 既存の表情制御との共存について
FaceTuneは、デフォルトでアバターに既に設定されている表情制御に対し特別な操作を行いません。
生成されるアニメーションは既存の制御より高い優先度で実行されます。
そのため、既存の表情制御を無効化したい場合は手動での無効化、もしくは常に再生されるExpressionコンポーネントを配置することで表情用のアニメーションが無効化できます。
なお、テンプレートにはデフォルトで無効化用のExpressionコンポーネントが含まれています。

## 各コンポーネントの説明

各コンポーネントの説明です。以下に説明のないコンポーネントは現在動作していません。

## Expression

### Expression
このコンポーネントが存在することにより実際にアバターに対する適用が行われます。
基本的にはこのコンポーネントは単体ではなく、`Expression Data`コンポーネントや、`Condition`コンポーネントを併用します。Hierarchyにおいて下にあるほど高い優先度として動作します。

一切の`Condition`と紐づかない場合、常に再生されるExpressionとなります。

`他の表情とブレンドする`はOFF(デフォルト)のとき、設定されていない表情ブレンドシェイプを全て0として扱うことで、より優先度の低い表情アニメーションを無効化します。一方でtrueのとき、設定されたデータの再生のみを行うことで、より低い優先度のExpressionとの結合を可能にします。

### Expression Data
表情のブレンドシェイプのアニメーションや、表情以外のアニメーションを設定するコンポーネントです。アタッチされたGameObject以上の`Expression`コンポーネントと紐づきます。
Animation Clipの割り当て、手動での設定、またはその併用などにより編集できます。
同一の`Expression`コンポーネントに対し複数の`Expression Data`コンポーネントを紐づけることができます。同じプロパティが設定されていた場合、Hierarchyにおいて下にあるコンポーネントの値が使用されます。

### Facial Style
顔つきのように、複数のExpressionで共通して適用されてほしい表情用のブレンドシェイプを設定するコンポーネントです。アタッチされたGameObject以下の`Expression`コンポーネントに対し適用されます。このコンポーネントは各`Expression`コンポーネントに対する適用のみを行うため、この顔つきが適用された表情をデフォルトとして使用する場合、`デフォルトとして設定`ボタンから追加の`Condition`コンポーネントと紐づかない`Expression`コンポーネントを配置してください。このコンポーネントで設定された値は適用先の各Expressionで上書きできます。`他の表情とブレンドする`がONのExpressionに対しては動作しません。

### Advanced Eyeblink / Advanced LipSync
高度なまばたき/リップシンクの設定を適用します。アタッチされたGameObject以下の`Expression`コンポーネントに対し適用されます。複数のコンポーネントが設定された場合、最も親子関係が近いコンポーネントが使用されます。

## Condition

### Condition
条件を設定します。アタッチされたGameObject以下の`Expression`コンポーネントと紐づきます。ハンドジェスチャーもしくはパラメーターを用いた条件が設定でき、複数の条件はAND演算となります。
同じGameObjectに複数のConditionをアタッチした場合はそれらのOR演算となり、ConditionをアタッチしたGameObjectを入れ子にした場合はそれらのAND演算となります。

このコンポーネントに設定するパラメータをModular Avatar Parametersで定義している場合、自動リネーム機能がオンの場合には対応していません。自動リネームをオフにし、リネームを用いる場合はリネーム後の名前をこのコンポーネントに設定するようにしてください。

### MenuItem (Modular Avatar)
FaceTuneのコンポーネントではありませんが、Toggle/Buttonの場合、boolの条件として`Condition`コンポーネント同様に動作します。パラメーターは設定されていない場合、自動で生成されます。メニューとして使う場合はMenu Installer (Modular Avatar) を同時に使用してください。またRadialの場合、Motion Timeとして動作します。アタッチされたGameObject以下の`Expression`コンポーネントに対しMotion Timeを設定します。

### Pattern
アタッチされたGameObject以下の複数の`Condition`とそれに紐づく`Expression`を排他制御としてマークします。

### Preset
アタッチされたGameObject以下の制御をプリセットとしてマークします。このプリセットをオンオフするメニューは同じ階層に自動生成されます。このPresetコンポーネントを複数配置することで、複数の制御をメニューから切り替えできるようになります。

## その他

### Allow Tracked BlendShapes
まばたきやリップシンクに使用され、通常表情に用いることが許可されていないブレンドシェイプを用いることが出来るようにします。このコンポーネントが設定されておらず、許可されないブレンドシェイプが使用されていた場合、警告の上でそのブレンドシェイプは除外されます。動作原理はビルド時におけるブレンドシェイプの複製です。

### Override Face Renderer
適用対象のSkinnedMeshRendererを明示的に指定します。このコンポーネントが設定されていない場合、自動的に選定されます。

### FaceTune Assistant
Editor上でのみ機能するコンポーネントです。現在アバターに対し設定されたFaceTuneの設定を簡単に解析し、設定に関する簡単な情報の提供をします。またGameObjectやコンポーネントの生成を行う機能などを提供します。
