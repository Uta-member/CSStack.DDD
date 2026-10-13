# AGENTS.md

このファイルは、このリポジトリで作業する AI コーディングエージェント向けの手引きです。特定のエージェント製品に依存しない内容にしています。

## プロジェクトの概要

CSStack.DDD は、C# でドメイン駆動設計（DDD）を行うための軽量なビルディングブロック集です。NuGet パッケージ `CSStack.DDD` として公開します。

- 値オブジェクト、エンティティ、集約ルート、ドメインイベント、リポジトリ、Unit of Work のインターフェースと基底クラスを提供する。
- 永続化技術（EF Core、Dapper など）には一切依存しない。外部パッケージへの依存もない。
- `v0.2.0` までリリース済み。

## ディレクトリ構成

```
CSStack.DDD.slnx                 ソリューション
src/CSStack.DDD/
  CSStack.DDD.csproj             パッケージ情報とリリースノートもここにある
  CSStack.DDD.snk                厳密な名前のキー（意図的に公開している）
  Domain/                        名前空間 CSStack.DDD.Domain
    DomainException.cs           ドメインが操作を拒否したときの例外の基底クラス
    ValueObject/                 IValueObject, ISingleValueObject
    Entity/                      IEntity, Entity, IAggregateRoot, AggregateRoot
    DomainEvent/                 IDomainEvent, IDomainEventSource
    Repository/                  IRepository, IDeletableRepository
    Validation/                  IValidatable, ValidationError（と組み込みエラー）, DomainValidationException, ValidationErrorExtensions
  Application/                   名前空間 CSStack.DDD.Application
                                 IUnitOfWork, ConcurrencyConflictException, AggregateNotFoundException,
                                 RepositoryExtensions（GetAsync）
  Polyfills/                     netstandard2.0 向けの IsExternalInit
  PublicAPI.Shipped.txt          リリース済みの公開 API（全ターゲット共通）
  PublicAPI.Unshipped.txt        未リリースの公開 API の変更（全ターゲット共通）
  PublicAPI/{ターゲット}/          ターゲットによって異なる公開 API
docs/{en,ja,zh-CN}/              利用者向けドキュメント（8 ページ × 3 言語。migrating-from-tada.md は前身の CSStack.TADA からの移行ガイド）
README.md / README.ja.md / README.zh-CN.md
global.json                      dotnet test で Microsoft.Testing.Platform を使う設定
tests/CSStack.DDD.Tests/         xUnit v3 のテスト（net8.0;net10.0）
  Domain/ Application/           src と同じ構成。名前空間はすべて CSStack.DDD.Tests
  TestDoubles/                   テスト用のエンティティ、集約、イベント
```

サブフォルダーは名前空間に反映しません。`Domain/` 配下はすべて `CSStack.DDD.Domain`、`Application/` 配下はすべて `CSStack.DDD.Application` です。

## ビルドと検証

```shell
dotnet build CSStack.DDD.slnx
dotnet test --solution CSStack.DDD.slnx
```

- ターゲットは `netstandard2.0;net8.0;net10.0` です。.NET 8 と .NET 10 の SDK が必要です。
- `TreatWarningsAsErrors` と `GenerateDocumentationFile` が有効です。public なメンバーに XML ドキュメントコメントがないと、ビルドが失敗します。
- 変更後は、ビルドが警告 0 で通り、テストがすべて成功することを確認してください。動作を変えたり追加したりしたときは、テストも追加・更新します。
- テストは xUnit v3 で、Microsoft.Testing.Platform 上で動きます。.NET 10 SDK の `dotnet test` はルートの `global.json` の設定で MTP モードになるので、`--solution` などの MTP の引数を使います。
- テストプロジェクトを追加する場合は `tests/` に置き、`CSStack.DDD.slnx` の `/tests/` フォルダーに登録します。
- パッケージの作成は `dotnet pack src/CSStack.DDD/CSStack.DDD.csproj -c Release` です（README.md と CSStack.png が同梱される）。
- csproj の `Version` はローカルビルド用の `0.0.0-local` です。リリースのバージョンは書き換えません。

### 厳密な名前とアセンブリのバージョン

アセンブリには厳密な名前を付けています（イシュー #9 で決定）。

- 理由: netstandard2.0 を通じて .NET Framework の利用者も対象にしている。.NET Framework では、厳密な名前を持つアセンブリから名前のないアセンブリを参照できない。.NET Core 以降では厳密な名前は検証されないので、付けても害はない。
- キーは `src/CSStack.DDD/CSStack.DDD.snk` です。.NET のライブラリのガイドラインどおり、秘密鍵ごとリポジトリで公開しています。厳密な名前はセキュリティのためではなく、アセンブリの識別のためのものです。
- キーを変えたり、厳密な名前をやめたりしないでください。アセンブリの識別子が変わり、参照しているすべてのアセンブリにとって破壊的変更になります。`AssemblyTests` が公開キートークンを確認しています。
- `AssemblyVersion` はメジャーバージョンだけ（`{Major}.0.0.0`、0.x の間は `0.0.0.0`）にしています。.NET Framework のアプリがメジャーバージョン内で更新したときに、バインディングリダイレクトを不要にするためです。`FileVersion` と `InformationalVersion` には完全なバージョンが入ります。

