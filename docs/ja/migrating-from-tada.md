# CSStack.TADA からの移行

CSStack.DDD は CSStack.TADA の後継です。このガイドでは、TADA を使っているプロジェクトを CSStack.DDD に移行する方法を説明します。

このガイドは TADA 4.0.0 を前提に書いています。それより古いバージョンを使っている場合は、先に[以前のバージョンからの移行](#以前のバージョンからの移行)を読んでください。移行の前に TADA 4.0.0 へ上げておく必要はありません。TADA のバージョンによって違うコードは、どのみち書き直すコードだからです。

## 始める前に

- **トランザクションの表し方が逆になります。** TADA では、`ExecuteTransactionAsync` の中でリポジトリを呼ぶたびにセッションを渡します。CSStack.DDD では、リポジトリと Unit of Work が DI のスコープ内で接続を共有し、アプリケーションサービスが最後にコミットします。アプリケーションサービスは名前の置き換えではなく書き直しになりますが、書き直した後のコードは短くなります。
- **バリデーションが例外からエラーに変わります。** `Validate()` はエラーのリストを返し、エンティティの操作が例外をスローします。[バリデーション](validation.md)を参照してください。
- **TADA が提供していたものの一部は提供しません。** 集約サービス、ドメインサービスのインターフェース、コマンドサービスとクエリサービスのインターフェース、`Optional<T>`、トランザクションマネージャーはありません。理由は[設計判断](design-decisions.md#ツールキットが提供しないもの)にあります。
- **下位のレイヤーから順に移行します。** パッケージを差し替えると、すべての `using CSStack.TADA;` がコンパイルエラーになります。ドメイン層から直し、アプリケーション層、インフラストラクチャ、コンポジションルートの順に進め、レイヤーごとにビルドしてください。

## 変わること

| 観点 | CSStack.TADA | CSStack.DDD |
|---|---|---|
| トランザクション | `ITransactionManager.ExecuteTransactionAsync` がユースケースをラムダで包み、セッションを渡す | リポジトリが変更を登録し、`IUnitOfWork.CommitAsync` がコミットする |
| バリデーション | `Validate()`（または `Create`）が例外をスローする | `Validate()` はエラーを返し、操作が `DomainValidationException` をスローする |
| 生成と再構築 | 値オブジェクトとエンティティに `Create` と `Reconstruct` | エンティティだけに持たせる。値オブジェクトのコンストラクターは 1 つ |
| 保存 | `SaveAsync` は upsert | `Save` は登録だけ。挿入か更新かは、読み込んだかどうかからインフラストラクチャが判断する |
| 見つからない場合 | `Optional<T>.Empty` | `null` |
| ドメインイベント | 提供しない | `AggregateRoot` が記録し、Unit of Work がコミット時に取り出す |
| 操作を行ったのは誰か | `SaveAsync` に渡す `TOperateInfo` | 業務上の「誰」は操作の引数。監査のための「誰」はインフラストラクチャが記録する |
| 同時実行制御 | 提供しない | インフラストラクチャが検出し、`ConcurrencyConflictException` をスローする |

## 型の対応表（TADA 4.0.0）

| CSStack.TADA | CSStack.DDD | 備考 |
|---|---|---|
| `using CSStack.TADA;` | `using CSStack.DDD.Domain;` / `using CSStack.DDD.Application;` | レイヤーごとに名前空間が分かれる |
| `IValueObject` | `IValueObject<TSelf>` | record なら自動的に満たす |
| `IValueObject.Validate()`（`void`、例外をスロー） | `IValidatable.Validate()`（`IReadOnlyList<ValidationError>` を返す） | `IValidatable` は別のインターフェース |
| `ISingleValueObject<TValue>` | `ISingleValueObject<TSelf, TValue>` | `TSelf` が**先頭に**増える |
| `ISingleValueObject<TValue, TSelf>` | `ISingleValueObject<TSelf, TValue>` | 型引数の順序が**逆**になる。`Create` と `Reconstruct` は要求しない |
| `IEntity<TIdentifier>` / `EntityBase<TSelf, TIdentifier>` | `IEntity<TSelf, TId>` / `Entity<TSelf, TId>` | `TId` は `IEquatable<TId>` を実装する必要がある |
| `Identifier` | `Id` | |
| `IEntity.Validate()` | なし | 代わりに操作の中で強制する |
| なし | `AggregateRoot<TSelf, TId>`、`IDomainEvent`、`IDomainEventSource` | 自作の基底クラスがあれば置き換える |
| `IAggregateService<...>` / `AggregateServiceBase<...>` | なし | エンティティの操作と、アプリケーションサービス |
| `IRepository<TEntity, TId, TOperateInfo, TSession>` | `IRepository<TAggregate, TId>` | セッションと操作情報がなくなる |
| `IRepositoryDeletable<...>` | `IDeletableRepository<TAggregate, TId>` | |
| `FindByIdentifierAsync(session, id, ct)` → `ValueTask<Optional<T>>` | `FindAsync(id, ct)` → `Task<T?>` | |
| `SaveAsync(session, entity, operateInfo, ct)` | `Save(aggregate)` | 同期。登録だけ |
| `DeleteAsync(session, entity, operateInfo, ct)` | `Delete(aggregate)` | 同期。登録だけ |
| `ITransactionManager`、`TransactionManager`、`TransactionSessions` | `IUnitOfWork` | |
| `ITransactionService<TSession>` | `IUnitOfWork` の実装 | インフラストラクチャ |
| 例外時のロールバック | コミットせずに Dispose する | ロールバックのメソッドはない |
| `ICommandService<...>`、`ICommandServiceDTO`、`IQueryService<...>`、`IQueryServiceDTO` | なし | 各インターフェースに `ExecuteAsync` を宣言する |
| `Optional<T>`、`OptionalExtensions` | なし | 手順 5 を参照 |
| `TADAException`、`NestedTransactionException`、`TransactionSessionNotFoundException` | なし | |
| なし | `ValidationError`、`Required`、`InvalidFormat`、`TooLong`、`TooShort`、`BelowMinimum`、`AboveMaximum`、`DomainException`、`DomainValidationException` | |
| なし | `ConcurrencyConflictException` | |

## 手順 1：パッケージを差し替える

```xml
<!-- 移行前 -->
<PackageReference Include="CSStack.TADA" Version="4.0.0" />
<!-- 移行後 -->
<PackageReference Include="CSStack.DDD" Version="x.y.z" />
```

次に、ドメイン層の `using CSStack.TADA;` を `using CSStack.DDD.Domain;` に置き換えます。`using CSStack.DDD.Application;` は、Unit of Work を使う箇所にだけ追加します。

## 手順 2：値オブジェクト

```csharp
// 移行前（TADA）
public sealed record Email : ISingleValueObject<string, Email>
{
	public string Value { get; }

	private Email(string value)
	{
		Value = value;
	}

	public static Email Create(string value)
	{
		var email = new Email(value);
		email.Validate();
		return email;
	}

	public static Email Reconstruct(string value) => new(value);

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) throw new ArgumentException("The email address is required.");
		if (!Value.Contains("@")) throw new ArgumentException("The email address is invalid.");
	}
}

// 移行後（CSStack.DDD）
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		Value = value ?? throw new ArgumentNullException(nameof(value));
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains("@")) return [new InvalidFormat()];
		return [];
	}
}
```

- 型引数を入れ替えます。`ISingleValueObject<string, Email>` は `ISingleValueObject<Email, string>` になります。
- `IValidatable` も宣言します。`ISingleValueObject` には含まれていません。
- 例外をスローする代わりにエラーを返します。問題を表すエラーの型（`Required`、`TooLong` など）を選ぶか、`ValidationError` から派生した独自の型を定義します。
- `Email.Create(x)` と `Email.Reconstruct(x)` は、どちらも `new Email(x)` に置き換えます。値を作っても検証されなくなります。検証は、その値を使ってエンティティを生成・変更するときにエンティティが行います（手順 3）。`Create` を呼んでいた箇所が、そうした操作に値を渡すようになっているかを確認してください。
- コンストラクターの代わりに `required` の `init` プロパティを使っても `Value { get; }` を満たします。呼び出し側を変えないためにこれを残す場合は、`null` を拒否するコンストラクターがないので、`Validate()` で `null` を `Required` として報告してください。

[値オブジェクト](value-objects.md)を参照してください。

## 手順 3：エンティティと集約ルート

```csharp
// 移行前（TADA）
public sealed class Member : EntityBase<Member, MemberId>
{
	public override MemberId Identifier { get; }
	public Email Email { get; private set; }

	private Member(MemberId identifier, Email email)
	{
		Identifier = identifier;
		Email = email;
	}

	public static Member Create(MemberId identifier, Email email)
	{
		var member = new Member(identifier, email);
		member.Validate();
		return member;
	}

	public static Member Reconstruct(MemberId identifier, Email email) => new(identifier, email);

	public void ChangeEmail(Email email)
	{
		email.Validate();
		Email = email;
	}

	public override void Validate()
	{
		Email.Validate();
	}
}

// 移行後（CSStack.DDD）
public sealed class Member : AggregateRoot<Member, MemberId>
{
	public override MemberId Id { get; }
	public Email Email { get; private set; }

	private Member(MemberId id, Email email)
	{
		Id = id;
		Email = email;
	}

	public static Member Register(Email email, DateTimeOffset occurredAt)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid(); // 生成する前に検証する
		var member = new Member(MemberId.New(), email);
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), occurredAt, member.Id, email));
		return member;
	}

	public static Member Reconstruct(MemberId id, Email email) => new(id, email);

	public void ChangeEmail(Email email, DateTimeOffset occurredAt)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		Email = email;
		AddDomainEvent(new MemberEmailChanged(Guid.NewGuid(), occurredAt, Id, email));
	}
}
```

- 集約ルートは `AggregateRoot<TSelf, TId>` から、集約の内部のエンティティは `Entity<TSelf, TId>` から派生させます。
- `Identifier` を `Id` に改名します。エンティティが `MemberId` のような業務上の名前のプロパティを持っている場合は、それを残して `public override MemberId Id => MemberId;` と書きます。
- `TId` は `IEquatable<TId>` を実装する必要があります。record で実装した識別子は、すでに実装しています。
- 公開の `Validate()` は削除します。入力は各操作の中で検証し、複数の入力のエラーは `Under` で集めてから `ThrowIfInvalid()` を 1 回呼びます。[バリデーション](validation.md#ゲート)を参照してください。
- エンティティを生成した後ではなく、生成する**前に**検証します。
- 再構築した後のデータを確かめるために `Validate()` を使っていた場合は、エンティティに `IValidatable` を実装し、例外をスローせずにエラーを返します。
- `Reconstruct` では、これまでどおり検証もイベントの記録もしません。

[エンティティと集約](entities-and-aggregates.md)を参照してください。

## 手順 4：ドメインイベント

TADA はドメインイベントを提供していません。集約ルートの基底クラスとイベントのインターフェースを自作していた場合は、それを削除して `AggregateRoot<TSelf, TId>` と `IDomainEvent` を使います。

- `IDomainEvent.OccurredAt` は `DateTimeOffset` です。`DateTime` は暗黙に変換されますが、`Kind` が `Local` や `Unspecified` の `DateTime` にはローカルのオフセットが付きます。渡す時刻が UTC であることを確認するか、`DateTimeOffset` に変えてください。
- アプリケーションサービスでイベントをクリアしないでください。`outbox.AddAsync(session, aggregate.DomainEvents)` の後に `ClearDomainEvents()` を呼ぶようなコードは削除します。イベントは、Unit of Work がコミット時に `PullDomainEvents()` で取り出します（手順 9）。
- `DomainEvents` は読み取り専用のビューです。`PullDomainEvents()` の後は空になるので、イベントを保持したい場合はコピーしてください。

[ドメインイベント](domain-events.md)を参照してください。

## 手順 5：`Optional<T>` を使わない部分更新

CSStack.DDD には `Optional<T>` がありません。使っていた箇所の置き換え方は、それが何を表していたかによって変わります。

| 箇所 | 置き換え方 |
|---|---|
| `FindByIdentifierAsync` の戻り値 | `FindAsync` は `null` を返す |
| 項目ごとに `Optional<T>` を持つ要求を受け取る `Update(UpdateReq)` 操作（「指定なし」が「変更しない」の意味） | 項目ごとの操作（`ChangeName`、`ChangeEmail` など）に分ける |
| 1 つのトランザクションで複数の項目を変えるコマンドの要求 DTO | アプリケーション層だけで使う、3 つの状態を持つ型を自作する |

**`Optional<T>` を null 許容型（`string?`）で置き換えないでください。** `null` を取るプロパティがないうちは動きますが、1 つでもできた時点で「`null` に変更する」と「変更しない」を区別できなくなります。

操作を分けるのは、[上書きよりも意図を優先する](entities-and-aggregates.md#操作)という指針に沿ったものです。操作を呼ぶこと自体が「変更する」という意味なので、引数に「指定なし」の状態は要りません。

```csharp
// 移行前（TADA）
public void Update(UpdateReq req) // UpdateReq { Optional<MemberName> NameOptional; Optional<Email> EmailOptional; }

// 移行後
public void ChangeName(MemberName name, DateTimeOffset occurredAt)
public void ChangeEmail(Email email, DateTimeOffset occurredAt)
```

1 回の要求で複数の項目を変えるコマンドが必要な場合は、その要求 DTO に 3 つの状態を持つ型を残し、指定された項目の操作だけを呼びます。この型はドメイン層では使いません。

```csharp
if (req.Name.TryGetValue(out var name))
{
	member.ChangeName(new MemberName(name), req.OccurredAt);
}
if (req.Email.TryGetValue(out var email))
{
	member.ChangeEmail(new Email(email), req.OccurredAt);
}
members.Save(member);
await unitOfWork.CommitAsync(cancellationToken);
```

型は、`HasValue`、`TryGetValue`、`T` からの暗黙の変換を持つ小さな `readonly struct` で、たいてい足ります。次のトレードオフに注意してください。

- 更新全体で 1 つのイベントではなく、変更した項目ごとにイベントが記録されます。
- 複数の操作が検証に失敗しても、報告されるのは最初の操作のエラーだけです。すべてを報告したい場合は、先に要求を検証します。[フォームの名前で入力を検証する](validation.md#フォームの名前で入力を検証する)を参照してください。
- 後の操作で例外が発生した場合は何もコミットされないので、一部の項目だけが変更されることはありません。

## 手順 6：リポジトリ

```csharp
// 移行前（TADA）
public interface IMemberRepository<TSession> : IRepository<Member, MemberId, OperateInfo, TSession>
	where TSession : IDisposable
{
}

// 移行後（CSStack.DDD）
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

- セッションと操作情報の型引数がなくなるので、リポジトリを使うサービスからも `<TSession>` がなくなります。
- リポジトリには、`FindByEmailAsync` のようにドメインが必要とするクエリを持たせて構いません。戻り値は `FindAsync` に合わせて `Task` にします。
- `IDeletableRepository` から派生させるのは、本当に存在しなくなる集約だけです。

集約サービス（`IAggregateService` と `AggregateServiceBase`）に相当するものはありません。「取得して、変更して、保存する」は、エンティティの操作とアプリケーションサービス（手順 7）に移します。`GetEntityByIdentifierAsync` は `FindAsync` に置き換えます。

[リポジトリと Unit of Work](repositories-and-unit-of-work.md) を参照してください。

## 手順 7：アプリケーションサービス

コードがもっとも大きく変わる部分です。

```csharp
// 移行前（TADA）
public sealed class ChangeMemberEmailCommandService<TMemberSession>(
	ITransactionManager transactionManager,
	IMemberRepository<TMemberSession> members) : IChangeMemberEmailCommandService
	where TMemberSession : IDisposable
{
	public async ValueTask ExecuteAsync(IChangeMemberEmailCommandService.Req req, CancellationToken cancellationToken = default)
	{
		await transactionManager.ExecuteTransactionAsync<TMemberSession>(
			async (sessions, ct) =>
			{
				var session = sessions.GetSession<TMemberSession>();
				var found = await members.FindByIdentifierAsync(session, req.MemberId, ct);
				if (!found.TryGetValue(out var member))
				{
					throw new MemberNotFoundException(req.MemberId);
				}

				member.ChangeEmail(Email.Create(req.Email));
				await members.SaveAsync(session, member, req.OperateInfo, ct);
			},
			cancellationToken: cancellationToken);
	}
}

// 移行後（CSStack.DDD）
public sealed class ChangeMemberEmailCommandService(
	IMemberRepository members,
	IUnitOfWork unitOfWork) : IChangeMemberEmailCommandService
{
	public async ValueTask ExecuteAsync(IChangeMemberEmailCommandService.Req req, CancellationToken cancellationToken = default)
	{
		var member = await members.FindAsync(req.MemberId, cancellationToken)
			?? throw new MemberNotFoundException(req.MemberId);

		member.ChangeEmail(new Email(req.Email), req.OccurredAt);
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

- ラムダ、セッション、`<TSession>` の型引数がなくなります。
- 例外が発生すると `CommitAsync` まで到達せず、DI のスコープとともに Unit of Work が Dispose されます。登録した変更は破棄され、これが TADA のロールバックの代わりになります。`beforeRollbackHandler` に書いていた処理は、アプリケーションサービスの `try`/`catch` か、全体の例外処理に移します。
- **アプリケーションサービスから別のアプリケーションサービスを呼ばないでください。** TADA はこれを `NestedTransactionException` で拒否していました。CSStack.DDD ではコンパイルも実行も通りますが、内側の `CommitAsync` が外側のユースケースの途中でコミットしてしまいます。共通の処理は、ドメインサービスかエンティティに移します。

### ドメインサービス

TADA の 3.x までは、要求 DTO にセッションを載せる `IDomainService<TReq>` がありました。CSStack.DDD では、ドメインサービスはセッションを持たない普通のクラス（必要ならインターフェースも）です。コミットはしません。読み込み、保存、コミットはアプリケーションサービスが行います。メールアドレスが使われていないかの確認のように、ドメインサービスが何かを探す必要がある場合は、リポジトリのインターフェースを受け取って構いません。

### 複数のデータストア

TADA は、データストアをセッションの型で区別していました（`ExecuteTransactionAsync<TUserSession, TOrderSession>`）。`IUnitOfWork` の型は 1 つしかないので、アプリケーション層でデータストアごとにインターフェースを派生させます。

```csharp
public interface IMemberUnitOfWork : IUnitOfWork { }
public interface IOrderUnitOfWork : IUnitOfWork { }
```

データストアが 1 つなら、`IUnitOfWork` をそのまま使います。2 つの Unit of Work をコミットしてもアトミックにはなりません。これは TADA で 2 つのセッションをコミットする場合と同じです。1 つのユースケースで変更するデータストアは 1 つにし、もう一方にはドメインイベントを通じて反映するほうを優先してください。

### イベントハンドラー

ハンドラーが `ExecuteTransactionAsync` で自分のトランザクションを開始していた場合は、変更を登録し、自分のスコープの Unit of Work で `CommitAsync` を呼ぶ形にします。[イベントへの反応](domain-events.md#イベントへの反応)を参照してください。

## 手順 8：操作を行ったのは誰か

TADA では `SaveAsync` と `DeleteAsync` に `TOperateInfo` を渡し、多くの場合それを監査列に記録していました。CSStack.DDD では 2 種類の「誰」を分けます（[操作を行ったのは誰か](entities-and-aggregates.md#操作を行ったのは誰か)）。

- **業務上の意味を持つ「誰」**（投稿者、承認者）：操作の引数として渡します。そのために、要求 DTO の `OperateInfo` を残しておいて構いません。
- **監査のための「誰」**（`UpdatedBy`、`UpdatedAt`）：インフラストラクチャが記録します。リクエストのパイプラインやコマンドラインのような入り口で実行コンテキスト（例えば、利用者と時刻を持つスコープ単位のオブジェクト）を設定し、リポジトリと Unit of Work がそれを読みます。ドメイン層とアプリケーション層は読みません。

Outbox を通じて別のスコープでイベントを配信する場合は、Outbox のレコードのエンベロープに操作者を書き込み、ハンドラーを実行するスコープでそこから実行コンテキストを復元します（[イベントのメタデータ](domain-events.md#イベントのメタデータ)）。

## 手順 9：インフラストラクチャ

`ITransactionService<TSession>` の実装を、`IUnitOfWork` の実装に置き換えます。必要なものは[インフラストラクチャの実装](repositories-and-unit-of-work.md#インフラストラクチャの実装)にあります。TADA と違う点は次のとおりです。

| 観点 | TADA | CSStack.DDD |
|---|---|---|
| トランザクションの開始 | ラムダを実行する前に `BeginAsync` | 読み取りと書き込みを同じトランザクションにするため、データベースへの最初のアクセスで開始する |
| 挿入か更新か | `SaveAsync` は upsert | どの集約を読み込んだかを覚えておく（アイデンティティマップ）。既存のデータを気づかないうちに上書きしてしまう **upsert 文は使わない** |
| ドメインイベント | アプリケーションサービスが書き込む | `CommitAsync` で、登録された集約から取り出して Outbox に書き込む |
| ロールバック | `RollbackAsync` | コミットせずに Dispose する |
| 同時実行制御 | 提供しない | 集約ルートでの楽観的同時実行制御。競合したら `ConcurrencyConflictException` をスローする |

DI コンテナーには次のように登録します。

- リポジトリはスコープの Unit of Work に依存するので、Unit of Work とリポジトリは**スコープ**で登録します。TADA でシングルトンにしていたリポジトリは、スコープに変える必要があります。`ValidateScopes = true` にしておくと、変え忘れを見つけられます。
- 同じスコープ内でアプリケーション層とリポジトリが同じインスタンスを使うように、実装を 1 回だけ登録し、インターフェースからそれに転送します。

```csharp
services.AddScoped<MemberUnitOfWork>();
services.AddScoped<IMemberUnitOfWork>(sp => sp.GetRequiredService<MemberUnitOfWork>());
services.AddScoped<IMemberRepository, MemberRepository>(); // MemberUnitOfWork に依存する
```

- `TransactionManager` と `ITransactionService<TSession>` の登録は削除します。

## 手順 10：コマンドサービスとクエリサービスのインターフェース

CSStack.DDD には、`ICommandService`、`IQueryService`、それらの DTO のマーカーがありません。各インターフェースに `ExecuteAsync` を宣言します。要求 DTO と応答 DTO は、これまでどおりネストして構いません。

```csharp
// 移行前（TADA）
public interface IChangeMemberEmailCommandService : ICommandService<IChangeMemberEmailCommandService.Req>
{
	public sealed record Req : ICommandServiceDTO { ... }
}

// 移行後
public interface IChangeMemberEmailCommandService
{
	ValueTask ExecuteAsync(Req req, CancellationToken cancellationToken = default);

	public sealed record Req { ... }
}
```

共通のインターフェースに依存していた（例えば、ジェネリックの制約に使っていた）場合は、同じ形のインターフェースをアプリケーションで定義してください。

## 手順 11：例外

| CSStack.TADA | CSStack.DDD |
|---|---|
| `Validate()` や `Create` がスローする例外 | `DomainValidationException`。`Errors` にパス付きの型付きエラーが入る |
| `ValueObjectNullException`、`ValueObjectLengthException`、`ValueObjectInvalidException`（3.x まで） | `Validate()` が報告する `Required`、`TooShort`/`TooLong`、`InvalidFormat` |
| `ObjectNotFoundException`、`ObjectAlreadyExistException`、`DomainInvalidOperationException`（3.x まで） | 独自の例外。ビジネスルールの例外は `DomainException` から派生させる |
| `NestedTransactionException`、`TransactionSessionNotFoundException` | なし |
| なし | `ConcurrencyConflictException` |

`DomainException`（`DomainValidationException` を含む）と `ConcurrencyConflictException` は、プレゼンテーション層で 1 か所にまとめて処理します。エラーはメッセージを持たないので、メッセージへの変換もそこで行います。[エラーをメッセージに変換する](validation.md#エラーをメッセージに変換する)を参照してください。

## コンパイルは通るが振る舞いが変わること

| 変更 | 影響 |
|---|---|
| `Save` は upsert ではない | 読み込んでいない集約を保存したとき、同じ識別子の行がすでにあると、上書きされずに主キーで失敗する |
| エンティティの等価性は `is TSelf` で判定する | TADA 3.0 以降では、実行時の型が違うエンティティは等価にならない。CSStack.DDD では、同じ `TSelf` から派生した 2 つのクラスは、識別子が等しければ等価になる。ハッシュコードに型は含まれなくなる |
| `FindAsync` は `null` を返す | `HasValue` を確認していたコードは、`null` を確認する必要がある |
| 検証の例外 | `ArgumentException` などの例外が `DomainValidationException` になる |
| `OccurredAt` は `DateTimeOffset` | ローカルまたは未指定の `DateTime` には、ローカルのオフセットが付く |
| ユースケースの入れ子 | 拒否されなくなり、内側のユースケースが途中でコミットする |

## 以前のバージョンからの移行

リポジトリ、トランザクション、エンティティの形は TADA のどのバージョンでも同じなので、手順 1 と手順 3〜11 はそのまま当てはまります。違いのほとんどはバリデーションにあります。

使っているバージョンを含む行を見てください。

| TADA のバージョン | 4.0.0 との違い | 対応 |
|---|---|---|
| 2.0.1〜3.0.x | `IValueObject` と `IEntity` に `Validate()` がない。ルールは `Create` で確認している | `Create` の確認を `Validate()` に移し、エラーを返すようにする（手順 2） |
| 3.0.x 以前 | `IDomainService<TReq>`、`IDomainServiceDTO`、`ILengthDefinedSingleValueObject`、手順 11 の例外がある | ドメインサービスは[ドメインサービス](#ドメインサービス)を参照。長さの上下限は、値オブジェクトの `static` メンバーか、`Validate()` で使う定数として残す |
| 2.0.x 以前 | `ExecuteTransactionAsync` のラムダが `CancellationToken` を受け取らない。エンティティの等価性が実行時の型を比較しない | ラムダは手順 7 でなくなるので、1 つ目の違いは問題にならない。派生した型の等価性について、CSStack.DDD はこれらのバージョンと同じように振る舞う |
| 1.x と 0.x | `ValueObjectBase`（`Validate()` と `IsInvalidValue` を持つ抽象 record）、`IEntity.IsInvalidValue`、`ValidateHelper`、`KeyedValidateHelper<TKey>`、`MultiReasonException`、`KeyedMultiReasonException<TKey>`。`ICommandServiceWithRes`、`IQueryServiceWithoutReq`、`IDomainServiceWithRes` | `ValueObjectBase` から派生する代わりに、`IValueObject<TSelf>` と `IValidatable` を実装する。`IsInvalidValue` は `Validate().Count > 0` に置き換える。ヘルパーを使う代わりに `ValidationError` をリストに集め、キーを使っていた箇所では `Under` を使う。`WithRes` と `WithoutReq` のインターフェースは手順 10 と同じように置き換える |
| 1.x と 0.x | `ISingleValueObject<TValue>` がない（`ISingleValueObject<TValue, TSelf>` だけ）。1.0.4 より前は `IRepositoryDeletable` もない | 追加の対応はない。`ISingleValueObject<TSelf, TValue>` を使い、削除する集約には `IDeletableRepository` を使う |

## チェックリスト

- [ ] パッケージと名前空間を置き換え、`CSStack.TADA` を参照している箇所がない。
- [ ] 値オブジェクトは `Validate()` でエラーを報告し、コンストラクターでは型が存在するために必要なことだけを確認している。
- [ ] エンティティを生成・変更するすべての操作が、入力を `ThrowIfInvalid()` で検証している。
- [ ] `Reconstruct` は検証もイベントの記録もしていない。
- [ ] `null` が正当な値である箇所で、`Optional<T>` を null 許容型に置き換えていない。
- [ ] アプリケーションサービスは `Save` と `CommitAsync` を呼び、別のアプリケーションサービスを呼んでいない。
- [ ] Unit of Work がコミット時にドメインイベントを取り出し、アプリケーション層ではクリアしていない。
- [ ] 監査列は、インフラストラクチャが実行コンテキストから記録している。
- [ ] リポジトリは、upsert 文を使わずに、読み込んだかどうかで挿入か更新かを判断している。
- [ ] Unit of Work とリポジトリをスコープで登録している。
- [ ] `DomainException`（`DomainValidationException` を含む）と `ConcurrencyConflictException` を、プレゼンテーション層で処理している。
