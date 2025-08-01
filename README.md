FaceTune
====
A modular tool for avatar emotion expression.

Dependencies
- [NDMF](https://github.com/bdunderscore/ndmf) >= 1.8.0
- [Modular Avatar](https://github.com/bdunderscore/modular-avatar) >= 1.12.0

## FaceTuneとは
FaceTuneはモジュール的に設計された、アバター向けの表情編集ツールです。

柔軟な制御設計、軽量な編集エディタ、高度なプレビューシステムなどを特徴としています。

## 導入方法

### Git
プロジェクトの `Assets` または `Packages` フォルダにリポジトリをクローンします。
```
git clone https://github.com/Tliks/FaceTune
```

### VCC/ALCOM対応
現時点では対応していません。

## クイックスタート
1.  テンプレートの追加:
    - Hierarchyからアバターを右クリックし、メニューから `FaceTune` > `Base Template` を選択します。
    - アバターのルートなどに `Base Template` GameObjectが追加されます。このルートに `FaceTune Assistant` コンポーネントがあります。
2.  サンプルのパターンの追加:
    - `FaceTune Assistant`のInspectorに表示される「サンプルパターンを追加」セクションで、作りたい表情制御の種類を選びます。
        - **ハンドジェスチャー** 片手制御、基本的な両手制御、両手で表情をブレンドする制御などがあります。
        - **その他**: メニューを用いた制御などがあります。
    - 「追加」ボタンを押すと、選択した制御が子オブジェクトとして生成されます。
        - これには `Condition` コンポーネントや`Expression`コンポーネントなどが含まれています。
3. 表情の設定
    - `Expression`コンポーネントがアタッチされたGameObject以下の存在する`Facial Data`コンポーネントから表情用のブレンドシェイプを設定します。
    以下の2つのモードがあります。
    *   **Manualモード**: ブレンドシェイプ名とそのウェイト値を直接手動で指定して表情を作ります。`Open Editor`からEditorが起動できます。
    *   **From Clipモード**: 既存のAnimationClipを指定し、そのクリップ内のブレンドシェイプを表情として利用します。
    また、表情用のブレンドシェイプ以外のアニメーションを可能にする`Animation Data`コンポーネントを追加することもできます。


### ビルドとアップロード
設定に基づいて必要なAnimator ControllerやAnimation Clipなどがビルド時に生成され、アバターに非破壊的に適応されます。

### 既存の表情制御との共存について
FaceTuneは、デフォルトでアバターに既に設定されている表情制御に対し特別な操作を行いません。
生成されるアニメーションは既存の制御より高い優先度で実行されます。
そのため、既存の表情制御を無効化したい場合は手動での無効化、もしくは常に再生されるExpressionコンポーネントを配置することで表情用のアニメーションが無効化できます。

## 各コンポーネントの説明

各コンポーネントの説明です。以下に説明のないコンポーネントは現在動作していません。

## Expression

### Expression
最も重要なコンポーネントであり、このコンポーネントが存在することにより実際にアバターに対する適応が行われます。
基本的にはこのコンポーネントは単体ではなく、`Facial Data`コンポーネントや、`Condition`コンポーネントを併用します。Hierarchy上で下にあるほど高い優先度として動作します。

`Enable Blending`はOFF(デフォルト)のとき、設定されていない表情ブレンドシェイプを全て0として扱うことで、より優先度の低い表情アニメーションを無効化します。一方でtrueのとき、設定されたデータの再生のみを行うことで、より低い優先度のExpressionとの結合を可能にします。

一切の`Condition`と紐づかない場合、常に再生されるExpressionとなり、一切の`Facial Data`等と紐づかない場合、`Enable Blending`がOFFのとき全表情ブレンドシェイプを0とするExpressionとなり、ONのとき空のExpressionとなります。

### Facial Data
表情用のブレンドシェイプを設定するコンポーネントです。アタッチされたGameObject以上の`Expression`コンポーネントと紐づきます。同一の`Expression`コンポーネントに対し複数の`Facial Data`コンポーネントが紐づき、かつ同じブレンドシェイプが設定されていた場合、Hierarchy上で下にあるコンポーネントの値が使用されます。

### Animation Data
汎用的なアニメーションを設定するコンポーネントです。アタッチされたGameObject以上の`Expression`コンポーネントと紐づきます。同一の`Expression`コンポーネントに対し複数の`Animation Data`コンポーネントが紐づき、かつ同じプロパティが設定されていた場合、Hierarchy上で下にあるコンポーネントの値が使用されます。

### Facial Style
顔つきのように、複数のExpressionで共通して適用されてほしい表情用のブレンドシェイプを設定するコンポーネントです。アタッチされたGameObject以上の`Expression`コンポーネントに対し適用されます。このコンポーネントは各`Expression`コンポーネントに対する適用のみを行うため、この顔つきが適用された表情をデフォルトとして使用する場合、`As Default`ボタンから追加の`Condition`コンポーネントと紐づかない`Expression`コンポーネントを配置してください。このコンポーネントで設定された値は適用先の各Expressionで上書きできます。`Enable Blending`がONのExpressionに対しては動作しません。

### Advanced Eyeblink 
高度なまばたきの設定を適用します。アタッチされたGameObject以下の`Expression`コンポーネントに対し適用されます。複数のコンポーネントが設定された場合、最も親子関係が近いコンポーネントが使用されます。

### Advanced LipSync
高度なリップシンクの設定を適用します。アタッチされたGameObject以下の`Expression`コンポーネントに対し適用されます。複数のコンポーネントが設定された場合、最も親子関係が近いコンポーネントが使用されます。

## Condition

### Condition
条件を設定します。アタッチされたGameObject以下の`Expression`コンポーネントと紐づきます。ハンドジェスチャーもしくはパラメーターを用いた条件が設定でき、複数の条件はAND演算となります。
同じGameObjectに複数のConditionをアタッチした場合はそれらのOR演算となり、ConditionをアタッチしたGameObjectを入れ子にした場合はそれらのAND演算となります。

### MenuItem (Modular Avatar)
FaceTuneのコンポーネントではありませんが、同様に条件定義として動作します。ビルド時にパラメータを生成し、Conditionと同様に振る舞います。メニューとして使う場合はMenu Installer (Modular Avatar) を同時に使用してください。

### Pattern
アタッチされたGameObject以下の複数の`Condition`とそれに紐づく`Expression`を排他制御としてマークします。

### Preset
アタッチされたGameObject以下の制御をプリセットとしてマークします。このプリセットをオンオフするメニューは同じ階層に自動生成されます。このPresetを複数配置することで、複数の制御をメニューから切り替えできるようになります。

## その他

### Allow Tracked BlendShapes
まばたきやリップシンクに使用され、通常表情に用いることが許可されていないブレンドシェイプを用いることが出来るようにします。このコンポーネントが設定されておらず、許可されないブレンドシェイプが使用されていた場合、警告の上でそのブレンドシェイプは除外されます。動作原理はビルド時におけるブレンドシェイプの複製です。

### Override Face Renderer
適用対象のSkinnedMeshRendererを明示的に指定します。このコンポーネントが設定されていない場合、自動的に選定されます。

### FaceTune Assistant
Editor上でのみ機能するコンポーネントです。現在アバターに対し設定されたFaceTuneの設定を簡単に解析し、設定に関する簡単な情報の提供をします。またGameObjectやコンポーネントの生成を行う機能などを提供します。