### 公開 API とパッケージの互換性

公開 API の意図しない変更をビルドで検出します（イシュー #11）。

- `Microsoft.CodeAnalysis.PublicApiAnalyzers` が、公開 API を `PublicAPI.Shipped.txt`（リリース済み）と `PublicAPI.Unshipped.txt`（未リリースの変更）に照らして確認します。どちらにもない公開 API を追加したり、記載された API を消したりすると、ビルドが失敗します（RS0016、RS0017 など）。
- ターゲットによって異なる API は `PublicAPI/{ターゲット}/` のファイルに書きます。いまは record の `<Clone>$()` だけです（netstandard2.0 では基底の `ValidationError` を返し、net8.0 以降では共変戻り値で派生型を返す）。それ以外はプロジェクト直下の共通のファイルに書きます。
- 公開 API を追加・変更・削除したときは、`PublicAPI.Unshipped.txt` を同じ変更で更新します。
  - 追加した API は 1 行ずつ書く。IDE のコード修正か、`dotnet format analyzers src/CSStack.DDD/CSStack.DDD.csproj --diagnostics RS0016 --severity info` で追記できる（ただし `dotnet format` は最初のターゲットの netstandard2.0 で解析するので、ターゲットによって異なる行は手で直す）。
  - 削除した API は、Shipped にある行を `*REMOVED*` を先頭に付けて Unshipped に書く。
  - 引数や戻り値の null 許容の注釈だけが変わった場合は削除扱いにならない。新しい注釈の行だけを Unshipped に書く。
  - record を追加・削除するときは、`PublicAPI/` の 3 つのターゲットのファイルにある `<Clone>$()` の行も更新する。
- 公開 API の変更は破壊的変更かどうかを確認し、`PackageReleaseNotes` に書きます。Unshipped のファイルの差分が、そのまま次のリリースでの API の変更の一覧になります。
- `EnablePackageValidation` により、パック時にパッケージ内のターゲット間の互換性（netstandard2.0 の API が net8.0 以降にもあるかなど）を確認します。1.0 をリリースした後は `PackageValidationBaselineVersion` に直前のバージョンを設定し、前のバージョンとの互換性も確認します。
- アナライザーは `PrivateAssets="all"` で参照しており、パッケージの依存関係には含まれません。パッケージ検証は SDK の機能なので、パッケージの参照は不要です。

## リリース

master へのプッシュとプルリクエストでは、`.github/workflows/build.yml` がビルド、テスト、パックを確認します。

GitHub でリリースを公開すると、`.github/workflows/publish.yml` が NuGet.org に発行します。

- タグは `v1.2.3`（プレリリースは `v1.2.3-beta.1`）。先頭の `v` を除いた値が `-p:Version` としてビルドとパックに渡される。
- リリース前に `CSStack.DDD.csproj` の `PackageReleaseNotes` を更新してコミットしておく。
- リリース前に、すべての `PublicAPI.Unshipped.txt`（プロジェクト直下と `PublicAPI/{ターゲット}/`）の内容を同じ場所の `PublicAPI.Shipped.txt` に移してコミットしておく。`*REMOVED*` の行と、それに対応する Shipped の行は両方消す。null 許容の注釈が変わった API は、Shipped の古い行を新しい行で置き換える。Unshipped には `#nullable enable` の行だけを残す。
- 1.0 以降は、リリースしたら `PackageValidationBaselineVersion` をそのバージョンに更新する。
- NuGet.org へは Trusted Publishing（OIDC）で認証する。API キーはリポジトリに置かない。

## コーディング規約

既存のコードに合わせてください。

- インデントはタブ。
- 名前空間はブロック形式（`namespace X { ... }`）。ファイルスコープ形式は使わない。
- `ImplicitUsings` が有効なので、`System` 系の `using` は書かない。
- 制御文のキーワードと括弧の間に空白を入れない（`if(...)`）。
- コレクション式（`[]`、`[.. x]`）を使う。
- 1 ファイル 1 型が基本。ただし `ValidationError.cs` は基底と組み込みエラーをまとめて定義している。
- XML ドキュメントコメントは英語で書く。`<summary>` に加え、設計上の意図は `<remarks>` に書く。実装済みのメンバーには `<inheritdoc/>` を使う。
- `.cs` ファイルは BOM なし UTF-8、改行は CRLF（`.gitattributes` は `text=auto`）。

