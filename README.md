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

FaceTuneのセットアップ方法として、2つの例を紹介します。

### 例1: テンプレートを用いたセットアップ
FaceTune Assistantとサンプルのパターンを用いて表情制御を導入します。

1.  `Base Template` の追加:
    - Hierarchyからアバターを右クリックし、メニューから `FaceTune` > `Base Template` を選択します。
    - アバターのルートなどに `Base Template` GameObjectが追加されます。このルートに `FaceTune Assistant` コンポーネントがあります。
2.  サンプルのパターンの追加:
    - `FaceTune Assistant`のInspectorに表示される「サンプルパターンを追加」セクションで、作りたい表情制御の種類を選びます。
        - **ハンドジェスチャー** 片手制御、基本的な両手制御、両手で表情をブレンドする制御などがあります。
        - **その他**: メニューを用いた制御などがあります。
    - 「追加」ボタンを押すと、選択した制御が子オブジェクトとして生成されます。
        - これには `Condition` コンポーネントや`Expression`コンポーネントなどが含まれています。

### 例2: 最小構成から組み立てる
FaceTuneは各機能が独立しているため、必要なものだけを選んで組み合わせたシンプルな構成から始めることも可能です。

*   **ジェスチャー1つで特定の表情を出す**:
    `Condition` コンポーネントと `Expression` コンポーネントの2つがあれば実現できます。

*   **Expressionメニューから特定の表情をON/OFFする**:
    `Condition`　コンポーネントの代わりとして`MA MenuItem` コンポーネントが利用できます。`Expression` コンポーネントと`MA Menu Installer` コンポーネントを追加した3つのコンポーネントで表情制御のメニューが生成できます。

`Condition`など、FaceTuneにおけるコンポーネントは基本的にHierarhyで下にあるほど優先されます。

### 表情の設定
上記いずれの方法でも、最終的には `Condition` (または `MA MenuItem`) に紐づく `Expression` を使って実際の表情を設定します。
*   `Expression` コンポーネントを選択し、Inspectorから設定します。
    *   **Manualモード**: ブレンドシェイプ名とそのウェイト値を直接手動で指定して表情を作ります。`Open Editor`からEditorが起動できます。
    *   **From Clipモード**: 既存のAnimationClipを指定し、そのクリップ内のブレンドシェイプを表情として利用します。

### ビルドとアップロード
設定に基づいて必要なAnimator Controllerやアニメーションクリップなどがビルド時に生成され、アバターに非破壊的に結合されます。

### 既存の表情制御との共存について
FaceTuneは、デフォルトでアバターに既に設定されている表情制御と共存するように動作します。

*   例えば、上記「例2」のように最小構成で特定のジェスチャー表情だけを追加した場合、そのジェスチャーが実行されている間だけFaceTuneの表情が適用され、それ以外の時は元々のアバターの表情制御が機能します。これにより、既存のセットアップを壊さずに新しい表情を追加・上書きできます。
*   もし、FaceTuneでアバターの表情全体を管理し、既存の表情制御を無効化したい場合は、`DisableExistingControl` というコンポーネントをアバターの任意のGameObjectにアタッチしてください。
    *   `Template Base` を使用する場合、このコンポーネントは最初から含まれています。
    *   このコンポーネントは、主に表情に関連するブレンドシェイプのアニメーションを無効化しようと試みます。

## 各コンポーネントの説明

各コンポーネントの説明です。以下に説明のないコンポーネントは現在動作していません。

## Expression

### Expression

### Facial Data

### Animation Data

### Facial Style

### Advanced Eyeblink 

### Advanced LipSync

## Condition

### Condition
条件を設定します。ハンドジェスチャーもしくはパラメーターを用いた条件が設定でき、複数の条件はAND演算となります。アタッチされたGameObject以下のExpressionがこのConditionの影響を受けます。。
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
