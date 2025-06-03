FaceTune
====
A modular tool for avatar emotion expression.

Dependencies
- [NDMF](https://github.com/bdunderscore/ndmf) >= 1.7.0
- [Modular Avatar](https://github.com/bdunderscore/modular-avatar) >= 1.12.0

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
FaceTune Assistantとサンプルパターンを使って、表情制御を導入できます。

1.  **`Template Base` の追加**:
    *   Hierarchyウィンドウで右クリックし、メニューから `FaceTune` > `Template Base` を選択します。
    *   アバターのルートなどに `Template Base` GameObjectが追加されます。このルートに `FaceTune Assistant` コンポーネントがあります。
2.  **サンプルパターンの選択と追加**:
    *   `FaceTune Assistant`のInspectorに表示される「サンプルパターンを追加」セクションで、作りたい表情制御の種類を選びます。
        *   **ハンドジェスチャー**: 左手のみ・右手のみの個別制御、両手を使った基本的な制御、両手のジェスチャーをブレンドする制御、先に行ったジェスチャーを優先する制御、左右の手の組み合わせでなどのサンプルがあります。
        *   **その他**: 特定の表情を一つだけONにする排他メニュー、他の表情と混ぜて使えるブレンドメニュー、コンタクトを用いたサンプルなどがあります。
    *   「追加」ボタンを押すと、選択したサンプルパターンが子オブジェクトとして生成されます。これには `Condition` コンポーネントや`Expression`コンポーネントなどが含まれています。

### 例2: 最小構成から組み立てる
FaceTuneは各機能が独立しているため、必要なものだけを選んで組み合わせたシンプルな構成から始めることも可能です。

*   **ジェスチャー1つで特定の表情を出す**:
    `FT Condition` コンポーネントと `FT Expression` コンポーネントの2つがあれば実現できます。

*   **Expressionメニューから特定の表情をON/OFFする**:
    `FT Condition`の代わりとして`MA MenuItem` コンポーネントが利用できます。`FT Expression` と`MA Menu Installer` を追加した3つのコンポーネントで表情制御のメニューが生成できます。

`FT Condition`など、FaceTuneにおけるコンポーネントは基本的にHierarhyで下にあるほど優先されます。

### 表情の設定
上記いずれの方法でも、最終的には `FT Condition` (または `MA MenuItem`) に紐づく `FT Expression` コンポーネントを使って実際の表情を設定します。
*   `FT Expression` コンポーネントを選択し、Inspectorから設定します。
    *   **Manualモード**: ブレンドシェイプ名とそのウェイト値を直接手動で指定して表情を作ります。`Open Editor`からEditorが起動できます。
    *   **From Clipモード**: 既存のAnimationClipを指定し、そのクリップ内のブレンドシェイプを表情として利用します。

### ビルドとアップロード
設定に基づいて必要なAnimator Controllerやアニメーションクリップなどがビルド時に生成され、アバターに非破壊的に結合されます。

### 既存の表情制御との共存について
FaceTuneは、デフォルトでアバターに既に設定されている表情制御と共存するように動作します。

*   例えば、上記「例2」のように最小構成で特定のジェスチャー表情だけを追加した場合、そのジェスチャーが実行されている間だけFaceTuneの表情が適用され、それ以外の時は元々のアバターの表情制御が機能します。これにより、既存のセットアップを壊さずに新しい表情を追加・上書きできます。
*   もし、FaceTuneでアバターの表情全体を管理し、既存の表情制御を無効化したい場合は、`DisableExistingControlComponent` というコンポーネントをアバターの任意のGameObjectにアタッチしてください。
    *   `Template Base` を使用する場合、このコンポーネントは最初から含まれています。
    *   このコンポーネントは、主に表情に関連するブレンドシェイプのアニメーションを無効化しようと試みます。他の種類のアニメーション（オブジェクトのON/OFFなど）には影響しません。

## より高度なカスタマイズ
`AddComponent` メニューからFaceTuneの各コンポーネントを個別にGameObjectへ追加することで、プリセット等に縛られない表情制御を設定することも可能です。