### netstandard2.0 の制約

すべてのコードは netstandard2.0 でもコンパイルできなければなりません。

- インターフェースの static abstract メンバーは使えない（このため `IEntity` と `IValueObject` は `==`/`!=` を要求していない。演算子は record と `Entity` 基底クラスが提供する）。
- `init` と record は `Polyfills/IsExternalInit.cs` によって使える。
- 新しい API（`ArgumentNullException.ThrowIfNull`、`Guid.CreateVersion7`、`DateOnly`、`TimeProvider`、`string.Contains(char)` など）は使わない。どうしても必要な場合は `#if` で分岐する。

## 設計の原則

設計判断の正本は [docs/en/design-decisions.md](docs/en/design-decisions.md) です。API を追加・変更する前に必ず読んでください。要点は次のとおりです。

- 値オブジェクトのコンストラクターでは、型が存在するための最低限の条件（null でないことなど）だけを確認する。ルールは `IValidatable.Validate()` がエラーのリストとして報告する。
- 検証の強制はエンティティの操作で行い、`ThrowIfInvalid()` で `DomainValidationException` をスローする。永続化からの再構築では検証もイベント発行もしない。
- エラーは文字列コードではなく、`ValidationError` から派生した型付きの record で表す。パスはドット区切りの文字列で、`Under()` で各階層が組み立てる。
- エンティティの等価性は識別子による。`Entity` の `Equals` と `GetHashCode` は sealed。
- ドメインイベントは集約が記録し、インフラストラクチャがコミット時に `PullDomainEvents()` で取り出す。`AggregateRoot` は `PullDomainEvents()` を明示的に実装し、`IDomainEventSource` 経由でだけ呼べるようにしている（`DomainEvents` は public）。
- リポジトリは `FindAsync` と `Save` だけを持つ（`Add`/`Update` は持たない）。削除は `IDeletableRepository` に分ける。リポジトリはトランザクションを扱わず、`IUnitOfWork` がコミットする。
- 集約が存在しないことをエラーとするかはユースケースが決める。`FindAsync` は `null` を返し、アプリケーション層の拡張メソッド `GetAsync` が `AggregateNotFoundException` をスローする（`DomainException` からは派生しない）。
- ドメインは `Version` を持たない。同時実行の競合はインフラストラクチャが検出し、`ConcurrencyConflictException` をスローする。

次のものは意図的に提供していません。追加を提案する前に、設計判断のページの理由を確認してください。

- `IDomainService`、実行コンテキスト（現在のユーザー）、`Result` 型、コマンドハンドラーやメディエーター、ドメインイベントのハンドラーとディスパッチャー

## ドキュメント

- 英語（`docs/en/`、`README.md`）が正本です。日本語と簡体字中国語は翻訳で、食い違う場合は英語が正しいものとします。
- ドキュメントや README を変更したら、3 言語すべてを同じ変更で更新してください。ページ構成と見出しの対応を揃えます。
- README のリンクは nuget.org 上でも切れないよう、`https://github.com/Uta-member/CSStack.DDD/blob/master/...` の絶対 URL にします。`docs/` 内のページ間リンクは相対パスです。
- ドキュメントのコード例は .NET Framework（C# 9 以降で record を使える状態）でも動くように書きます。上記の新しい API は使わず、発生日時のような値は `TimeProvider` ではなく引数で受け取る形にします。
- public API を変更したときは、次も合わせて更新します。
  - XML ドキュメントコメント
  - 関係する `docs/*/` のページと README の例
  - `CSStack.DDD.csproj` の `PackageReleaseNotes`（と必要なら `Description`）

## Git の運用

- `master` に直接コミットしません。変更はイシューから始めます。
  1. GitHub にイシューを作る（イシューがない場合は、作業を始める前に利用者に作成を依頼するか確認する）。
  2. 最新の `master` から作業ブランチを作る。ブランチ名は `feature/<イシュー番号>-<内容>`（不具合の修正は `fix/<イシュー番号>-<内容>`）。
  3. ブランチにコミットし、`master` へのプルリクエストを作る。本文に `Closes #<イシュー番号>` を書く。
- 1 つのブランチには、原則として 1 つのイシューの変更だけを入れます。
- コミットメッセージは日本語で書きます。1 行目は「〜を追加。」「〜に対応。」のように句点で終わる要約にし、変更が複数ある場合は空行の後に箇条書きで詳細を書きます。

```
netstandard2.0 への対応と、ライセンス・パッケージ情報の整備。

- ターゲットを netstandard2.0;net8.0;net10.0 にした。
- README のリンクを nuget.org でも切れないよう絶対 URL にした。
```

- `.vs/`、`bin/`、`obj/` はコミットしません（`.gitignore` 済み）。
